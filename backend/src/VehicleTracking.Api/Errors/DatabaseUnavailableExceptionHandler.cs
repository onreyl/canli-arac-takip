using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace VehicleTracking.Api.Errors;

/// <summary>
/// DB'ye ulaşılamadığı için çıkan hatayı 500 yerine 503 + ProblemDetails'e çevirir: 500 "sunucuda hata var", 503 "geçici olarak
/// kullanılamıyor" der; istemci bunu geçici durum olarak gösterip yeniden deneyebilir.
/// </summary>
/// <remarks>
/// Npgsql yerine temel <see cref="DbException"/>'a bakılır: Api katmanı veritabanı sürücüsünü (Infrastructure ayrıntısı) bilmesin.
/// EF Core hatayı başka bir istisnaya sarabildiği için iç istisnalar da taranır. Diğer hatalarda false döner; framework 500 +
/// ProblemDetails üretir ve stack trace'i dışarı vermez.
/// </remarks>
internal sealed partial class DatabaseUnavailableExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DatabaseUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not (DbException or TimeoutException))
            {
                continue;
            }

            LogDatabaseUnavailable(exception);
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Veritabanına şu an ulaşılamıyor",
                    Detail = "Geçmiş verisi geçici olarak kullanılamıyor; lütfen biraz sonra tekrar deneyin."
                }
            });
            return true;
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Veritabanına şu an ulaşılamıyor; geçmiş sorgusu tamamlanamadı.")]
    private partial void LogDatabaseUnavailable(Exception exception);
}
