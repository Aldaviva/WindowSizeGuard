#nullable enable

using Gma.System.MouseKeyHook;
using Gma.System.MouseKeyHook.Implementation;
using ManagedWinapi.Windows;
using System.Windows.Forms;

namespace WindowSizeGuard;

public static class WindowExtensions {

    public static bool isWinKeyPressed(this KeyEventArgsExt _) {
        KeyboardState keyboardState = KeyboardState.GetCurrent();
        return keyboardState.IsDown(Keys.LWin) || keyboardState.IsDown(Keys.RWin);
    }

    public static string toString(this RECT rect) {
        return $"top: {rect.Top}, right: {rect.Right}, bottom: {rect.Bottom}, left: {rect.Left}";
    }

    /*public static Rect toWindowsRect(this RECT mwinapiRect) {
        return new Rect(mwinapiRect.Left, mwinapiRect.Top, mwinapiRect.Width, mwinapiRect.Height);
    }

    public static RECT toMwinapiRect(this Rect windowsRect) {
        return new RECT((int) windowsRect.X, (int) windowsRect.Y, (int) windowsRect.Right, (int) windowsRect.Bottom);
    }


    public static SystemWindow toSystemWindow(this AutomationElement automationWindow) {
        return new SystemWindow(new IntPtr(automationWindow.Current.NativeWindowHandle));
    }

    public static string getProcessExecutableBasename(this SystemWindow window) {
        using Process process = window.Process;
        return process.ProcessName;
    }*/

}