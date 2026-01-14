using System.Net.Http.Json;
using EyeExamParser.DTO;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EyeExamParser.HealthChecks
{
    /// <summary>
    /// Readiness check: confirms the external EyeExamAPI is reachable and returning expected shape.
    /// Uses the same HttpClient base address & auth configured for IScheduleServices.
    /// </summary>
    public sealed class EyeExamApiHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;

        public EyeExamApiHealthCheck(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient(nameof(ScheduleServices));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var raw = await _httpClient.GetFromJsonAsync<List<RawScheduleDTO>>(
                    "/schedules",
                    cancellationToken
                );

                raw ??= new List<RawScheduleDTO>();

                return HealthCheckResult.Healthy($"EyeExamAPI reachable. RawCount={raw.Count}");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("EyeExamAPI not reachable or invalid response.", ex);
            }
        }
    }
}
