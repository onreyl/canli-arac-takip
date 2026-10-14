using Google.Protobuf;
using Microsoft.Extensions.Options;
using TransitRealtime;
using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.Infrastructure.Feeds;

/// <summary>
/// Entur GTFS-RT vehicle-positions akışını çeker ve kendi tiplerimize çevirir.
/// HttpClient'ın başlığı, zaman aşımı ve gzip ayarı DependencyInjection'da yapılır.
/// </summary>
internal sealed class EnturFeedSource(HttpClient http, IOptions<EnturOptions> options) : IFeedSource
{
    public async Task<FeedSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        FeedMessage feed;
        try
        {
            byte[] bytes = await http.GetByteArrayAsync(options.Value.VehiclePositionsUrl, cancellationToken);
            feed = FeedMessage.Parser.ParseFrom(bytes);
        }
        catch (HttpRequestException ex)
        {
            throw new FeedUnavailableException("Entur'a ulaşılamadı.", ex);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new FeedUnavailableException("Entur yanıtı çözülemedi (bozuk protobuf).", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient zaman aşımı da TaskCanceledException fırlatır; uygulama kapanışından ayırt ediyoruz.
            throw new FeedUnavailableException("Entur isteği zaman aşımına uğradı.", ex);
        }

        return GtfsRealtimeMapper.ToSnapshot(feed);
    }
}
