﻿#nullable enable

using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using Gma.System.MouseKeyHook.Implementation;
using ManagedWinapi.Windows;

namespace WindowSizeGuard {

    public static class WindowExtensions {

        public static bool isWinKeyPressed(this KeyEventArgsExt _) {
            KeyboardState keyboardState = KeyboardState.GetCurrent();
            return keyboardState.IsDown(Keys.LWin) || keyboardState.IsDown(Keys.RWin);
        }

        public static Rect toWindowsRect(this RECT mwinapiRect) {
            return new(mwinapiRect.Left, mwinapiRect.Top, mwinapiRect.Width, mwinapiRect.Height);
        }

        public static RECT toMwinapiRect(this Rect windowsRect) {
            return new((int) windowsRect.X, (int) windowsRect.Y, (int) windowsRect.Right, (int) windowsRect.Bottom);
        }

        public static string toString(this RECT rect) {
            return $"top: {rect.Top}, right: {rect.Right}, bottom: {rect.Bottom}, left: {rect.Left}";
        }

        public static SystemWindow toSystemWindow(this AutomationElement automationWindow) {
            return new(new IntPtr(automationWindow.Current.NativeWindowHandle));
        }

    }

}