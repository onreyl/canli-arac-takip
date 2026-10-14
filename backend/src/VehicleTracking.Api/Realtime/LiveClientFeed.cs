using System.Threading.Channels;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Realtime;

/// <summary>Bir istemcinin bekleyen değişiklikleri.</summary>
internal sealed class LiveClientFeed : IDisposable
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

    public LiveClientFeed()
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
