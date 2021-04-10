using System.Collections.Generic;
using System.Drawing;

namespace MuTest.Core.Common
{
    public class Constants
    {
        public static readonly int FontSizeTwo = 2;
        public static readonly string CSharpProjectFile = ".csproj";
        public static readonly string HtmlFile = "html";
        public static readonly string ReportFilename = "report";
        public static readonly string TestDllNotSelectedErrorMessage = "Select Test Dll in C# Mutation Tool";
        public static readonly string SourceDllNotSelectedErrorMessage = "Select Source Dll in C# Mutation Tool";
        public static readonly string ErrorMessage = "Unexpected Error Occurred! Please contact Administrator";
        public static readonly string FailedOutCome = "Failed";
        public static readonly string LibraryFile = ".dll";
        public static readonly string ExecutableFile = ".exe";
        public static readonly string LibraryFilesFilter = "Library Files";
        public static readonly string MSBuildOutput = "MSBuild Output";
        public static readonly string NoAnyClassFound = "No any Class found!";
        public static readonly string PassedOutCome = "Passed";
        public static readonly string PreEnd = "</pre>";
        public static readonly string PreStart = "<pre style='margin: 0;'>";
        public static readonly string PreStartWithMargin = "<pre style='margin: 0,30;'>";
        public static readonly string ProjectFilesFilter = "Project Files";
        public static readonly string ProjectNotSelectedErrorMessage = "Select Test Project in C# Mutation Tool";
        public static readonly string SourceProjectNotSelectedErrorMessage = "Select Source Project in C# Mutation Tool";
        public static readonly string SelectSourceMethodErrorMessage = "Select Source Method to Analyze";
        public static readonly string SelectTestMethodsErrorMessage = "Select Test Methods to Execute";
        public static readonly string ExportToHtmlErrorMessage = "There are No Output to Export Html";
        public static readonly string TestsExecutionOutput = "Tests Execution Output";
        public static readonly string TestsExecutionResult = "Tests Results And Coverage";
        public static readonly string VerbosityOption = " -verbosity:";
        public static readonly string DefaultColor = "#f1f1f1";
        public static readonly string SourceCodeDllSetting = "SourceCodeDllSetting";
        public static readonly string TestCodeDllSetting = "TestCodeDllSetting";
        public static readonly string TestCodeProjectSetting = "TestProjectSetting";
        public static readonly string SourceCodeProjectSetting = "SourceCodeProjectSetting";
        public static readonly string SelectAtLeastOneMutator = "Select at Least One Mutator";
        public static readonly string SourceDllFileNameNotValid = "Source Dll File Name is not Valid";
        public static readonly string ProgressBarFormat = "{0:p0}";
        public static readonly string MutantsNotExist = "No Any Mutant Exists!";
        public static readonly string NoAnyTestsExist = "No Any Tests Exists!";
        public static readonly string MutationIsCompletedNotification = "Mutation is Completed";
        public static readonly string GenericMethodStart = "<";
        public static readonly string GenericMethodEnd = ">";
        public static readonly string BuildIsSucceededNotification = "Build is Succeeded";
        public static readonly string BuildIsFailingNotification = "Build is Failing. Please Fix the Build";
        public static readonly string TestExecutionIsCompletedNotification = "Test Execution is Completed";
        public static readonly string DuplicateCodeWindow = "Duplicate Code";
        public static readonly string CodeInspectionReport = "Code Inspection Report";
        public static readonly string HtmlTemplate = @"<html><body style=""background-color: #2d2d30;"">";
        public static readonly string SourceClassPlaceholder = "#source_class#";
        public static readonly string ParameterizedTemplate = @"
            if (CheckParameter(""{0}""))
            {{
{1}
            }}
";
        public static readonly string ParameterizedTemplateWithoutLine = @"
            if (CheckParameter(""{0}""))
            {{
{1}
            }}";

        public static readonly string ShouldEqual = @"
        private static void ShouldEqual(IStructuralComparable tuple, params object[] param)
        {
            var method = typeof(Tuple).GetMethods().FirstOrDefault(x=> x.GetParameters().Length == param.Length);
            var genericMethod = method?.MakeGenericMethod(param.Select(x=> x.GetType()).ToArray());
            tuple.ShouldBe(genericMethod?.Invoke(null, param));
        }";

        public static readonly string ShouldContain = @"
        private static void ShouldContain(IEnumerable<IStructuralComparable> collection, params object[] param)
        {
            var method = typeof(Tuple).GetMethods().FirstOrDefault(x=> x.GetParameters().Length == param.Length);
            var genericMethod = method?.MakeGenericMethod(param.Select(x=> x?.GetType() ?? typeof(string)).ToArray());
            collection.ToList().ShouldContain(genericMethod?.Invoke(null, param));
        }";

        public static readonly string CheckParameter = @"
        private static bool CheckParameter(string argument)
        {
            var testName = NUnit.Framework.TestContext.CurrentContext.Test.Name;
            var arguments = testName.Substring(testName.IndexOf(""("", StringComparison.InvariantCultureIgnoreCase) + 1);
            return arguments.StartsWith(argument);
        }";

        public static readonly IList<string> UnitTestsSetupAttributes = new List<string>
        {
            "SetUp",
            "TearDown",
            "OneTimeSetup",
            "OneTimeSetUp",
            "OneTimeTearDown",
            "TestInitialize",
            "TestCleanup",
            "ClassInitialize",
            "ClassCleanup",
            "Ignore",
            "AssemblyInitialize",
            "AssemblyCleanup"
        };

        public static class Colors
        {
            public static readonly string Red = nameof(Color.LightPink);
            public static readonly string Blue = nameof(Color.Aqua);
            public static readonly string Green = nameof(Color.GreenYellow);
            public static readonly string Gold = nameof(Color.Gold);
            public static readonly string BlueViolet = nameof(Color.Violet);
            public static readonly string Brown = nameof(Color.Brown);
            public static readonly string Orange = nameof(Color.Khaki);
        }

        public enum TestExecutionStatus
        {
            Failed = -1,
            Success = 0,
            Timeout = 1
        }

        public enum BuildExecutionStatus
        {
            Success = 0,
            Failed = 1
        }

        public enum ExecutionStatus
        {
            Success = 0,
            Failed = 1
        }

        public enum SeverityType
        {
            Red = 0,
            Severe = 1,
            Yellow = 2
        }

        public static readonly IReadOnlyDictionary<int, TestExecutionStatus> TestStatusList = new Dictionary<int, TestExecutionStatus>
        {
            [-1] = TestExecutionStatus.Failed,
            [0] = TestExecutionStatus.Success,
            [1] = TestExecutionStatus.Timeout,
            [2] = TestExecutionStatus.Failed,
            [3] = TestExecutionStatus.Failed
        };
    }
}