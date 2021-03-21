#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using ManagedWinapi.Windows;
using MoreLinq;

namespace WindowSizeGuard {

    public interface WindowZoneManager {

        void toggleForegroundWindowAlwaysOnTop();

        void maximizeForegroundWindow();

        void minimizeForegroundWindow();

        void resizeWindowToZone(SystemWindow window, WindowZone zone, int? rectangleInZone = null);

        WindowZoneSearchResult findClosestZoneRectangleToWindow(RECT windowPosition, RECT workingArea);

    }

    public enum WindowZone {

        RIGHT,
        LEFT,
        TOP,
        BOTTOM,
        TOP_LEFT,
        TOP_RIGHT,
        BOTTOM_LEFT,
        BOTTOM_RIGHT,
        CENTER

    }

    public struct WindowZoneSearchResult {

        public Rect       proportionalZoneRectangle;
        public RECT       actualZoneRectPosition;
        public double     distance;
        public WindowZone zone;
        public int        zoneRectangleIndex;

    }

    [Component]
    public class WindowZoneManagerImpl: WindowZoneManager {

        private const int RECTANGLE_DISTANCE_SAME  = 1;
        private const int RECTANGLE_DISTANCE_CLOSE = 5;

        private readonly WindowResizer windowResizer;

        public WindowZoneManagerImpl(WindowResizer windowResizer) {
            this.windowResizer = windowResizer;
        }

        public void toggleForegroundWindowAlwaysOnTop() {
            SystemWindow.ForegroundWindow.TopMost ^= true;
        }

        public void maximizeForegroundWindow() {
            SystemWindow.ForegroundWindow.WindowState = FormWindowState.Maximized;
        }

        public void minimizeForegroundWindow() {
            SystemWindow.ForegroundWindow.WindowState = FormWindowState.Minimized;
        }

        public void resizeWindowToZone(SystemWindow window, WindowZone zone, int? rectangleInZone = null) {
            FormWindowState oldWindowState = window.WindowState;

            if (!window.Resizable && oldWindowState != FormWindowState.Maximized) {
                return;
            }

            if (oldWindowState == FormWindowState.Maximized) {
                rectangleInZone = 0;
            }

            IList<Rect> proportionalRectanglesForZone = getProportionalRectanglesForZone(zone).ToList();
            RECT        windowPadding                 = windowResizer.getWindowPadding(window);
            Rect        proportionalRectangleToResizeTo;

            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;

            if (rectangleInZone != null) {
                proportionalRectangleToResizeTo = proportionalRectanglesForZone[rectangleInZone.Value];
            } else {
                WindowZoneSearchResult closestZoneRectangleToOldWindowPosition =
                    findClosestZoneRectangleToWindow(windowResizer.shrinkRectangle(window.Position, windowPadding), workingArea, zone);

                int zoneRectangleIndex = closestZoneRectangleToOldWindowPosition switch {
                    var r when r.distance <= RECTANGLE_DISTANCE_SAME  => (r.zoneRectangleIndex + 1) % proportionalRectanglesForZone.Count,
                    var r when r.distance <= RECTANGLE_DISTANCE_CLOSE => r.zoneRectangleIndex,
                    _                                                 => 0
                };

                proportionalRectangleToResizeTo = proportionalRectanglesForZone[zoneRectangleIndex];
            }

            RECT newPositionRelativeToWorkingArea = convertProportionalRectangleToActualRectangle(proportionalRectangleToResizeTo, workingArea);
            RECT newPositionRelativeToScreen      = windowResizer.getRelativePosition(newPositionRelativeToWorkingArea, workingArea.Location);
            RECT newPositionWithPaddingRemoved    = windowResizer.enlargeRectangle(newPositionRelativeToScreen, windowPadding);

            // LOGGER.Debug(
            //     $"Moving {window.Process.ProcessName} to [top={newPositionRelativeToScreen.Top}, bottom={newPositionRelativeToScreen.Bottom}, left={newPositionRelativeToScreen.Left}, right={newPositionRelativeToScreen.Right}, width={newPositionRelativeToScreen.Width}, height={newPositionRelativeToScreen.Height}], accounting for window padding of {windowPadding.toString()}");

            if (oldWindowState == FormWindowState.Maximized) {
                window.WindowState = FormWindowState.Normal;
            }

            windowResizer.moveWindowToPosition(window, newPositionWithPaddingRemoved);
        }

        private static RECT convertProportionalRectangleToActualRectangle(Rect proportionalRectangle, RECT actualRectangle) {
            return new((int) (actualRectangle.Width * proportionalRectangle.Left) + actualRectangle.Left,
                (int) (actualRectangle.Height * proportionalRectangle.Top) + actualRectangle.Top,
                (int) (actualRectangle.Width * proportionalRectangle.Right) + actualRectangle.Left,
                (int) (actualRectangle.Height * proportionalRectangle.Bottom) + actualRectangle.Top
            );
        }

        public WindowZoneSearchResult findClosestZoneRectangleToWindow(RECT windowPosition, RECT workingArea) {
            return Enum.GetValues(typeof(WindowZone)).Cast<WindowZone>()
                .Select(zone => findClosestZoneRectangleToWindow(windowPosition, workingArea, zone))
                .MinBy(result => result.distance).First();
        }

        private WindowZoneSearchResult findClosestZoneRectangleToWindow(RECT windowPosition, RECT workingArea, WindowZone zone) {
            IEnumerable<Rect> proportionalZoneRectangles = getProportionalRectanglesForZone(zone);

            IEnumerable<WindowZoneSearchResult> windowZoneSearchResults = proportionalZoneRectangles.Select((proportionalZoneRectangle, zoneIndex) => {
                var result = new WindowZoneSearchResult {
                    proportionalZoneRectangle = proportionalZoneRectangle,
                    actualZoneRectPosition    = windowResizer.getRelativePosition(convertProportionalRectangleToActualRectangle(proportionalZoneRectangle, workingArea), workingArea.Location),
                    zone                      = zone,
                    zoneRectangleIndex        = zoneIndex
                };
                result.distance = windowResizer.getRectangleDistance(result.actualZoneRectPosition, windowPosition);

                return result;
            });

            return windowZoneSearchResults.MinBy(result => result.distance).First();
        }

        private static IEnumerable<Rect> getProportionalRectanglesForZone(WindowZone zone) {
            return zone switch {
                WindowZone.RIGHT => new[] {
                    new Rect(0.5, 0, 0.5, 1),
                    new Rect(2.0 / 3.0, 0, 1.0 / 3.0, 1),
                    new Rect(1.0 / 3.0, 0, 2.0 / 3.0, 1),
                    new Rect(0.75, 0, 0.25, 1),
                    new Rect(0.5, 0, 0.25, 1)
                },
                WindowZone.LEFT => new[] {
                    new Rect(0, 0, 0.5, 1),
                    new Rect(0, 0, 1.0 / 3.0, 1),
                    new Rect(0, 0, 2.0 / 3.0, 1),
                    new Rect(0, 0, 0.25, 1),
                    new Rect(0.25, 0, 0.25, 1),
                },
                WindowZone.TOP => new[] {
                    new Rect(0, 0, 1, 0.5),
                    new Rect(1.0 / 3.0, 0, 1.0 / 3.0, 0.5)
                },
                WindowZone.BOTTOM => new[] {
                    new Rect(0, 0.5, 1, 0.5),
                    new Rect(1.0 / 3.0, 0.5, 1.0 / 3.0, 0.5)
                },
                WindowZone.TOP_LEFT => new[] {
                    new Rect(0, 0, 0.5, 0.5),
                    new Rect(0, 0, 1.0 / 3.0, 0.5),
                    new Rect(0, 0, 2.0 / 3.0, 0.5),
                    new Rect(0, 0, 0.25, 0.5),
                    new Rect(0.25, 0, 0.25, 0.5)
                },
                WindowZone.TOP_RIGHT => new[] {
                    new Rect(0.5, 0, 0.5, 0.5),
                    new Rect(2.0 / 3.0, 0, 1.0 / 3.0, 0.5),
                    new Rect(1.0 / 3.0, 0, 2.0 / 3.0, 0.5),
                    new Rect(0.75, 0, 0.25, 0.5),
                    new Rect(0.5, 0, 0.25, 0.5)
                },
                WindowZone.BOTTOM_LEFT => new[] {
                    new Rect(0, 0.5, 0.5, 0.5),
                    new Rect(0, 0.5, 1.0 / 3.0, 0.5),
                    new Rect(0, 0.5, 2.0 / 3.0, 0.5),
                    new Rect(0, 0.5, 0.25, 0.5),
                    new Rect(0.25, 0.5, 0.25, 0.5)
                },
                WindowZone.BOTTOM_RIGHT => new[] {
                    new Rect(0.5, 0.5, 0.5, 0.5),
                    new Rect(2.0 / 3.0, 0.5, 1.0 / 3.0, 0.5),
                    new Rect(1.0 / 3.0, 0.5, 2.0 / 3.0, 0.5),
                    new Rect(0.75, 0.5, 0.25, 0.5),
                    new Rect(0.5, 0.5, 0.25, 0.5)
                },
                WindowZone.CENTER => new[] {
                    new Rect(0.25, 0, 0.5, 1),
                    new Rect(1.0 / 3.0, 0, 1.0 / 3.0, 1),
                    new Rect(0, 0, 1, 1)
                },
                _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, "unknown WindowZone")
            };
        }

    }

}