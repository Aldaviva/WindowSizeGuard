using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedWinapi.Windows;
using NLog;
using Timer = System.Timers.Timer;

#nullable enable

namespace WindowSizeGuard.ProgramHandlers {

    public interface ExplorerDesktopHandler { }

    /// <summary>
    /// Underlying Windows defect should have been fixed in KB5003214 2021-05 Cumulative Update For Windows 10 Version 21H1
    /// </summary>
    [Obsolete]
    // [Component]
    public class ExplorerDesktopHandlerImpl: ExplorerDesktopHandler, IDisposable {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private const int  WM_KEYDOWN = 0x0100;
        private const int  WM_KEYUP   = 0x0101;
        private const int  F5         = 0x74;
        private const long F5_DOWN    = 0x003F0001;
        private const long F5_UP      = 0xC03F0001;

        private static readonly TimeSpan MAGIC_HOTKEY_DELAY           = TimeSpan.FromMilliseconds(600); //563 works, 500 fails?, 75 works, 250 seems more safe
        private static readonly TimeSpan MAX_REFRESH_RETRY_DURATION   = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan REFRESH_RETRY_INTERVAL       = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan COUNT_DESKTOP_ICONS_INTERVAL = TimeSpan.FromSeconds(30);

        private readonly Timer desktopIconsCounterTimer;

        private int           desktopIconsCount;
        private SystemWindow? desktopIconsWindow;

        public ExplorerDesktopHandlerImpl(ToolbarAwareSizeGuard toolbarAwareSizeGuard) {
            toolbarAwareSizeGuard.toolbarVisibilityChanged += toolbarVisibilityChanged;

            desktopIconsWindow = findDesktopIconsWindow();

            desktopIconsCounterTimer = new Timer {
                AutoReset = true,
                Interval  = COUNT_DESKTOP_ICONS_INTERVAL.TotalMilliseconds,
                Enabled   = true
            };
            desktopIconsCounterTimer.Elapsed += (sender, args) => desktopIconsCount = countDesktopIcons();
            desktopIconsCount                =  countDesktopIcons();
        }

        private static SystemWindow? findDesktopIconsWindow() {
            return SystemWindow
                .FilterToplevelWindows(window => window.ClassName == "WorkerW" || window.ClassName == "Progman")
                .SelectMany(parent => parent.FilterDescendantWindows(true, child => child.ClassName == "SHELLDLL_DefView"))
                .SelectMany(parent => parent.FilterDescendantWindows(true, child => child.ClassName == "SysListView32"))
                .FirstOrDefault();
        }

        private int countDesktopIcons() {
            static int getPropertyListCount() {
                int propertyListCount = findDesktopIconsWindow()?.Content.PropertyList.Count - 5 ?? 0;
                LOGGER.Trace($"There are {propertyListCount:N0} desktop icons or something.");
                return propertyListCount;
            }

            try {
                return getPropertyListCount();
            } catch (Exception e) when (!(e is OutOfMemoryException)) {
                desktopIconsWindow = findDesktopIconsWindow();
                return getPropertyListCount();
            }
        }

        private void toolbarVisibilityChanged(bool isToolbarVisible) {
            LOGGER.Debug("Toolbar visibility changed.");
            desktopIconsCounterTimer.Enabled = false;
            /* I think Windows has a global timer on keyboard shortcuts. If you try to send F5 to Explorer, Windows or Explorer
             * will ignore it because it is already in the middle of processing Ctrl+Alt+W to toggle the Winamp toolbar, and Windows
             * will only handle one keyboard shortcut at a time. To work around this, wait until the Ctrl+Alt+W shortcut is done being
             * handled, then try to inject F5 into Explorer.
             */

            int oldDesktopIconCount = desktopIconsCount;
            int newDesktopIconCount = countDesktopIcons();

            Task task;

            if (newDesktopIconCount > oldDesktopIconCount) {
                /*
                 * The number of desktop icons changed between when we last checked and when the toolbar was toggled, so a deleted file probably reappeared.
                 * Try aggressively to F5 Explorer every 50ms until the number of icons changes again or 2 seconds pass.
                 */

                task = Task.Run(async () => {
                    Stopwatch totalRetryDuration = Stopwatch.StartNew();

                    for (int attempts = 1; totalRetryDuration.Elapsed < MAX_REFRESH_RETRY_DURATION; attempts++) {
                        refreshDesktop();

                        if (countDesktopIcons() != newDesktopIconCount) {
                            LOGGER.Debug($"Fixed desktop in {attempts:N0} attempts over {totalRetryDuration.ElapsedMilliseconds:N0} ms.");
                            break;
                        } else {
                            await Task.Delay(REFRESH_RETRY_INTERVAL);
                        }
                    }

                    totalRetryDuration.Stop();
                });

            } else {
                /*
                 * The number of desktop icons did not change between when we last checked and when the toolbar was last toggled, so there are probably no deleted
                 * files that have reappeared.
                 * Just to be safe, F5 Explorer once after a little delay.
                 */
                task = Task.Delay(MAGIC_HOTKEY_DELAY).ContinueWith(task1 => { refreshDesktop(); });
            }

            task.ContinueWith(task1 => {
                desktopIconsCount                = countDesktopIcons();
                desktopIconsCounterTimer.Enabled = true;
            });
        }

        private bool refreshDesktop() {
            if (desktopIconsWindow == null) return false;

            LOGGER.Debug($"Posting F5 to {desktopIconsWindow.HWnd.ToInt64():X}...");

            bool keyDownResult = PostMessage(desktopIconsWindow.HWnd, WM_KEYDOWN, new IntPtr(F5), new IntPtr(F5_DOWN));
            Thread.Sleep(30);
            bool keyUpResult = PostMessage(desktopIconsWindow.HWnd, WM_KEYUP, new IntPtr(F5), new IntPtr(F5_UP));

            bool success = keyDownResult && keyUpResult;
            if (!success) {
                LOGGER.Warn($"Failed to post WM_KEYUP or WM_KEYDOWN to Explorer window (results =  keyDown: {keyDownResult}, keyUp: {keyUpResult})");
            }

            return success;
        }

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        public void Dispose() {
            desktopIconsCounterTimer.Dispose();
        }

    }

}