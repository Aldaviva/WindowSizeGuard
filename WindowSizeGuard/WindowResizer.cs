#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using Microsoft.Win32;

namespace WindowSizeGuard {

    public interface WindowResizer {

        RECT enlargeRectangle(RECT windowRectangle, RECT padding);

        RECT shrinkRectangle(RECT windowRectangle, RECT padding);

        RECT getWindowPadding(SystemWindow window);

        void moveWindowToPosition(SystemWindow window, RECT newPosition);

        bool canWindowBeManuallyResized(SystemWindow window);

        IEnumerable<SystemWindow> findResizableWindows(SystemWindow? parent = null, int depth = 1);

        RECT getRelativePosition(RECT original, POINT relativeTo);

        double getRectangleDistance(RECT a, RECT b);

        bool canWindowBeAutomaticallyResized(SystemWindow window);

    }

    [Component]
    public class WindowResizerImpl: WindowResizer {

        private const int TWIPS_PER_INCH = 15;
        private static readonly bool GAPLESS_WINDOWS = true;

        // can't find a good way to detect this programmatically, so whitelist them
        // to blacklist a title suffix, use titlePattern: new Regex(@"^.*(?<! ‎- OneNote for Windows 10)$")
        // these are all case-sensitive
        private static readonly IEnumerable<WindowSelector> WINDOWS_WITH_NO_PADDING = new[] {
            new WindowSelector(className: "XLMAIN"),                          //Excel
            new WindowSelector(className: "OpusApp"),                         //Word
            new WindowSelector(className: "rctrl_renwnd32"),                  //Outlook
            new WindowSelector(className: "PPTFrameClass"),                   //PowerPoint
            new WindowSelector(executableBaseName: "devenv.exe"),             //Visual Studio
            new WindowSelector(className: "Chrome_WidgetWin_1"),              //Chromium programs like Vivaldi and Logitech G Hub
            new WindowSelector(className: "vguiPopupWindow"),                 //Steam
            new WindowSelector(title: "Epic Games Launcher"),                 //Epic Games Launcher
            new WindowSelector(className: "Photoshop"),                       //Photoshop
            new WindowSelector(className: "illustrator"),                     //Illustrator
            new WindowSelector(className: "indesign"),                        //InDesign
            new WindowSelector(className: "_macr_dreamweaver_frame_window_"), //Dreamweaver
            new WindowSelector(title: "TagScanner"),                          //TagScanner
            new WindowSelector(className: "ESET Main Frame"),                 //ESET NOD32
            new WindowSelector(className: "MozillaWindowClass")               //Firefox

            // new WindowSelector(className: "test"),
            // new WindowSelector(executableBaseName: "test"),
            // new WindowSelector(title: "test"),
            // new WindowSelector(title: new Regex("test")),
            // new WindowSelector(className: "test", title: new Regex("test")),
            // new WindowSelector(className: "test", title: "test"),
            // new WindowSelector(executableBaseName: "test", className: "test"),
            // new WindowSelector(executableBaseName: "test", className: "test"),
            // new WindowSelector(executableBaseName: "test", className: "test", title: "test"),
            // new WindowSelector(executableBaseName: "test", className: "test", title: new Regex("test")),
            // new WindowSelector(executableBaseName: "test", title: "test"),
            // new WindowSelector(executableBaseName: "test", title: new Regex("test")),
        };

        private static readonly RECT NO_PADDING = new RECT(0, 0, 0, 0);

        private readonly RECT defaultPadding;

        public WindowResizerImpl() {
            using var windowMetrics = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
            int borderWidth = Convert.ToInt32(windowMetrics.GetValue(@"BorderWidth", -15)) / -TWIPS_PER_INCH;
            int paddedBorderWidth = Convert.ToInt32(windowMetrics.GetValue(@"PaddedBorderWidth", -60)) / -TWIPS_PER_INCH;
            int totalPadding = 2 + borderWidth + paddedBorderWidth;
            const int BORDER_LINE_THICKNESS = 1;

            if (GAPLESS_WINDOWS) {
                defaultPadding = new RECT(
                    totalPadding + BORDER_LINE_THICKNESS,
                    BORDER_LINE_THICKNESS,
                    totalPadding + BORDER_LINE_THICKNESS,
                    totalPadding + BORDER_LINE_THICKNESS);
            } else {
                defaultPadding = new RECT(totalPadding, 0, totalPadding, totalPadding);
            }
        }

        public RECT enlargeRectangle(RECT windowRectangle, RECT padding) {
            windowRectangle.Left   -= padding.Left;
            windowRectangle.Right  += padding.Right;
            windowRectangle.Bottom += padding.Bottom;
            windowRectangle.Top    -= padding.Top;
            return windowRectangle;
        }

        public RECT shrinkRectangle(RECT windowRectangle, RECT padding) {
            var negativePadding = new RECT(-padding.Left, -padding.Top, -padding.Right, -padding.Bottom);
            return enlargeRectangle(windowRectangle, negativePadding);
        }

        public RECT getWindowPadding(SystemWindow window) => isWindowWithNoPadding(window) ? NO_PADDING : defaultPadding;

        internal static bool isWindowWithNoPadding(SystemWindow window) {
            return (from selector in WINDOWS_WITH_NO_PADDING
                    where (selector.className?.Equals(window.ClassName) ?? true) &&
                          (selector.titlePattern?.IsMatch(window.Title) ?? true) &&
                          (selector.executableBaseNameWithoutExeExtension?.Equals(window.Process.ProcessName) ?? true)
                    select selector).Any();
        }

        public void moveWindowToPosition(SystemWindow window, RECT newPosition) => window.Position = newPosition;

        public bool canWindowBeManuallyResized(SystemWindow window) => window.Resizable;

        public bool canWindowBeAutomaticallyResized(SystemWindow window) => window.Resizable && window.VisibilityFlag && (window.WindowState == FormWindowState.Normal);

        public IEnumerable<SystemWindow> findResizableWindows(SystemWindow? parent = null, int depth = 1) {
            if (depth <= 0) {
                yield break;
            }

            SystemWindow[] children = parent == null
                ? SystemWindow.FilterToplevelWindows(canWindowBeAutomaticallyResized)
                : parent.FilterDescendantWindows(true, canWindowBeAutomaticallyResized);

            foreach (SystemWindow child in children) {
                yield return child;
            }

            foreach (SystemWindow child in children) {
                foreach (var grandChild in findResizableWindows(child, depth - 1)) {
                    yield return grandChild;
                }
            }
        }

        public RECT getRelativePosition(RECT original, POINT relativeTo) {
            return new RECT(
                left_: original.Left - relativeTo.X,
                top_: original.Top - relativeTo.Y,
                right_: original.Right - relativeTo.X,
                bottom_: original.Bottom - relativeTo.Y);
        }

        public double getRectangleDistance(RECT a, RECT b) {
            double squaredEdgeDistances = 0;
            squaredEdgeDistances += Math.Pow(a.Top - b.Top, 2);
            squaredEdgeDistances += Math.Pow(a.Bottom - b.Bottom, 2);
            squaredEdgeDistances += Math.Pow(a.Left - b.Left, 2);
            squaredEdgeDistances += Math.Pow(a.Right - b.Right, 2);
            return Math.Sqrt(squaredEdgeDistances);
        }

    }

    internal readonly struct WindowSelector {

        public readonly string? executableBaseNameWithoutExeExtension;
        public readonly string? className;
        public readonly Regex? titlePattern;

        public WindowSelector(string? className = null, string? executableBaseName = null): this(executableBaseName, className, null, null) { }

        public WindowSelector(Regex title, string? className = null, string? executableBaseName = null): this(executableBaseName, className, null, title) { }

        public WindowSelector(string title, string? className = null, string? executableBaseName = null): this(executableBaseName, className, title, null) { }

        private WindowSelector(string? executableBaseName, string? className, string? title, Regex? titlePattern) {
            if (titlePattern != null && title != null) {
                throw new ArgumentException("Please specify at most 1 of the titlePattern and title arguments, not both.");
            }

            this.className = className;

            executableBaseNameWithoutExeExtension = executableBaseName != null
                ? Regex.Replace(executableBaseName, @"\.exe$", string.Empty, RegexOptions.IgnoreCase)
                : null;

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