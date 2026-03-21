using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class UniqueMetarPerObservationTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Önce mevcut non-unique composite index'i düşür
            migrationBuilder.DropIndex(
                name: "IX_metar_history_StationId_ObservationTime",
                table: "metar_history");

            // Duplicate kayıtları temizle — aynı (StationId, ObservationTime) için en eski Id'li kaydı tut
            migrationBuilder.Sql(@"
                DELETE FROM metar_history
                WHERE ""Id"" NOT IN (
                    SELECT MIN(""Id"")
                    FROM metar_history
                    GROUP BY ""StationId"", ""ObservationTime""
                );
            ");

            // Unique constraint olarak yeniden oluştur
            migrationBuilder.CreateIndex(
                name: "IX_metar_history_StationId_ObservationTime",
                table: "metar_history",
                columns: new[] { "StationId", "ObservationTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_metar_history_StationId_ObservationTime",
                table: "metar_history");

            migrationBuilder.CreateIndex(
                name: "IX_metar_history_StationId_ObservationTime",
                table: "metar_history",
                columns: new[] { "StationId", "ObservationTime" });
        }
    }
}
