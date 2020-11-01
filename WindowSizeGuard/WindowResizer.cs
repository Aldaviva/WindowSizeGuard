#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using WindowSizeGuard.ProgramHandlers;

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

        private const int TWIPS_PER_INCH        = 15;
        private const int BORDER_LINE_THICKNESS = 1;

        private static readonly RECT NO_PADDING = new RECT(0, 0, 0, 0);

        private readonly RECT defaultPadding;

        // can't find a good way to detect this programmatically, so whitelist them
        // to blacklist a title suffix, use titlePattern: new Regex(@"^.*(?<! ‎- OneNote for Windows 10)$")
        // these are all case-sensitive
        private readonly ICollection<WindowSelector> windowsWithNoPadding = new List<WindowSelector> {
            // Vivaldi will be injected into this list in the constructor below
            new WindowSelector(className: "XLMAIN"),                                               //Excel
            new WindowSelector(className: "OpusApp"),                                              //Word
            new WindowSelector(className: "rctrl_renwnd32"),                                       //Outlook
            new WindowSelector(className: "PPTFrameClass"),                                        //PowerPoint
            new WindowSelector(executableBaseName: "devenv.exe"),                                  //Visual Studio
            new WindowSelector(className: "Chrome_WidgetWin_1", title: "Logitech G HUB"),          //Logitech G Hub
            new WindowSelector(className: "Chrome_WidgetWin_1", title: "Visual Studio Installer"), //Visual Studio Installer
            new WindowSelector(className: "vguiPopupWindow"),                                      //Steam
            new WindowSelector(title: "Epic Games Launcher"),                                      //Epic Games Launcher
            new WindowSelector(className: "Photoshop"),                                            //Photoshop
            new WindowSelector(className: "illustrator"),                                          //Illustrator
            new WindowSelector(className: "indesign"),                                             //InDesign
            new WindowSelector(className: "_macr_dreamweaver_frame_window_"),                      //Dreamweaver
            new WindowSelector(className: "Bridge_WindowClass"),                                   //Bridge
            new WindowSelector(title: "TagScanner"),                                               //TagScanner
            new WindowSelector(className: "ESET Main Frame"),                                      //ESET NOD32
            new WindowSelector(className: "MozillaWindowClass"),                                   //Firefox
            new WindowSelector(className: "VMUIFrame"),                                            //VMware Workstation (affects version 16 and later)
            new WindowSelector(title: new Regex(@"^dnSpy v"))                               //dnSpy
        };

        public WindowResizerImpl(VivaldiHandler vivaldiHandler) {
            windowsWithNoPadding.Add(vivaldiHandler.windowSelector);

            using RegistryKey windowMetrics = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics")!;

            int borderWidth       = Convert.ToInt32(windowMetrics.GetValue(@"BorderWidth", -15)) / -TWIPS_PER_INCH;
            int paddedBorderWidth = Convert.ToInt32(windowMetrics.GetValue(@"PaddedBorderWidth", -60)) / -TWIPS_PER_INCH;
            int totalPadding      = 2 + borderWidth + paddedBorderWidth;

            defaultPadding = new RECT(
                left_: totalPadding + BORDER_LINE_THICKNESS,
                top_: BORDER_LINE_THICKNESS,
                right_: totalPadding + BORDER_LINE_THICKNESS,
                bottom_: totalPadding + BORDER_LINE_THICKNESS);
        }

        public RECT enlargeRectangle(RECT windowRectangle, RECT padding) {
            windowRectangle.Left   -= padding.Left;
            windowRectangle.Right  += padding.Right;
            windowRectangle.Bottom += padding.Bottom;
            windowRectangle.Top    -= padding.Top;
            return windowRectangle;
        }

        public RECT shrinkRectangle(RECT windowRectangle, RECT padding) {
            var negativePadding = new RECT(
                left_: -padding.Left,
                top_: -padding.Top,
                right_: -padding.Right,
                bottom_: -padding.Bottom);
            return enlargeRectangle(windowRectangle, negativePadding);
        }

        public RECT getWindowPadding(SystemWindow window) => isWindowWithNoPadding(window) ? NO_PADDING : defaultPadding;

        private bool isWindowWithNoPadding(SystemWindow window) {
            return (from selector in windowsWithNoPadding
                    where selector.matches(window)
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

        public RECT getRelativePosition(RECT original, POINT relativeTo) => new RECT(
            left_: original.Left - relativeTo.X,
            top_: original.Top - relativeTo.Y,
            right_: original.Right - relativeTo.X,
            bottom_: original.Bottom - relativeTo.Y);

        public double getRectangleDistance(RECT a, RECT b) {
            double squaredEdgeDistances = 0;
            squaredEdgeDistances += Math.Pow(a.Top - b.Top, 2);
            squaredEdgeDistances += Math.Pow(a.Bottom - b.Bottom, 2);
            squaredEdgeDistances += Math.Pow(a.Left - b.Left, 2);
            squaredEdgeDistances += Math.Pow(a.Right - b.Right, 2);
            return Math.Sqrt(squaredEdgeDistances);
        }

    }

}