using System.Text.RegularExpressions;
using ManagedWinapi.Windows;

#nullable enable

namespace WindowSizeGuard.ProgramHandlers {

    public interface VivaldiHandler {

        void fixVivaldiResizeBug(SystemWindow window);

        WindowSelector windowSelector { get; }

    }

    [Component]
    public class VivaldiHandlerImpl: VivaldiHandler {

        public WindowSelector windowSelector { get; } = new WindowSelector(className: "Chrome_WidgetWin_1", title: new Regex(@" - Vivaldi$")); //Vivaldi, but not the detached DevTools windows whose titles don't end with - Vivaldi

        public void fixVivaldiResizeBug(SystemWindow window) {
            RECT windowSize = window.Position;
            windowSize.Right++;
            windowSize.Bottom++;
            window.Position = windowSize;
        }


    }

}