using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MuTest.Api.Clients.ServiceClients;
using MuTest.Core.Common;
using MuTest.Core.Common.Settings;
using MuTest.Core.Exceptions;
using MuTest.Core.Model;
using MuTest.Core.Mutants;
using MuTest.Core.Testing;
using MuTest.Core.Utility;
using MuTest.Cpp.CLI.Core;
using MuTest.Cpp.CLI.Core.AridNodes;
using MuTest.Cpp.CLI.Model;
using MuTest.Cpp.CLI.Mutants;
using MuTest.Cpp.CLI.Options;
using MuTest.Cpp.CLI.Utility;
using Newtonsoft.Json;
using static MuTest.Core.Common.Constants;
using IMutantSelector = MuTest.Cpp.CLI.Core.IMutantSelector;
using MutantSelector = MuTest.Cpp.CLI.Core.MutantSelector;

namespace MuTest.Cpp.CLI
{
    public class MuTestRunner : IMuTestRunner
    {
        public static readonly MuTestSettings MuTestSettings = MuTestSettingsSection.GetSettings();

        private readonly IChalk _chalk;
        private readonly IFirebaseApiClient _client;

        public ICppDirectoryFactory DirectoryFactory { get; }

        public CppBuildContext Context { get; private set; }

        private MuTestOptions _options;
        private Stopwatch _stopwatch;
        private int _totalMutants;
        private int _mutantProgress;
        private static readonly object Sync = new object();
        private CppClass _cppClass;
        private readonly IMutantSelector _mutantsSelector;
        private readonly IAridNodeMutantFilterer _aridNodeMutantFilterer;

        public ICppMutantExecutor MutantsExecutor { get; private set; }

        public MuTestRunner(IChalk chalk,
            ICppDirectoryFactory directoryFactory,
            IFirebaseApiClient client,
            IMutantSelector mutantsSelector = null,
            IAridNodeMutantFilterer aridNodeMutantFilterer = null)
        {
            _chalk = chalk;
            _client = client;
            DirectoryFactory = directoryFactory;
            _mutantsSelector = mutantsSelector ?? new MutantSelector();
            var aridNodeFilterProvider = new AridNodeFilterProvider();
            _aridNodeMutantFilterer = aridNodeMutantFilterer ?? new AridNodeMutantFilterer(aridNodeFilterProvider);
        }

        public async Task RunMutationTest(MuTestOptions options)
        {
            try
            {
                if (!File.Exists(MuTestSettings.MSBuildPath))
                {
                    throw new MuTestInputException($"Unable to locate MSBuild Path at {MuTestSettings.MSBuildPath}. Please update MSBuildPath in MuTest.Console.exe.config if you are using different version");
                }

                _stopwatch = new Stopwatch();
                _stopwatch.Start();
                _options = options;

                _chalk.Default("\nPreparing Required Files...\n");

                DirectoryFactory.NumberOfMutantsExecutingInParallel = _options.ConcurrentTestRunners;

                _cppClass = new CppClass
                {
                    Configuration = _options.Configuration,
                    SourceClass = _options.SourceClass,
                    Platform = _options.Platform,
                    TestClass = _options.TestClass,
                    TestProject = _options.TestProject,
                    Target = _options.Target,
                    SourceHeader = _options.SourceHeader,
                    TestSolution = _options.TestSolution,
                    IncludeBuildEvents = _options.IncludeBuildEvents
                };

                Context = !_options.InIsolation
                    ? DirectoryFactory.PrepareTestFiles(_cppClass)
                    : DirectoryFactory.PrepareSolutionFiles(_cppClass);

                if (Context.TestContexts.Any())
                {
                    await ExecuteBuild();
                    await ExecuteTests();

                    if (!_options.DisableBuildOptimization)
                    {
                        Context.EnableBuildOptimization = true;
                    }

                    _chalk.Default("\nRunning Mutation Analysis...\n");


                    var defaultMutants = CppMutantOrchestrator.GetDefaultMutants(_options.SourceClass, _options.SpecificLines).ToList();
                    defaultMutants = _aridNodeMutantFilterer.FilterMutants(defaultMutants).ToList();
                    defaultMutants = _mutantsSelector.SelectMutants(_options.MutantsPerLine, defaultMutants).ToList();
                    _cppClass.Mutants.AddRange(defaultMutants);

                    if (_cppClass.CoveredLineNumbers.Any())
                    {
                        foreach (var mutant in _cppClass.Mutants)
                        {
                            if (_cppClass.CoveredLineNumbers.All(x => x != mutant.Mutation.LineNumber))
                            {
                                mutant.ResultStatus = MutantStatus.NotCovered;
                            }
                            else if (mutant.Mutation.EndLineNumber > mutant.Mutation.LineNumber)
                            {
                                if (!_cppClass.CoveredLineNumbers.Any(x => x > mutant.Mutation.LineNumber &&
                                                                          x <= mutant.Mutation.EndLineNumber))
                                {
                                    mutant.ResultStatus = MutantStatus.Skipped;
                                }
                            }
                        }
                    }

                    _chalk.Default($"\nNumber of Mutants: {_cppClass.Mutants.Count}\n");

                    var sourceHash = _cppClass
                        .SourceClass
                        .GetCodeFileContent()
                        .ComputeHash();

                    var testHash = _cppClass
                        .TestClass
                        .GetCodeFileContent()
                        .ComputeHash();

                    _cppClass.Sha256 = (sourceHash + testHash).ComputeHash();

                    var data = await _client.GetFileDataFromStorage(_cppClass.Sha256);
                    _cppClass.StoreInDb = true;

                    if (data != null)
                    {
                        var cppClass = JsonConvert.DeserializeObject<CppClass>(data);
                        cppClass.StoreInDb = false;
                        cppClass.SourceClass = _cppClass.SourceClass;
                        cppClass.SourceHeader = _cppClass.SourceHeader;
                        cppClass.TestClass = _cppClass.TestClass;
                        cppClass.TestProject = _cppClass.TestProject;
                        cppClass.Configuration = _cppClass.Configuration;
                        cppClass.Target = _cppClass.Target;
                        cppClass.Platform = _cppClass.Platform;
                        cppClass.TestSolution = _cppClass.TestSolution;
                        cppClass.IncludeBuildEvents = _cppClass.IncludeBuildEvents;

                        _cppClass = cppClass;
                    }

                    MutantsExecutor = new CppMutantExecutor(_cppClass, Context, MuTestSettings)
                    {
                        EnableDiagnostics = _options.EnableDiagnostics,
                        KilledThreshold = _options.KilledThreshold,
                        SurvivedThreshold = _options.SurvivedThreshold,
                        NumberOfMutantsExecutingInParallel = _options.ConcurrentTestRunners
                    };

                    _totalMutants = _cppClass.NotRunMutants.Count;
                    _mutantProgress = 0;

                    if (_cppClass.Mutants.Any() && data == null)
                    {
                        MutantsExecutor.MutantExecuted += MutantAnalyzerOnMutantExecuted;
                        await MutantsExecutor.ExecuteMutants();
                    }

                    await GenerateReports();
                }
            }
            finally
            {
                if (Context != null)
                {
                    DirectoryFactory.DeleteTestFiles(Context);
                }
            }
        }

        private void MutantAnalyzerOnMutantExecuted(object sender, CppMutantEventArgs e)
        {
            lock (Sync)
            {
                var mutant = e.Mutant;
                var lineNumber = mutant.Mutation.LineNumber;
                var status = $"{Environment.NewLine}Line: {lineNumber} - {mutant.ResultStatus.ToString()} - {mutant.Mutation.DisplayName}".PrintWithDateTimeSimple();

                if (mutant.ResultStatus == MutantStatus.Survived)
                {
                    _chalk.Yellow($"{status}{Environment.NewLine}");
                }
                else if (mutant.ResultStatus == MutantStatus.BuildError)
                {
                    _chalk.Red($"{status}{Environment.NewLine}");
                }
                else if (mutant.ResultStatus == MutantStatus.Timeout)
                {
                    _chalk.Cyan($"{status}{Environment.NewLine}");
                }
                else
                {
                    _chalk.Green($"{status}{Environment.NewLine}");
                }

                if (_options.EnableDiagnostics)
                {
                    _chalk.Red($"{e.BuildLog.ConvertToPlainText()}{Environment.NewLine}");
                    _chalk.Red($"{e.TestLog.ConvertToPlainText()}{Environment.NewLine}");
                }

                _mutantProgress++;
                UpdateProgress();
            }
        }

        private void UpdateProgress()
        {
            if (_totalMutants == 0)
            {
                return;
            }

            var percentage = (int)100.0 * _mutantProgress / _totalMutants;
            lock (Sync)
            {
                _chalk.Cyan(" [" + new string('*', percentage / 2) + "] " + percentage + "%");
            }
        }

        private async Task ExecuteBuild()
        {
            _chalk.Default("\nBuilding Solution...\n");
            var log = new StringBuilder();
            void OutputData(object sender, string args) => log.AppendLine(args);
            var testCodeBuild = new CppBuildExecutor(
                MuTestSettings,
                string.Format(Context.TestSolution.FullName, 0),
                _cppClass.Target)
            {
                Configuration = _options.Configuration,
                EnableLogging = _options.EnableDiagnostics,
                IntDir = string.Format(Context.IntDir, 0),
                IntermediateOutputPath = string.Format(Context.IntermediateOutputPath, 0),
                OutDir = string.Format(Context.OutDir, 0),
                OutputPath = string.Format(Context.OutputPath, 0),
                Platform = _options.Platform,
                QuietWithSymbols = true
            };

            if (!_options.IncludeBuildEvents)
            {
                string.Format(Context.TestProject.FullName, 0).RemoveBuildEvents();
            }

            testCodeBuild.OutputDataReceived += OutputData;
            testCodeBuild.BeforeMsBuildExecuted += (sender, args) =>
            {
                _chalk.Yellow($"\nRunning MSBuild with {args}\n");
            };
            await testCodeBuild.ExecuteBuild();

            testCodeBuild.OutputDataReceived -= OutputData;

            if (testCodeBuild.LastBuildStatus == BuildExecutionStatus.Failed && !_options.InIsolation)
            {
                _chalk.Yellow("\nBuild Failed...Preparing new solution files\n");
                DirectoryFactory.DeleteTestFiles(Context);
                Context = DirectoryFactory.PrepareSolutionFiles(_cppClass);

                testCodeBuild = new CppBuildExecutor(
                    MuTestSettings,
                    string.Format(Context.TestSolution.FullName, 0),
                    _cppClass.Target)
                {
                    Configuration = _options.Configuration,
                    EnableLogging = _options.EnableDiagnostics,
                    IntDir = string.Format(Context.IntDir, 0),
                    IntermediateOutputPath = string.Format(Context.IntermediateOutputPath, 0),
                    OutDir = string.Format(Context.OutDir, 0),
                    OutputPath = string.Format(Context.OutputPath, 0),
                    Platform = _options.Platform,
                    QuietWithSymbols = true
                };

                testCodeBuild.BeforeMsBuildExecuted += (sender, args) =>
                {
                    _chalk.Yellow($"\nRunning MSBuild with {args}\n");
                };
                await testCodeBuild.ExecuteBuild();
            }

            if (testCodeBuild.LastBuildStatus == BuildExecutionStatus.Failed)
            {
                _chalk.Yellow("\nBuild Failed...Taking Source Code Backup\n");
                _options.ConcurrentTestRunners = 1;
                DirectoryFactory.DeleteTestFiles(Context);
                Context = DirectoryFactory.TakingSourceCodeBackup(_cppClass);

                testCodeBuild = new CppBuildExecutor(
                    MuTestSettings,
                    Context.TestSolution.FullName,
                    _cppClass.Target)
                {
                    Configuration = _options.Configuration,
                    EnableLogging = _options.EnableDiagnostics,
                    IntDir = Context.IntDir,
                    IntermediateOutputPath = Context.IntermediateOutputPath,
                    OutDir = Context.OutDir,
                    OutputPath = Context.OutputPath,
                    Platform = _options.Platform,
                    QuietWithSymbols = true
                };

                testCodeBuild.BeforeMsBuildExecuted += (sender, args) =>
                {
                    _chalk.Yellow($"\nRunning MSBuild with {args}\n");
                };
                await testCodeBuild.ExecuteBuild();
            }

            if (testCodeBuild.LastBuildStatus == BuildExecutionStatus.Failed)
            {
                throw new MuTestFailingBuildException(log.ToString());
            }

            _chalk.Green("\nBuild Succeeded!");
        }

        private async Task ExecuteTests()
        {
            _chalk.Default("\nExecuting Tests...");
            var log = new StringBuilder();
            void OutputData(object sender, string args) => log.AppendLine(args);
            var testExecutor = new GoogleTestExecutor
            {
                LogDir = MuTestSettings.TestsResultDirectory
            };

            testExecutor.OutputDataReceived += OutputData;
            var projectDirectory = Path.GetDirectoryName(_options.TestProject);
            var projectName = Path.GetFileNameWithoutExtension(_options.TestProject);
            var projectNameFromTestContext = string.Format(Path.GetFileNameWithoutExtension(Context.TestProject.Name), 0);

            var app = $"{projectDirectory}\\{string.Format(Context.OutDir.TrimEnd('/'), 0)}\\{projectName}.exe";

            if (!File.Exists(app))
            {
                app = $"{projectDirectory}/{string.Format(Context.OutDir, 0)}{projectNameFromTestContext}.exe";
            }

            if (!File.Exists(app))
            {
                throw new MuTestFailingTestException($"Unable to find google tests at path {app}");
            }

            var cppTestContext = Context.TestContexts.First();
            var filter = $"{Path.GetFileNameWithoutExtension(cppTestContext.TestClass.Name)}*";
            await testExecutor.ExecuteTests(app, filter);

            if (testExecutor.TestResult != null)
            {
                _cppClass.NumberOfTests = Convert.ToInt32(testExecutor.TestResult.Tests);

                if (_cppClass.NumberOfTests == 0)
                {
                    filter = string.Empty;
                    _cppClass.UseClassFilter = false;
                    await testExecutor.ExecuteTests(app, string.Empty);
                }
            }

            if (testExecutor.TestResult != null)
            {
                _cppClass.NumberOfTests = Convert.ToInt32(testExecutor.TestResult.Tests);
                _cppClass.NumberOfDisabledTests = Convert.ToInt32(testExecutor.TestResult.Disabled);

                _chalk.Default($"\n\nNumber of Tests: {_cppClass.NumberOfTests}\n");

                _cppClass.Tests.AddRange(testExecutor
                    .TestResult
                    .Testsuite
                    .SelectMany(x => x.Testcase)
                    .Select(x => new Test
                    {
                        Name = $"{x.Classname.Replace("_mutest_test_0", string.Empty)}.{x.Name}",
                        ExecutionTime = Convert.ToDouble(x.Time)
                    }));
            }

            if (testExecutor.LastTestExecutionStatus != TestExecutionStatus.Success)
            {
                throw new MuTestFailingTestException(log.ToString());
            }

            _chalk.Default("\nCalculating Code Coverage...\n");
            var coverageExecutor = new OpenCppCoverageExecutor(MuTestSettings.OpenCppCoveragePath, MuTestSettings.TestsResultDirectory);

            await coverageExecutor
                .GenerateCoverage(
                    cppTestContext.SourceClass.DirectoryName, app, filter);

            if (coverageExecutor.CoverageReport != null)
            {
                foreach (var package in coverageExecutor.CoverageReport.Packages.Package)
                {
                    foreach (var packageClass in package.Classes.Class)
                    {
                        if (cppTestContext.SourceClass.FullName.EndsWith(packageClass.Filename, StringComparison.InvariantCultureIgnoreCase))
                        {
                            var coveredLines = (uint)packageClass.Lines.Line.Count(x => x.Hits > 0);
                            var totalLines = (uint)packageClass.Lines.Line.Count;

                            if (totalLines == 0)
                            {
                                totalLines = 1;
                            }

                            _chalk.Yellow($"\nCode Coverage for Class {Path.GetFileName(_cppClass.SourceClass)} is {decimal.Divide(coveredLines, totalLines):P} ({coveredLines}/{totalLines})\n");
                            _cppClass.Coverage = new Coverage
                            {
                                LinesCovered = coveredLines,
                                LinesNotCovered = totalLines - coveredLines
                            };

                            var factor = Context.UseMultipleSolutions || !Context.NamespaceAdded
                                ? 0u
                                : 3u;

                            foreach (var line in packageClass.Lines.Line)
                            {
                                var currentLineNumber = Convert.ToUInt32(line.Number);
                                if (line.Hits > 0)
                                {
                                    _cppClass.CoveredLineNumbers.Add(currentLineNumber - factor);
                                }
                            }

                            break;
                        }
                    }
                }
            }

            testExecutor.OutputDataReceived -= OutputData;
        }

        private async Task GenerateReports()
        {
            var consoleBuilder = new StringBuilder();
            if (_cppClass != null)
            {
                MutantsExecutor?.PrintMutatorSummary(consoleBuilder, _cppClass.Mutants);
                MutantsExecutor?.PrintClassSummary(_cppClass, consoleBuilder);
                consoleBuilder.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10;\">");
                consoleBuilder.AppendLine("Execution Time: ".PrintImportantWithLegend());
                consoleBuilder.Append($"{_stopwatch.Elapsed}".PrintWithPreTagWithMarginImportant());
                consoleBuilder.AppendLine("</fieldset>");

                _chalk.Default($"{Environment.NewLine}{consoleBuilder.ToString().ConvertToPlainText()}{Environment.NewLine}");

                _cppClass.ExecutionTime = _stopwatch.ElapsedMilliseconds;

                var builder = new StringBuilder(HtmlTemplate);

                if (!_cppClass.StoreInDb)
                {
                    MutantsExecutor?.PrintMutationReport(new StringBuilder(), _cppClass.Mutants);
                }

                builder.Append(MutantsExecutor?.LastExecutionOutput.PrintImportant() ?? string.Empty);
                MutantsExecutor?.PrintMutatorSummary(builder, _cppClass.Mutants);
                MutantsExecutor?.PrintClassSummary(_cppClass, builder);
                builder.AppendLine("<fieldset style=\"margin-bottom:10; margin-top:10;\">");
                builder.AppendLine("Execution Time: ".PrintImportantWithLegend());
                builder.Append($"{_stopwatch.Elapsed}".PrintWithPreTagWithMarginImportant());
                builder.AppendLine("</fieldset>");

                if (_cppClass.StoreInDb)
                {
                    await StoreInDb();
                }

                var fileName = Path.GetFileNameWithoutExtension(_cppClass.SourceClass)?.Replace(".", "_");
                CreateHtmlReport(builder, fileName);
                CreateJsonReport(fileName,
                    new JsonOptions
                    {
                        Options = _options,
                        Result = _cppClass
                    });
            }
        }

        private async Task StoreInDb()
        {
            var result = new MutationResult
            {
                Coverage = new CodeCoverage
                {
                    Covered = _cppClass.Coverage?.LinesCovered ?? 0,
                    Uncovered = _cppClass.Coverage?.LinesNotCovered ?? 0
                },
                Key = _cppClass.Sha256,
                DateCreated = DateTime.UtcNow,
                Mutation = new Api.Clients.ServiceClients.Mutation
                {
                    Survived = _cppClass.MutationScore.Survived,
                    Killed = _cppClass.MutationScore.Killed
                },
                NoOfTests = _cppClass.NumberOfTests,
                Source = _cppClass.SourceClass.GithubPath(),
                Test = _cppClass.TestClass.GithubPath()
            };

            foreach (var score in _cppClass.MutatorWiseMutationScores)
            {
                result.MutatorWiseMutations.Add(score.Mutator, new Api.Clients.ServiceClients.Mutation
                {
                    Killed = score.MutationScore.Killed,
                    Survived = score.MutationScore.Survived
                });
            }

            await _client.StoreInDatabaseAsync(result);
        }

        private void CreateJsonReport<T>(string fileName, T output)
        {
            if (!string.IsNullOrWhiteSpace(_options.JsonOutputPath))
            {
                var outputPath = _options.JsonOutputPath.Replace(SourceClassPlaceholder, fileName);
                var file = new FileInfo(outputPath);
                if (file.Exists)
                {
                    file.Delete();
                }

                var directoryName = Path.GetDirectoryName(file.FullName);
                if (!string.IsNullOrWhiteSpace(directoryName) &&
                    !Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                file.Create().Close();
                File.WriteAllText(outputPath, JsonConvert.SerializeObject(output, Formatting.Indented));

                if (_cppClass.StoreInDb && output is JsonOptions data)
                {
                    _client.StoreFileAsync(_cppClass.Sha256, JsonConvert.SerializeObject(data.Result));
                }

                _chalk.Green($"\nYour json report has been generated at: \n {file.FullName} \n");
            }
        }

        private void CreateHtmlReport(StringBuilder builder, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(_options.HtmlOutputPath))
            {
                var outputPath = _options.HtmlOutputPath.Replace(SourceClassPlaceholder, fileName);
                var file = new FileInfo(outputPath);
                if (file.Exists)
                {
                    file.Delete();
                }

                var directoryName = Path.GetDirectoryName(file.FullName);
                if (!string.IsNullOrWhiteSpace(directoryName) &&
                    !Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                file.Create().Close();
                File.WriteAllText(outputPath, builder.ToString());

                _chalk.Green($"\nYour html report has been generated at: \n {file.FullName} \nYou can open it in your browser of choice. \n");
            }
        }
    }
}