using Microsoft.AspNetCore.SignalR;

namespace VehicleTracking.Api.Realtime;

/// <summary>
/// İstemcilerin bağlandığı nokta (/hubs/vehicles). Sadece sunucudan istemciye yayın yapıyoruz, istemcinin
/// çağırdığı bir metot yok. Bağlanan her istemci dağıtıcıya kaydolur; ilk mesajı tam görüntü olur.
/// </summary>
public sealed class VehiclesHub(LiveUpdateDispatcher dispatcher) : Hub
{
    public override Task OnConnectedAsync()
    {
        dispatcher.AddConnection(Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        dispatcher.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
