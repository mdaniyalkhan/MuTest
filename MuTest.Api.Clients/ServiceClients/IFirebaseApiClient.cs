using System.Threading.Tasks;

namespace MuTest.Api.Clients.ServiceClients
{
    public interface IFirebaseApiClient
    {
        Task<MutationResponse> StoreInDatabaseAsync(MutationResult result);

        Task<MutationResult> GetFromDatabaseAsync(string id);

        Task StoreFileAsync(string id, string json);

        Task<string> GetFileDataFromStorage(string id);
    }
}