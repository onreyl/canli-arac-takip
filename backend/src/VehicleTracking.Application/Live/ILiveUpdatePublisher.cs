namespace VehicleTracking.Application.Live;

/// <summary>
/// Değişiklikleri bağlı istemcilere iletir. Application, bunun SignalR mı düz WebSocket mi olduğunu bilmez.
/// </summary>
public interface ILiveUpdatePublisher
{
    /// <summary>
    /// Beklemeden döner: sadece her istemcinin kuyruğuna bırakır. Çekme döngüsü hiçbir istemciyi beklememeli.
    /// </summary>
    void Publish(LiveUpdate update);
}
