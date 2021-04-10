using MuTest.Core.Mutators;
using Newtonsoft.Json;

namespace MuTest.Cpp.CLI.Mutants
{
    /// <summary>
    /// Represents a single mutation on code level
    /// </summary>
    public class CppMutation
    {
        [JsonProperty("line-number")]
        public int LineNumber { get; set; }

        [JsonProperty("original-node")]
        public string OriginalNode { get; set; }

        [JsonProperty("replacement-node")]
        public string ReplacementNode { get; set; }

        [JsonProperty("mutant-detail")]
        public string DisplayName { get; set; }

        [JsonProperty("mutator-type")]
        public MutatorType Type { get; set; }

        [JsonIgnore]
        public int EndLineNumber { get; set; }
    }
}
