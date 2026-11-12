using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Application.Live;

/// <summary>Araçların canlı durumunu tutar.</summary>
public interface ILiveVehicleStore
{
    /// <summary>Canlı durumdaki araç sayısı.</summary>
    int Count { get; }

    /// <summary>Yeni konumları uygular, değişen araçları döndürür.</summary>
    LiveUpdate Apply(IReadOnlyList<VehiclePosition> positions, DateTimeOffset now);

    /// <summary>Tüm araçların anlık görüntüsünü döndürür.</summary>
    LiveSnapshot GetSnapshot();
}
