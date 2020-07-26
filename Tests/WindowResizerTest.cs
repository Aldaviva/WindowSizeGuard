#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using BenchmarkDotNet.Attributes;
using ManagedWinapi.Windows;
using WindowSizeGuard;
using Xunit;
using Xunit.Abstractions;
using WindowSizeGuard;

namespace Tests {

    public class WindowResizerTest: BenchmarkTest {

        private readonly ITestOutputHelper? testOutputHelper;

        private static readonly AndCondition RESIZABLE_WINDOWS_CONDITION = new AndCondition(new Condition[] {
            new PropertyCondition(AutomationElement.IsWindowPatternAvailableProperty, true),
            new PropertyCondition(WindowPattern.WindowVisualStateProperty, WindowVisualState.Normal),
            new PropertyCondition(TransformPattern.CanResizeProperty, true)
        });

        private readonly SystemWindow window;
        private readonly WindowResizerImpl windowResizer;

        [Params(1, 2)]
        public int depth { get; set; }

        public WindowResizerTest(ITestOutputHelper? testOutputHelper = null): base(testOutputHelper) {
            this.testOutputHelper = testOutputHelper;
            window = SystemWindow.DesktopWindow;
            windowResizer = new WindowResizerImpl();
        }

        // [Benchmark]
        // public void isWindowWithNoPadding() {
        //     WindowResizerImpl.isWindowWithNoPadding(window);
        // }

        // [Benchmark]
        // public void getTopMostWindowsUsingUiAutomation() {
        //     // AutomationElementCollection automationElementCollection = AutomationElement.RootElement.FindAll(TreeScope.Children, RESIZABLE_WINDOWS_CONDITION);
        //     IList<AutomationElement> findResizableWindows = windowResizer.findResizableWindows(parent: (AutomationElement?) null, depth: depth).ToList();
        // }

        [Benchmark]
        public void getTopMostWindowsUsingWinApi() {
            IList<SystemWindow> systemWindows = windowResizer.findResizableWindows(parent: (SystemWindow?) null, depth: depth).ToList();
        }

        [Fact]
        public void negativeTitleMatching() {
            const string INPUT_TO_MATCH = "Microsoft Store";
            const string INPUT_TO_NOT_MATCH = "Notes - OneNote for Windows 10";

            Regex pattern = new Regex(@"^.*(?<! - OneNote for Windows 10)$");

            Assert.Matches(pattern, INPUT_TO_MATCH);
            Assert.DoesNotMatch(pattern, INPUT_TO_NOT_MATCH);
        }

        [Fact]
        public void getWindowPadding() {
            Process p = Process.Start("notepad");
            p.WaitForInputIdle();
            var systemWindow = new SystemWindow(p.MainWindowHandle);
            RECT windowPadding = windowResizer.getWindowPadding(systemWindow);
            testOutputHelper!.WriteLine(windowPadding.toString());
            p.Kill();
        }

    }

}