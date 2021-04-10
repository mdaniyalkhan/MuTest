using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MuTest.Core.Common;
using MuTest.Core.Common.Settings;
using MuTest.Core.Model;
using MuTest.Core.Mutants;
using MuTest.Core.Utility;
using MuTest.Cpp.CLI.Model;
using MuTest.Cpp.CLI.Mutants;
using MuTest.Cpp.CLI.Utility;

namespace MuTest.Cpp.CLI.Core
{
    public class CppMutantExecutor : ICppMutantExecutor
    {
        private static readonly object BuildAndExecuteTestLock = new object();
        private static readonly object AppendLock = new object();

        public double SurvivedThreshold { get; set; } = 1;

        public double KilledThreshold { get; set; } = 1;

        public bool CancelMutationOperation { get; private set; }

        public bool EnableDiagnostics { get; set; }

        private readonly CppClass _cpp;
        private readonly CppBuildContext _context;
        private readonly MuTestSettings _settings;

        private string _testDiagnostics;

        private string _buildDiagnostics;

        public event EventHandler<CppMutantEventArgs> MutantExecuted;

        public virtual void OnMutantExecuted(CppMutantEventArgs args)
        {
            MutantExecuted?.Invoke(this, args);
        }

        public CppMutantExecutor(CppClass cpp, CppBuildContext context, MuTestSettings settings)
        {
            _cpp = cpp ?? throw new ArgumentNullException(nameof(cpp));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string LastExecutionOutput { get; set; }

        public int NumberOfMutantsExecutingInParallel { get; set; }

        public async Task ExecuteMutants()
        {
            var mutationProcessLog = new StringBuilder();
            if (!_cpp.Mutants.Any())
            {
                PrintMutationReport(mutationProcessLog, _cpp.Mutants);
                return;
            }

            var mutants = _cpp.NotRunMutants;

            var testTasks = new List<Task>();
            int totalMutants = mutants.Count;
            for (var index = 0; index < mutants.Count; index += NumberOfMutantsExecutingInParallel)
            {
                if (CancelMutationOperation)
                {
                    break;
                }

                var directoryIndex = -1;
                var buildExecutor = new CppBuildExecutor(
                    _settings,
                    _context.TestSolution.FullName,
                    _cpp.Target)
                {
                    EnableLogging = false,
                    Configuration = _cpp.Configuration,
                    Platform = _cpp.Platform,
                    IntDir = _context.IntDir,
                    OutDir = _context.OutDir,
                    OutputPath = _context.OutputPath,
                    IntermediateOutputPath = _context.IntermediateOutputPath
                };

                if (!_context.UseMultipleSolutions)
                {
                    for (var mutationIndex = index;
                        mutationIndex < Math.Min(index + NumberOfMutantsExecutingInParallel, mutants.Count);
                        mutationIndex++)
                    {
                        directoryIndex++;
                        var mutant = mutants[mutationIndex];

                        var testContext = _context.TestContexts[directoryIndex];
                        var destinationFile = testContext.SourceClass.FullName;

                        _cpp.SourceClass.ReplaceLine(
                            mutant.Mutation.LineNumber,
                            mutant.Mutation.ReplacementNode,
                            destinationFile);

                        destinationFile.AddNameSpace(testContext.Index);
                    }

                    if (_context.EnableBuildOptimization)
                    {
                        if (!_cpp.IncludeBuildEvents)
                        {
                            _context.TestProject.FullName.RemoveBuildEvents();
                        }

                        _context.TestProject.FullName.OptimizeTestProject();
                    }

                    var buildLog = new StringBuilder();
                    void BuildOutputDataReceived(object sender, string args) => buildLog.Append(args.PrintWithPreTag());
                    buildExecutor.OutputDataReceived += BuildOutputDataReceived;

                    await buildExecutor.ExecuteBuild();

                    SetBuildLog(buildExecutor, buildLog.ToString());
                    buildExecutor.OutputDataReceived -= BuildOutputDataReceived;
                }

                directoryIndex = -1;
                for (var mutationIndex = index;
                    mutationIndex < Math.Min(index + NumberOfMutantsExecutingInParallel, mutants.Count);
                    mutationIndex++)
                {
                    if (CancelMutationOperation)
                    {
                        break;
                    }

                    if (decimal.Divide(mutants.Count(x => x.ResultStatus == MutantStatus.Survived), totalMutants) >
                        (decimal)SurvivedThreshold ||
                        decimal.Divide(mutants.Count(x => x.ResultStatus == MutantStatus.Killed), totalMutants) >
                        (decimal)KilledThreshold)
                    {
                        break;
                    }

                    var mutant = mutants[mutationIndex];
                    directoryIndex++;

                    try
                    {
                        if (_context.UseMultipleSolutions)
                        {
                            var testContext = _context.TestContexts[directoryIndex];
                            var destinationFile = testContext.SourceClass.FullName;

                            if (_context.TestContexts.Any(x => x.BackupSourceClass != null))
                            {
                                _context.TestContexts[0]
                                    .BackupSourceClass
                                    .FullName
                                    .ReplaceLine(
                                        mutant.Mutation.LineNumber,
                                        mutant.Mutation.ReplacementNode,
                                        destinationFile);
                            }
                            else
                            {
                                _cpp.SourceClass.ReplaceLine(
                                    mutant.Mutation.LineNumber,
                                    mutant.Mutation.ReplacementNode,
                                    destinationFile);
                            }
                        }
                        else if (buildExecutor.LastBuildStatus == Constants.BuildExecutionStatus.Failed)
                        {
                            mutant.ResultStatus = MutantStatus.BuildError;
                            OnMutantExecuted(new CppMutantEventArgs
                            {
                                Mutant = mutant,
                                TestLog = _testDiagnostics,
                                BuildLog = _buildDiagnostics
                            });
                            continue;
                        }

                        var current = directoryIndex;
                        testTasks.Add(Task.Run(() => BuildAndExecuteTests(mutant, current)));
                    }
                    catch (Exception e)
                    {
                        mutant.ResultStatus = MutantStatus.Skipped;
                        Trace.TraceError("Unable to Execute Mutant {0} Exception: {1}",
                            mutant.Mutation.OriginalNode.Encode(), e);
                    }
                }

                await Task.WhenAll(testTasks);
                _context.EnableBuildOptimization = false;
            }

            PrintMutationReport(mutationProcessLog, _cpp.Mutants);
        }

        public void Stop()
        {
            if (_cpp == null || _context == null)
            {
                return;
            }

            var projectName = Path.GetFileNameWithoutExtension(_cpp.TestProject);
            var projectNameFromTestContext = Path.GetFileNameWithoutExtension(_context.TestProject.Name)
                .Replace("_{0}", string.Empty);
            var processes = Process.GetProcesses().Where(x => x.ProcessName.StartsWith(projectName) ||
                                                              x.ProcessName.StartsWith(projectNameFromTestContext))
                .ToList();
            CancelMutationOperation = true;
            foreach (var process in processes)
            {
                try
                {
                    if (process != null && !process.HasExited)
                    {
                        process.Kill();
                        process.Dispose();
                    }
                }
                catch
                {
                    Trace.WriteLine("\nClosing Child Process...\n");
                }
            }
        }

        private void SetBuildLog(CppBuildExecutor buildExecutor, string log)
        {
            _buildDiagnostics = string.Empty;

            if (buildExecutor.LastBuildStatus == Constants.BuildExecutionStatus.Failed && EnableDiagnostics)
            {
                _buildDiagnostics = log;
            }
        }

        public void PrintMutationReport(StringBuilder mutationProcessLog, IList<CppMutant> mutants)
        {
            if (mutationProcessLog == null)
            {
                throw new ArgumentNullException(nameof(mutationProcessLog));
            }

            if (mutants == null)
            {
                throw new ArgumentNullException(nameof(mutants));
            }

            mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10\">");
            mutationProcessLog.AppendLine("Mutation Report".PrintImportantWithLegend());
            mutationProcessLog.Append("  ".PrintWithPreTag());
            mutationProcessLog.Append($"{"Source Path   :".PrintImportant()} {_cpp.SourceClass}".PrintWithPreTag());
            mutationProcessLog.Append($"{"Test Path     :".PrintImportant()} {_cpp.TestClass}".PrintWithPreTag());
            mutationProcessLog.Append($"{"Test Project  :".PrintImportant()} {_cpp.TestProject}".PrintWithPreTag());
            mutationProcessLog.Append("  ".PrintWithPreTag());

            if (mutants.Any())
            {
                mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10\">");
                mutationProcessLog.Append("Mutants".PrintImportantWithLegend(color: Constants.Colors.BlueViolet));
            }

            foreach (var mutant in mutants)
            {
                var lineNumber = mutant.Mutation.LineNumber;
                mutationProcessLog
                    .Append(
                        $"Line: {lineNumber.ToString().PrintImportant(color: Constants.Colors.Blue)} - {mutant.ResultStatus.ToString().PrintImportant()} - {mutant.Mutation.DisplayName.Encode()}"
                            .PrintWithDateTime()
                            .PrintWithPreTagWithMargin());
            }

            if (mutants.Any())
            {
                mutationProcessLog.AppendLine("</fieldset>");
            }

            mutationProcessLog.AppendLine("</fieldset>");

            LastExecutionOutput = mutationProcessLog.ToString();
        }

        private async Task BuildAndExecuteTests(CppMutant mutant, int index)
        {
            try
            {
                if (_context.UseMultipleSolutions)
                {
                    var buildExecutor = new CppBuildExecutor(
                        _settings,
                        string.Format(_context.TestSolution.FullName, index),
                        _cpp.Target)
                    {
                        EnableLogging = false,
                        Configuration = _cpp.Configuration,
                        Platform = _cpp.Platform,
                        IntDir = string.Format(_context.IntDir, index),
                        OutDir = string.Format(_context.OutDir, index),
                        OutputPath = string.Format(_context.OutputPath, index),
                        IntermediateOutputPath = string.Format(_context.IntermediateOutputPath, index)
                    };

                    if (_context.EnableBuildOptimization)
                    {
                        if (!_cpp.IncludeBuildEvents)
                        {
                            string.Format(_context.TestProject.FullName, index).RemoveBuildEvents();
                        }

                        string.Format(_context.TestProject.FullName, index).OptimizeTestProject();
                    }

                    var buildLog = new StringBuilder();

                    void BuildOutputDataReceived(object sender, string args)
                    {
                        lock (AppendLock)
                        {
                            buildLog.Append(args.PrintWithPreTag());
                        }
                    }

                    buildExecutor.OutputDataReceived += BuildOutputDataReceived;
                    await buildExecutor.ExecuteBuild();

                    if (buildExecutor.LastBuildStatus == Constants.BuildExecutionStatus.Failed)
                    {
                        buildLog.Clear();
                        buildExecutor.Rebuild = true;

                        await buildExecutor.ExecuteBuild();
                        buildExecutor.Rebuild = false;
                    }

                    lock (BuildAndExecuteTestLock)
                    {
                        SetBuildLog(buildExecutor, buildLog.ToString());
                        buildExecutor.OutputDataReceived -= BuildOutputDataReceived;
                    }

                    if (buildExecutor.LastBuildStatus == Constants.BuildExecutionStatus.Failed)
                    {
                        mutant.ResultStatus = MutantStatus.BuildError;
                        OnMutantExecuted(new CppMutantEventArgs
                        {
                            Mutant = mutant,
                            TestLog = _testDiagnostics,
                            BuildLog = _buildDiagnostics
                        });

                        return;
                    }
                }

                var testExecutor = new GoogleTestExecutor
                {
                    KillProcessOnTestFail = true,
                    EnableTestTimeout = true,
                    TestTimeout = _settings.TestTimeout
                };

                var log = new StringBuilder();
                void OutputDataReceived(object sender, string args) => log.Append(args.PrintWithPreTag());
                if (EnableDiagnostics)
                {
                    log.AppendLine("<fieldset style=\"margin-bottom:10\">");
                    var lineNumber = mutant.Mutation.LineNumber;
                    log.Append(
                        $"Line: {lineNumber.ToString().PrintImportant(color: Constants.Colors.Blue)} - {mutant.Mutation.DisplayName.Encode()}"
                            .PrintWithDateTime()
                            .PrintWithPreTag());
                    testExecutor.OutputDataReceived += OutputDataReceived;
                }

                var projectDirectory = Path.GetDirectoryName(_cpp.TestProject);
                var projectName = Path.GetFileNameWithoutExtension(_cpp.TestProject);

                var projectNameFromTestContext =
                    string.Format(Path.GetFileNameWithoutExtension(_context.TestProject.Name), index);
                var app = $"{projectDirectory}/{string.Format(_context.OutDir, index)}{projectName}.exe";

                if (!File.Exists(app))
                {
                    app = $"{projectDirectory}/{string.Format(_context.OutDir, index)}{projectNameFromTestContext}.exe";
                }

                await testExecutor.ExecuteTests(
                    app,
                    _cpp.UseClassFilter
                        ? $"{Path.GetFileNameWithoutExtension(_context.TestContexts[index].TestClass.Name)}*"
                        : string.Empty);

                testExecutor.OutputDataReceived -= OutputDataReceived;
                if (EnableDiagnostics && testExecutor.LastTestExecutionStatus == Constants.TestExecutionStatus.Timeout)
                {
                    log.AppendLine("</fieldset>");
                    _testDiagnostics = log.ToString();
                }

                mutant.ResultStatus = testExecutor.LastTestExecutionStatus == Constants.TestExecutionStatus.Success
                    ? MutantStatus.Survived
                    : testExecutor.LastTestExecutionStatus == Constants.TestExecutionStatus.Timeout
                        ? MutantStatus.Timeout
                        : MutantStatus.Killed;
                OnMutantExecuted(new CppMutantEventArgs
                {
                    Mutant = mutant,
                    BuildLog = _buildDiagnostics,
                    TestLog = _testDiagnostics
                });
            }
            catch (Exception e)
            {
                mutant.ResultStatus = MutantStatus.BuildError;
                OnMutantExecuted(new CppMutantEventArgs
                {
                    Mutant = mutant,
                    BuildLog = e.ToString(),
                });
            }
        }

        public void PrintMutatorSummary(StringBuilder mutationProcessLog, IList<CppMutant> mutants)
        {
            _cpp.MutatorWiseMutationScores.Clear();
            var mutators = mutants
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
                var covered = mutator.Mutants.Count(x => x.ResultStatus != MutantStatus.NotCovered) - timeout -
                              buildErrors - skipped;
                var coverage = decimal.Divide(killed, covered == 0
                    ? 1
                    : covered);
                var mutation = covered == 0
                    ? "N/A"
                    : $"{killed}/{covered}[{coverage:P}]";
                mutationProcessLog.AppendLine("<fieldset>");
                mutationProcessLog.AppendLine($"{mutator.Mutator}".PrintImportantWithLegend());
                mutationProcessLog.Append(
                    $"Coverage: Mutation({mutation}) [Survived({survived}) Killed({killed}) Not Covered({uncovered}) Timeout({timeout}) Build Errors({buildErrors}) Skipped({skipped})]"
                        .PrintWithPreTagWithMarginImportant(color: Constants.Colors.Blue));
                mutationProcessLog.AppendLine("</fieldset>");

                _cpp.MutatorWiseMutationScores.Add(new MutatorMutationScore
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

        public void PrintClassSummary(CppClass cppClass, StringBuilder mutationProcessLog)
        {
            cppClass.CalculateMutationScore();
            mutationProcessLog.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10\">");
            mutationProcessLog.AppendLine("ClassWise Summary".PrintImportantWithLegend());
            mutationProcessLog.Append(cppClass.MutationScore.ToString()
                .PrintWithPreTagWithMarginImportant(color: Constants.Colors.BlueViolet));
            mutationProcessLog.Append(
                $"Coverage: Mutation({cppClass.MutationScore.Mutation}) Line({cppClass.LinesCovered}{cppClass.LineCoverage})"
                    .PrintWithPreTagWithMarginImportant(color: Constants.Colors.Blue));
            mutationProcessLog.AppendLine("</fieldset>");
        }
    }
}
