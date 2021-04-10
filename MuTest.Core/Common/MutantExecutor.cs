using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using MuTest.Core.Common.Settings;
using MuTest.Core.Model;
using MuTest.Core.Mutants;
using MuTest.Core.Utility;
using static MuTest.Core.Common.Constants;

namespace MuTest.Core.Common
{
    public class MutantExecutor : IMutantExecutor
    {
        public double SurvivedThreshold { get; set; } = 1;

        public double KilledThreshold { get; set; } = 1;

        public bool CancelMutationOperation { get; set; }

        public bool UseClassFilter { get; set; }

        public bool EnableDiagnostics { get; set; }

        public string BaseAddress { get; set; }

        private readonly SourceClassDetail _source;
        private readonly ITestDirectoryFactory _directoryFactory;
        private readonly MuTestSettings _settings;

        private string _testDiagnostics;

        private string _buildDiagnostics;

        public event EventHandler<MutantEventArgs> MutantExecuted;

        public virtual void OnMutantExecuted(MutantEventArgs args)
        {
            MutantExecuted?.Invoke(this, args);
        }

        public MutantExecutor(SourceClassDetail source, MuTestSettings settings)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _directoryFactory = new TestDirectoryFactory(_source);
            _settings = settings;
        }

        public string LastExecutionOutput { get; set; }

        public int NumberOfMutantsExecutingInParallel { get; set; }

        public async Task ExecuteMutants()
        {
            var mutationProcessLog = new StringBuilder();
            var methodDetails = _source.MethodDetails.Where(x => x.TestMethods.Any()).ToList();
            if (!methodDetails.Any())
            {
                PrintMutationReport(mutationProcessLog, methodDetails);
                return;
            }

            var mutants = methodDetails.SelectMany(x => x.Mutants).Where(mutant => mutant.ResultStatus != MutantStatus.Killed &&
                                                                                   mutant.ResultStatus != MutantStatus.NotCovered &&
                                                                                   mutant.ResultStatus != MutantStatus.Skipped).ToList();
            var testTasks = new List<Task>();
            int totalMutants = mutants.Count;
            for (var index = 0; index < mutants.Count; index += NumberOfMutantsExecutingInParallel)
            {
                if (CancelMutationOperation)
                {
                    break;
                }

                var directoryIndex = -1;
                for (var mutationIndex = index; mutationIndex < Math.Min(index + NumberOfMutantsExecutingInParallel, mutants.Count); mutationIndex++)
                {
                    if (CancelMutationOperation)
                    {
                        break;
                    }

                    if (decimal.Divide(mutants.Count(x => x.ResultStatus == MutantStatus.Survived), totalMutants) > (decimal)SurvivedThreshold ||
                        decimal.Divide(mutants.Count(x => x.ResultStatus == MutantStatus.Killed), totalMutants) > (decimal)KilledThreshold)
                    {
                        break;
                    }

                    var mutant = mutants[mutationIndex];

                    try
                    {
                        var updatedSourceCode = _source.Claz
                            .Syntax
                            .Root()
                            .ReplaceNode(mutant.Mutation.OriginalNode, mutant.Mutation.ReplacementNode);

                        directoryIndex++;
                        await UpdateSourceCode(updatedSourceCode.ToFullString(), directoryIndex);

                        var current = directoryIndex;
                        testTasks.Add(Task.Run(() => BuildAndSetBinaries(current, mutant)));
                    }
                    catch (Exception e)
                    {
                        mutant.ResultStatus = MutantStatus.Skipped;
                        Trace.TraceError("Unable to Execute Mutant {0} Exception: {1}", mutant.Mutation.OriginalNode.ToString().Encode(), e);
                    }
                }

                await Task.WhenAll(testTasks);
            }

            PrintMutationReport(mutationProcessLog, methodDetails);
        }

        public void PrintMutationReport(StringBuilder mutationProcessLog, IList<MethodDetail> methodDetails)
        {
            if (mutationProcessLog == null)
            {
                throw new ArgumentNullException(nameof(mutationProcessLog));
            }

            mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10\">");
            mutationProcessLog.AppendLine("Mutation Report".PrintImportantWithLegend());
            mutationProcessLog.Append("  ".PrintWithPreTag());
            mutationProcessLog.Append($"{"Class Name    :".PrintImportant()} {_source.Claz.Syntax.FullName()}".PrintWithPreTag());
            mutationProcessLog.Append($"{"Source Path   :".PrintImportant()} {_source.FilePath}".PrintWithPreTag());
            mutationProcessLog.Append($"{"Source Project:".PrintImportant()} {_source.ClassProject}".PrintWithPreTag());
            mutationProcessLog.Append($"{"Test Path     :".PrintImportant()} {_source.TestClaz.FilePath}".PrintWithPreTag());
            mutationProcessLog.Append($"{"Test Project  :".PrintImportant()} {_source.TestClaz.ClassProject}".PrintWithPreTag());
            mutationProcessLog.Append("  ".PrintWithPreTag());
            foreach (var method in methodDetails)
            {
                if (method.Mutants.Any())
                {
                    mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10\">");
                    mutationProcessLog.Append($"Method: {method.MethodName}".PrintImportantWithLegend(color: Colors.Orange));
                }

                foreach (var mutant in method.Mutants)
                {
                    var lineNumber = mutant.Mutation.Location;
                    mutationProcessLog
                        .Append(
                            $"Line: {lineNumber.ToString().PrintImportant(color: Colors.Blue)} - {mutant.ResultStatus.ToString().PrintImportant()} - {mutant.Mutation.DisplayName.Encode()}"
                                .PrintWithDateTime()
                                .PrintWithPreTagWithMargin());
                }

                PrintMethodSummary(method, mutationProcessLog);

                if (method.Mutants.Any())
                {
                    mutationProcessLog.AppendLine("</fieldset>");
                }
            }

            mutationProcessLog.AppendLine("</fieldset>");

            LastExecutionOutput = mutationProcessLog.ToString();
        }

        private async Task BuildAndSetBinaries(int directoryIndex, Mutant mutant)
        {
            try
            {
                var projectFile = _directoryFactory.GetProjectFile(directoryIndex);
                var buildExecutor = new BuildExecutor(_settings, projectFile.FullName)
                {
                    EnableLogging = false,
                    BaseAddress = BaseAddress,
                    OutputPath = $"{Path.GetDirectoryName(_source.ClassLibrary)}{directoryIndex}",
                    IntermediateOutputPath = $@"{Path.GetDirectoryName(_source.ClassLibrary)}{directoryIndex}\obj\"
                };

                await BuildProject(buildExecutor);

                var log = new StringBuilder();
                void OutputDataReceived(object sender, string args) => log.Append(args.PrintWithPreTag());

                if (buildExecutor.LastBuildStatus == BuildExecutionStatus.Failed)
                {
                    if (EnableDiagnostics)
                    {
                        log.AppendLine("<fieldset style=\"margin-bottom:10\">");
                        var lineNumber = mutant.Mutation.OriginalNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        log.Append(
                            $"Method: {mutant.Method.MethodName} Line: {lineNumber.ToString().PrintImportant(color: Colors.Blue)} - {mutant.Mutation.DisplayName.Encode()}"
                                .PrintWithDateTime()
                                .PrintWithPreTag());
                        buildExecutor.OutputDataReceived += OutputDataReceived;
                    }

                    await BuildProject(buildExecutor);
                    buildExecutor.OutputDataReceived -= OutputDataReceived;
                }

                if (buildExecutor.LastBuildStatus == BuildExecutionStatus.Failed)
                {
                    if (EnableDiagnostics)
                    {
                        log.AppendLine("</fieldset>");
                        _buildDiagnostics = log.ToString();
                    }
                }

                if (buildExecutor.LastBuildStatus == BuildExecutionStatus.Success)
                {
                    UpdateSourceLibrary(directoryIndex);
                    var classLibrary = Path.Combine(
                        $"{Path.GetDirectoryName(_source.TestClaz.ClassLibrary)}_test_{directoryIndex}",
                        Path.GetFileName(_source.TestClaz.ClassLibrary) ?? throw new InvalidOperationException("Could not find class library path"));
                    await ExecuteTests(mutant, classLibrary);
                }
                else
                {
                    mutant.ResultStatus = MutantStatus.BuildError;
                    OnMutantExecuted(new MutantEventArgs
                    {
                        Mutant = mutant,
                        TestLog = _testDiagnostics,
                        BuildLog = _buildDiagnostics
                    });
                }
            }
            catch (Exception e)
            {
                mutant.ResultStatus = MutantStatus.BuildError;
                Trace.TraceError("Unable to Execute Mutant {0} Exception: {1}", mutant.Mutation.OriginalNode.ToString().Encode(), e);
            }
        }

        private async Task UpdateSourceCode(string updatedSourceCode, int fileIndex)
        {
            while (true)
            {
                try
                {
                    var codeFile = _directoryFactory.GetSourceCodeFile(fileIndex);
                    using (var outputFile = new StreamWriter(codeFile.FullName))
                    {
                        await outputFile.WriteAsync(updatedSourceCode);
                    }

                    break;
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Exception suppressed: {0}", ex);
                    Debug.WriteLine("File is inaccessible....Try again");
                }
            }
        }

        private async Task ExecuteTests(Mutant mutant, string testLibrary)
        {
            var testExecutor = new TestExecutor(_settings, testLibrary)
            {
                EnableLogging = false,
                EnableCustomOptions = false,
                KillProcessOnTestFail = true,
                EnableParallelTestExecution = true,
                X64TargetPlatform = _source.X64TargetPlatform,
                BaseAddress = BaseAddress,
                EnableTimeout = _settings.EnableTestTimeout,
                FullyQualifiedName = UseClassFilter
                    ? _source.TestClaz.Claz.Syntax.FullName()
                    : string.Empty
            };

            var log = new StringBuilder();
            void OutputDataReceived(object sender, string args) => log.Append(args.PrintWithPreTag());
            if (EnableDiagnostics)
            {
                log.AppendLine("<fieldset style=\"margin-bottom:10\">");
                var lineNumber = mutant.Mutation.OriginalNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                log.Append(
                    $"Method: {mutant.Method.MethodName} Line: {lineNumber.ToString().PrintImportant(color: Colors.Blue)} - {mutant.Mutation.DisplayName.Encode()}"
                        .PrintWithDateTime()
                        .PrintWithPreTag());
                testExecutor.OutputDataReceived += OutputDataReceived;
            }

            await testExecutor.ExecuteTests(mutant.Method.TestMethods);
            testExecutor.OutputDataReceived -= OutputDataReceived;
            if (EnableDiagnostics && testExecutor.LastTestExecutionStatus == TestExecutionStatus.Timeout)
            {
                log.AppendLine("</fieldset>");
                _testDiagnostics = log.ToString();
            }

            mutant.ResultStatus = testExecutor.LastTestExecutionStatus == TestExecutionStatus.Success
                ? MutantStatus.Survived
                : testExecutor.LastTestExecutionStatus == TestExecutionStatus.Timeout
                    ? MutantStatus.Timeout
                    : MutantStatus.Killed;
            OnMutantExecuted(new MutantEventArgs
            {
                Mutant = mutant,
                BuildLog = _buildDiagnostics,
                TestLog = _testDiagnostics
            });
        }

        private void UpdateSourceLibrary(int directoryIndex)
        {
            var testBinariesPath = $"{Path.GetDirectoryName(_source.TestClaz.ClassLibrary)}_test_{directoryIndex}";
            var sourceDllPath = Path.GetFileName(_source.ClassLibrary);
            var sourceDllInTestProject = Path.Combine(testBinariesPath, sourceDllPath ?? throw new InvalidOperationException(SourceDllFileNameNotValid));

            if (File.Exists(sourceDllInTestProject))
            {
                File.Delete(sourceDllInTestProject);
            }

            var sourceDirectory = $"{Path.GetDirectoryName(_source.ClassLibrary)}{directoryIndex}";
            File.Copy(Path.Combine(sourceDirectory, sourceDllPath), sourceDllInTestProject);
        }

        private async Task BuildProject(IBuildExecutor buildExecutor)
        {
            if (!_source.BuildInReleaseMode)
            {
                await buildExecutor.ExecuteBuildInDebugModeWithoutDependencies();
            }
            else
            {
                await buildExecutor.ExecuteBuildInReleaseModeWithoutDependencies();
            }
        }

        private static void PrintMethodSummary(MethodDetail method, StringBuilder mutationProcessLog)
        {
            if (method.Mutants.Any())
            {
                method.CalculateMutationScore();

                mutationProcessLog.Append(method.MutationScore.ToString().PrintWithPreTagWithMarginImportant(color: Colors.Blue));
                mutationProcessLog.Append(
                    $"Method Coverage: Mutation({method.MutationScore.Mutation}) Line({method.LinesCovered}{method.LineCoverage}) Branch({method.BlocksCovered}{method.BranchCoverage})"
                        .PrintWithPreTagWithMarginImportant(color: Colors.Blue));
            }
        }

        public void PrintMutatorSummary(StringBuilder mutationProcessLog)
        {
            _source.MutatorWiseMutationScores.Clear();
            var mutators = _source.MethodDetails
                .SelectMany(x => x.Mutants)
                .GroupBy(grp => grp.Mutation.Type)
                .Select(x => new
                {
                    Mutator = x.Key,
                    Mutants = x.ToList()
                });
            mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10\">");
            mutationProcessLog.AppendLine("MutatorWise Summary".PrintImportantWithLegend());
            foreach (var mutator in mutators)
            {
                var survived = mutator.Mutants.Count(x => x.ResultStatus == MutantStatus.Survived);
                var killed = mutator.Mutants.Count(x => x.ResultStatus == MutantStatus.Killed);
                var uncovered = mutator.Mutants.Count(x => x.ResultStatus == MutantStatus.NotCovered);
                var timeout = mutator.Mutants.Count(x => x.ResultStatus == MutantStatus.Timeout);
                var buildErrors = mutator.Mutants.Count(x => x.ResultStatus == MutantStatus.BuildError);
                var skipped = mutator.Mutants.Count(x => x.ResultStatus == MutantStatus.Skipped);
                var covered = mutator.Mutants.Count(x => x.ResultStatus != MutantStatus.NotCovered) - timeout - buildErrors - skipped;
                var coverage = decimal.Divide(killed, covered == 0
                    ? 1
                    : covered);
                var mutation = covered == 0
                    ? "N/A"
                    : $"{killed}/{covered}[{coverage:P}]";
                mutationProcessLog.AppendLine("<fieldset>");
                mutationProcessLog.AppendLine($"{mutator.Mutator}".PrintImportantWithLegend());
                mutationProcessLog.Append($"Coverage: Mutation({mutation}) [Survived({survived}) Killed({killed}) Not Covered({uncovered}) Timeout({timeout}) Build Errors({buildErrors}) Skipped({skipped})]"
                    .PrintWithPreTagWithMarginImportant(color: Colors.Blue));
                mutationProcessLog.AppendLine("</fieldset>");

                _source.MutatorWiseMutationScores.Add(new MutatorMutationScore
                {
                    Mutator = mutator.Mutator.ToString(),
                    MutationScore = new MutationScore
                    {
                        BuildErrors = buildErrors,
                        Coverage = coverage,
                        Covered = covered,
                        Killed = killed,
                        Skipped = skipped,
                        Survived = survived,
                        Timeout = timeout,
                        Uncovered = uncovered
                    }
                });
            }

            mutationProcessLog.AppendLine("</fieldset>");
        }

        public void PrintClassSummary(StringBuilder mutationProcessLog)
        {
            _source.CalculateMutationScore();
            mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10\">");
            mutationProcessLog.AppendLine("ClassWise Summary".PrintImportantWithLegend());
            mutationProcessLog.Append(_source.MutationScore.ToString().PrintWithPreTagWithMarginImportant(color: Colors.Blue));
            mutationProcessLog.Append(
                $"Coverage: Mutation({_source.MutationScore.Mutation}) Line({_source.LinesCovered}{_source.LineCoverage}) Branch({_source.BlocksCovered}{_source.BranchCoverage})"
                    .PrintWithPreTagWithMarginImportant(color: Colors.Blue));
            mutationProcessLog.AppendLine("</fieldset>");

            PrintExternalCoverage(mutationProcessLog);
        }

        private void PrintExternalCoverage(StringBuilder mutationProcessLog)
        {
            if (_source.ExternalCoveredClasses.Any())
            {
                mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10\">");
                mutationProcessLog.AppendLine($"External Coverage [{_source.ExternalCoverage:P}]".PrintImportantWithLegend(color: Colors.Red));

                foreach (var clz in _source.ExternalCoveredClasses)
                {
                    mutationProcessLog.Append(clz.ToString().PrintWithPreTagWithMarginImportant(color: Colors.Blue));
                }

                mutationProcessLog.AppendLine("</fieldset>");
            }
        }
    }
}
