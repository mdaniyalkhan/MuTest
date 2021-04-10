using MuTest.Core.Model;
using Newtonsoft.Json;

namespace MuTest.Core.Mutants
{
    public class Mutant
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("mutation")]
        public Mutation Mutation { get; set; }

        [JsonProperty("mutant-status")]
        public MutantStatus ResultStatus { get; set; }

        [JsonProperty("status")]
        public string Status => ResultStatus.ToString();

        [JsonIgnore]
        public MethodDetail Method { get; set; }
    }
}