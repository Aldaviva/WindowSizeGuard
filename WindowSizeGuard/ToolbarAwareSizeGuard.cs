#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using Autofac.Features.AttributeFilters;
using KoKo.Events;
using KoKo.Property;
using ManagedWinapi.Windows;
using NLog;
using ThrottleDebounce;

namespace WindowSizeGuard {

    public interface ToolbarAwareSizeGuard { }

    [Component]
    public class ToolbarAwareSizeGuardImpl: IDisposable, ToolbarAwareSizeGuard {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private const int ESTIMATED_TOOLBAR_HEIGHT = 25;
        private static readonly double MAX_RECTANGLE_DISTANCE_AFTER_TOOLBAR_RESIZE = Math.Sqrt(2 * (Math.Pow(ESTIMATED_TOOLBAR_HEIGHT, 2) + Math.Pow(2, 2)));

        private readonly WindowResizer windowResizer;
        private readonly WindowZoneManager windowZoneManager;
        private readonly SystemWindow applicationFrameWindow;

        private readonly ManuallyRecalculatedProperty<Rectangle> workingArea = new ManuallyRecalculatedProperty<Rectangle>(() => {
            Rectangle primaryScreenWorkingArea = Screen.PrimaryScreen.WorkingArea;
            // LOGGER.Debug($"screen working area is now {primaryScreenWorkingArea}");
            return primaryScreenWorkingArea;
        });

        public ToolbarAwareSizeGuardImpl(WindowResizer windowResizer, WindowZoneManager windowZoneManager, [KeyFilter("ApplicationFrameWindow")] SystemWindow applicationFrameWindow) {
            this.windowResizer = windowResizer;
            this.windowZoneManager = windowZoneManager;
            this.applicationFrameWindow = applicationFrameWindow;

            Property<bool> isToolbarVisible = DerivedProperty<bool>.Create(workingArea, rect => rect.Top != 0);
            isToolbarVisible.PropertyChanged += onToolbarResized;

            AutomationElement applicationFrameWindowElement = AutomationElement.FromHandle(applicationFrameWindow.HWnd);
            Automation.AddAutomationPropertyChangedEventHandler(applicationFrameWindowElement, TreeScope.Element, (sender, args) => {
                    // LOGGER.Debug("application frame window resized");
                    workingArea.Recalculate();
                },
                AutomationElement.BoundingRectangleProperty);

            // Automation.AddAutomationPropertyChangedEventHandler(AutomationElement.RootElement, TreeScope.Children, onAnyWindowRestored, WindowPattern.WindowVisualStateProperty);

            Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Descendants, onAnyWindowOpened);

            // LOGGER.Info("Waiting for toolbars to be shown or hidden...");
        }

        private void onAnyWindowRestored(object sender, AutomationPropertyChangedEventArgs e) {
            var window = (AutomationElement) sender;

            if ((WindowVisualState) e.NewValue == WindowVisualState.Normal && windowResizer.isWindowResizable(window)) {
                // LOGGER.Info("Window restored, checking if it needs to be resized...");
                resizeWindowIfNecessary(window.toSystemWindow());
            }
        }

        private void onAnyWindowOpened(object sender, AutomationEventArgs e) {
            var window = (AutomationElement) sender;

            if (windowResizer.isWindowResizable(window)) {
                // LOGGER.Info("Window opened, checking if it needs to be resized...");
                resizeWindowIfNecessary(window.toSystemWindow());
            }
        }

        private void onToolbarResized(object sender, KoKoPropertyChangedEventArgs<bool> e) {
            var stopwatch = Stopwatch.StartNew();
            // LOGGER.Info($"Toolbar is now {(e.NewValue ? "visible" : "invisible")}");

            IEnumerable<SystemWindow> resizableWindows = windowResizer.findResizableWindows(parent: (SystemWindow?) null, depth: 2);
            foreach (SystemWindow window in resizableWindows) {
                resizeWindowIfNecessary(window);
            }

            stopwatch.Stop();
            LOGGER.Debug($"Resized all windows in {stopwatch.ElapsedMilliseconds:N0} ms.");
        }

        private void resizeWindowIfNecessary(SystemWindow window) {
            RECT windowPosition = window.Position;
            // RECT windowPosition = windowEl.Current.BoundingRectangle.toMwinapiRECT();
            RECT windowPadding = windowResizer.getWindowPadding(window);

            // LOGGER.Trace($"Deciding whether or not to resize window from {window.Process.ProcessName}...");
            WindowZoneSearchResult closestZoneRectangleToWindow =
                windowZoneManager.findClosestZoneRectangleToWindow(windowResizer.shrinkRectangle(windowPosition, windowPadding), workingArea.Value);

            if (closestZoneRectangleToWindow.distance <= MAX_RECTANGLE_DISTANCE_AFTER_TOOLBAR_RESIZE) {
                windowZoneManager.resizeWindowToZone(window, closestZoneRectangleToWindow.zone, closestZoneRectangleToWindow.zoneRectangleIndex);
                // LOGGER.Trace($"Resizing {window.Process.ProcessName} to {closestZoneRectangleToWindow.zone}");
            } else {
                // LOGGER.Trace(
                    // $"Not resizing {window.Process.ProcessName} to {closestZoneRectangleToWindow.zone} because its distance to that zone is {closestZoneRectangleToWindow.distance}, which is greater than {MAX_RECTANGLE_DISTANCE_AFTER_TOOLBAR_RESIZE}");
            }

            /*int expectedTop, desiredTop, expectedBottom, desiredBottom;

            if (isToolbarVisible()) {
                expectedTop = 0;
                desiredTop = applicationFrameWindow.Position.Top;
                expectedBottom = applicationFrameWindow.Position.Bottom + applicationFrameWindow.Position.Top;
                desiredBottom = applicationFrameWindow.Position.Bottom;
            } else {
                expectedTop = (int) oldApplicationFrameWindowPosition.Top;
                desiredTop = 0;
                expectedBottom = (int) (oldApplicationFrameWindowPosition.Bottom + oldApplicationFrameWindowPosition.Top);
                desiredBottom = applicationFrameWindow.Position.Bottom;
            }

            Rect windowPosition = windowEl.Current.BoundingRectangle;
            int actualTop = (int) windowPosition.Top;
            int actualBottom = (int) windowPosition.Bottom;

            bool shouldMoveTop = actualTop == expectedTop;
            bool shouldMoveBottom = actualBottom <= expectedBottom && actualBottom >= expectedBottom - 5;

            if (shouldMoveTop || shouldMoveBottom) {
                var window = new SystemWindow(new IntPtr(windowEl.Current.NativeWindowHandle));
                RECT newPosition = window.Position;

                if (shouldMoveTop) {
                    newPosition.Top = desiredTop;
                }

                if (shouldMoveBottom) {
                    newPosition.Bottom = desiredBottom;
                }

                windowResizer.moveWindowToPosition(window, newPosition);
            } else {
                Console.WriteLine($"Not resizing {windowEl.Current.ClassName}: bottom was {actualBottom}, not {expectedBottom}, and top was {actualTop}, not {expectedTop}");
            }*/
        }

        // private bool isToolbarVisible() {
        //     return applicationFrameWindow.Rectangle.Top != 0;
        // }

        public void Dispose() {
            Automation.RemoveAllEventHandlers();
        }

    }

}