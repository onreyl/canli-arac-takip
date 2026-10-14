using Microsoft.EntityFrameworkCore;
using VehicleTracking.Application.History;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence;

/// <summary>Okuma EF Core ile (yazma Npgsql ile, bkz. NpgsqlPositionWriter).</summary>
internal sealed class EfVehicleHistoryReader(VehicleTrackingDbContext db) : IVehicleHistoryReader
{
    public async Task<HistoryPage> GetPageAsync(
        string vehicleId,
        DateTimeOffset from,
        DateTimeOffset to,
        int pageSize,
        HistoryCursor? after,
        CancellationToken cancellationToken)
    {
        // AsNoTracking: sadece okuyoruz, EF'in nesneleri takip etmesine gerek yok (daha hızlı, daha az bellek).
        IQueryable<VehiclePosition> query = db.VehiclePositions
            .AsNoTracking()
            .Where(p => p.VehicleId == vehicleId && p.MeasuredAt >= from && p.MeasuredAt < to);

        if (after is not null)
        {
            // Keyset: önceki sayfanın son satırından (zaman, varlık) "daha eski" olanlar.
            query = query.Where(p => p.MeasuredAt < after.MeasuredAt
                || (p.MeasuredAt == after.MeasuredAt && string.Compare(p.EntityId, after.EntityId) < 0));
        }

        // Bir fazla çekiyoruz: gelirse sonraki sayfa var demektir (ayrıca COUNT sorgusu gerekmez).
        List<VehiclePosition> rows = await query
            .OrderByDescending(p => p.MeasuredAt)
            .ThenByDescending(p => p.EntityId)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        if (rows.Count <= pageSize)
        {
            return new HistoryPage(rows, Next: null);
        }

        rows.RemoveAt(rows.Count - 1);
        VehiclePosition last = rows[^1];
        return new HistoryPage(rows, new HistoryCursor(last.MeasuredAt, last.EntityId));
    }
}
