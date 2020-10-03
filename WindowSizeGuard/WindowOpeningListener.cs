using System;
using ManagedWinapi.Windows;

#nullable enable

namespace WindowSizeGuard {

    public interface WindowOpeningListener {

        delegate void OnWindowOpened(SystemWindow window);
        event OnWindowOpened? windowOpened;

    }

    [Component]
    public class WindowOpeningListenerImpl: WindowOpeningListener, IDisposable {

        public event WindowOpeningListener.OnWindowOpened? windowOpened;

        private readonly ShellHook shellHook;

        public WindowOpeningListenerImpl() {
            shellHook = new ShellHookImpl();
            shellHook.shellEvent += (sender, args) => {
                if (args.shellEvent == ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED) {
                    windowOpened?.Invoke(new SystemWindow(args.windowHandle));
                }
            };
        }

        public void Dispose() {
            shellHook.Dispose();
        }

    }

}