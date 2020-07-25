#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using Timer = System.Threading.Timer;

namespace WindowSizeGuard {

    internal static class AvidemuxSizeGuard {

        private const string WINDOW_CLASS_NAME = "Qt5QWindowIcon";
        private static readonly TimeSpan WINDOW_SIZE_CHECK_FREQUENCY = TimeSpan.FromMilliseconds(100);
        private static readonly Size DEFAULT_WINDOW_SIZE = new Size(731, 689);
        private static readonly ManualResetEvent STAY_RUNNING = new ManualResetEvent(false);
        private static Timer? TIMER;

        private static void Main() {
            var mostRecentWindowSize = new MostRecentWindowGeometry();
            TIMER = new Timer(state => { getWindowSize((MostRecentWindowGeometry) state); }, mostRecentWindowSize, TimeSpan.Zero,
                WINDOW_SIZE_CHECK_FREQUENCY);

            STAY_RUNNING.WaitOne();
        }

        private static void getWindowSize(MostRecentWindowGeometry mostRecentWindowGeometry) {
            using Process? avidemuxProcess = Process.GetProcessesByName("avidemux").FirstOrDefault();

            if (avidemuxProcess != null) {
                try {
                    var avidemuxWindow = new SystemWindow(avidemuxProcess.MainWindowHandle);
                    if (avidemuxWindow.ClassName == WINDOW_CLASS_NAME && avidemuxWindow.WindowState != FormWindowState.Minimized) {
                        int newArea = avidemuxWindow.Rectangle.Width * avidemuxWindow.Rectangle.Height;
                        int oldArea = mostRecentWindowGeometry.size.Width * mostRecentWindowGeometry.size.Height;

                        // Console.WriteLine(
                        //     $"{avidemuxWindow.Style}; {avidemuxWindow.ExtendedStyle}; {avidemuxWindow.Title}; {avidemuxWindow.ClassName}; {avidemuxWindow.Parent}");

                        bool shrankTooMuch = newArea < 0.75 * oldArea;
                        bool fileWasClosed = avidemuxWindow.Size == DEFAULT_WINDOW_SIZE && mostRecentWindowGeometry.size != DEFAULT_WINDOW_SIZE && mostRecentWindowGeometry.size != default;
                        bool fileWasOpened = shrankTooMuch;

                        if (fileWasOpened || fileWasClosed) {
                            avidemuxWindow.Location = mostRecentWindowGeometry.location;

                            Size slightlySmaller = mostRecentWindowGeometry.size;
                            slightlySmaller.Width -= 2;
                            avidemuxWindow.Size = slightlySmaller;
                            Thread.Sleep(40);

                            avidemuxWindow.Size = mostRecentWindowGeometry.size;

                            Console.WriteLine("resized");
                        }

                        mostRecentWindowGeometry.size = avidemuxWindow.Size;
                        mostRecentWindowGeometry.location = avidemuxWindow.Location;
                        mostRecentWindowGeometry.title = avidemuxWindow.Title;

                        Console.WriteLine(mostRecentWindowGeometry);
                    }
                } catch (Win32Exception) {
                    //continue to next timer loop
                }
            } else {
                TIMER?.Change(-1, -1);
                STAY_RUNNING.Set();
            }
        }

        private class MostRecentWindowGeometry {

            internal Size size;
            internal Point location;
            internal string? title;

            public override string ToString() {
                return $"{nameof(size)}: {size}, {nameof(location)}: {location}, {nameof(title)}: {title}";
            }

        }

    }

}