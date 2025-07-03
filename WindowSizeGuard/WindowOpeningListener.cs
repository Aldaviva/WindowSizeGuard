#nullable enable

using ManagedWinapi.Windows;
using System;
using System.Collections.Concurrent;
using System.Windows.Automation;
using Unfucked;

namespace WindowSizeGuard;

public interface WindowOpeningListener {

    delegate void OnWindowOpened(SystemWindow? window);

    event OnWindowOpened? windowOpened;

}

[Component]
public class WindowOpeningListenerImpl: WindowOpeningListener, IDisposable {

    public event WindowOpeningListener.OnWindowOpened? windowOpened;

    private readonly ShellHook shellHook;

    private readonly ConcurrentDictionary<int, ValueHolder<long>> alreadyOpenedWindows = Enumerables.CreateConcurrentDictionary<int, long>();

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
        if (sender is AutomationElement windowEl && windowEl.ToSystemWindow() is { } systemWindow) {
            onWindowOpened(systemWindow);
        }
    }

    private void onWindowOpened(SystemWindow window) {
        DateTime  now            = DateTime.Now;
        DateTime? lastOpenedTime = alreadyOpenedWindows.Exchange(window.HWnd.ToInt32(), now.ToBinary()) is { } openedTime ? DateTime.FromBinary(openedTime) : null;

        bool isNewWindow = lastOpenedTime == null;
        if (isNewWindow) {
            windowOpened?.Invoke(window);
        }
    }

    public void Dispose() {
        shellHook.Dispose();
        Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, onWindowOpened);
    }

}