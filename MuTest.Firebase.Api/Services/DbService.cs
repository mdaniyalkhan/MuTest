using System;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Extensions.Configuration;
using MuTest.Firebase.Api.Models;

namespace MuTest.Firebase.Api.Services
{
    public class DbService : IDbService
    {
        private const string MuTest = "mutest";
        private const string Mutations = "mutations";
        private const string DbUrl = "DbUrl";

        private readonly IConfiguration _configuration;
        private readonly IAuthenticationService _auth;

        public DbService(IConfiguration configuration, IAuthenticationService auth)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public async Task<string> AddAsync(MutationResult model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            using var client = GetClient();

            await client
                .Child(MuTest)
                .Child(Mutations)
                .Child($"{model.Key}")
                .PutAsync(model);

            return model.Key;
        }

        public async Task<MutationResult> GetAsync(string id)
        {
            using var client = GetClient();

            var result = await client
                .Child(MuTest)
                .Child(Mutations)
                .Child($"{id}").OrderByKey().OnceSingleAsync<MutationResult>();

            return result;
        }

        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var result = await GetAsync("-1");
                return result == null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private FirebaseClient GetClient()
        {
            return new FirebaseClient(
                _configuration.GetValue<string>(DbUrl),
                new FirebaseOptions
                {
                    AuthTokenAsyncFactory = _auth.AuthenticateAsync
                });
        }
    }
}
