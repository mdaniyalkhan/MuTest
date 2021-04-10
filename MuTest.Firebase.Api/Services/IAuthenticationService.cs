using System;
using System.Threading.Tasks;

namespace MuTest.Firebase.Api.Services
{
    public interface IAuthenticationService
    {
        Task<string> AuthenticateAsync();
    }
}