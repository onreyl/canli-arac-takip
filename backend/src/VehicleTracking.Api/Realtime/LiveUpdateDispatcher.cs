using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Realtime;

/// <summary>
/// Değişiklikleri her istemciye AYRI bir gönderim döngüsüyle iletir (birleştirme / coalescing).
/// </summary>
/// <remarks>
/// Neden her istemciye ayrı döngü: hepsine tek döngüden gönderseydik, yavaş bir istemcinin gönderimini beklerken
/// diğerleri de beklerdi. Böylece yavaş istemci sadece kendi döngüsünü yavaşlatır.
/// Çekme döngüsü Publish'i çağırır ve hiç beklemez: sadece her istemcinin bekleyen listesine ekler.
/// </remarks>
public sealed partial class LiveUpdateDispatcher(
    IHubContext<VehiclesHub> hubContext,
    LiveVehicleState liveState,
    ILogger<LiveUpdateDispatcher> logger) : ILiveUpdatePublisher, IDisposable
{
    private readonly ConcurrentDictionary<string, LiveClientFeed> _clients = new();

    public int ConnectionCount => _clients.Count;

    public void AddConnection(string connectionId)
    {
        // Önce kayıt, sonra (gönderim döngüsünde) tam görüntü: bu sırayla arada gelen hiçbir değişiklik kaybolmaz.
        // En kötü ihtimal bir değişikliğin hem görüntüde hem sonraki pakette gelmesi; aynı konumu iki kez yazmak zararsız.
        var feed = new LiveClientFeed();
        if (_clients.TryAdd(connectionId, feed))
        {
            _ = RunSendLoopAsync(connectionId, feed);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out LiveClientFeed? feed))
        {
            feed.Dispose();
        }
    }

    public void Publish(LiveUpdate update)
    {
        foreach (LiveClientFeed feed in _clients.Values)
        {
            feed.Merge(update);
        }
    }

    private async Task RunSendLoopAsync(string connectionId, LiveClientFeed feed)
    {
        IClientProxy client = hubContext.Clients.Client(connectionId);
        try
        {
            while (await feed.WaitForWorkAsync())
            {
                // İlk tur (needsSnapshot) tam görüntü; sonrakiler son gönderimden beri biriken değişiklikler.
                object message = feed.TakePending(liveState, out string method);
                await client.SendAsync(method, message, feed.Cancellation);
            }
        }
        catch (OperationCanceledException)
        {
            // İstemci ayrıldı.
        }
        catch (Exception ex)
        {
            LogSendLoopFailed(ex, connectionId);
            RemoveConnection(connectionId);
        }
    }

    public void Dispose()
    {
        foreach (LiveClientFeed feed in _clients.Values)
        {
            feed.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "İstemciye gönderim başarısız, bağlantı bırakıldı: {ConnectionId}")]
    private partial void LogSendLoopFailed(Exception ex, string connectionId);
}
