using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTracking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_positions",
                columns: table => new
                {
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    measured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vehicle_id = table.Column<string>(type: "text", nullable: false),
                    trip_id = table.Column<string>(type: "text", nullable: true),
                    route_id = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    bearing = table.Column<float>(type: "real", nullable: true),
                    speed = table.Column<float>(type: "real", nullable: true),
                    stop_id = table.Column<string>(type: "text", nullable: true),
                    current_status = table.Column<short>(type: "smallint", nullable: true),
                    occupancy = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicle_positions", x => new { x.entity_id, x.measured_at });
                });

            // TimescaleDB (Karar 3): tabloyu measured_at'e göre 15 dakikalık parçalara (chunk) bölünen hypertable yap.
            // Silme, süresi dolan chunk'ı bütün olarak atarak yapılır (satır satır DELETE değil).
            // 15 dk: 3 saatlik pencerede ~12 chunk; bir chunk silinmeden en fazla ~15 dk fazla kalır, sorgular yine 3 saate filtreler.
            migrationBuilder.Sql(
                "SELECT create_hypertable('vehicle_positions', by_range('measured_at', INTERVAL '15 minutes'));");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_positions_vehicle_id_measured_at",
                table: "vehicle_positions",
                columns: new[] { "vehicle_id", "measured_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_positions");
        }
    }
}
