using Newtonsoft.Json;

namespace MuTest.Core.Model
{
    public class Coverage
    {
        public override string ToString()
        {
            return $"Lines: {LinesCovered}/{TotalLines}({LinesCoveredPercentage}) Branch: {BlocksCovered}/{TotalBlocks}({BlocksCoveredPercentage})";
        }

        [JsonProperty("lines-covered")]
        public uint LinesCovered { get; set; }

        [JsonProperty("lines-not-covered")]
        public uint LinesNotCovered { get; set; }

        [JsonProperty("branches-covered")]
        public uint BlocksCovered { get; set; }

        [JsonProperty("branches-not-covered")]
        public uint BlocksNotCovered { get; set; }

        [JsonProperty("total-lines")]
        public uint TotalLines => LinesCovered + LinesNotCovered;

        [JsonProperty("total-branches")]
        public uint TotalBlocks => BlocksCovered + BlocksNotCovered;

        [JsonProperty("lines-covered-percentage")]
        public string LinesCoveredPercentage => $"{LinesCoverage:P}";

        [JsonIgnore]
        public decimal LinesCoverage => decimal.Divide(LinesCovered, TotalLines == 0 ? 1 : TotalLines);

        [JsonProperty("branches-covered-percentage")]
        public string BlocksCoveredPercentage => $"{BlockCoverage:P}";

        [JsonIgnore]
        public decimal BlockCoverage => decimal.Divide(BlocksCovered, TotalBlocks == 0 ? 1 : TotalBlocks);
    }
}