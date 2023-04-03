#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using WindowSizeGuard.ProgramHandlers;

namespace WindowSizeGuard;

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

    private const int BORDER_LINE_THICKNESS = 1; // thickness of the semitransparent border that Windows 10 draws on most normal windows, like Notepad

    private readonly VivaldiHandler vivaldiHandler;

    public WindowResizerImpl(VivaldiHandler vivaldiHandler) {
        this.vivaldiHandler = vivaldiHandler;
    }

    public RECT enlargeRectangle(RECT windowRectangle, RECT padding) {
        windowRectangle.Left   -= padding.Left;
        windowRectangle.Right  += padding.Right;
        windowRectangle.Bottom += padding.Bottom;
        windowRectangle.Top    -= padding.Top;
        return windowRectangle;
    }

    public RECT shrinkRectangle(RECT windowRectangle, RECT padding) {
        RECT negativePadding = new(
            left_: -padding.Left,
            top_: -padding.Top,
            right_: -padding.Right,
            bottom_: -padding.Bottom);
        return enlargeRectangle(windowRectangle, negativePadding);
    }

    public RECT getWindowPadding(SystemWindow window) {
        /*
         * In Vivaldi 5.7, windows gained an invisible 1px border on the left, right, and bottom.
         * Without the following special case, there would be a 1px gap on those sides when resized by WindowSizeGuard.
         */
        if (vivaldiHandler.windowSelector.matches(window)) {
            return new RECT(1, 0, 1, 1);
        }

        RECT positionWithPadding    = window.Rectangle;
        RECT positionWithoutPadding = getAccuratePosition(window);

        //custom-drawn windows (like Office, Visual Studio, and Photoshop) don't have the 1px semitransparent border
        //traditional windows (like notepad or UWP apps) get a semitransparent 1px border that we want to exclude here, because abutting windows should not show the desktop between them
        bool isCustomDrawnWindow = positionWithPadding.Equals(positionWithoutPadding);
        int  borderThickness     = isCustomDrawnWindow ? 0 : BORDER_LINE_THICKNESS;

        return new RECT(
            left_: positionWithoutPadding.Left - positionWithPadding.Left + borderThickness,
            top_: positionWithPadding.Top - positionWithoutPadding.Top + borderThickness,
            right_: positionWithPadding.Right - positionWithoutPadding.Right + borderThickness,
            bottom_: positionWithPadding.Bottom - positionWithoutPadding.Bottom + borderThickness);
    }

    public void moveWindowToPosition(SystemWindow window, RECT newPosition) => window.Position = newPosition;

    public bool canWindowBeManuallyResized(SystemWindow window) => window.Resizable;

    public bool canWindowBeAutomaticallyResized(SystemWindow window) => window.Resizable && window.VisibilityFlag && window.WindowState == FormWindowState.Normal;

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
            foreach (SystemWindow? grandChild in findResizableWindows(child, depth - 1)) {
                yield return grandChild;
            }
        }
    }

    public RECT getRelativePosition(RECT original, POINT relativeTo) => new(
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

    private static RECT getAccuratePosition(SystemWindow window) {
        DwmGetWindowAttribute(window.HWnd, DwmWindowAttribute.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT extendedFrameBounds, Marshal.SizeOf<RECT>());
        return extendedFrameBounds;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr windowHandle, DwmWindowAttribute dwmWindowAttribute, out RECT resultBuffer, int resultBufferSize);

    private enum DwmWindowAttribute {

        DWMWA_NCRENDERING_ENABLED = 1,
        DWMWA_NCRENDERING_POLICY,
        DWMWA_TRANSITIONS_FORCEDISABLED,
        DWMWA_ALLOW_NCPAINT,
        DWMWA_CAPTION_BUTTON_BOUNDS,
        DWMWA_NONCLIENT_RTL_LAYOUT,
        DWMWA_FORCE_ICONIC_REPRESENTATION,
        DWMWA_FLIP3D_POLICY,
        DWMWA_EXTENDED_FRAME_BOUNDS,
        DWMWA_HAS_ICONIC_BITMAP,
        DWMWA_DISALLOW_PEEK,
        DWMWA_EXCLUDED_FROM_PEEK,
        DWMWA_CLOAK,
        DWMWA_CLOAKED,
        DWMWA_FREEZE_REPRESENTATION,
        DWMWA_LAST

    }

}