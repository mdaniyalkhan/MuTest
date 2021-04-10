using Microsoft.CodeAnalysis;
using MuTest.Core.Mutators;
using Newtonsoft.Json;

namespace MuTest.Core.Mutants
{
    /// <summary>
    /// Represents a single mutation on code level
    /// </summary>
    public class Mutation
    {
        [JsonIgnore]
        public SyntaxNode OriginalNode { get; set; }

        private int? _location;
        [JsonProperty("line-number")]
        public int? Location
        {
            get => _location ?? (_location = OriginalNode?.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
            set => _location = value;
        }

        [JsonIgnore]
        public SyntaxNode ReplacementNode { get; set; }

        [JsonProperty("mutant-detail")]
        public string DisplayName { get; set; }

        [JsonIgnore]
        public MutatorType Type { get; set; }
    }
}
