using System.Configuration;

namespace MuTest.Core.Common.Settings
{
    public class MuTestSettings : ConfigurationElement
    {
        [ConfigurationProperty(nameof(VSTestConsolePath), IsRequired = true)]
        public string VSTestConsolePath
        {
            get => (string)this[nameof(VSTestConsolePath)];
            set => value = (string)this[nameof(VSTestConsolePath)];
        }

        [ConfigurationProperty(nameof(RunSettingsPath), IsRequired = true)]
        public string RunSettingsPath
        {
            get => (string)this[nameof(RunSettingsPath)];
            set => value = (string)this[nameof(RunSettingsPath)];
        }

        [ConfigurationProperty(nameof(ParallelTestExecution), DefaultValue = " /Parallel", IsRequired = false)]
        public string ParallelTestExecution
        {
            get => (string)this[nameof(ParallelTestExecution)];
            set => value = (string)this[nameof(ParallelTestExecution)];
        }

        [ConfigurationProperty(nameof(Blame), DefaultValue = " ", IsRequired = false)]
        public string Blame
        {
            get => (string)this[nameof(Blame)];
            set => value = (string)this[nameof(Blame)];
        }

        [ConfigurationProperty(nameof(MSBuildPath), IsRequired = true)]
        public string MSBuildPath
        {
            get => (string)this[nameof(MSBuildPath)];
            set => value = (string)this[nameof(MSBuildPath)];
        }

        [ConfigurationProperty(nameof(TestsResultDirectory), IsRequired = true)]
        public string TestsResultDirectory
        {
            get => (string)this[nameof(TestsResultDirectory)];
            set => value = (string)this[nameof(TestsResultDirectory)];
        }

        [ConfigurationProperty(nameof(TestTimeout), DefaultValue = 35000, IsRequired = false)]
        public int TestTimeout
        {
            get => (int)this[nameof(TestTimeout)];
            set => value = (int)this[nameof(TestTimeout)];
        }

        [ConfigurationProperty(nameof(EnableTestTimeout), DefaultValue = true, IsRequired = false)]
        public bool EnableTestTimeout
        {
            get => (bool)this[nameof(EnableTestTimeout)];
            set => value = (bool)this[nameof(EnableTestTimeout)];
        }

        [ConfigurationProperty(nameof(MSBuildLogDirectory), IsRequired = true)]
        public string MSBuildLogDirectory
        {
            get => (string)this[nameof(MSBuildLogDirectory)];
            set => value = (string)this[nameof(MSBuildLogDirectory)];
        }

        [ConfigurationProperty(nameof(Options), IsRequired = true)]
        public string Options
        {
            get => (string)this[nameof(Options)];
            set => value = (string)this[nameof(Options)];
        }

        [ConfigurationProperty(nameof(MSBuildVerbosity), IsRequired = true)]
        public string MSBuildVerbosity
        {
            get => (string)this[nameof(MSBuildVerbosity)];
            set => value = (string)this[nameof(MSBuildVerbosity)];
        }

        [ConfigurationProperty(nameof(PostBuildEvents), DefaultValue = " /p:PostBuildEvent=", IsRequired = false)]
        public string PostBuildEvents
        {
            get => (string)this[nameof(PostBuildEvents)];
            set => value = (string)this[nameof(PostBuildEvents)];
        }

        [ConfigurationProperty(nameof(PreBuildEvents), DefaultValue = " /p:PreBuildEvent=", IsRequired = false)]
        public string PreBuildEvents
        {
            get => (string)this[nameof(PreBuildEvents)];
            set => value = (string)this[nameof(PreBuildEvents)];
        }

        [ConfigurationProperty(nameof(QuietBuild), DefaultValue = "quiet /nologo -p:DebugSymbols=false -p:DebugType=None", IsRequired = false)]
        public string QuietBuild
        {
            get => (string)this[nameof(QuietBuild)];
            set => value = (string)this[nameof(QuietBuild)];
        }

        [ConfigurationProperty(nameof(QuietBuildWithSymbols), DefaultValue = "quiet /nologo", IsRequired = false)]
        public string QuietBuildWithSymbols
        {
            get => (string)this[nameof(QuietBuildWithSymbols)];
            set => value = (string)this[nameof(QuietBuildWithSymbols)];
        }

        [ConfigurationProperty(nameof(MSBuildCustomOption), DefaultValue = "", IsRequired = false)]
        public string MSBuildCustomOption
        {
            get => (string)this[nameof(MSBuildCustomOption)];
            set => value = (string)this[nameof(MSBuildCustomOption)];
        }

        [ConfigurationProperty(nameof(SettingsOption), DefaultValue = " /settings:", IsRequired = false)]
        public string SettingsOption
        {
            get => (string)this[nameof(SettingsOption)];
            set => value = (string)this[nameof(SettingsOption)];
        }

        [ConfigurationProperty(nameof(LoggerOption), DefaultValue = " /Logger:trx;LogFileName={0}", IsRequired = false)]
        public string LoggerOption
        {
            get => (string)this[nameof(LoggerOption)];
            set => value = (string)this[nameof(LoggerOption)];
        }

        [ConfigurationProperty(nameof(TestsOption), DefaultValue = " /Tests:", IsRequired = false)]
        public string TestsOption
        {
            get => (string)this[nameof(TestsOption)];
            set => value = (string)this[nameof(TestsOption)];
        }

        [ConfigurationProperty(nameof(MSBuildConfigurationOption), DefaultValue = " /p:Configuration=Release", IsRequired = false)]
        public string MSBuildConfigurationOption
        {
            get => (string)this[nameof(MSBuildConfigurationOption)];
            set => value = (string)this[nameof(MSBuildConfigurationOption)];
        }

        [ConfigurationProperty(nameof(MSBuildDependenciesOption), DefaultValue = " /p:BuildProjectReferences=false", IsRequired = false)]
        public string MSBuildDependenciesOption
        {
            get => (string)this[nameof(MSBuildDependenciesOption)];
            set => value = (string)this[nameof(MSBuildDependenciesOption)];
        }

        [ConfigurationProperty(nameof(MSBuildLogger), DefaultValue = " -fl -flp:logfile=", IsRequired = false)]
        public string MSBuildLogger
        {
            get => (string)this[nameof(MSBuildLogger)];
            set => value = (string)this[nameof(MSBuildLogger)];
        }

        [ConfigurationProperty(nameof(DefaultEditor), DefaultValue = "notepad++.exe", IsRequired = false)]
        public string DefaultEditor
        {
            get => (string)this[nameof(DefaultEditor)];
            set => value = (string)this[nameof(DefaultEditor)];
        }

        [ConfigurationProperty(nameof(DefaultEditorOptions), DefaultValue = "-multiInst \"{0}\"", IsRequired = false)]
        public string DefaultEditorOptions
        {
            get => (string)this[nameof(DefaultEditorOptions)];
            set => value = (string)this[nameof(DefaultEditorOptions)];
        }

        [ConfigurationProperty(nameof(DuplicateFinderToolPath), DefaultValue = @"CommandLineTools\Resharper\dupfinder.exe", IsRequired = false)]
        public string DuplicateFinderToolPath
        {
            get => (string)this[nameof(DuplicateFinderToolPath)];
            set => value = (string)this[nameof(DuplicateFinderToolPath)];
        }

        [ConfigurationProperty(nameof(UseClassFilterTestsThreshold), DefaultValue = @"70", IsRequired = false)]
        public string UseClassFilterTestsThreshold
        {
            get => (string)this[nameof(UseClassFilterTestsThreshold)];
            set => value = (string)this[nameof(UseClassFilterTestsThreshold)];
        }

        [ConfigurationProperty(nameof(DynamicAssertsAssemblyPath), DefaultValue = @"DynamicAsserts\", IsRequired = false)]
        public string DynamicAssertsAssemblyPath
        {
            get => (string)this[nameof(DynamicAssertsAssemblyPath)];
            set => value = (string)this[nameof(DynamicAssertsAssemblyPath)];
        }

        [ConfigurationProperty(nameof(DynamicAssertsCoreAssemblyPath), DefaultValue = @"DynamicAssertsCore\", IsRequired = false)]
        public string DynamicAssertsCoreAssemblyPath
        {
            get => (string)this[nameof(DynamicAssertsCoreAssemblyPath)];
            set => value = (string)this[nameof(DynamicAssertsCoreAssemblyPath)];
        }

        [ConfigurationProperty(nameof(DynamicAssertsOutputPath), DefaultValue = @"C:\Dashboard\DynamicAsserts\", IsRequired = false)]
        public string DynamicAssertsOutputPath
        {
            get => (string)this[nameof(DynamicAssertsOutputPath)];
            set => value = (string)this[nameof(DynamicAssertsOutputPath)];
        }

        [ConfigurationProperty(nameof(ServiceAddress), DefaultValue = @"http://localhost:9000/", IsRequired = false)]
        public string ServiceAddress
        {
            get => (string)this[nameof(ServiceAddress)];
            set => value = (string)this[nameof(ServiceAddress)];
        }

        [ConfigurationProperty(nameof(OpenCppCoveragePath), DefaultValue = @"OpenCppCoverage\OpenCppCoverage.exe", IsRequired = false)]
        public string OpenCppCoveragePath
        {
            get => (string)this[nameof(OpenCppCoveragePath)];
            set => value = (string)this[nameof(OpenCppCoveragePath)];
        }

        [ConfigurationProperty(nameof(FireBaseDatabaseApiUrl), DefaultValue = "", IsRequired = false)]
        public string FireBaseDatabaseApiUrl
        {
            get => (string)this[nameof(FireBaseDatabaseApiUrl)];
            set => value = (string)this[nameof(FireBaseDatabaseApiUrl)];
        }

        [ConfigurationProperty(nameof(FireBaseStorageApiUrl), DefaultValue = "", IsRequired = false)]
        public string FireBaseStorageApiUrl
        {
            get => (string)this[nameof(FireBaseStorageApiUrl)];
            set => value = (string)this[nameof(FireBaseStorageApiUrl)];
        }
    }
}
