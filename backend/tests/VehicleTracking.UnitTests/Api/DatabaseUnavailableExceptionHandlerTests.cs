using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VehicleTracking.Api.Errors;

namespace VehicleTracking.UnitTests.Api;

public sealed class DatabaseUnavailableExceptionHandlerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TryHandleAsync_DatabaseOrTimeoutException_WritesServiceUnavailableProblem(
        bool wrapped, bool timeout)
    {
        using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        var handler = new DatabaseUnavailableExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<DatabaseUnavailableExceptionHandler>.Instance);
        Exception exception = timeout ? new TimeoutException("İç hata") : new TestDbException();
        if (wrapped)
        {
            exception = new InvalidOperationException("Dış hata", exception);
        }

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        ProblemDetails? problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(body);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal("Veritabanına şu an ulaşılamıyor", problem.Title);
        Assert.Equal("Geçmiş verisi geçici olarak kullanılamıyor; lütfen biraz sonra tekrar deneyin.", problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_UnrelatedException_LeavesResponseUntouched()
    {
        using ServiceProvider services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        context.Response.StatusCode = StatusCodes.Status202Accepted;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("Mevcut yanıt");
        byte[] originalBody = body.ToArray();
        long originalPosition = body.Position;
        var handler = new DatabaseUnavailableExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<DatabaseUnavailableExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(context, new ArgumentException("İlgisiz hata"), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal("text/plain", context.Response.ContentType);
        Assert.Equal(originalBody, body.ToArray());
        Assert.Equal(originalPosition, body.Position);
    }

    private sealed class TestDbException : DbException;
}
