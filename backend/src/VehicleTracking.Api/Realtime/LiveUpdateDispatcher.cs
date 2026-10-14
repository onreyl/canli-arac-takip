using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Realtime;

/// <summary>
/// Değişiklikleri her istemciye AYRI bir gönderim döngüsüyle iletir (Karar 7: birleştirme / coalescing).
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
    private readonly ConcurrentDictionary<string, ClientFeed> _clients = new();

    public int ConnectionCount => _clients.Count;

    public void AddConnection(string connectionId)
    {
        // Önce kayıt, sonra (gönderim döngüsünde) tam görüntü: bu sırayla arada gelen hiçbir değişiklik kaybolmaz.
        // En kötü ihtimal bir değişikliğin hem görüntüde hem sonraki pakette gelmesi; aynı konumu iki kez yazmak zararsız.
        var feed = new ClientFeed();
        if (_clients.TryAdd(connectionId, feed))
        {
            _ = RunSendLoopAsync(connectionId, feed);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out ClientFeed? feed))
        {
            feed.Dispose();
        }
    }

    public void Publish(LiveUpdate update)
    {
        foreach (ClientFeed feed in _clients.Values)
        {
            feed.Merge(update);
        }
    }

    private async Task RunSendLoopAsync(string connectionId, ClientFeed feed)
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
        foreach (ClientFeed feed in _clients.Values)
        {
            feed.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "İstemciye gönderim başarısız, bağlantı bırakıldı: {ConnectionId}")]
    private partial void LogSendLoopFailed(Exception ex, string connectionId);

    /// <summary>Bir istemcinin bekleyen değişiklikleri.</summary>
    private sealed class ClientFeed : IDisposable
    {
        private readonly Lock _lock = new();
        private readonly CancellationTokenSource _cts = new();

        // Araç kimliği → en son konum. Aynı aracın yeni konumu eskisinin üzerine yazılır (birleştirme).
        // Bu yüzden istemci ne kadar yavaş olursa olsun liste araç sayısıyla (~3.000) sınırlı kalır.
        private Dictionary<string, LiveVehicleDto> _upserts = new();
        private HashSet<string> _removals = new();
        private bool _needsSnapshot = true;
        private long _version;

        // "Gönderilecek bir şey var" sinyali. Kapasite 1 + DropWrite: ard arda gelen sinyaller tek uyanmaya birleşir.
        // Kapatıldıktan sonra TryWrite hata fırlatmaz, false döner; ayrılan istemciye Publish güvenli.
        private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        public ClientFeed()
        {
            // İlk uyanış: tam görüntüyü göndermek için.
            _signal.Writer.TryWrite(true);
        }

        public CancellationToken Cancellation => _cts.Token;

        public void Merge(LiveUpdate update)
        {
            lock (_lock)
            {
                foreach (var position in update.Upserts)
                {
                    _upserts[position.VehicleId] = LiveVehicleDto.From(position);
                    _removals.Remove(position.VehicleId);
                }

                foreach (string vehicleId in update.Removals)
                {
                    _upserts.Remove(vehicleId);
                    _removals.Add(vehicleId);
                }

                _version = update.Version;
            }

            Signal();
        }

        public async Task<bool> WaitForWorkAsync()
        {
            await _signal.Reader.ReadAsync(_cts.Token);
            return true;
        }

        public object TakePending(LiveVehicleState liveState, out string method)
        {
            lock (_lock)
            {
                if (_needsSnapshot)
                {
                    // Görüntü, o ana kadar uygulanmış her şeyi içerir; biriken listeyi temizlemek güvenli.
                    LiveSnapshot snapshot = liveState.GetSnapshot();
                    _needsSnapshot = false;
                    _upserts = new();
                    _removals = new();
                    _version = snapshot.Version;
                    method = LiveMethods.Snapshot;
                    return new LiveSnapshotMessage(snapshot.Version, snapshot.Vehicles.Select(LiveVehicleDto.From).ToList());
                }

                var message = new LiveUpdateMessage(_version, _upserts.Values.ToList(), _removals.ToList());
                _upserts = new();
                _removals = new();
                method = LiveMethods.Update;
                return message;
            }
        }

        // Zaten bir sinyal bekliyorsa yenisi düşer; döngü uyanınca biriken her şeyi birden alacak.
        private void Signal() => _signal.Writer.TryWrite(true);

        public void Dispose()
        {
            _signal.Writer.TryComplete();
            _cts.Cancel();
        }
    }
}
