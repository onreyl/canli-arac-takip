using System.Net;

namespace VehicleTracking.Benchmarks;

// Ağ ve disk gecikmesini ölçüme katmadan her isteğe bağımsız bir gövde sağlar.
internal sealed class FeedMessageHandler(byte[] bytes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(bytes, writable: false))
        });
    }
}
