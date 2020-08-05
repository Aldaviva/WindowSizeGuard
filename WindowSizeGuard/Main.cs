#nullable enable

using System;
using System.Windows.Forms;
using Autofac;
using NLog;

namespace WindowSizeGuard {

    public static class MainClass {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        [STAThread] //important, otherwise hotkeys don't trigger and Windows times out after ~2 seconds, then handles the hotkey normally
        public static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += (sender, args) => LOGGER.Error(args.ExceptionObject);

            IContainer autofacContainer = AutofacHelpers.createContainer();
            using var scope = autofacContainer.BeginLifetimeScope();
            scope.Resolve<ToolbarAwareSizeGuard>();
            scope.Resolve<HotkeyHandler>();

            Application.Run(new ApplicationContext()); //required to make Screen.PrimaryScreen.WorkingArea return accurate values. I suspect this allows Screen.DesktopChangedCount to listen to SystemEvents.UserPreferenceChanged events.
        }

    }

}