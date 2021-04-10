using System.Threading.Tasks;
using MuTest.Firebase.Api.Models;

namespace MuTest.Firebase.Api.Services
{
    public interface IFirestoreService
    {
        Task<string> AddAsync(MutationResult model);

        Task<MutationResult> GetAsync(string id);
    }
}