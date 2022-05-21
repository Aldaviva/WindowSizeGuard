#nullable enable

using System;
using System.Collections.Concurrent;
using System.Windows.Automation;
using ManagedWinapi.Windows;

namespace WindowSizeGuard;

public interface WindowOpeningListener {

    delegate void OnWindowOpened(SystemWindow? window);
    event OnWindowOpened? windowOpened;

}

[Component]
public class WindowOpeningListenerImpl: WindowOpeningListener, IDisposable {

    public event WindowOpeningListener.OnWindowOpened? windowOpened;

    private readonly ShellHook shellHook;

    private readonly ConcurrentDictionary<int, ValueHolder<long>> alreadyOpenedWindows =
        ConcurrentDictionaryExtensions.createConcurrentDictionary<int, long>();

    public WindowOpeningListenerImpl() {
        shellHook            =  new ShellHookImpl();
        shellHook.shellEvent += onWindowOpened;

        Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children, onWindowOpened);
    }

    private void onWindowOpened(object? sender, ShellEventArgs args) {
        if (args.shellEvent == ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED) {
            onWindowOpened(new SystemWindow(args.windowHandle));
        }
    }

    private void onWindowOpened(object? sender, AutomationEventArgs e) {
        if (sender is AutomationElement windowEl) {
            onWindowOpened(windowEl.toSystemWindow());
        }
    }

    private void onWindowOpened(SystemWindow window) {
        DateTime now            = DateTime.Now;
        DateTime lastOpenedTime = DateTime.FromBinary(alreadyOpenedWindows.exchange(window.HWnd.ToInt32(), now.ToBinary()));

        bool isNewWindow = lastOpenedTime == now;
        if (isNewWindow) {
            windowOpened?.Invoke(window);
        }
    }

    public void Dispose() {
        shellHook.Dispose();
        Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, onWindowOpened);
    }

}