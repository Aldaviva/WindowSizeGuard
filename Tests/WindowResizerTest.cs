#nullable enable

using BenchmarkDotNet.Attributes;
using ManagedWinapi.Windows;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using WindowSizeGuard;
using WindowSizeGuard.ProgramHandlers;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable xUnit1013 // it's a benchmark that must be public, stfu

namespace Tests {

    public class WindowResizerTest: BenchmarkTest {

        private readonly ITestOutputHelper? testOutputHelper;

        private static readonly AndCondition RESIZABLE_WINDOWS_CONDITION = new(new PropertyCondition(AutomationElement.IsWindowPatternAvailableProperty, true),
            new PropertyCondition(WindowPattern.WindowVisualStateProperty, WindowVisualState.Normal), new PropertyCondition(TransformPattern.CanResizeProperty, true));

        private readonly SystemWindow      window;
        private readonly WindowResizerImpl windowResizer;

        [Params(1, 2)]
        public int depth { get; set; }

        public WindowResizerTest(ITestOutputHelper? testOutputHelper = null): base(testOutputHelper) {
            this.testOutputHelper = testOutputHelper;
            window                = SystemWindow.DesktopWindow;
            windowResizer         = new WindowResizerImpl(new VivaldiHandlerImpl());
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
            IList<SystemWindow> systemWindows = windowResizer.findResizableWindows(parent: null, depth: depth).ToList();
        }

        [Fact]
        public void negativeTitleMatching() {
            const string INPUT_TO_MATCH     = "Microsoft Store";
            const string INPUT_TO_NOT_MATCH = "Notes - OneNote for Windows 10";

            Regex pattern = new(@"^.*(?<! - OneNote for Windows 10)$");

            Assert.Matches(pattern, INPUT_TO_MATCH);
            Assert.DoesNotMatch(pattern, INPUT_TO_NOT_MATCH);
        }

        [Fact]
        public void getWindowPadding() {
            Process p = Process.Start("notepad");
            p.WaitForInputIdle();
            SystemWindow systemWindow  = new(p.MainWindowHandle);
            RECT         windowPadding = windowResizer.getWindowPadding(systemWindow);
            testOutputHelper!.WriteLine(windowPadding.toString());
            p.Kill();
        }

    }

}