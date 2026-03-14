using System;
using System.Collections.Generic;
using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "airports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ident = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LatitudeDeg = table.Column<double>(type: "double precision", nullable: false),
                    LongitudeDeg = table.Column<double>(type: "double precision", nullable: false),
                    ElevationFt = table.Column<int>(type: "integer", nullable: true),
                    IsoCountry = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Municipality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IataCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    MagneticVariation = table.Column<double>(type: "double precision", nullable: true),
                    LastSynced = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_airports", x => x.Id);
                    table.UniqueConstraint("AK_airports_Ident", x => x.Ident);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    PreferredLanguage = table.Column<string>(type: "text", nullable: false),
                    PreferredUnits = table.Column<string>(type: "text", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsOnboardingCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "magic_link_tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_link_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "taf_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StationId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IssueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Periods = table.Column<List<TafPeriod>>(type: "jsonb", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taf_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "metar_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StationId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ObservationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindDirection = table.Column<int>(type: "integer", nullable: false),
                    WindSpeed = table.Column<int>(type: "integer", nullable: false),
                    WindGust = table.Column<int>(type: "integer", nullable: true),
                    IsVariableWind = table.Column<bool>(type: "boolean", nullable: false),
                    VariableWindFrom = table.Column<int>(type: "integer", nullable: true),
                    VariableWindTo = table.Column<int>(type: "integer", nullable: true),
                    VisibilityMeters = table.Column<int>(type: "integer", nullable: false),
                    RvrRaw = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CloudLayers = table.Column<List<CloudLayer>>(type: "jsonb", nullable: false),
                    WeatherConditions = table.Column<List<WeatherCondition>>(type: "jsonb", nullable: false),
                    Temperature = table.Column<int>(type: "integer", nullable: true),
                    DewPoint = table.Column<int>(type: "integer", nullable: true),
                    AltimeterHpa = table.Column<decimal>(type: "numeric", nullable: true),
                    AltimeterInHg = table.Column<decimal>(type: "numeric", nullable: true),
                    Trend = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    CeilingFeet = table.Column<int>(type: "integer", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsSpeci = table.Column<bool>(type: "boolean", nullable: false),
                    AirportId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metar_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metar_history_airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "airports",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "runways",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AirportIdent = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LengthFt = table.Column<int>(type: "integer", nullable: true),
                    WidthFt = table.Column<int>(type: "integer", nullable: true),
                    Surface = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsLighted = table.Column<bool>(type: "boolean", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    LeIdent = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    LeHeadingDegT = table.Column<double>(type: "double precision", nullable: true),
                    HeIdent = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    HeHeadingDegT = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runways", x => x.Id);
                    table.ForeignKey(
                        name: "FK_runways_airports_AirportIdent",
                        column: x => x.AirportIdent,
                        principalTable: "airports",
                        principalColumn: "Ident",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    StationIcao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActiveDays = table.Column<string>(type: "jsonb", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NotifyOnEveryMetar = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnCategoryChange = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnSpeci = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnVfrAchieved = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnSignificantWeather = table.Column<bool>(type: "boolean", nullable: false),
                    VisibilityThresholdMeters = table.Column<int>(type: "integer", nullable: true),
                    CeilingThresholdFeet = table.Column<int>(type: "integer", nullable: true),
                    WindThresholdKnots = table.Column<int>(type: "integer", nullable: true),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_preferences_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "pilot_profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    LicenseType = table.Column<int>(type: "integer", nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LicenseExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalFlightHours = table.Column<int>(type: "integer", nullable: true),
                    AircraftTypeRatings = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BaseAirportIcao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SecondaryAirportIcao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PersonalCrosswindLimitKts = table.Column<int>(type: "integer", nullable: true),
                    PersonalTailwindLimitKts = table.Column<int>(type: "integer", nullable: true),
                    PersonalVisibilityMinMeters = table.Column<int>(type: "integer", nullable: true),
                    PersonalCeilingMinFeet = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pilot_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pilot_profiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pilot_profiles_airports_BaseAirportIcao",
                        column: x => x.BaseAirportIcao,
                        principalTable: "airports",
                        principalColumn: "Ident",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceInfo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_bookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    StationIcao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_bookmarks_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_bookmarks_airports_StationIcao",
                        column: x => x.StationIcao,
                        principalTable: "airports",
                        principalColumn: "Ident",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_airports_IataCode",
                table: "airports",
                column: "IataCode");

            migrationBuilder.CreateIndex(
                name: "IX_airports_Ident",
                table: "airports",
                column: "Ident",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_airports_IsoCountry",
                table: "airports",
                column: "IsoCountry");

            migrationBuilder.CreateIndex(
                name: "IX_airports_Type",
                table: "airports",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_Email",
                table: "magic_link_tokens",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_ExpiresAt",
                table: "magic_link_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_magic_link_tokens_Token",
                table: "magic_link_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metar_history_AirportId",
                table: "metar_history",
                column: "AirportId");

            migrationBuilder.CreateIndex(
                name: "IX_metar_history_ObservationTime",
                table: "metar_history",
                column: "ObservationTime");

            migrationBuilder.CreateIndex(
                name: "IX_metar_history_StationId",
                table: "metar_history",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_metar_history_StationId_ObservationTime",
                table: "metar_history",
                columns: new[] { "StationId", "ObservationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_ApplicationUserId",
                table: "notification_preferences",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_UserId",
                table: "notification_preferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_UserId_StationIcao",
                table: "notification_preferences",
                columns: new[] { "UserId", "StationIcao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pilot_profiles_BaseAirportIcao",
                table: "pilot_profiles",
                column: "BaseAirportIcao");

            migrationBuilder.CreateIndex(
                name: "IX_pilot_profiles_UserId",
                table: "pilot_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_ExpiresAt",
                table: "refresh_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_runways_AirportIdent",
                table: "runways",
                column: "AirportIdent");

            migrationBuilder.CreateIndex(
                name: "IX_taf_history_IssueTime",
                table: "taf_history",
                column: "IssueTime");

            migrationBuilder.CreateIndex(
                name: "IX_taf_history_StationId",
                table: "taf_history",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_user_bookmarks_ApplicationUserId",
                table: "user_bookmarks",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_bookmarks_StationIcao",
                table: "user_bookmarks",
                column: "StationIcao");

            migrationBuilder.CreateIndex(
                name: "IX_user_bookmarks_UserId",
                table: "user_bookmarks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_bookmarks_UserId_StationIcao",
                table: "user_bookmarks",
                columns: new[] { "UserId", "StationIcao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "magic_link_tokens");

            migrationBuilder.DropTable(
                name: "metar_history");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "pilot_profiles");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "runways");

            migrationBuilder.DropTable(
                name: "taf_history");

            migrationBuilder.DropTable(
                name: "user_bookmarks");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "airports");
        }
    }
}
