using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderSettingOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderSettingOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: true),
                    BaseUrl = table.Column<string>(type: "text", nullable: true),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: true),
                    CircuitBreakerThreshold = table.Column<int>(type: "integer", nullable: true),
                    CircuitBreakerDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderSettingOverrides", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderSettingOverrides");
        }
    }
}
