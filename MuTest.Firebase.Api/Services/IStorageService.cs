using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MuTest.Firebase.Api.Services
{
    public interface IStorageService
    {
        Task AddAsync(string id, IFormFile model);

        Task<string> GetAsync(string id);
    }
}