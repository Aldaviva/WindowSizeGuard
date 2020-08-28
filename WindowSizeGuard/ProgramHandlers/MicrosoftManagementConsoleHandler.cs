using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using SimWinInput;

#nullable enable

namespace WindowSizeGuard.ProgramHandlers {

    public interface MicrosoftManagementConsoleHandler {

        void onWindowOpened(object? sender, AutomationEventArgs automationEventArgs);

    }

    [Component]
    public class MicrosoftManagementConsoleHandlerImpl: MicrosoftManagementConsoleHandler, IDisposable {

        private const int    DESIRED_ACTION_PANE_WIDTH = 185;
        private const int    RESIZER_BAR_OFFSET        = 2;
        private const string MMC_CLASS_NAME            = "MMCMainFrame";

        public MicrosoftManagementConsoleHandlerImpl() {
            Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children, onWindowOpened);
        }

        public void onWindowOpened(object? sender, AutomationEventArgs automationEventArgs) {
            SystemWindow? window = (sender as AutomationElement)?.toSystemWindow();

            if (window != null && isForegroundMmcWindow(window)) {
                RECT actionPaneRectangle = getActionPaneRectangleFromMmcWindow(window);

                if (shouldResizeActionPane(actionPaneRectangle)) {
                    resizeActionPane(actionPaneRectangle, DESIRED_ACTION_PANE_WIDTH);
                }
            }
        }

        private static bool isForegroundMmcWindow(SystemWindow window) {
            return window.ClassName == MMC_CLASS_NAME && SystemWindow.ForegroundWindow == window;
        }

        private static RECT getActionPaneRectangleFromMmcWindow(SystemWindow window) {
            AutomationElement? automationElement = AutomationElement.FromHandle(window.HWnd);
            foreach (string childAutomationId in new[] { "59648", "65280", "59648", "12796" }) {
                automationElement = automationElement?.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, childAutomationId));
            }

            return automationElement != null && automationElement.TryGetClickablePoint(out System.Windows.Point _)
                ? automationElement.toSystemWindow().Rectangle
                : default;
        }

        private static bool shouldResizeActionPane(RECT actionPaneRectangle) {
            return !actionPaneRectangle.Equals(default(RECT)) && actionPaneRectangle.Width > DESIRED_ACTION_PANE_WIDTH;
        }

        private static void resizeActionPane(RECT actionPaneAbsolutePosition, int width) {
            Point originalCursorPosition = Cursor.Position;
            int dragYPosition = (int) new[] { actionPaneAbsolutePosition.Top, actionPaneAbsolutePosition.Bottom }.Average();

            SimMouse.Act(SimMouse.Action.LeftButtonDown, actionPaneAbsolutePosition.Left - RESIZER_BAR_OFFSET, dragYPosition);

            Thread.Sleep(10);
            SimMouse.Act(SimMouse.Action.LeftButtonUp, actionPaneAbsolutePosition.Left - RESIZER_BAR_OFFSET + actionPaneAbsolutePosition.Width - width, dragYPosition);

            Cursor.Position = originalCursorPosition;
        }

        public void Dispose() {
            Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, onWindowOpened);
        }

    }

}