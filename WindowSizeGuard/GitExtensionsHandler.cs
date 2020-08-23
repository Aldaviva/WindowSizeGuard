using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Windows.Automation;
using ManagedWinapi.Windows;
using NLog;
using ThrottleDebounce;

#nullable enable

namespace WindowSizeGuard {

    public interface GitExtensionsHandler {

        event CommitWindowOpenedEventHandler? commitWindowOpened;

        IEnumerable<SystemWindow> commitWindows { get; }

    }

    public delegate void CommitWindowOpenedEventHandler(SystemWindow newWindow);

    [Component]
    public class GitExtensionsHandlerImpl: GitExtensionsHandler, IDisposable {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
        private readonly Timer timer;

        public event CommitWindowOpenedEventHandler? commitWindowOpened;

        public IEnumerable<SystemWindow> commitWindows => new HashSet<SystemWindow>(_commitWindows);

        private readonly DebouncedAction<bool> _onWindowClosedThrottled;

        private ISet<SystemWindow> _commitWindows = new HashSet<SystemWindow>();

        public GitExtensionsHandlerImpl() {
            _onWindowClosedThrottled = Throttler.Throttle((bool firstRun) =>
                onWindowClosed(firstRun), TimeSpan.FromSeconds(2));

            timer = new Timer {
                AutoReset = true,
                Interval  = 200
            };

            timer.Elapsed += findCommitWindows;

            Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children, onWindowOpened);
            Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, TreeScope.Subtree, onWindowClosedThrottled);

            _onWindowClosedThrottled.Run(true);

            LOGGER.Trace("Waiting for Git Extensions commit window");
        }

        private void onWindowOpened(object? sender, AutomationEventArgs? e = null) {
            if (!timer.Enabled && sender != null && isGitExtensionsMainWindow(((AutomationElement) sender).toSystemWindow())) {
                timer.Enabled = true;
            }

            LOGGER.Trace("Window opened, timer is {0}", timer.Enabled ? "enabled" : "disabled");
        }

        private void onWindowClosedThrottled(object? sender = null, AutomationEventArgs? e = null) {
            _onWindowClosedThrottled.Run(false);
        }

        private void onWindowClosed(bool firstRun = false) {
            if (firstRun || timer.Enabled) {
                timer.Enabled = SystemWindow.FilterToplevelWindows(isGitExtensionsMainWindow).Any();
                LOGGER.Trace("Starting up or a window was closed, timer is {0}", timer.Enabled ? "enabled" : "disabled");
            }
        }

        private static bool isGitExtensionsMainWindow(SystemWindow window) => window.Title.EndsWith(" - Git Extensions");

        private void findCommitWindows(object? sender = null, ElapsedEventArgs? elapsedEventArgs = null) {
            ISet<SystemWindow> foundWindows = new HashSet<SystemWindow>(SystemWindow.FilterToplevelWindows(window =>
                window.ClassName == "WindowsForms10.Window.8.app.0.2bf8098_r7_ad1" && window.Title.StartsWith("Commit to ")));

            ISet<SystemWindow> newWindows = new HashSet<SystemWindow>(foundWindows);
            newWindows.ExceptWith(_commitWindows);

            _commitWindows = foundWindows;

            LOGGER.Trace("Found {0} commit windows, {1} of them new.", _commitWindows.Count, newWindows.Count);

            foreach (SystemWindow newWindow in newWindows) {
                commitWindowOpened?.Invoke(newWindow);
            }
        }

        public void Dispose() {
            Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, onWindowOpened);
            Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, onWindowClosedThrottled);

            timer.Stop();
            timer.Dispose();
        }

    }

}