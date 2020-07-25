#nullable enable

using System;
using System.Threading;
using System.Windows.Forms;
using Autofac;

namespace WindowSizeGuard {

    public static class MainClass {

        // private static readonly ManualResetEvent KEEP_RUNNING = new ManualResetEvent(false);

        [STAThread] //important, otherwise hotkeys don't trigger and Windows times out after ~2 seconds, then handles the hotkey normally
        public static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Console.CancelKeyPress += (sender, args) => {
            //     args.Cancel = true;
            //     KEEP_RUNNING.Set();
            // };

            IContainer autofacContainer = AutofacHelpers.createContainer();
            using var scope = autofacContainer.BeginLifetimeScope();
            scope.Resolve<ToolbarAwareSizeGuard>();
            scope.Resolve<HotkeyHandler>();

            Application.Run(new ApplicationContext()); //required to make Screen.PrimaryScreen.WorkingArea return accurate values. I suspect this allows Screen.DesktopChangedCount to listen to SystemEvents.UserPreferenceChanged events.
            // KEEP_RUNNING.WaitOne();
        }

    }

}