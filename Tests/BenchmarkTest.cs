#nullable enable

using System;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Xunit;
using Xunit.Abstractions;

namespace Tests {

    public abstract class BenchmarkTest {

        private readonly ITestOutputHelper? testOutputHelper;

        protected BenchmarkTest(ITestOutputHelper? testOutputHelper = null) {
            this.testOutputHelper = testOutputHelper;
        }

        public static Summary runBenchmarksForClass(Type benchmarkClass, ITestOutputHelper testOutputHelper) {
            testOutputHelper.WriteLine("Running benchmarks...");
            Summary summary = BenchmarkRunner.Run(benchmarkClass);
            testOutputHelper.WriteLine("\nFull benchmark output saved to {0}\n", summary.LogFilePath);
            testOutputHelper.WriteLine(summary.getDetailedResultsAndSummary());
            Assert.False(summary.HasCriticalValidationErrors, "Benchmarks encountered critical validation errors.");
            Assert.True(summary.GetNumberOfExecutedBenchmarks() > 0, $"No benchmarks were found, make sure {benchmarkClass.Name} has at least one public method with a [Benchmark] attribute.");
            return summary;
        }

        public static Summary runBenchmarksForClass<T>(ITestOutputHelper testOutputHelper) {
            return runBenchmarksForClass(typeof(T), testOutputHelper);
        }

        [Fact]
        public void benchmark() {
            runBenchmarksForClass(GetType(), testOutputHelper!);
        }

    }

}