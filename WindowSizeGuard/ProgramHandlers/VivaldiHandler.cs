using System.Text.RegularExpressions;
using ManagedWinapi.Windows;
using NLog;

#nullable enable

namespace WindowSizeGuard.ProgramHandlers {

    public interface VivaldiHandler {

        void fixVivaldiResizeBug(SystemWindow window);

        WindowSelector windowSelector { get; }

    }

    [Component]
    public class VivaldiHandlerImpl: VivaldiHandler {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private const int MAX_RESIZE_ATTEMPTS = 10; //usually only 1 or 2 attempts are needed

        //Vivaldi main windows, Settings window, and PiP video window, but not the detached DevTools windows
        public WindowSelector windowSelector { get; } = new(className: "Chrome_WidgetWin_1", title: new Regex(@"(?: - Vivaldi$)|(?:^Picture in picture$)"));

        public void fixVivaldiResizeBug(SystemWindow window) {
            //SystemWindow.Position is inaccurate in Windows 10 because traditional (non-metro) windows report a Position that is bigger than the actual window's pixels, but this is okay because we're only do relative resizing here.
            RECT oldSize     = window.Position;
            RECT desiredSize = oldSize;
            desiredSize.Right++;
            desiredSize.Bottom++;

            // For some reason, it takes two attempts to resize the PiP video window if a main Vivaldi window is also open and not minimized. If Vivaldi is minimized, the PiP window can be
            // resized on the first attempt.
            for (int attempts = 0; !window.Position.Equals(desiredSize) && attempts < MAX_RESIZE_ATTEMPTS; attempts++) {
                window.Position = desiredSize;
                LOGGER.Trace($"Resized Vivaldi window {window.Title} from ({oldSize.Width}px × {oldSize.Height}px) to ({desiredSize.Width}px × {desiredSize.Height}px)");
            }
        }

    }

}