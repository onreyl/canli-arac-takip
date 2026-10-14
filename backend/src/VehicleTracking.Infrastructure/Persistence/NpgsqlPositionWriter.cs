using Npgsql;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>
/// Toplu yazma, EF Core yerine doğrudan Npgsql ile. Neden:
/// - EF Core'un SaveChanges'i ON CONFLICT desteklemez (tekrar eden kayıtta hata fırlatır) ve her nesneyi
///   takip ettiği için saatte ~1 milyon satır için yavaştır.
/// - unnest: her sütun tek bir dizi parametresi olarak gider; satır sayısı ne olursa olsun tek sorgu, 12 parametre.
///   Çok satırlı VALUES'ta PostgreSQL'in 65.535 parametre sınırına (~5.000 satır) takılırdık.
/// </summary>
internal sealed class NpgsqlPositionWriter(NpgsqlDataSource dataSource) : IPositionWriter
{
    // Sütun sırası unnest'teki dizi sırasıyla birebir aynı olmalı.
    // ON CONFLICT DO NOTHING: birincil anahtar (entity_id, measured_at) zaten varsa satır sessizce atlanır.
    private const string InsertSql = """
        INSERT INTO vehicle_positions
            (entity_id, vehicle_id, trip_id, route_id, latitude, longitude,
             bearing, speed, stop_id, current_status, occupancy, measured_at)
        SELECT * FROM unnest(
            @entity_ids, @vehicle_ids, @trip_ids, @route_ids, @latitudes, @longitudes,
            @bearings, @speeds, @stop_ids, @current_statuses, @occupancies, @measured_ats)
        ON CONFLICT DO NOTHING
        """;

    public async Task<int> WriteAsync(IReadOnlyCollection<VehiclePosition> positions, CancellationToken cancellationToken)
    {
        if (positions.Count == 0)
        {
            return 0;
        }

        // Satırları sütunlara çeviriyoruz: her alan için bir dizi.
        int n = positions.Count;
        var entityIds = new string[n];
        var vehicleIds = new string[n];
        var tripIds = new string?[n];
        var routeIds = new string?[n];
        var latitudes = new float[n];
        var longitudes = new float[n];
        var bearings = new float?[n];
        var speeds = new float?[n];
        var stopIds = new string?[n];
        var currentStatuses = new short?[n];
        var occupancies = new short?[n];
        var measuredAts = new DateTimeOffset[n];

        int i = 0;
        foreach (VehiclePosition p in positions)
        {
            entityIds[i] = p.EntityId;
            vehicleIds[i] = p.VehicleId;
            tripIds[i] = p.TripId;
            routeIds[i] = p.RouteId;
            latitudes[i] = p.Latitude;
            longitudes[i] = p.Longitude;
            bearings[i] = p.Bearing;
            speeds[i] = p.Speed;
            stopIds[i] = p.StopId;
            currentStatuses[i] = (short?)p.CurrentStatus;   // EF eşlemesiyle aynı: enum → smallint
            occupancies[i] = (short?)p.Occupancy;
            measuredAts[i] = p.MeasuredAt;
            i++;
        }

        await using NpgsqlCommand command = dataSource.CreateCommand(InsertSql);
        command.Parameters.Add(new NpgsqlParameter<string[]>("entity_ids", entityIds));
        command.Parameters.Add(new NpgsqlParameter<string[]>("vehicle_ids", vehicleIds));
        command.Parameters.Add(new NpgsqlParameter<string?[]>("trip_ids", tripIds));
        command.Parameters.Add(new NpgsqlParameter<string?[]>("route_ids", routeIds));
        command.Parameters.Add(new NpgsqlParameter<float[]>("latitudes", latitudes));
        command.Parameters.Add(new NpgsqlParameter<float[]>("longitudes", longitudes));
        command.Parameters.Add(new NpgsqlParameter<float?[]>("bearings", bearings));
        command.Parameters.Add(new NpgsqlParameter<float?[]>("speeds", speeds));
        command.Parameters.Add(new NpgsqlParameter<string?[]>("stop_ids", stopIds));
        command.Parameters.Add(new NpgsqlParameter<short?[]>("current_statuses", currentStatuses));
        command.Parameters.Add(new NpgsqlParameter<short?[]>("occupancies", occupancies));
        command.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("measured_ats", measuredAts));

        try
        {
            // Etkilenen satır sayısı = gerçekten eklenenler (ON CONFLICT ile atlananlar sayılmaz).
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (NpgsqlException ex) when (!IsPermanentDataError(ex))
        {
            // Bağlantı reddedildi, DB kapanıyor, zaman aşımı...: beklenip tekrar denenebilir.
            throw new StorageUnavailableException("Veritabanına yazılamadı (geçici).", ex);
        }
    }

    // SQLSTATE 22xxx (veri hatası) ve 23xxx (kısıt ihlali): aynı veriyle kaç kez denense de başarısız olur.
    // Bunları geçici sayarsak tek bir bozuk parti yazıcıyı sonsuz döngüye sokar, arkasındaki her şey birikir.
    private static bool IsPermanentDataError(NpgsqlException ex) =>
        ex is PostgresException pg && (pg.SqlState.StartsWith("22", StringComparison.Ordinal)
                                    || pg.SqlState.StartsWith("23", StringComparison.Ordinal));
}
