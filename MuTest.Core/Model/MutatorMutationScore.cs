using Newtonsoft.Json;

namespace MuTest.Core.Model
{
    public class MutatorMutationScore
    {
        [JsonProperty("mutator")]
        public string Mutator { get; set; }

        [JsonProperty("mutation-score")]
        public MutationScore MutationScore { get; set; }
    }
}
