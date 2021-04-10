using System;
using System.Threading.Tasks;
using Firebase.Auth;

namespace MuTest.Firebase.Api.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private string _token; 
        private int _expireIn;
        private DateTime _created;

        public AuthenticationService()
        {
            _expireIn = 10;
            _created = DateTime.MinValue;
        }

        public async Task<string> AuthenticateAsync()
        {
            if (IsExpired())
            {
                using var provider =
                    new FirebaseAuthProvider(new FirebaseConfig(Environment.GetEnvironmentVariable("FirebaseApiKey")));
                var auth = await provider.SignInWithEmailAndPasswordAsync(
                    Environment.GetEnvironmentVariable("FirebaseUser"), 
                    Environment.GetEnvironmentVariable("FireBaseUserPassword"));

                _token = auth.FirebaseToken;
                _expireIn = auth.ExpiresIn;
                _created = auth.Created;
            }

            return _token;
        }

        private bool IsExpired()
        {
            return DateTime.Now > _created.AddSeconds(_expireIn - 10);
        }
    }
}
