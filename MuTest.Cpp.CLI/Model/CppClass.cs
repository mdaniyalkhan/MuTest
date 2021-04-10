using System;
using System.Collections.Generic;
using System.Linq;
using MuTest.Core.Model;
using MuTest.Core.Mutants;
using MuTest.Cpp.CLI.Mutants;
using Newtonsoft.Json;

namespace MuTest.Cpp.CLI.Model
{
    public class CppClass
    {
        [JsonProperty("mutation-score")]
        public MutationScore MutationScore { get; } = new MutationScore();

        [JsonProperty("mutator-wise-mutation")]
        public List<MutatorMutationScore> MutatorWiseMutationScores { get; } = new List<MutatorMutationScore>();

        [JsonProperty("coverage")]
        public Coverage Coverage { get; set; }

        [JsonProperty("number-of-tests")]
        public int NumberOfTests { get; set; }

        [JsonProperty("use-class-filter")]
        public bool UseClassFilter { get; set; } = true;

        [JsonProperty("number-of-disabled-tests")]
        public int NumberOfDisabledTests { get; set; }

        [JsonProperty("tests")]
        public List<Test> Tests { get; } = new List<Test>();

        [JsonProperty("mutants")]
        public List<CppMutant> Mutants { get; } = new List<CppMutant>();

        [JsonProperty("execution-time")]
        public long ExecutionTime { get; set; }

        [JsonIgnore]
        public string SourceClass { get; set; }

        [JsonIgnore]
        public bool StoreInDb { get; set; }

        [JsonIgnore]
        public string SourceHeader { get; set; }

        [JsonIgnore]
        public string TestClass { get; set; }

        [JsonIgnore]
        public string TestProject { get; set; }

        [JsonIgnore]
        public IList<uint> CoveredLineNumbers { get; } = new List<uint>();

        [JsonIgnore]
        public bool CoverageExist => Coverage != null;

        [JsonIgnore]
        public string LinesCovered => CoverageExist
            ? $"{Coverage.LinesCovered}/{Coverage.TotalLines}"
            : string.Empty;

        [JsonIgnore]
        public string LineCoverage => CoverageExist
            ? $"[{Coverage.LinesCoveredPercentage}]"
            : "NA";

        [JsonIgnore]
        public IList<CppMutant> SurvivedMutants => Mutants.Where(x => x.ResultStatus == MutantStatus.Survived).ToList();

        [JsonIgnore]
        public IList<CppMutant> KilledMutants => Mutants.Where(x => x.ResultStatus == MutantStatus.Killed).ToList();

        [JsonIgnore]
        public IList<CppMutant> NotCoveredMutants => Mutants.Where(x => x.ResultStatus == MutantStatus.NotCovered).ToList();

        [JsonIgnore]
        public IList<CppMutant> NotRunMutants => Mutants.Where(x => x.ResultStatus != MutantStatus.Killed &&
                                                                 x.ResultStatus != MutantStatus.Skipped &&
                                                                 x.ResultStatus != MutantStatus.NotCovered).ToList();

        [JsonIgnore]
        public IList<CppMutant> CoveredMutants => Mutants.Where(x => x.ResultStatus != MutantStatus.NotCovered).ToList();

        [JsonIgnore]
        public IList<CppMutant> TimeoutMutants => Mutants.Where(x => x.ResultStatus == MutantStatus.Timeout).ToList();

        [JsonIgnore]
        public IList<CppMutant> BuildErrorMutants => Mutants.Where(x => x.ResultStatus == MutantStatus.BuildError).ToList();

        [JsonIgnore]
        public IList<CppMutant> SkippedMutants => Mutants.Where(x => x.ResultStatus == MutantStatus.Skipped).ToList();

        [JsonIgnore]
        public string Configuration { get; set; }

        [JsonIgnore]
        public string Target { get; set; }

        [JsonIgnore]
        public string Platform { get; set; }

        [JsonIgnore]
        public string TestSolution { get; set; }

        [JsonIgnore]
        public bool IncludeBuildEvents { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }

        public void CalculateMutationScore()
        {
            MutationScore.Survived = SurvivedMutants.Count;
            MutationScore.Killed = KilledMutants.Count;
            MutationScore.Uncovered = NotCoveredMutants.Count;
            MutationScore.Timeout = TimeoutMutants.Count;
            MutationScore.BuildErrors = BuildErrorMutants.Count;
            MutationScore.Skipped = SkippedMutants.Count;
            MutationScore.Covered = CoveredMutants.Count - MutationScore.Timeout - MutationScore.BuildErrors - MutationScore.Skipped;
            MutationScore.Coverage = decimal.Divide(MutationScore.Killed, MutationScore.Covered == 0
                ? 1
                : MutationScore.Covered);
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TestClass))
            {
                throw new ArgumentNullException(nameof(TestClass));
            }

            if (string.IsNullOrWhiteSpace(SourceClass))
            {
                throw new ArgumentNullException(nameof(SourceClass));
            }

            if (string.IsNullOrWhiteSpace(TestProject))
            {
                throw new ArgumentNullException(nameof(TestProject));
            }

            if (string.IsNullOrWhiteSpace(TestSolution))
            {
                throw new ArgumentNullException(nameof(TestSolution));
            }
        }
    }
}