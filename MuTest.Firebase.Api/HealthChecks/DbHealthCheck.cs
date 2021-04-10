using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MuTest.Firebase.Api.Services;

namespace MuTest.Firebase.Api.HealthChecks
{
    public class DbHealthCheck: IHealthCheck
    {
        private readonly IDbService _service;

        public DbHealthCheck(IDbService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            var ok = await _service.CheckHealthAsync();
            return new HealthCheckResult(ok ? HealthStatus.Healthy : HealthStatus.Unhealthy);
        }
    }
}
