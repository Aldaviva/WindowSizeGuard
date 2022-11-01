#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Windows.Automation;
using ManagedWinapi.Windows;
using NLog;
using ThrottleDebounce;
using Timer = System.Timers.Timer;

namespace WindowSizeGuard.ProgramHandlers;

public interface GitExtensionsHandler {

    delegate void CommitWindowOpenedEventHandler(SystemWindow newWindow);
    event CommitWindowOpenedEventHandler? commitWindowOpened;

    IEnumerable<SystemWindow> commitWindows { get; }

}

[Component]
public class GitExtensionsHandlerImpl: GitExtensionsHandler, IDisposable {

    private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
    private readonly        Timer  timer;

    public event GitExtensionsHandler.CommitWindowOpenedEventHandler? commitWindowOpened;

    public IEnumerable<SystemWindow> commitWindows => new HashSet<SystemWindow>(_commitWindows);

    private readonly RateLimitedAction<bool> _onWindowClosedThrottled;

    private ISet<SystemWindow> _commitWindows = new HashSet<SystemWindow>();

    public GitExtensionsHandlerImpl(WindowOpeningListener windowOpeningListener) {
        _onWindowClosedThrottled = Throttler.Throttle((bool firstRun) =>
            onWindowClosed(firstRun), TimeSpan.FromSeconds(2));

        timer = new Timer {
            AutoReset = true,
            Interval  = 200
        };

        timer.Elapsed += findCommitWindows;

        windowOpeningListener.windowOpened += onWindowOpened;
        Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, TreeScope.Subtree, onWindowClosedThrottled);

        _onWindowClosedThrottled.Invoke(true);

        LOGGER.Trace("Waiting for Git Extensions commit window");
    }

    private void onWindowOpened(SystemWindow? window) {
        if (window is null) return;
        if (!timer.Enabled && isGitExtensionsMainWindow(window)) {
            timer.Enabled = true;
        }

        LOGGER.Trace("Window opened, timer is {0}", timer.Enabled ? "enabled" : "disabled");
    }

    private void onWindowClosedThrottled(object? sender = null, AutomationEventArgs? e = null) {
        _onWindowClosedThrottled.Invoke(false);
    }

    private void onWindowClosed(bool firstRun = false) {
        if (firstRun || timer.Enabled) {
            timer.Enabled = SystemWindow.FilterToplevelWindows(isGitExtensionsMainWindow).Any();
            LOGGER.Trace("Starting up or a window was closed, timer is {0}", timer.Enabled ? "enabled" : "disabled");
        }
    }

    private static bool isGitExtensionsMainWindow(SystemWindow window) {
        try {
            return window.Title.EndsWith(" - Git Extensions");
        } catch (Win32Exception) {
            return false;
        }
    }

    private void findCommitWindows(object? sender = null, ElapsedEventArgs? elapsedEventArgs = null) {
        ISet<SystemWindow> foundWindows = new HashSet<SystemWindow>(SystemWindow.FilterToplevelWindows(window =>
            window.ClassName == "WindowsForms10.Window.8.app.0.2bf8098_r7_ad1" && window.Title.StartsWith("Commit")));

        ISet<SystemWindow> newWindows = new HashSet<SystemWindow>(foundWindows);
        newWindows.ExceptWith(_commitWindows);

        _commitWindows = foundWindows;

        LOGGER.Trace("Found {0} commit windows, {1} of them new.", _commitWindows.Count, newWindows.Count);

        foreach (SystemWindow newWindow in newWindows) {
            //commit windows are sometimes created with visibilityflag=false briefly, which prevents our automatic resizing (intentionally), so wait for up to 0.5 seconds for it to become visible before triggering callbacks
            SpinWait.SpinUntil(() => newWindow.VisibilityFlag, 500);
            commitWindowOpened?.Invoke(newWindow);
        }
    }

    public void Dispose() {
        Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, AutomationElement.RootElement, onWindowClosedThrottled);

        timer.Stop();
        timer.Dispose();

        _onWindowClosedThrottled.Dispose();
    }

}