using System;
using System.Collections.Generic;
using System.Linq;
using MuTest.Core.Model;
using Newtonsoft.Json;

namespace MuTest.Console.Model
{
    public class ProjectSummary
    {
        [JsonProperty("source-project")]
        public string SourceProject { get; set; }

        [JsonProperty("test-project")]
        public string TestProject { get; set; }

        [JsonProperty("mutation-score")]
        public MutationScore MutationScore { get; } = new MutationScore();

        [JsonProperty("classes")]
        public IList<ClassSummary> Classes { get; } = new List<ClassSummary>();

        [JsonProperty("code-coverage")]
        public Coverage Coverage { get; private set; }

        public void CalculateMutationScore()
        {
            MutationScore.Survived = Classes.Sum(x => x.MutationScore.Survived);
            MutationScore.Killed = Classes.Sum(x => x.MutationScore.Killed);
            MutationScore.Uncovered = Classes.Sum(x => x.MutationScore.Uncovered);
            MutationScore.Timeout = Classes.Sum(x => x.MutationScore.Timeout);
            MutationScore.BuildErrors = Classes.Sum(x => x.MutationScore.BuildErrors);
            MutationScore.Skipped = Classes.Sum(x => x.MutationScore.Skipped);
            MutationScore.Covered = Classes.Sum(x => x.MutationScore.Covered) - MutationScore.Timeout - MutationScore.BuildErrors - MutationScore.Skipped;
            MutationScore.Coverage = decimal.Divide(MutationScore.Killed, MutationScore.Covered == 0
                ? 1
                : MutationScore.Covered);

            Coverage = new Coverage
            {
                LinesCovered = Convert.ToUInt32(Classes.Sum(x => x.Coverage.LinesCovered)),
                LinesNotCovered = Convert.ToUInt32(Classes.Sum(x => x.Coverage.LinesNotCovered)),
                BlocksCovered = Convert.ToUInt32(Classes.Sum(x => x.Coverage.BlocksCovered)),
                BlocksNotCovered = Convert.ToUInt32(Classes.Sum(x => x.Coverage.BlocksNotCovered))
            };
        }
    }
}
