#nullable enable

using System;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Validators;
using Perfolizer.Horology;

namespace Tests {

    public static class SummaryExtensions {

        /// <summary>
        /// Return the Detailed Results (stats and histogram), Summary (CPU, OS, runtime, table of methods), and Legend
        /// from the benchmark output that normally just goes to the console or other loggers you configure.
        /// Does not contain the validation, fine-grained overhead and workload timings, or report filenames.
        /// </summary>
        /// <param name="summary">The result from <c>BenchmarkRunner.Run()</c>.</param>
        /// <returns>The summary string snippet that would have normally gone to the console logger.</returns>
        public static string getDetailedResultsAndSummary(this Summary summary) {
            var logger = new StringLogger();

            if (summary.HasCriticalValidationErrors) {
                foreach (ValidationError validationError in summary.ValidationErrors) {
                    logger.WriteLine(validationError.Message);
                }
            } else {
                // extracted from https://github.com/dotnet/BenchmarkDotNet/blob/ceef311bfc73a08a3b07f177f6b9f9782191b794/src/BenchmarkDotNet/Running/BenchmarkRunnerClean.cs PrintSummary()

                var cultureInfo = summary.GetCultureInfo();
                // logger.WriteLineHeader("// * Detailed results *");
                // BenchmarkReportExporter.Default.ExportToLog(summary, logger);
                logger.WriteLineHeader("// * Summary *");
                MarkdownExporter.Console.ExportToLog(summary, logger);
                ImmutableConfig config = DefaultConfig.Instance.CreateImmutableConfig();
                ConclusionHelper.Print(logger, config.GetCompositeAnalyser().Analyse(summary).Distinct().ToList());
                /*var columnWithLegends = summary.Table.Columns.Select(c => c.OriginalColumn).Where(c => !string.IsNullOrEmpty(c.Legend)).ToList();
                var effectiveTimeUnit = summary.Table.EffectiveSummaryStyle.TimeUnit;
                if (columnWithLegends.Any() || effectiveTimeUnit != null) {
                    logger.WriteLine();
                    logger.WriteLineHeader("// * Legends *");
                    int maxNameWidth = 0;
                    if (columnWithLegends.Any())
                        maxNameWidth = Math.Max(maxNameWidth, columnWithLegends.Select(c => c.ColumnName.Length).Max());
                    if (effectiveTimeUnit != null)
                        maxNameWidth = Math.Max(maxNameWidth, effectiveTimeUnit.Name.ToString(cultureInfo).Length + 2);

                    foreach (var column in columnWithLegends)
                        logger.WriteLineHint($"  {column.ColumnName.PadRight(maxNameWidth, ' ')} : {column.Legend}");

                    if (effectiveTimeUnit != null)
                        logger.WriteLineHint($"  {("1 " + effectiveTimeUnit.Name).PadRight(maxNameWidth, ' ')} :" +
                                             $" 1 {effectiveTimeUnit.Description} ({TimeUnit.Convert(1, effectiveTimeUnit, TimeUnit.Second).ToString("0.#########", summary.GetCultureInfo())} sec)");
                }*/

                if (config.GetDiagnosers().Any()) {
                    logger.WriteLine();
                    config.GetCompositeDiagnoser().DisplayResults(logger);
                }
            }

            return logger.toString();
        }

        private class StringLogger: ILogger {

            public string Id { get; } = nameof(StreamLogger);
            public int Priority { get; } = 0;

            private readonly StringBuilder stringBuilder = new StringBuilder();

            public void Write(LogKind logKind, string text) {
                stringBuilder.Append(text);
            }

            public void WriteLine() {
                stringBuilder.AppendLine();
            }

            public void WriteLine(LogKind logKind, string text) {
                stringBuilder.AppendLine(text);
            }

            public void Flush() { }

            public string toString() {
                return stringBuilder.ToString();
            }

        }

    }

}