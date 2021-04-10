using System;
using System.Collections.Generic;

namespace MuTest.Firebase.Api.Models
{
    public class MutationResult
    {
        public string Key { get; set; }

        public string Source { get; set; }

        public string Test { get; set; }

        public int NoOfTests { get; set; }

        public DateTime DateCreated { get; set; }

        public Mutation Mutation { get; set; }

        public CodeCoverage Coverage { get; set; }

        public decimal ExternalCoverage { get; set; }

        public IDictionary<string, Mutation> MutatorWiseMutations { get; set; } = new Dictionary<string, Mutation>();
    }
}
