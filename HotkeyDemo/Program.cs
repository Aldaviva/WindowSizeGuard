using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;

namespace HotkeyDemo {

    static class Program {

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ApplicationContext applicationContext = new MyApplicationContext();
            Application.Run(applicationContext);
        }

    }

    internal class MyApplicationContext: ApplicationContext {

        public MyApplicationContext() {
            void DoSomething() {
                MessageBox.Show("You pressed UNDO");
            }

            Hook.GlobalEvents().OnCombination(new Dictionary<Combination, Action> {
                { Combination.FromString("Control+Z"), DoSomething },
                { Combination.FromString("Shift+Alt+Enter"), () => { Console.WriteLine("You Pressed FULL SCREEN"); } }
            });
        }

    }

}