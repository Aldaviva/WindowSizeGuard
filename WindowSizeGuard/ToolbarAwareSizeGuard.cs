#nullable enable

using System;
using System.Collections.Generic;
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

namespace WindowSizeGuard {

    public interface ToolbarAwareSizeGuard { }

    [Component]
    public class ToolbarAwareSizeGuardImpl: IDisposable, ToolbarAwareSizeGuard {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private const           int    ESTIMATED_TOOLBAR_HEIGHT                    = 25;
        private static readonly double MAX_RECTANGLE_DISTANCE_AFTER_TOOLBAR_RESIZE = Math.Sqrt(2 * (Math.Pow(ESTIMATED_TOOLBAR_HEIGHT, 2) + Math.Pow(2, 2)));

        private readonly WindowResizer        windowResizer;
        private readonly WindowZoneManager    windowZoneManager;
        private readonly VivaldiHandler       vivaldiHandler;
        private readonly GitExtensionsHandler gitExtensionsHandler;

        private readonly ManuallyRecalculatedProperty<Rectangle> workingArea = new ManuallyRecalculatedProperty<Rectangle>(() => Screen.PrimaryScreen.WorkingArea);

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

            Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children, onAnyWindowOpened);

            gitExtensionsHandler.commitWindowOpened += onAnyWindowOpened;
        }

        private void onAnyWindowRestored(object sender, AutomationPropertyChangedEventArgs e) {
            var window = ((AutomationElement) sender).toSystemWindow();
            if ((WindowVisualState) e.NewValue == WindowVisualState.Normal && windowResizer.canWindowBeManuallyResized(window)) {
                resizeWindowIfNecessary(window);
            }
        }

        private void onAnyWindowOpened(object sender, AutomationEventArgs e) {
            onAnyWindowOpened(((AutomationElement) sender).toSystemWindow());
        }

        private void onAnyWindowOpened(SystemWindow window) {
            LOGGER.Debug("Window opened: {0} ({1})", window.Title, window.ClassName);
            if (windowResizer.canWindowBeManuallyResized(window)) {
                resizeWindowIfNecessary(window);
            }
        }

        private void onToolbarResized(object sender, KoKoPropertyChangedEventArgs<bool> e) {
            var stopwatch = Stopwatch.StartNew();

            IEnumerable<SystemWindow> resizableWindows = windowResizer.findResizableWindows(parent: null, depth: 1);
            foreach (SystemWindow window in resizableWindows) {
                if (vivaldiHandler.isWindowVivaldi(window)) {
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
            RECT windowPosition = window.Position;
            RECT windowPadding = windowResizer.getWindowPadding(window);

            WindowZoneSearchResult closestZoneRectangleToWindow = windowZoneManager.findClosestZoneRectangleToWindow(windowResizer.shrinkRectangle(windowPosition, windowPadding), workingArea.Value);

            if (closestZoneRectangleToWindow.distance <= MAX_RECTANGLE_DISTANCE_AFTER_TOOLBAR_RESIZE) {
                windowZoneManager.resizeWindowToZone(window, closestZoneRectangleToWindow.zone, closestZoneRectangleToWindow.zoneRectangleIndex);
            }
        }

        public void Dispose() {
            Automation.RemoveAllEventHandlers();
        }

    }

}