using System.Threading.Tasks;
using MuTest.Firebase.Api.Models;

namespace MuTest.Firebase.Api.Services
{
    public interface IDbService
    {
        Task<string> AddAsync(MutationResult model);

        Task<MutationResult> GetAsync(string id);
        
        Task<bool> CheckHealthAsync();
    }
}