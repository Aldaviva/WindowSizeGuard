using ManagedWinapi.Windows;

#nullable enable

namespace WindowSizeGuard.ProgramHandlers {

    public interface VivaldiHandler {

        bool isWindowVivaldi(SystemWindow window);

        void fixVivaldiResizeBug(SystemWindow window);

    }

    [Component]
    public class VivaldiHandlerImpl: VivaldiHandler {

        public bool isWindowVivaldi(SystemWindow window) => window.ClassName == "Chrome_WidgetWin_1" && window.Process.ProcessName == "vivaldi";

        public void fixVivaldiResizeBug(SystemWindow window) {
            RECT windowSize = window.Position;
            windowSize.Right++;
            windowSize.Bottom++;
            window.Position = windowSize;
        }

    }

}