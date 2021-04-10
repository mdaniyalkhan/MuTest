using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dashboard.Common;
using Dashboard.Views;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.POCO;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.WindowsAPICodePack.Dialogs;
using MuTest.Core.Common;
using MuTest.Core.Common.ClassDeclarationLoaders;
using MuTest.Core.Common.InspectCode;
using MuTest.Core.Common.Settings;
using MuTest.Core.Common.StaticAnalyzers;
using MuTest.Core.Model;
using MuTest.Core.Utility;
using static MuTest.Core.Common.Constants;
using Compilation = MuTest.Core.Common.InspectCode.Compilation;

namespace Dashboard.ViewModel
{
    [POCOViewModel]
    public class ClassViewerViewModel
    {
        private const string UnSupportedProject = "Dynamic Asserts no supported for this type of Project";
        private const string ShouldlyMethod = "() => ";
        private const string ShouldThrow = ".ShouldThrow";
        private const string ShouldNotThrow = ".ShouldNotThrow";

        public virtual ListBoxEditViewModel SourceMethodList { get; }

        public virtual bool IsSplashScreenShown { get; set; }

        public virtual ListBoxEditViewModel TestMethodList { get; }

        public virtual ControlViewModel ChkInReleaseMode { get; }

        public virtual ControlViewModel ChkTargetPlatformX64 { get; }

        public virtual ControlViewModel ChkIgnoreFailingTests { get; }

        public virtual ControlViewModel ChkIgnoreCollectionOrder { get; }

        public virtual ControlViewModel ChkCompareChildren { get; }

        public virtual ControlViewModel ChkParameterizedAsserts { get; }

        public virtual ControlViewModel ChkUseClassFilter { get; }

        public virtual ControlViewModel ChkBuildReferences { get; }

        public virtual decimal DiscardCost { get; set; } = 50;

        public virtual decimal StructDept { get; set; } = 5;

        public virtual string SourceCodeHtml { get; set; } = HtmlTemplate;

        [ServiceProperty(Key = "NotificationService")]
        protected virtual INotificationService NotificationService => null;

        [ServiceProperty(Key = "MessageBoxService")]
        protected virtual IMessageBoxService MessageBoxService => null;

        public static readonly MuTestSettings MuTestSettings = MuTestSettingsSection.GetSettings();

        [ServiceProperty(SearchMode = ServiceSearchMode.PreferParents)]
        protected virtual IDocumentManagerService DocumentManagerService => null;

        private readonly SourceClassDetail _source;
        private readonly IBuildExecutor _testCodeBuild;
        private readonly ICommandPromptOutputLogger _outputLogger;
        private readonly ITestExecutor _testExecutor;
        private readonly MethodAnalyzer _methodAnalyzer;
        private CommandPromptOutputViewerViewModel _buildLog;
        private string _currentFileName;

        protected ClassViewerViewModel()
        {
        }

        protected ClassViewerViewModel(SourceClassDetail source)
        {
            _source = source;

            SourceMethodList = ListBoxEditViewModel.CreateListBoxEdit();
            TestMethodList = ListBoxEditViewModel.CreateListBoxEdit();
            ChkBuildReferences = ControlViewModel.CreateWithChecked();
            ChkInReleaseMode = ControlViewModel.Create();
            ChkTargetPlatformX64 = ControlViewModel.Create();
            ChkIgnoreFailingTests = ControlViewModel.Create();
            ChkUseClassFilter = ControlViewModel.CreateWithChecked();
            ChkIgnoreCollectionOrder = ControlViewModel.CreateWithChecked();
            ChkCompareChildren = ControlViewModel.CreateWithChecked();
            ChkParameterizedAsserts = ControlViewModel.CreateWithChecked();

            _testCodeBuild = new BuildExecutor(MuTestSettings, _source.TestClaz.ClassProject);
            _outputLogger = new CommandPromptOutputLogger();
            _testExecutor = new TestExecutor(MuTestSettings, source.TestClaz.ClassLibrary);
            _methodAnalyzer = new MethodAnalyzer();
            InitItemSources();
        }

        private async void InitItemSources()
        {
            var methodAnalyzer = new MethodsInitializer
            {
                IncludeNestedClasses = _source.IncludeNestedClasses
            };

            await methodAnalyzer.FindMethods(_source);
            SourceMethodList.ItemsSource = _source.MethodDetails;
            TestMethodList.ItemsSource = _source.TestClaz.MethodDetails;
        }

        public static ClassViewerViewModel Create(SourceClassDetail source)
        {
            return ViewModelSource.Create(() => new ClassViewerViewModel(source));
        }

        public void BtnStaticMutantsClick(object selectedItems)
        {
            _currentFileName = $"{_source.Claz.Syntax.ClassName()}_Static_Mutation_Analysis";
            var selectedMethods = (ObservableCollection<object>)selectedItems;
            if (!selectedMethods.Any())
            {
                MessageBoxService.Show(SelectSourceMethodErrorMessage);
                return;
            }

            SourceCodeHtml = HtmlTemplate;
            SourceCodeHtml += _methodAnalyzer.FindMutants(_source.TestClaz.Claz.Syntax, selectedMethods.Cast<MethodDetail>().ToList()).ToString();
        }

        public void BtnDynamicMutantsClick(object selectedItems)
        {
            if (!_source.ClassLibrary.EndsWith(LibraryFile) && !_source.ClassLibrary.EndsWith(ExecutableFile))
            {
                MessageBoxService.Show(SourceDllNotSelectedErrorMessage);
                return;
            }

            if (!_source.ClassProject.EndsWith(CSharpProjectFile))
            {
                MessageBoxService.Show(SourceProjectNotSelectedErrorMessage);
                return;
            }

            if (!_source.TestClaz.ClassLibrary.EndsWith(LibraryFile))
            {
                MessageBoxService.Show(TestDllNotSelectedErrorMessage);
                return;
            }

            var selectedMethods = (ObservableCollection<object>)selectedItems;
            if (!selectedMethods.Any())
            {
                MessageBoxService.Show(SelectSourceMethodErrorMessage);
                return;
            }

            var methods = selectedMethods.Cast<MethodDetail>().ToList();
            var classDetail = new SourceClassDetail
            {
                Claz = _source.Claz,
                ClassLibrary = _source.ClassLibrary,
                TestClaz = _source.TestClaz,
                ClassProject = _source.ClassProject,
                FilePath = _source.FilePath,
                FullName = _source.FullName,
                Coverage = _source.Coverage,
                BuildInReleaseMode = ChkInReleaseMode.IsChecked,
                X64TargetPlatform = ChkTargetPlatformX64.IsChecked,
                DoNetCoreProject = _source.DoNetCoreProject
            };

            classDetail.MethodDetails.AddRange(methods);
            var document = DocumentManagerService
                .CreateDocument(nameof(MutantViewer), MutantViewerViewModel.Create(classDetail));
            document.Title = $"Dynamic Mutant Analyzer - {_source.FullName}";
            document.Show();
        }

        public async Task BtnExecuteTestsClick(object selectedItems)
        {
            try
            {
                if (!_source.TestClaz.ClassLibrary.EndsWith(LibraryFile))
                {
                    MessageBoxService.Show(TestDllNotSelectedErrorMessage);
                    return;
                }

                var selectedMethods = (ObservableCollection<object>)selectedItems;

                if (!selectedMethods.Any())
                {
                    MessageBoxService.Show(SelectTestMethodsErrorMessage);
                    return;
                }

                var log = _outputLogger.GetLogFromOutput(TestsExecutionOutput, string.Empty);
                var document = DocumentManagerService.CreateDocument(nameof(CommandPromptOutputViewer), log);
                document.Title = TestsExecutionOutput;
                document.Show();

                void OutputData(object sender, string args) => log.CommandPromptOutput += args.Encode().PrintWithPreTag();
                _testExecutor.EnableCustomOptions = true;
                _testExecutor.EnableLogging = true;
                _testExecutor.OutputDataReceived += OutputData;
                _testExecutor.X64TargetPlatform = ChkTargetPlatformX64.IsChecked;
                _testExecutor.FullyQualifiedName = selectedMethods.Count > Convert.ToInt32(MuTestSettings.UseClassFilterTestsThreshold) ||
                                                   _source.TestClaz.BaseClass != null
                    ? _source.TestClaz.Claz.Syntax.FullName()
                    : string.Empty;
                await _testExecutor.ExecuteTests(selectedMethods.Cast<MethodDetail>().ToList());

                var testLog = _outputLogger.GetLogFromOutput(TestsExecutionResult, string.Empty);
                if (_testExecutor.TestResult != null)
                {
                    testLog.CommandPromptOutput += _testExecutor
                        .PrintTestResult(_testExecutor.TestResult)
                        .PrintWithPreTagImportant(color: Colors.Brown);
                }

                var coverageAnalyzer = new CoverageAnalyzer();
                coverageAnalyzer.FindCoverage(_source, _testExecutor.CodeCoverage);
                testLog.CommandPromptOutput += coverageAnalyzer.Output;

                var testResult = DocumentManagerService.CreateDocument(nameof(CommandPromptOutputViewer), testLog);
                testResult.Title = TestsExecutionResult;
                testResult.Show();
                var notification = NotificationService.CreatePredefinedNotification(TestExecutionIsCompletedNotification, string.Empty, string.Empty);
                await notification.ShowAsync();
                _testExecutor.OutputDataReceived -= OutputData;
            }
            catch (Exception exp)
            {
                Trace.TraceError("Tests Execution Failed [{0}]", exp);
                MessageBoxService.Show(exp.Message);
            }
        }

        public async Task BtnInspectCodeClick()
        {
            IsSplashScreenShown = true;
            var duplicateFinder = new DuplicateCodeFinder(MuTestSettings, _source.TestClaz.FilePath)
            {
                IncludePartialClasses = _source.TestClaz.PartialClasses.Count > 1,
                DiscardCost = DiscardCost
            };
            await duplicateFinder.FindDuplicateCode();

            var inspectionsOutput = new StringBuilder();
            Compilation.TestClass = _source.TestClaz;
            Compilation.LoadSemanticModels();
            foreach (var partialClass in _source.TestClaz.PartialClasses)
            {
                var inspections = await Inspector.FindInspections(partialClass.Claz.Syntax, partialClass.FilePath);
                if (inspections.Any())
                {
                    inspectionsOutput.Append(Inspector.PrintInspections(inspections, partialClass.FilePath));
                }
            }

            Compilation.UnLoadSemanticModels();

            var inspectionWindow = DocumentManagerService.CreateDocument(
                nameof(CommandPromptOutputViewer),
                _outputLogger.GetLogFromOutput(CodeInspectionReport, inspectionsOutput.ToString()));
            inspectionWindow.Title = CodeInspectionReport;
            inspectionWindow.Show();

            if (!string.IsNullOrWhiteSpace(duplicateFinder.OutputHtml))
            {
                var duplicateCode = DocumentManagerService.CreateDocument(
                    nameof(CommandPromptOutputViewer),
                    _outputLogger.GetLogFromOutput(DuplicateCodeWindow, duplicateFinder.OutputHtml));
                duplicateCode.Title = DuplicateCodeWindow;
                duplicateCode.Show();
            }

            IsSplashScreenShown = false;
        }

        public void BtnBuildProjectClick()
        {
            if (!_source.TestClaz.ClassProject.EndsWith(CSharpProjectFile))
            {
                MessageBoxService.Show(ProjectNotSelectedErrorMessage);
                return;
            }

            ExecuteBuild();
        }

        private async void ExecuteBuild()
        {
            try
            {
                _buildLog = _outputLogger.GetLogFromOutput(MSBuildOutput, string.Empty);
                var document = DocumentManagerService.CreateDocument(nameof(CommandPromptOutputViewer), _buildLog);
                document.Title = MSBuildOutput;
                document.Show();

                void OutputData(object sender, string args)
                {
                    _buildLog.CommandPromptOutput += args.PrintWithPreTag();
                }

                _testCodeBuild.OutputDataReceived += OutputData;

                if (ChkInReleaseMode.IsChecked && ChkBuildReferences.IsChecked)
                {
                    await _testCodeBuild.ExecuteBuildInReleaseModeWithDependencies();
                }
                else if (ChkInReleaseMode.IsChecked)
                {
                    await _testCodeBuild.ExecuteBuildInReleaseModeWithoutDependencies();
                }
                else if (ChkBuildReferences.IsChecked)
                {
                    await _testCodeBuild.ExecuteBuildInDebugModeWithDependencies();
                }
                else
                {
                    await _testCodeBuild.ExecuteBuildInDebugModeWithoutDependencies();
                }

                INotification notification = NotificationService.CreatePredefinedNotification(
                    _testCodeBuild.LastBuildStatus == BuildExecutionStatus.Success
                        ? BuildIsSucceededNotification
                        : BuildIsFailingNotification, string.Empty, string.Empty);
                await notification.ShowAsync();
                _testCodeBuild.OutputDataReceived -= OutputData;
            }
            catch (Exception exp)
            {
                Trace.TraceError("Unknown Exception Occurred On Test Project Build {0}", exp);
                MessageBoxService.Show(exp.Message);
            }
        }

        public async Task BtnGenerateDynamicAssertsClick(object selectedItems)
        {
            _currentFileName = $"{_source.Claz.Syntax.ClassName()}_Dynamic_Asserts";
            var originalLines = new List<string>();
            FileInfo testFile = null;
            FileInfo project = null;
            try
            {
                if (!_source.TestClaz.ClassLibrary.EndsWith(LibraryFile))
                {
                    MessageBoxService.Show(TestDllNotSelectedErrorMessage);
                    return;
                }

                if (!_source.TestClaz.ClassProject.EndsWith(CSharpProjectFile))
                {
                    MessageBoxService.Show(ProjectNotSelectedErrorMessage);
                    return;
                }

                if (!Directory.Exists(MuTestSettings.DynamicAssertsAssemblyPath))
                {
                    MessageBoxService.Show("Dynamic Asserts Assembly Path Not Found!");
                    return;
                }

                if (_source.TestClaz.DoNetCoreProject && !Directory.Exists(MuTestSettings.DynamicAssertsCoreAssemblyPath))
                {
                    MessageBoxService.Show("Dynamic Asserts Core Assembly Path Not Found!");
                    return;
                }

                var selectedMethods = (ObservableCollection<object>)selectedItems;

                if (!selectedMethods.Any())
                {
                    MessageBoxService.Show(SelectTestMethodsErrorMessage);
                    return;
                }

                var setupMethod = _source.TestClaz.PartialClassWithSetupLogic?.Claz.Syntax.NUnitSetupMethod();
                var tearDownMethod = _source.TestClaz.PartialClassWithSetupLogic?.Claz.Syntax.NUnitTearDownMethod();
                var setupLibPath = _source.TestClaz.PartialClassWithSetupLogic ?? _source.TestClaz;

                var factory = new TestDirectoryFactory(_source)
                {
                    NumberOfMutantsExecutingInParallel = 1
                };
                project = factory.GetProjectFile(0, _source.TestClaz.ClassProject);
                if (project.Exists)
                {
                    project.Delete();
                }

                project.Create().Close();

                await factory.UpdateProjectFile(0, _source.TestClaz.Claz.Syntax.NameSpace(), setupLibPath?.FilePath, _source.TestClaz.ClassProject);
                var projectDocument = new FileInfo(project.FullName).GetProjectDocument();
                var referenceNode = projectDocument.SelectSingleNode("/Project/ItemGroup/Reference")?.ParentNode;

                var libraryPathName = $"{Path.GetDirectoryName(_source.TestClaz.ClassLibrary)}0";
                Path.GetDirectoryName(_source.TestClaz.ClassLibrary).DirectoryCopy(libraryPathName);
                var xml = $@"
                            <Reference Include=""KellermanSoftware.Compare-NET-Objects, Version=4.73.0.0, Culture=neutral, PublicKeyToken=d970ace04cc85217, processorArchitecture=MSIL"">
                              <SpecificVersion>False</SpecificVersion>
                              <HintPath>{libraryPathName}\KellermanSoftware.Compare-NET-Objects.dll</HintPath>
                            </Reference>
                            <Reference Include=""DeepCloner, Version=0.10.0.0, Culture=neutral, PublicKeyToken=dc0b95cf99bf4e99, processorArchitecture=MSIL"">
                              <HintPath>{libraryPathName}\DeepCloner.dll</HintPath>
                            </Reference>
	                        <Reference Include=""MuTest.DynamicAsserts, Version=1.0.0.0, Culture=neutral, processorArchitecture=MSIL"">
                              <SpecificVersion>False</SpecificVersion>
                              <HintPath>{libraryPathName}\MuTest.DynamicAsserts.dll</HintPath>
                            </Reference>
	                        ";

                if (referenceNode == null)
                {
                    referenceNode = projectDocument.SelectSingleNode("/Project");
                    if (referenceNode != null)
                    {
                        xml = string.Join(string.Empty, "<ItemGroup>", xml, "</ItemGroup>");
                    }
                }

                if (referenceNode == null)
                {
                    MessageBoxService.Show(UnSupportedProject);
                }
                else
                {
                    referenceNode.InnerXml += xml;
                    projectDocument.Save(project.FullName);

                    testFile = factory.GetSourceCodeFile(0, setupLibPath?.FilePath);
                    if (!_source.DoNetCoreProject)
                    {
                        MuTestSettings.DynamicAssertsAssemblyPath.DirectoryCopy(libraryPathName);
                    }
                    else
                    {
                        MuTestSettings.DynamicAssertsCoreAssemblyPath.DirectoryCopy(libraryPathName);
                    }

                    var setupLocation = _source.TestClaz.Claz.Syntax.DescendantNodes<MethodDeclarationSyntax>().FirstOrDefault().LineNumber();
                    var tearDownLocationEnd = _source.TestClaz.Claz.Syntax.DescendantNodes<MethodDeclarationSyntax>().FirstOrDefault().EndLineNumber() + 1;

                    if (setupMethod != null && tearDownMethod != null)
                    {
                        setupLocation = setupMethod.EndLineNumber();
                        tearDownLocationEnd = tearDownMethod.LineNumber() + 3;
                    }

                    var fileLines = new List<string>();
                    var id = $"{DateTime.Now:yyyyMdhhmmss}";
                    using (var reader = new StreamReader(testFile.FullName))
                    {
                        string line;
                        var lineNumber = 1;
                        while ((line = reader.ReadLine()) != null)
                        {
                            fileLines.Add(line);
                            originalLines.Add(line);
                            if (lineNumber == setupLocation)
                            {
                                var compareChildren = ChkCompareChildren.IsChecked
                                    ? "true"
                                    : "false";

                                fileLines.Add(
                                    _source.TestClaz.SetupInBaseClass || setupMethod == null
                                        ? $"[NUnit.Framework.SetUp]public void  SetupDynamicAsserts() {{ MuTest.DynamicAsserts.ObjectGraphGenerator.SetupTestClass(this, NUnit.Framework.TestContext.CurrentContext.Test.Name, \"{id}\", {StructDept}, {compareChildren});}}\n"
                                        : $"MuTest.DynamicAsserts.ObjectGraphGenerator.SetupTestClass(this, NUnit.Framework.TestContext.CurrentContext.Test.Name, \"{id}\", {StructDept}, {compareChildren});");
                            }

                            if (lineNumber == tearDownLocationEnd)
                            {
                                fileLines.Add(_source.TestClaz.SetupInBaseClass || tearDownMethod == null
                                    ? "[NUnit.Framework.TearDown]public void  TearDownDynamicAsserts() {{ MuTest.DynamicAsserts.ObjectGraphGenerator.GenerateObjectGraphForTestClass(this); }}\n"
                                    : " MuTest.DynamicAsserts.ObjectGraphGenerator.GenerateObjectGraphForTestClass(this);");
                            }

                            lineNumber++;
                        }
                    }

                    testFile.FullName.WriteLines(fileLines);

                    SourceCodeHtml = HtmlTemplate;
                    SourceCodeHtml += "Building Project".PrintWithDateTime().PrintWithPreTagImportant();

                    _buildLog = _outputLogger.GetLogFromOutput(MSBuildOutput, string.Empty);
                    var buildDocument = DocumentManagerService.CreateDocument(nameof(CommandPromptOutputViewer), _buildLog);
                    buildDocument.Title = MSBuildOutput;
                    buildDocument.Show();

                    void BuildOutputData(object sender, string args) => _buildLog.CommandPromptOutput += args.PrintWithPreTag();

                    var build = new BuildExecutor(MuTestSettings, project.FullName)
                    {
                        OutputPath = libraryPathName,
                        IntermediateOutputPath = $@"{libraryPathName}\obj\"
                    };
                    build.OutputDataReceived += BuildOutputData;
                    if (!ChkInReleaseMode.IsChecked)
                    {
                        await build.ExecuteBuildInDebugModeWithoutDependencies();
                    }
                    else
                    {
                        await build.ExecuteBuildInReleaseModeWithoutDependencies();
                    }

                    if (build.LastBuildStatus == BuildExecutionStatus.Failed)
                    {
                        SourceCodeHtml += "Build Failed!".PrintWithPreTagImportant();
                    }
                    else
                    {
                        buildDocument.Close();

                        SourceCodeHtml += "Executing Tests".PrintWithDateTime().PrintWithPreTagImportant();
                        var log = _outputLogger.GetLogFromOutput(TestsExecutionOutput, string.Empty);
                        var testDocument = DocumentManagerService.CreateDocument(nameof(CommandPromptOutputViewer), log);
                        testDocument.Title = TestsExecutionOutput;
                        testDocument.Show();

                        void OutputData(object sender, string args) => log.CommandPromptOutput += args.Encode().PrintWithPreTag();
                        var testExecutor = new TestExecutor(MuTestSettings, $"{libraryPathName}\\{Path.GetFileName(_source.TestClaz.ClassLibrary)}");
                        testExecutor.OutputDataReceived += OutputData;
                        testExecutor.EnableCustomOptions = false;
                        testExecutor.EnableLogging = false;
                        testExecutor.FullyQualifiedName = selectedMethods.Count > Convert.ToInt32(MuTestSettings.UseClassFilterTestsThreshold) ||
                                                          ChkUseClassFilter.IsChecked
                            ? _source.TestClaz.Claz.Syntax.FullName()
                            : string.Empty;

                        await testExecutor.ExecuteTests(selectedMethods.Cast<MethodDetail>().ToList());

                        if (testExecutor.LastTestExecutionStatus == TestExecutionStatus.Success ||
                            ChkIgnoreFailingTests.IsChecked)
                        {
                            testDocument.Close();
                            testFile.FullName.WriteLines(originalLines);

                            var outputFile = $"{MuTestSettings.DynamicAssertsOutputPath}{id}";
                            var asserts = new List<AssertMethod>();
                            if (Directory.Exists(outputFile) &&
                                Directory.GetFiles(outputFile).Any())
                            {
                                var files = Directory.GetFiles(outputFile);
                                foreach (var file in files)
                                {
                                    using (var reader = new StreamReader(file))
                                    {
                                        string line;
                                        AssertMethod assertMethod = null;
                                        StringBuilder builder = null;
                                        while ((line = reader.ReadLine()) != null)
                                        {
                                            if (line.StartsWith("Name"))
                                            {
                                                assertMethod = new AssertMethod
                                                {
                                                    Method = reader.ReadLine()
                                                };
                                            }

                                            if (line.StartsWith("Asserts"))
                                            {
                                                continue;
                                            }

                                            if (line.StartsWith("()") ||
                                                line.StartsWith("//()"))
                                            {
                                                if (builder != null)
                                                {
                                                    var assertValue = builder.ToString();
                                                    var typeIndex = assertValue.LastIndexOf('-');
                                                    var type = assertValue.Substring(typeIndex, assertValue.Length - typeIndex).Trim('-');
                                                    assertValue = assertValue.Substring(0, typeIndex);
                                                    assertMethod?.Asserts.Add(new Assert(assertValue, type));
                                                }

                                                builder = new StringBuilder();
                                            }

                                            builder?.Append(line);
                                        }

                                        if (builder != null)
                                        {
                                            var assertValue = builder.ToString();
                                            var typeIndex = assertValue.LastIndexOf('-');
                                            var type = assertValue.Substring(typeIndex, assertValue.Length - typeIndex).Trim('-');
                                            assertValue = assertValue.Substring(0, typeIndex);
                                            assertMethod?.Asserts.Add(new Assert(assertValue, type));
                                        }

                                        if (assertMethod != null)
                                        {
                                            asserts.Add(assertMethod);
                                        }
                                    }
                                }

                                if (asserts.Any())
                                {
                                    GenerateDynamicAsserts(asserts);
                                }
                            }
                            else
                            {
                                SourceCodeHtml += "No any Asserts are generated".PrintWithPreTagImportant();
                            }
                        }
                        else
                        {
                            SourceCodeHtml += "Tests are failing! Please Fix Tests".PrintWithPreTag();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Unsupported Project {0}", e);
                MessageBoxService.Show(UnSupportedProject);
            }
            finally
            {
                if (testFile != null && testFile.Exists)
                {
                    testFile.Delete();
                }

                if (project != null && project.Exists)
                {
                    project.Delete();
                }
            }
        }

        private void GenerateDynamicAsserts(IList<AssertMethod> asserts)
        {
            SourceCodeHtml += "Taking Backup of Test File".PrintWithDateTime().PrintWithPreTagImportant();
            var originalFile = new FileInfo(_source.TestClaz.FilePath).CopyTo($"{_source.TestClaz.FilePath}.original", true);
            SourceCodeHtml += $"Original Test file is located at: {originalFile}".PrintWithPreTagWithMargin();

            SourceCodeHtml += "Analyzing Asserts".PrintWithDateTime().PrintWithPreTagImportant();
            var assertStrings = new List<AssertString>();

            var methods = _source.TestClaz.Claz.Syntax.Methods().ToList();
            var methodsWithParameters = methods.Where(x => x.ParameterList.Parameters.Any(p => p.Identifier.ValueText.StartsWith("expected")));
            foreach (var method in methodsWithParameters)
            {
                var methodName = method.MethodName();
                var methodAsserts = asserts.Where(x => x.Method.Equals(methodName, StringComparison.InvariantCultureIgnoreCase) ||
                                                       x.Method.StartsWith($"{methodName}(")).ToList();

                if (methodAsserts.Any())
                {
                    var parameters = method
                        .ParameterList
                        .Parameters
                        .Where(x => x.Identifier.ValueText.StartsWith("expected"))
                        .Select(x => x.Identifier.ValueText).ToList();

                    foreach (var parameter in parameters)
                    {
                        foreach (var methodAssert in methodAsserts)
                        {
                            try
                            {
                                var parameterAsserts = methodAssert
                                    .Asserts
                                    .Where(x => x.Value.Replace(ShouldlyMethod, string.Empty)
                                        .Split('.')[0]
                                        .Split('[')[0]
                                        .Equals(
                                            $"_{parameter.Substring(8, parameter.Length - 8)}",
                                            StringComparison.InvariantCultureIgnoreCase)).ToList();

                                for (var index = 0; index < parameterAsserts.Count; index++)
                                {
                                    var assert = parameterAsserts[index];
                                    if (index == 0)
                                    {
                                        methodAssert.Asserts[methodAssert.Asserts.IndexOf(assert)].Value = $"{assert.Value.Split('.')[0].Split('[')[0]}.ShouldBe({parameter}),";
                                        continue;
                                    }

                                    assert.Skip = true;
                                }
                            }
                            catch (Exception e)
                            {
                                Trace.TraceError($"Unable to create Expected Parameter Assert {parameter} in method {method.MethodName()} [{e}]");
                            }
                        }
                    }
                }
            }

            var updatedMethods = new List<MethodParameterList>();
            var updatedTestCases = new List<TestCase>();
            foreach (var method in methods)
            {
                var methodName = method.MethodName();
                var methodAsserts = asserts.Where(x => x.Method.Equals(methodName, StringComparison.InvariantCultureIgnoreCase) ||
                                                       x.Method.StartsWith($"{methodName}(")).ToList();
                Func<Assert, bool> predicate = x => x.Value.Contains(".ShouldBeGreaterThanOrEqualTo") || x.Value.Contains(".ShouldHaveSingleItem");
                var mAssertsWithCollections = methodAsserts.Where(x => x.Asserts.Any(predicate)).ToList();
                var assertWithElements = mAssertsWithCollections.SelectMany(x => x.Asserts).ToList();
                foreach (var assert in assertWithElements.Where(predicate))
                {
                    var left = assert.Value.Substring(0, assert.Value.LastIndexOf('.'));
                    var leftSingle = left.Replace(".Count", string.Empty).Replace(".Length", string.Empty);
                    foreach (var assertMethod in methodAsserts.Where(x => x.Asserts.All(y => !y.Value.StartsWith(left) && !y.Value.StartsWith($"{leftSingle}.ShouldHaveSingleItem"))))
                    {
                        var assertValue = $"{leftSingle}.ShouldBeEmpty(),";
                        assertMethod.Asserts.Add(new Assert(assertValue, string.Empty));
                    }

                    if (mAssertsWithCollections.Count == methodAsserts.Count &&
                        !assert.Value.StartsWith($"{leftSingle}.ShouldHaveSingleItem"))
                    {
                        Func<Assert, bool> countAssertPredicate = y => y.Value.StartsWith(left) || y.Value.StartsWith($"{leftSingle}.ShouldHaveSingleItem");
                        var assertMethods = methodAsserts
                            .Where(x => x.Asserts.Any(countAssertPredicate)).ToList();
                        var assertMethodsWithCollections = assertMethods.SelectMany(x => x.Asserts).Where(countAssertPredicate).ToList();
                        if (assertMethodsWithCollections.Any())
                        {
                            var assertValue = string.Empty;
                            if (assertMethodsWithCollections.Any(x => x.Value.StartsWith($"{leftSingle}.ShouldHaveSingleItem")))
                            {
                                assertValue = $"{left}.ShouldBeGreaterThanOrEqualTo(1),";
                            }
                            else
                            {
                                var min = assertMethodsWithCollections.Select(x =>
                                {
                                    var startIndex = x.Value.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase) + 1;
                                    var endIndex = x.Value.LastIndexOf(")", StringComparison.InvariantCultureIgnoreCase);
                                    return Convert.ToInt32(x.Value.Substring(startIndex, endIndex - startIndex));
                                }).Min();

                                assertValue = $"{left}.ShouldBeGreaterThanOrEqualTo({min}),";
                            }

                            if (!string.IsNullOrWhiteSpace(assertValue))
                            {
                                foreach (var assertMethod in assertMethods)
                                {
                                    assertMethod.Asserts.Add(new Assert(assertValue, string.Empty));
                                }
                            }
                        }
                    }
                }

                var commonAsserts = new List<string>();
                var outputBuilder = new StringBuilder();
                if (methodAsserts.Any())
                {
                    var preAssertVariables = new List<string>();
                    if (ChkIgnoreCollectionOrder.IsChecked)
                    {
                        var collectionAsserts = methodAsserts.SelectMany(x => x.Asserts)
                            .Where(x => x.Value.IndexOf("].ShouldBe(", StringComparison.InvariantCulture) != -1).ToList();
                        foreach (Assert assert in collectionAsserts)
                        {
                            var assertValue = assert.Value;
                            var shoudlyIndex = assertValue.IndexOf("ShouldBe(", StringComparison.InvariantCulture) + 9;
                            var expected = assertValue.Substring(shoudlyIndex, assertValue.LastIndexOf(")", StringComparison.InvariantCulture) - shoudlyIndex);
                            var actualList = assertValue.Substring(0, shoudlyIndex).Replace("() => ", string.Empty);
                            actualList = actualList.Substring(0, actualList.LastIndexOf("[", StringComparison.InvariantCulture));

                            var actualListVariable = $"{actualList.Split('.').LastOrDefault()?.TrimStart('_')}Array".ToCamelCase();
                            var unknownTypeList = actualList.Trim(')');
                            var unknownTypeIndex = unknownTypeList.LastIndexOf(")", StringComparison.InvariantCulture) + 1;
                            if (unknownTypeIndex != 0)
                            {
                                actualListVariable = $"{unknownTypeList.Substring(unknownTypeIndex, unknownTypeList.Length - unknownTypeIndex).TrimStart('_')}Array"
                                    .TrimStart('.')
                                    .ToCamelCase();

                                assert.Value = expected.StartsWith("Tuple.Create") &&  expected.ContainsNotAnyNullLiterals()
                                    ? $"() => ShouldContain({actualListVariable}, {expected.Replace("Tuple.Create(", string.Empty)},"
                                    : $"() => {actualListVariable}.ShouldContain({expected}),";
                                
                                preAssertVariables.Add($"            var {actualListVariable} = {actualList.Replace("((", "(").TrimEnd(')')};\r\n");
                            }
                            else
                            {
                                assert.Value = expected.StartsWith("Tuple.Create") && expected.ContainsNotAnyNullLiterals()
                                    ? $"() => ShouldContain({actualList}, {expected.Replace("Tuple.Create(", string.Empty)},"
                                    : $"() => {actualList}.ShouldContain({expected}),";
                            }
                        }
                    }
                    else
                    {
                        var tupleAsserts = methodAsserts.SelectMany(x => x.Asserts)
                            .Where(x => x.Value.IndexOf("].ShouldBe(", StringComparison.InvariantCulture) != -1 && 
                                        x.Value.Contains("Tuple.Create(") &&
                                        x.Value.ContainsNotAnyNullLiterals()).ToList();

                        foreach (Assert assert in tupleAsserts)
                        {
                            var assertValue = assert.Value;
                            var shoudlyIndex = assertValue.IndexOf("ShouldBe(", StringComparison.InvariantCulture) + 9;
                            var expected = assertValue.Substring(shoudlyIndex, assertValue.LastIndexOf(")", StringComparison.InvariantCulture) - shoudlyIndex);
                            var actualList = assertValue.Substring(0, shoudlyIndex).Replace("() => ", string.Empty);
                            var arrayStartIndex = actualList.LastIndexOf("[", StringComparison.InvariantCulture);
                            var arrayEndIndex = actualList.LastIndexOf("]", StringComparison.InvariantCulture);
                            var arrayIndex = actualList.Substring(arrayStartIndex + 1, arrayEndIndex - arrayStartIndex - 1);
                            actualList = actualList.Substring(0, arrayStartIndex);

                            var actualListVariable = $"{actualList.Split('.').LastOrDefault()?.TrimStart('_')}Array".ToCamelCase();
                            var unknownTypeList = actualList.Trim(')');
                            var unknownTypeIndex = unknownTypeList.LastIndexOf(")", StringComparison.InvariantCulture) + 1;
                            if (unknownTypeIndex != 0)
                            {
                                actualListVariable = $"{unknownTypeList.Substring(unknownTypeIndex, unknownTypeList.Length - unknownTypeIndex).TrimStart('_')}Array"
                                    .TrimStart('.')
                                    .ToCamelCase();

                                assert.Value = $"() => ShouldEqual({actualListVariable}[{arrayIndex}], {expected.Replace("Tuple.Create(", string.Empty)},";

                                preAssertVariables.Add($"            var {actualListVariable} = {actualList.Replace("((", "(").TrimEnd(')')};\r\n");
                            }
                            else
                            {
                                assert.Value = $"() => ShouldEqual({actualList}[{arrayIndex}], {expected.Replace("Tuple.Create(", string.Empty)},";
                            }
                        }
                    }

                    preAssertVariables = preAssertVariables.Distinct().ToList();

                    var assertString = new AssertString
                    {
                        Name = methodName,
                        ReplaceLocation = method.GetLocation().GetLineSpan().EndLinePosition.Line
                    };

                    var preConditionAsserts = PreConditionAsserts();
                    var nonPreConditionAsserts = NonPreConditionAsserts();
                    if (methodAsserts.Count == 1)
                    {
                        commonAsserts.AddRange(methodAsserts.First().Asserts.Where(x => !x.Skip).Select(x => x.Value.Replace(".ShouldBeGreaterThanOrEqualTo(", ".ShouldBe(")));
                        AddCommonAsserts(commonAsserts, outputBuilder, preAssertVariables);
                    }
                    else
                    {
                        foreach (var methodAssert in methodAsserts)
                        {
                            methodAssert.Asserts = methodAssert.Asserts.Where(x => !x.Skip).ToList();
                        }

                        var unCommonOutputBuilder = new StringBuilder();
                        var parameterizedTests = new List<Assert>();
                        var testCases = method.TestCases();
                        for (var index = 0; index < methodAsserts.Count; index++)
                        {
                            var methodAssert = methodAsserts[index];
                            var uncommonAsserts = new List<string>();
                            foreach (var assert in methodAssert.Asserts)
                            {
                                var assertMethods = methodAsserts
                                    .Where(x => x.Method != methodAssert.Method &&
                                                x.Method.Split('(')[0].Equals(methodAssert.Method.Split('(')[0])).ToList();
                                if (assertMethods.All(x => x.Asserts.Any(y => y.Value == assert.Value)))
                                {
                                    commonAsserts.Add(assert.Value);
                                    continue;
                                }

                                if (ChkParameterizedAsserts.IsChecked &&
                                    assert.Type.IsSimple() &&
                                    testCases.Any())
                                {
                                    var assertValue = assert.Value.StandardBooleanAssert();
                                    var shouldBeIndex = assertValue.IndexOf("ShouldBe(", StringComparison.InvariantCulture);
                                    var shouldBeCollectionIndex = assertValue.IndexOf("].ShouldBe(", StringComparison.InvariantCulture);
                                    if (shouldBeIndex != -1 && shouldBeCollectionIndex == -1)
                                    {
                                        var assertDeclaration = assertValue.Substring(0, shouldBeIndex);
                                        var assertList = assertMethods.SelectMany(x => x.Asserts)
                                            .Where(x => x.Value.StandardBooleanAssert().StartsWith($"{assertDeclaration}ShouldBe("))
                                            .ToList();
                                        var parameterizedTest = assertList.Count == assertMethods.Count;
                                        if (parameterizedTest)
                                        {
                                            assert.Value = assertValue;
                                            parameterizedTests.Add(assert);
                                            continue;
                                        }
                                    }
                                }

                                uncommonAsserts.Add(assert.Value);
                            }

                            var builder = new StringBuilder();
                            var preUncommonAsserts = uncommonAsserts.Where(preConditionAsserts).ToList();
                            var nonPreUncommonAsserts = uncommonAsserts.Where(nonPreConditionAsserts).ToList();

                            SetUnCommonAsserts(preUncommonAsserts, builder);
                            if (preAssertVariables.Any() &&
                                nonPreUncommonAsserts.Any() &&
                                preUncommonAsserts.Any(x => x.Contains(ShouldThrow) ||
                                                            x.Contains(ShouldNotThrow)))
                            {
                                builder.AppendLine();
                                builder.AppendLine();
                                foreach (var assertVariable in preAssertVariables)
                                {
                                    builder.Append($"    {assertVariable.Replace("var ", string.Empty)}");
                                }
                            }

                            if (preUncommonAsserts.Any() &&
                                nonPreUncommonAsserts.Any())
                            {
                                builder.AppendLine();
                            }

                            SetUnCommonAsserts(nonPreUncommonAsserts, builder);

                            if (uncommonAsserts.Any())
                            {
                                var methodArguments = methodAssert.Method.WithLineBreaks().TrimEnd(')');
                                unCommonOutputBuilder.AppendFormat(
                                    index < methodAsserts.Count - 1
                                        ? ParameterizedTemplate
                                        : ParameterizedTemplateWithoutLine,
                                    methodArguments.Substring(methodArguments.IndexOf("(", StringComparison.InvariantCultureIgnoreCase) + 1),
                                    builder);
                            }
                        }

                        if (parameterizedTests.Any())
                        {
                            foreach (var testCase in testCases)
                            {
                                testCase.Body = testCase.Body
                                    .Trim(testCase.ClosingCharacter)
                                    .TrimEnd()
                                    .Trim(')');
                            }

                            var parameterizedAsserts = parameterizedTests
                                .GroupBy(x => x.Value.Substring(0, x.Value.IndexOf("ShouldBe", StringComparison.InvariantCultureIgnoreCase)))
                                .Select(x => new
                                {
                                    Declarator = x.Key,
                                    x.First().Type,
                                    Values = x.Select(y =>
                                    {
                                        var shoudlyIndex = y.Value.IndexOf("ShouldBe", StringComparison.InvariantCultureIgnoreCase) + 9;
                                        var lastIndexOf = y.Value.LastIndexOf(")", StringComparison.InvariantCulture);
                                        return y.Value.Substring(shoudlyIndex, lastIndexOf - shoudlyIndex);
                                    }).ToList()
                                });

                            var methodParameterList = method.ParameterList();
                            methodParameterList.UpdatedList = methodParameterList.OriginalList.TrimEnd().Trim(')');
                            foreach (var assert in parameterizedAsserts)
                            {
                                var paramterName = assert.Declarator;
                                paramterName = paramterName.Replace("() => ", string.Empty)
                                    .Replace(".", string.Empty)
                                    .Replace("_", string.Empty);
                                var methodParameter = $"expected{paramterName.ToPascalCase()}"
                                    .ToCamelCase();
                                var unknownTypeIndex = paramterName.IndexOf(")", StringComparison.InvariantCulture) + 1;
                                if (unknownTypeIndex != 0)
                                {
                                    methodParameter = $"expected{paramterName.Substring(unknownTypeIndex, paramterName.Length - unknownTypeIndex).ToPascalCase()}"
                                        .Replace(")", string.Empty)
                                        .ToCamelCase();
                                }

                                var assertType = assert.Type != "enum"
                                    ? assert.Type
                                    : "object";
                                methodParameterList.UpdatedList += $", {assertType} {methodParameter}";
                                commonAsserts.Add($"{assert.Declarator}ShouldBe({methodParameter}),");

                                for (var index = 0; index < assert.Values.Count; index++)
                                {
                                    testCases[index].Body += $", {assert.Values[index]}";
                                }
                            }

                            methodParameterList.UpdatedList += ')';

                            foreach (var testCase in testCases)
                            {
                                testCase.Body += $"){testCase.ClosingCharacter}";
                            }

                            updatedMethods.Add(methodParameterList);
                            updatedTestCases.AddRange(testCases);
                        }

                        AddCommonAsserts(commonAsserts, outputBuilder, preAssertVariables);
                        if (!string.IsNullOrWhiteSpace(unCommonOutputBuilder.ToString()) && commonAsserts.Any())
                        {
                            outputBuilder.AppendLine();
                        }

                        outputBuilder.Append(unCommonOutputBuilder);
                    }

                    assertString.Content = outputBuilder.ToString();
                    assertStrings.Add(assertString);
                }
            }

            var classEnd = _source.TestClaz.Claz.Syntax.EndLineNumber();

            SourceCodeHtml += "Adding Asserts to Test File".PrintWithDateTime().PrintWithPreTagImportant();
            var fileLines = new List<string>();
            using (var reader = new StreamReader(_source.TestClaz.FilePath))
            {
                string line;
                var lineNumber = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    var testCase = updatedTestCases.FirstOrDefault(x => x.Location == lineNumber);
                    if (testCase != null)
                    {
                        line = testCase.Body;
                    }

                    var method = updatedMethods.FirstOrDefault(x => x.Location == lineNumber);
                    if (method != null)
                    {
                        line = line.Replace(method.OriginalList, method.UpdatedList);
                    }

                    fileLines.Add(line);
                    var assertString = assertStrings.FirstOrDefault(x => x.ReplaceLocation == lineNumber);
                    if (assertString != null)
                    {
                        fileLines.Add(assertString.Content);
                    }

                    if (lineNumber == classEnd)
                    {
                        if (!_source.TestClaz.Claz.Syntax.ContainMethod(nameof(CheckParameter)))
                        {
                            fileLines.Add(CheckParameter);
                        }

                        if (!_source.TestClaz.Claz.Syntax.ContainMethod(nameof(ShouldContain)) &&
                            ChkIgnoreCollectionOrder.IsChecked)
                        {
                            fileLines.Add(ShouldContain);
                        }

                        if (!_source.TestClaz.Claz.Syntax.ContainMethod(nameof(ShouldEqual)) &&
                            !ChkIgnoreCollectionOrder.IsChecked)
                        {
                            fileLines.Add(ShouldEqual);
                        }
                    }

                    lineNumber++;
                }
            }

            _source.TestClaz.FilePath.WriteLines(fileLines);
            var className = _source.TestClaz.Claz.Syntax.ClassName();
            var classDeclarationLoader = new ClassDeclarationLoader();
            _source.TestClaz.Claz = classDeclarationLoader.Load(_source.TestClaz.FilePath, className);
            var partialClass = _source.TestClaz.PartialClasses.FirstOrDefault(x => x.Claz.Syntax.ClassName() == className);
            if (partialClass != null)
            {
                partialClass.Claz = _source.TestClaz.Claz;
            }

            SourceCodeHtml += $"Updated Test file is located at: {_source.TestClaz.FilePath}".PrintWithPreTagWithMargin();
            SourceCodeHtml += "Completed!".PrintWithDateTime().PrintWithPreTagImportant();
        }

        private static void AddCommonAsserts(IList<string> commonAsserts, StringBuilder outputBuilder, IReadOnlyCollection<string> preAssertVariables)
        {
            var preConditionAsserts = PreConditionAsserts();
            var nonPreConditionAsserts = NonPreConditionAsserts();

            commonAsserts = commonAsserts.Distinct().ToList();
            var preAsserts = commonAsserts.Where(preConditionAsserts).ToList();
            var nonPreAsserts = commonAsserts.Where(nonPreConditionAsserts).ToList();
            SetCommonAsserts(preAsserts, outputBuilder);
            if (preAsserts.Any() &&
                nonPreAsserts.Any())
            {
                outputBuilder.AppendLine();
            }

            if (preAssertVariables.Any() && preAsserts.Any())
            {
                outputBuilder.AppendLine();
            }

            foreach (var assertVariable in preAssertVariables)
            {
                outputBuilder.Append(assertVariable);
            }

            SetCommonAsserts(nonPreAsserts, outputBuilder);
        }

        private static Func<string, bool> NonPreConditionAsserts()
        {
            return x => !x.Contains(".Count.") &&
                        !x.Contains(".Length.") &&
                        !x.Contains(ShouldThrow) &&
                        !x.Contains(".ShouldNotThrow()") &&
                        !x.Contains(".ShouldHaveSingleItem");
        }

        private static Func<string, bool> PreConditionAsserts()
        {
            return x => x.Contains(".Count.") ||
                        x.Contains(".Length.") ||
                        x.Contains(ShouldThrow) ||
                        x.Contains(".ShouldNotThrow()") ||
                        x.Contains(".ShouldHaveSingleItem");
        }

        private static void SetCommonAsserts(IList<string> commonAsserts, StringBuilder builder)
        {
            if (commonAsserts.Count == 1)
            {
                var assert = commonAsserts.First().Replace(ShouldlyMethod, string.Empty).Trim(',');
                builder.Append($"            {assert};");
                return;
            }

            for (var index = 0; index < commonAsserts.Count; index++)
            {
                var assert = commonAsserts[index];
                if (index == 0)
                {
                    builder.AppendLine("            this.ShouldSatisfyAllConditions(");
                }

                if (index != commonAsserts.Count - 1)
                {
                    builder.AppendLine($"                {assert}");
                }
                else
                {
                    builder.Append($"                {assert.Trim(',')});");
                }
            }
        }

        private static void SetUnCommonAsserts(IList<string> uncommonAsserts, StringBuilder builder)
        {
            uncommonAsserts = uncommonAsserts.Distinct().ToList();
            if (uncommonAsserts.Count == 1)
            {
                var assert = uncommonAsserts.First().Replace(ShouldlyMethod, string.Empty)
                    .Replace(".ShouldBeGreaterThanOrEqualTo(", ".ShouldBe(").Trim(',');
                builder.Append($"                {assert};");
                return;
            }

            for (var index = 0; index < uncommonAsserts.Count; index++)
            {
                var assert = uncommonAsserts[index].Replace(".ShouldBeGreaterThanOrEqualTo(", ".ShouldBe(");
                if (index == 0)
                {
                    builder.AppendLine("                this.ShouldSatisfyAllConditions(");
                }

                if (index != uncommonAsserts.Count - 1)
                {
                    builder.AppendLine($"                    {assert}");
                }
                else
                {
                    builder.Append($"                    {assert.Trim(',')});");
                }
            }
        }

        public void BtnGenerateAssertsClick(object selectedItems)
        {
            var selectedMethods = (ObservableCollection<object>)selectedItems;
            if (!selectedMethods.Any())
            {
                MessageBoxService.Show(SelectSourceMethodErrorMessage);
                return;
            }

            var methodDetails = selectedMethods.Cast<MethodDetail>().ToList();
            var asserts = new List<AssertsAnalyzer.AssertMapper>();

            methodDetails.ForEach(x => asserts.AddRange(x.Method.GetAsserts()));

            try
            {
                var assertsFile = asserts.GenerateFile(MuTestSettings.TestsResultDirectory, _source.Claz.Syntax.ClassName(), _source.Claz.Syntax.NameSpace());
                Process.Start(MuTestSettings.DefaultEditor, string.Format(MuTestSettings.DefaultEditorOptions, assertsFile));
            }
            catch (Exception e)
            {
                Trace.TraceError("Unable to Generate Asserts {0}", e);
                MessageBoxService.Show("Unable to Generate Asserts.");
            }
        }

        public void BtnCompareCodeClick()
        {
            try
            {
                Process.Start(MuTestSettings.DefaultEditor, string.Format(MuTestSettings.DefaultEditorOptions, _source.FilePath));
                Process.Start(MuTestSettings.DefaultEditor, string.Format(MuTestSettings.DefaultEditorOptions, _source.TestClaz.FilePath));
            }
            catch (Exception exp)
            {
                Trace.TraceError("Unknown Exception Occurred On Comparing Test Code with Source Code {0}", exp);
                MessageBoxService.Show(exp.Message);
            }
        }

        public void BtnExportToHtmlClick()
        {
            if (string.IsNullOrWhiteSpace(SourceCodeHtml) || SourceCodeHtml.Equals(HtmlTemplate))
            {
                MessageBoxService.Show(ExportToHtmlErrorMessage);
                return;
            }

            try
            {
                using (var dialog = new CommonSaveFileDialog
                {
                    DefaultFileName = _currentFileName ?? ReportFilename,
                    DefaultExtension = HtmlFile,
                    DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolderOption.None)
                })
                {
                    var result = dialog.ShowDialog();

                    if (result == CommonFileDialogResult.Ok)
                    {
                        File.WriteAllText(dialog.FileName, SourceCodeHtml);
                    }
                }
            }
            catch (Exception exp)
            {
                Trace.TraceError("Unknown Exception Occurred On Exporting data to Html {0}", exp);
                MessageBoxService.Show(ErrorMessage);
            }
        }
    }
}