using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MuTest.Api.Clients.ServiceClients
{
    public class FirebaseApiClient : IFirebaseApiClient
    {
        private readonly HttpClient _client;
        private readonly string _dbApiUrl;
        private readonly string _storageApiUrl;

        public FirebaseApiClient(HttpClient client, string dbApiUrl, string storageApiUrl)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _dbApiUrl = dbApiUrl;
            _storageApiUrl = storageApiUrl;
        }

        public async Task<MutationResponse> StoreInDatabaseAsync(MutationResult result)
        {
            if (string.IsNullOrWhiteSpace(_dbApiUrl))
            {
                return null;
            }

            var json = JsonConvert.SerializeObject(result);
            var response = await _client.PostAsync(new Uri($"{_dbApiUrl}/store"),
                new StringContent(json, Encoding.UTF8, "application/json"));

            var jsonResponse = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<MutationResponse>(jsonResponse);
        }

        public async Task<MutationResult> GetFromDatabaseAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(_dbApiUrl))
            {
                return null;
            }

            var response = await _client.GetAsync(new Uri($"{_dbApiUrl}/{id}"));

            var jsonResponse = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<MutationResult>(jsonResponse);
        }

        public async Task<string> GetFileDataFromStorage(string id)
        {
            if (string.IsNullOrWhiteSpace(_storageApiUrl))
            {
                return null;
            }

            var response = await _client.GetAsync(new Uri($"{_storageApiUrl}/{id}"));
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var url = JsonConvert.DeserializeObject<MutationFileResult>(jsonResponse);

                var json = await _client.GetAsync(url.DownloadUrl);
                if (json.IsSuccessStatusCode)
                {
                    return await json.Content.ReadAsStringAsync();
                }
            }

            return null;
        }

        public async Task StoreFileAsync(string id, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(json));
            }

            if (!string.IsNullOrWhiteSpace(_storageApiUrl))
            {
                using (var form = new MultipartFormDataContent
                {
                    {
                        new StringContent(json)
                        {
                            Headers =
                            {
                                ContentLength = json.Length,
                                ContentType = new MediaTypeHeaderValue("appliction/json")
                            }
                        },
                        "File", "file.json"
                    }
                })
                {
                    await _client.PostAsync(new Uri($"{_storageApiUrl}/{id}/store"), form);
                }
            }
        }
    }
}
