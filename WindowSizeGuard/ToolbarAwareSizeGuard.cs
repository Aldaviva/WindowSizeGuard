﻿#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Forms;
using KoKo.Events;
using KoKo.Property;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using NLog;
using WindowSizeGuard.ProgramHandlers;

namespace WindowSizeGuard {

    public interface ToolbarAwareSizeGuard {

        delegate void OnToolbarVisibilityChanged(bool isToolbarVisible);
        event OnToolbarVisibilityChanged? toolbarVisibilityChanged;

    }

    [Component]
    public class ToolbarAwareSizeGuardImpl: IDisposable, ToolbarAwareSizeGuard {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private const int ESTIMATED_TOOLBAR_HEIGHT  = 25;
        private const int HORIZONTAL_EDGE_TOLERANCE = 3;
        private const int VERTICAL_EDGE_TOLERANCE   = 16;

        private static readonly double MAX_RECTANGLE_DISTANCE_TO_AUTOMATICALLY_RESIZE =
            Math.Sqrt(2 * (Math.Pow(ESTIMATED_TOOLBAR_HEIGHT + VERTICAL_EDGE_TOLERANCE, 2) + Math.Pow(HORIZONTAL_EDGE_TOLERANCE, 2)));

        private readonly WindowResizer                           windowResizer;
        private readonly WindowZoneManager                       windowZoneManager;
        private readonly VivaldiHandler                          vivaldiHandler;
        private readonly GitExtensionsHandler                    gitExtensionsHandler;
        private readonly ManuallyRecalculatedProperty<Rectangle> workingArea = new ManuallyRecalculatedProperty<Rectangle>(() => Screen.PrimaryScreen.WorkingArea);

        private readonly ConcurrentDictionary<int, ValueHolder<int>> windowVisualStateCache = ConcurrentDictionaryExtensions.createConcurrentDictionary<int, int>();

        public event ToolbarAwareSizeGuard.OnToolbarVisibilityChanged? toolbarVisibilityChanged;

        public ToolbarAwareSizeGuardImpl(WindowResizer         windowResizer, WindowZoneManager windowZoneManager, VivaldiHandler vivaldiHandler, GitExtensionsHandler gitExtensionsHandler,
                                         WindowOpeningListener windowOpeningListener) {
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
            isToolbarVisible.PropertyChanged += (sender, args) => toolbarVisibilityChanged?.Invoke(args.NewValue);

            windowOpeningListener.windowOpened += onAnyWindowOpened;

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
            FormWindowState oldWindowState = windowVisualStateCache.exchangeEnum(windowHandle, newWindowState);

            if (oldWindowState != newWindowState && windowResizer.canWindowBeAutomaticallyResized(window)) {
                resizeWindowIfNecessary(window);
            }
        }

        private void onAnyWindowOpened(SystemWindow window) {
            try {
                if (windowResizer.canWindowBeAutomaticallyResized(window)) {
                    LOGGER.Trace("Attempting to resize new window {0} ({1})", window.Title, window.ClassName);
                    resizeWindowIfNecessary(window);
                } else if (LOGGER.IsTraceEnabled) {
                    LOGGER.Trace("Window {0} ({4}) was opened but it can't be automatically resized (resizable = {1}, visibility = {2}, state = {3})", window.Title, window.Resizable,
                        window.VisibilityFlag, window.WindowState, window.ClassName);
                }

                var newWindowState = new ValueHolder<int>((int) window.WindowState);
                windowVisualStateCache[window.HWnd.ToInt32()] = newWindowState;
            } catch (Win32Exception) {
                //window was closed, do nothing
            }
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

        private bool resizeWindowIfNecessary(SystemWindow window) {
            RECT windowPosition                   = window.Position;
            RECT windowPadding                    = windowResizer.getWindowPadding(window);
            RECT windowPositionWithPaddingRemoved = windowResizer.shrinkRectangle(windowPosition, windowPadding);

            WindowZoneSearchResult closestZoneRectangleToWindow = windowZoneManager.findClosestZoneRectangleToWindow(windowPositionWithPaddingRemoved, workingArea.Value);

            if (closestZoneRectangleToWindow.distance <= MAX_RECTANGLE_DISTANCE_TO_AUTOMATICALLY_RESIZE) {
                if (LOGGER.IsTraceEnabled) {
                    LOGGER.Trace("Resizing {0}...", window.Title);
                }

                windowZoneManager.resizeWindowToZone(window, closestZoneRectangleToWindow.zone, closestZoneRectangleToWindow.zoneRectangleIndex);
                return true;
            } else if (LOGGER.IsTraceEnabled) {
                LOGGER.Trace("Not resizing window {0} ({1}) because its dimensions are too far from zone {4} (distance {2:N2} is greater than maximum distance {3:N2}). " +
                    "Window position = {5}, zone position = {6}.", window.Title, window.ClassName, closestZoneRectangleToWindow.distance, MAX_RECTANGLE_DISTANCE_TO_AUTOMATICALLY_RESIZE,
                    closestZoneRectangleToWindow.zone, windowPositionWithPaddingRemoved.toString(), closestZoneRectangleToWindow.actualZoneRectPosition.toString());
            }

            return false;
        }

        public void Dispose() {
            Automation.RemoveAutomationPropertyChangedEventHandler(AutomationElement.RootElement, onAnyWindowRestored);
        }

    }

}