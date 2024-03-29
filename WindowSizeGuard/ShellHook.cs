﻿#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowSizeGuard; 

public interface ShellHook: IDisposable {

    delegate void ShellEventHandler(object? sender, ShellEventArgs eventArgs);
    event ShellEventHandler? shellEvent;

}

public class ShellHookImpl: Form, ShellHook {

    public event ShellHook.ShellEventHandler? shellEvent;

    private readonly uint subscriptionId;

    public ShellHookImpl() {
        subscriptionId = RegisterWindowMessage("SHELLHOOK");
        RegisterShellHookWindow(Handle);
    }

    protected override void WndProc(ref Message message) {
        if (message.Msg == subscriptionId) {
            shellEvent?.Invoke(this, new ShellEventArgs(shellEvent: (ShellEventArgs.ShellEvent) message.WParam.ToInt32(), windowHandle: message.LParam));
        }

        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing) {
        DeregisterShellHookWindow(Handle);
        base.Dispose(disposing);
    }

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool DeregisterShellHookWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool RegisterShellHookWindow(IntPtr hWnd);

}

public class ShellEventArgs: EventArgs {

    public readonly ShellEvent shellEvent;
    public readonly IntPtr     windowHandle;

    public ShellEventArgs(ShellEvent shellEvent, IntPtr windowHandle) {
        this.shellEvent   = shellEvent;
        this.windowHandle = windowHandle;
    }

    public enum ShellEvent {

        HSHELL_WINDOWCREATED       = 1,
        HSHELL_WINDOWDESTROYED     = 2,
        HSHELL_ACTIVATESHELLWINDOW = 3,
        HSHELL_WINDOWACTIVATED     = 4,
        HSHELL_GETMINRECT          = 5,
        HSHELL_REDRAW              = 6,
        HSHELL_TASKMAN             = 7,
        HSHELL_LANGUAGE            = 8,
        HSHELL_ACCESSIBILITYSTATE  = 11,
        HSHELL_APPCOMMAND          = 12

    }

}