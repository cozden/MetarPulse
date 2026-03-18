using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddNotams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotamId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AirportIdent = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FirIdent = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Series = table.Column<char>(type: "character(1)", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    NotamType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    QLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Traffic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    RadiusNm = table.Column<int>(type: "integer", nullable: true),
                    LowerLimit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpperLimit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPermanent = table.Column<bool>(type: "boolean", nullable: false),
                    IsEstimatedEnd = table.Column<bool>(type: "boolean", nullable: false),
                    Schedule = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RawText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    VfrImpact = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notams_airports_AirportIdent",
                        column: x => x.AirportIdent,
                        principalTable: "airports",
                        principalColumn: "Ident",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notams_AirportIdent",
                table: "notams",
                column: "AirportIdent");

            migrationBuilder.CreateIndex(
                name: "IX_notams_AirportIdent_EffectiveTo",
                table: "notams",
                columns: new[] { "AirportIdent", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_notams_NotamId",
                table: "notams",
                column: "NotamId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notams");
        }
    }
}
