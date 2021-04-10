using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MuTest.Core.Model;

namespace MuTest.Core.Common
{
    public interface IMutantExecutor
    {
        bool CancelMutationOperation { get; set; }

        bool UseClassFilter { get; set; }

        string BaseAddress { get; set; }

        bool EnableDiagnostics { get; set; }

        string LastExecutionOutput { get; set; }

        double SurvivedThreshold { get; set; }

        double KilledThreshold { get; set; }

        int NumberOfMutantsExecutingInParallel { get; set; }

        event EventHandler<MutantEventArgs> MutantExecuted;

        Task ExecuteMutants();

        void PrintMutationReport(StringBuilder mutationProcessLog, IList<MethodDetail> methodDetails);

        void PrintClassSummary(StringBuilder mutationProcessLog);

        void PrintMutatorSummary(StringBuilder mutationProcessLog);
    }
}