using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.Infrastructure.Persistence.Configurations;

internal sealed class VehiclePositionConfiguration : IEntityTypeConfiguration<VehiclePosition>
{
    public void Configure(EntityTypeBuilder<VehiclePosition> builder)
    {
        builder.ToTable("vehicle_positions");

        // Tekrar güvenli yazmanın anahtarı: aynı ölçüm iki kez gelirse ON CONFLICT DO NOTHING ile yok sayılır.
        // TimescaleDB kuralı: hypertable'daki tekil anahtarlar zaman sütununu (measured_at) içermek zorunda.
        builder.HasKey(p => new { p.EntityId, p.MeasuredAt });

        // "Bu aracın son 3 saati" sorgusu için. Araç kimliği tekil değil, geçmiş araç kimliğiyle sorgulanır.
        builder.HasIndex(p => new { p.VehicleId, p.MeasuredAt })
            .IsDescending(false, true);

        // Enum'lar smallint (2 byte): saatte ~1 milyon satırda metin saklamak gereksiz yer kaplar.
        builder.Property(p => p.CurrentStatus).HasConversion<short?>();
        builder.Property(p => p.Occupancy).HasConversion<short?>();

        // Hesaplanan kurallar sütun değil
        builder.Ignore(p => p.IsDeadRun);
    }
}
