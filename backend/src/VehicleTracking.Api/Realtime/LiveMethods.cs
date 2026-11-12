namespace VehicleTracking.Api.Realtime;

/// <summary>SignalR'da istemci tarafındaki metot adları (frontend'de connection.on(...) ile dinlenecek).</summary>
public static class LiveMethods
{
    public const string Snapshot = "snapshot";
    public const string Update = "update";
}
