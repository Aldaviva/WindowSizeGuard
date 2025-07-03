#nullable enable

using Autofac;
using NLog;
using System;
using System.Windows.Forms;
using WindowSizeGuard.ProgramHandlers;

namespace WindowSizeGuard;

public static class MainClass {

    private static readonly Logger LOGGER = LogManager.GetLogger(typeof(MainClass).FullName!);

    [STAThread] //important, otherwise hotkeys don't trigger and Windows times out after ~2 seconds, then handles the hotkey normally
    public static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppDomain.CurrentDomain.UnhandledException += (_, args) => LOGGER.Error(args.ExceptionObject);

        using IContainer     autofacContainer = AutofacHelpers.createContainer();
        using ILifetimeScope scope            = autofacContainer.BeginLifetimeScope();
        scope.Resolve<ToolbarAwareSizeGuard>();
        scope.Resolve<HotkeyHandler>();
        scope.Resolve<MicrosoftManagementConsoleHandler>();

        Application.Run(); //required to make Screen.PrimaryScreen.WorkingArea return accurate values. This creates a message pump which allows Screen.DesktopChangedCount to listen to SystemEvents.UserPreferenceChanged events when the program is Single-Threaded Apartment.
    }

}