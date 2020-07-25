#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using NLog;
using Condition = System.Windows.Automation.Condition;

namespace WindowSizeGuard {

    public interface WindowResizer {

        RECT enlargeRectangle(RECT windowRectangle, RECT padding);

        RECT shrinkRectangle(RECT windowRectangle, RECT padding);

        RECT getWindowPadding(SystemWindow window);

        void moveWindowToPosition(SystemWindow window, RECT newPosition);

        bool isWindowResizable(SystemWindow window);

        bool isWindowResizable(AutomationElement window);

        IEnumerable<AutomationElement> findResizableWindows(AutomationElement? parent = null, int depth = 1);
        IEnumerable<SystemWindow> findResizableWindows(SystemWindow? parent = null, int depth = 1);

    }

    [Component]
    public class WindowResizerImpl: WindowResizer {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private const int TWIPS_PER_INCH = 15;
        private const bool GAPLESS = true;

        private static readonly PropertyCondition TITLE_BAR_CONDITION =
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TitleBar);

        private static readonly AndCondition RESIZABLE_WINDOWS_CONDITION = new AndCondition(new Condition[] {
            new PropertyCondition(AutomationElement.IsWindowPatternAvailableProperty, true),
            new PropertyCondition(WindowPattern.WindowVisualStateProperty, WindowVisualState.Normal),
            new PropertyCondition(TransformPattern.CanResizeProperty, true)
        });

        // can't find a good way to detect this programmatically, so whitelist them
        // to blacklist a title suffix, use titlePattern: new Regex(@"^.*(?<! ‎- OneNote for Windows 10)$")
        // these are all case-sensitive
        private static readonly IEnumerable<WindowName> WINDOWS_WITH_NO_PADDING = new[] {
            new WindowName(className: "XLMAIN"), //Excel
            new WindowName(className: "OpusApp"), //Word
            new WindowName(className: "rctrl_renwnd32"), //Outlook
            new WindowName(executableBaseName: "powerpnt.exe"),
            new WindowName(executableBaseName: "devenv.exe"),
            new WindowName(className: "Chrome_WidgetWin_1"), //Chromium programs like Vivaldi and Logitech G Hub
            new WindowName(title: "Epic Games Launcher"),
            new WindowName(className: "vguiPopupWindow"), //Steam
            new WindowName(className: "Photoshop"),
            new WindowName(className: "illustrator"),
            new WindowName(className: "indesign"),
            new WindowName(className: "_macr_dreamweaver_frame_window_"),
            new WindowName(title: "TagScanner"),
            new WindowName(className: "ESET Main Frame"),
            new WindowName(className: "MozillaWindowClass"),
        };

        private static readonly RECT NO_PADDING = new RECT(0, 0, 0, 0);

        private readonly RECT defaultPadding;

        public WindowResizerImpl() {
            using var windowMetrics = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
            int borderWidth = Convert.ToInt32(windowMetrics.GetValue(@"BorderWidth", -15)) / -TWIPS_PER_INCH;
            int paddedBorderWidth = Convert.ToInt32(windowMetrics.GetValue(@"PaddedBorderWidth", -60)) / -TWIPS_PER_INCH;
            int totalPadding = 2 + borderWidth + paddedBorderWidth;
            const int BORDER_LINE_THICKNESS = 1;

            if (GAPLESS) {
                defaultPadding = new RECT(totalPadding+BORDER_LINE_THICKNESS, BORDER_LINE_THICKNESS, totalPadding + BORDER_LINE_THICKNESS, totalPadding + BORDER_LINE_THICKNESS);
            } else {
                defaultPadding = new RECT(totalPadding, 0, totalPadding, totalPadding);
            }

            // LOGGER.Debug("Default padding is {0}", defaultPadding.toString());
        }

        public RECT enlargeRectangle(RECT windowRectangle, RECT padding) {
            windowRectangle.Left -= padding.Left;
            windowRectangle.Right += padding.Right;
            windowRectangle.Bottom += padding.Bottom;
            windowRectangle.Top -= padding.Top;
            return windowRectangle;
        }

        public RECT shrinkRectangle(RECT windowRectangle, RECT padding) {
            var negativePadding = new RECT(-padding.Left, -padding.Top, -padding.Right, -padding.Bottom);
            return enlargeRectangle(windowRectangle, negativePadding);
        }

        public RECT getWindowPadding(SystemWindow window) {
            return isWindowWithNoPadding(window) ? NO_PADDING : defaultPadding;
        }

        internal static bool isWindowWithNoPadding(SystemWindow window) {
            return (from candidate in WINDOWS_WITH_NO_PADDING
                where (candidate.className?.Equals(window.ClassName) ?? true) &&
                      (candidate.titlePattern?.IsMatch(window.Title) ?? true) &&
                      (candidate.executableBaseNameWithoutExeExtension?.Equals(window.Process.ProcessName) ?? true)
                select candidate).Any();
        }

        public void moveWindowToPosition(SystemWindow window, RECT newPosition) {
            window.Position = newPosition;
        }

        public bool isWindowResizable(SystemWindow window) {
            return window.Resizable;
        }

        public bool isWindowResizable(AutomationElement window) {
            return (bool) window.GetCurrentPropertyValue(TransformPattern.CanResizeProperty);
        }

        public IEnumerable<AutomationElement> findResizableWindows(AutomationElement? parent = null, int depth = 1) {
            parent ??= AutomationElement.RootElement;

            if (depth > 0) {
                AutomationElementCollection children = parent.FindAll(TreeScope.Children, RESIZABLE_WINDOWS_CONDITION);
                // IEnumerable<AutomationElement> results = children.Cast<AutomationElement>().ToList();
                foreach (AutomationElement child in children) {
                    yield return child;
                }

                foreach (AutomationElement child in children) {
                    foreach (var grandChild in findResizableWindows(child, depth - 1)) {
                        yield return grandChild;
                    }
                }
            }
        }

        public IEnumerable<SystemWindow> findResizableWindows(SystemWindow? parent = null, int depth = 1) {
            // parent ??= AutomationElement.RootElement;

            if (depth > 0) {
                SystemWindow[] children = parent == null ? SystemWindow.FilterToplevelWindows(isResizableWindow) : parent.FilterDescendantWindows(true, isResizableWindow);
                // IEnumerable<AutomationElement> results = children.Cast<AutomationElement>().ToList();
                foreach (SystemWindow child in children) {
                    yield return child;
                }

                foreach (SystemWindow child in children) {
                    foreach (var grandChild in findResizableWindows(child, depth - 1)) {
                        yield return grandChild;
                    }
                }
            }
        }

        private static bool isResizableWindow(SystemWindow window) => window.Resizable && window.WindowState == FormWindowState.Normal;

    }

    internal readonly struct WindowName {

        public readonly string? executableBaseNameWithoutExeExtension;
        public readonly string? className;
        public readonly Regex? titlePattern;

        public WindowName(string? executableBaseName = null, string? className = null, string? title = null, Regex? titlePattern = null) {
            if (titlePattern != null && title != null) {
                throw new ArgumentException("Please specify at most 1 of the titlePattern and title arguments, not both.");
            }

            executableBaseNameWithoutExeExtension = executableBaseName != null ? Regex.Replace(executableBaseName, @"\.exe$", string.Empty, RegexOptions.IgnoreCase) : null;
            this.className = className;

            if (titlePattern != null) {
                this.titlePattern = titlePattern;
            } else if (title != null) {
                this.titlePattern = new Regex("^" + Regex.Escape(title) + "$");
            } else {
                this.titlePattern = null;
            }
        }

    }

}