using System;
using System.Threading.Tasks;
using Firebase.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MuTest.Firebase.Api.Services
{
    public class StorageService : IStorageService
    {
        private const string MuTest = "mutest";
        private const string StorageUrl = "StorageUrl";
        private const string Mutations = "mutations";

        private readonly IConfiguration _configuration;
        private readonly IAuthenticationService _auth;

        public StorageService(IConfiguration configuration, IAuthenticationService auth)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public async Task AddAsync(string id, IFormFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var client = GetClient();

            var stream = file.OpenReadStream();
            await client
                .Child(MuTest)
                .Child(Mutations)
                .Child($"{id}.json")
                .PutAsync(stream);
        }

        public async Task<string> GetAsync(string id)
        {
            var client = GetClient();

            var downloadUrl = await client
                .Child(MuTest)
                .Child(Mutations)
                .Child($"{id}.json")
                .GetDownloadUrlAsync();

            return downloadUrl;
        }

        private FirebaseStorage GetClient()
        {
            return new FirebaseStorage(
                _configuration.GetValue<string>(StorageUrl),
                new FirebaseStorageOptions
                {
                    AuthTokenAsyncFactory = _auth.AuthenticateAsync
                });
        }
    }
}
