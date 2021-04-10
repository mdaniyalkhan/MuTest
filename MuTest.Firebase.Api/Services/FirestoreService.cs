using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using MuTest.Firebase.Api.Models;

namespace MuTest.Firebase.Api.Services
{
    public class FirestoreService : IFirestoreService
    {
        private const string CredentialsPath = "CredentialsPath";
        private const string ProjectId = "ProjectId";
        private const string Collection = "mutations";

        private readonly IConfiguration _configuration;

        public FirestoreService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<string> AddAsync(MutationResult model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var db = GetDb();

            var docRef = db.Collection(Collection).Document(model.Key);
            var user = new Dictionary<string, object>
            {
                ["CreationDateTime"] = model.DateCreated
            };

            await docRef.SetAsync(user);
            return model.Key;
        }

        public async Task<MutationResult> GetAsync(string id)
        {
            var db = GetDb();
            var docRef = db.Collection(Collection).Document(id);
            var snapshot = await docRef.GetSnapshotAsync();

            MutationResult mutationResult = null;
            if (snapshot.Exists)
            {
                var dictionary = snapshot.ToDictionary();
                var timestamp = (Timestamp)dictionary[nameof(MutationResult.DateCreated)];
                mutationResult = new MutationResult
                {
                    Key = id,
                    DateCreated = timestamp.ToDateTime()
                };
            }

            return mutationResult;
        }

        private FirestoreDb GetDb()
        {
            var dbBuilder = new FirestoreDbBuilder
            {
                CredentialsPath = _configuration.GetValue<string>(CredentialsPath),
                ProjectId = _configuration.GetValue<string>(ProjectId)
            };

            return dbBuilder.Build();
        }
    }
}
