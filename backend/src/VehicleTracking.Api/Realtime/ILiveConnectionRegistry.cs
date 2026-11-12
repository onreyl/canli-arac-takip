namespace VehicleTracking.Api.Realtime;

/// <summary>Canlı yayına bağlı istemcilerin kaydı.</summary>
public interface ILiveConnectionRegistry
{
    /// <summary>Bağlı istemci sayısı.</summary>
    int ConnectionCount { get; }

    /// <summary>Bağlantıyı canlı yayına kaydeder.</summary>
    void AddConnection(string connectionId);

    /// <summary>Bağlantıyı canlı yayından çıkarır.</summary>
    void RemoveConnection(string connectionId);
}
