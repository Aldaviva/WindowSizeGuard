#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using KoKo.Events;
using KoKo.Property;
using ManagedWinapi.Hooks;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using NLog;
using WindowSizeGuard.ProgramHandlers;

namespace WindowSizeGuard {

    public interface ToolbarAwareSizeGuard {

        delegate void OnWindowOpened(SystemWindow window);
        event OnWindowOpened? windowOpened;

        delegate void OnToolbarVisibilityChanged(bool isToolbarVisible);
        event OnToolbarVisibilityChanged? onToolbarVisibilityChanged;

    }

    [Component]
    public class ToolbarAwareSizeGuardImpl: IDisposable, ToolbarAwareSizeGuard {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private const int ESTIMATED_TOOLBAR_HEIGHT  = 25;
        private const int HORIZONTAL_EDGE_TOLERANCE = 3;
        private const int VERTICAL_EDGE_TOLERANCE   = 16;
        private const int HSHELL_WINDOWCREATED      = 1;

        private static readonly double MAX_RECTANGLE_DISTANCE_TO_AUTOMATICALLY_RESIZE =
            Math.Sqrt(2 * (Math.Pow(ESTIMATED_TOOLBAR_HEIGHT + VERTICAL_EDGE_TOLERANCE, 2) + Math.Pow(HORIZONTAL_EDGE_TOLERANCE, 2)));

        private readonly WindowResizer        windowResizer;
        private readonly WindowZoneManager    windowZoneManager;
        private readonly VivaldiHandler       vivaldiHandler;
        private readonly GitExtensionsHandler gitExtensionsHandler;

        private readonly ManuallyRecalculatedProperty<Rectangle>     workingArea            = new ManuallyRecalculatedProperty<Rectangle>(() => Screen.PrimaryScreen.WorkingArea);
        private readonly ConcurrentDictionary<int, ValueHolder<int>> windowVisualStateCache = new ConcurrentDictionary<int, ValueHolder<int>>();
        private readonly ShellHook                                   shellHook;

        public event ToolbarAwareSizeGuard.OnWindowOpened? windowOpened;
        public event ToolbarAwareSizeGuard.OnToolbarVisibilityChanged? onToolbarVisibilityChanged;

        public ToolbarAwareSizeGuardImpl(WindowResizer windowResizer, WindowZoneManager windowZoneManager, VivaldiHandler vivaldiHandler, GitExtensionsHandler gitExtensionsHandler) {
            this.windowResizer        = windowResizer;
            this.windowZoneManager    = windowZoneManager;
            this.vivaldiHandler       = vivaldiHandler;
            this.gitExtensionsHandler = gitExtensionsHandler;

            SystemEvents.UserPreferenceChanged += (sender, args) => {
                if (args.Category == UserPreferenceCategory.Desktop) {
                    workingArea.Recalculate();
                }
            };

            Property<bool> isToolbarVisible = DerivedProperty<bool>.Create(workingArea, rect => rect.Top != 0);
            isToolbarVisible.PropertyChanged += onToolbarResized;
            isToolbarVisible.PropertyChanged += (sender, args) => onToolbarVisibilityChanged?.Invoke(args.NewValue);

            shellHook = new ShellHookImpl();
            shellHook.shellEvent += (sender, args) => {
                if (args.shellEvent == ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED) {
                    onAnyWindowOpened(new SystemWindow(args.windowHandle));
                }
            };

            Automation.AddAutomationPropertyChangedEventHandler(AutomationElement.RootElement, TreeScope.Children, onAnyWindowRestored, WindowPattern.WindowVisualStateProperty);

            gitExtensionsHandler.commitWindowOpened += onAnyWindowOpened;

            foreach (SystemWindow toplevelWindow in SystemWindow.AllToplevelWindows) {
                windowVisualStateCache[toplevelWindow.HWnd.ToInt32()] = new ValueHolder<int>((int) toplevelWindow.WindowState);
            }
        }

        private void onAnyWindowRestored(object? sender, AutomationPropertyChangedEventArgs e) {
            if (sender == null) {
                return;
            }

            int windowHandle = ((AutomationElement) sender).Current.NativeWindowHandle;
            var window       = new SystemWindow(new IntPtr(windowHandle));

            FormWindowState newWindowState = window.WindowState;
            FormWindowState oldWindowState = exchangeEnumInConcurrentDictionary(windowVisualStateCache, windowHandle, newWindowState);

            if (oldWindowState != newWindowState && windowResizer.canWindowBeAutomaticallyResized(window)) {
                resizeWindowIfNecessary(window);
            }
        }

        private void onAnyWindowOpened(SystemWindow window) {
            if (LOGGER.IsTraceEnabled) {
                LOGGER.Trace("Window opened: {0} ({1})", window.Title, window.ClassName);
            }

            if (windowResizer.canWindowBeAutomaticallyResized(window)) {
                LOGGER.Trace("Automatically resizing new window {0}", window.Title);
                resizeWindowIfNecessary(window);
            } else if (LOGGER.IsTraceEnabled) {
                LOGGER.Trace("Window {0} was opened but it can't be automatically resized (resizable = {1}, visibility = {2}, state = {3}", window.Title, window.Resizable, window.VisibilityFlag,
                    window.WindowState);
            }

            var newWindowState = new ValueHolder<int>((int) window.WindowState);
            windowVisualStateCache.AddOrUpdate(window.HWnd.ToInt32(), newWindowState, (i, holder) => newWindowState);

            windowOpened?.Invoke(window);
        }

        private void onToolbarResized(object sender, KoKoPropertyChangedEventArgs<bool> e) {
            var stopwatch = Stopwatch.StartNew();

            IEnumerable<SystemWindow> resizableWindows = windowResizer.findResizableWindows(parent: null, depth: 1);
            foreach (SystemWindow window in resizableWindows) {
                if (vivaldiHandler.windowSelector.matches(window)) {
                    vivaldiHandler.fixVivaldiResizeBug(window);
                }

                resizeWindowIfNecessary(window);
            }

            foreach (SystemWindow gitCommitWindow in gitExtensionsHandler.commitWindows.Where(windowResizer.canWindowBeAutomaticallyResized)) {
                resizeWindowIfNecessary(gitCommitWindow);
            }

            stopwatch.Stop();
            LOGGER.Debug($"Resized all windows in {stopwatch.ElapsedMilliseconds:N0} ms.");
        }

        private void resizeWindowIfNecessary(SystemWindow window) {
            RECT windowPosition                   = window.Position;
            RECT windowPadding                    = windowResizer.getWindowPadding(window);
            RECT windowPositionWithPaddingRemoved = windowResizer.shrinkRectangle(windowPosition, windowPadding);

            WindowZoneSearchResult closestZoneRectangleToWindow = windowZoneManager.findClosestZoneRectangleToWindow(windowPositionWithPaddingRemoved, workingArea.Value);

            if (closestZoneRectangleToWindow.distance <= MAX_RECTANGLE_DISTANCE_TO_AUTOMATICALLY_RESIZE) {
                if (LOGGER.IsTraceEnabled) {
                    LOGGER.Trace("Resizing {0}...", window.Title);
                }

                windowZoneManager.resizeWindowToZone(window, closestZoneRectangleToWindow.zone, closestZoneRectangleToWindow.zoneRectangleIndex);
            } else if (LOGGER.IsTraceEnabled) {
                LOGGER.Trace("Not resizing window {0} ({1}) because its dimensions are too far from zone {4} (distance {2:N2} is greater than maximum distance {3:N2}). " +
                    "Window position = {5}, zone position = {6}.", window.Title, window.ClassName, closestZoneRectangleToWindow.distance, MAX_RECTANGLE_DISTANCE_TO_AUTOMATICALLY_RESIZE,
                    closestZoneRectangleToWindow.zone, windowPositionWithPaddingRemoved.toString(), closestZoneRectangleToWindow.actualZoneRectPosition.toString());
            }
        }

        public void Dispose() {
            Automation.RemoveAutomationPropertyChangedEventHandler(AutomationElement.RootElement, onAnyWindowRestored);
            shellHook.Dispose();
        }

        private static V exchangeEnumInConcurrentDictionary<K, V>(ConcurrentDictionary<K, ValueHolder<int>> dictionary, K key, V newValue) where V: Enum {
            int              newValueInt         = (int) Convert.ChangeType(newValue, newValue.GetTypeCode());
            ValueHolder<int> existingWindowState = dictionary.GetOrAdd(key, new ValueHolder<int>(newValueInt));
            int              oldValue            = Interlocked.Exchange(ref existingWindowState.value, newValueInt);
            return (V) Enum.ToObject(typeof(V), oldValue);
        }

        private class ValueHolder<T> {

            public T value;

            public ValueHolder(T value) {
                this.value = value;
            }

        }

    }

}