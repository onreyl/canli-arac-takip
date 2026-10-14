using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VehicleTracking.Api.Health;

internal static class HealthResponseWriter
{
    /// <summary>Sağlık kontrollerinin sonuçlarını JSON olarak döndürür.</summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        return JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
                data = entry.Value.Data,
            }),
        }, cancellationToken: context.RequestAborted);
    }
}
