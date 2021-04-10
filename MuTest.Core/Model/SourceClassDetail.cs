using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MuTest.Core.Model
{
    public class SourceClassDetail : ClassDetail
    {
        [JsonIgnore]
        public bool CoverageExist => Coverage != null;

        [JsonIgnore]
        public string LinesCovered => CoverageExist
            ? $"{Coverage.LinesCovered}/{Coverage.TotalLines}"
            : string.Empty;

        [JsonIgnore]
        public string BlocksCovered => CoverageExist
            ? $"{Coverage.BlocksCovered}/{Coverage.TotalBlocks}"
            : string.Empty;

        [JsonIgnore]
        public string LineCoverage => CoverageExist
            ? $"[{Coverage.LinesCoveredPercentage}]"
            : "NA";

        [JsonIgnore]
        public string BranchCoverage => CoverageExist
            ? $"[{Coverage.BlocksCoveredPercentage}]"
            : "NA";

        [JsonIgnore]
        public TestClassDetail TestClaz { get; set; }

        [JsonProperty("number-of-tests")]
        public int NumberOfTests { get; set; }

        [JsonProperty("test-execution-times")]
        public List<TestExecutionTime> TestExecutionTimes { get; } = new List<TestExecutionTime>();

        [JsonProperty("test-execution-times-above-threshold")]
        public List<TestExecutionTime> TestExecutionTimesAboveThreshold { get; } = new List<TestExecutionTime>();

        [JsonProperty("mutation-score")]
        public MutationScore MutationScore { get; } = new MutationScore();

        [JsonProperty("mutator-wise-mutation")]
        public List<MutatorMutationScore> MutatorWiseMutationScores { get; } = new List<MutatorMutationScore>();

        [JsonProperty("coverage")]
        public Coverage Coverage { get; set; }

        [JsonProperty("execution-time")]
        public long ExecutionTime { get; set; }

        [JsonProperty("sha256")]
        public string SHA256 { get; set; }

        [JsonProperty("include-nested-classes")]
        public bool IncludeNestedClasses { get; set; }

        [JsonProperty("external-covered-lines")]
        public long ExternalLineCovered => ExternalCoveredClassesIncluded.Sum(x => x.Coverage.LinesCovered);

        [JsonProperty("external-coverage")]
        public decimal ExternalCoverage
        {
            get
            {
                var totalCoverageGain = Coverage?.LinesCovered ?? 0;
                totalCoverageGain = totalCoverageGain == 0
                    ? 1
                    : totalCoverageGain;

                return decimal.Divide(ExternalLineCovered, totalCoverageGain);
            }
        }

        [JsonIgnore]
        public List<ClassCoverage> ExternalCoveredClasses { get; } = new List<ClassCoverage>();

        [JsonProperty("external-covered-classes")]
        public List<ClassCoverage> ExternalCoveredClassesIncluded => ExternalCoveredClasses.Where(x => x.NumberOfMutants > 0 && 
                                                                                                           !x.Excluded && 
                                                                                                           !x.ZeroSurvivedMutants).ToList();

        [JsonProperty("external-covered-classes-excluded")]
        public List<ClassCoverage> ExternalCoveredClassesExcluded => ExternalCoveredClasses.Where(x => x.NumberOfMutants == 0 || 
                                                                                                           x.Excluded ||
                                                                                                           x.ZeroSurvivedMutants).ToList();
        [JsonIgnore]
        public bool StoreToDb { get; set; }

        public void CalculateMutationScore()
        {
            MutationScore.Survived = MethodDetails.SelectMany(x => x.SurvivedMutants).Count();
            MutationScore.Killed = MethodDetails.SelectMany(x => x.KilledMutants).Count();
            MutationScore.Uncovered = MethodDetails.SelectMany(x => x.NotCoveredMutants).Count();
            MutationScore.Timeout = MethodDetails.SelectMany(x => x.TimeoutMutants).Count();
            MutationScore.BuildErrors = MethodDetails.SelectMany(x => x.BuildErrorMutants).Count();
            MutationScore.Skipped = MethodDetails.SelectMany(x => x.SkippedMutants).Count();
            MutationScore.Covered = MethodDetails.SelectMany(x => x.CoveredMutants).Count() - MutationScore.Timeout - MutationScore.BuildErrors - MutationScore.Skipped;
            MutationScore.Coverage = decimal.Divide(MutationScore.Killed, MutationScore.Covered == 0
                ? 1
                : MutationScore.Covered);
        }
    }
}