using MetarPulse.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL;

/// <summary>
/// Ana PostgreSQL veritabanı bağlamı.
/// IdentityDbContext<ApplicationUser> — ASP.NET Identity tablolarını da yönetir.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Havacılık verileri
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Runway> Runways => Set<Runway>();
    public DbSet<Metar> MetarHistory => Set<Metar>();
    public DbSet<Taf> TafHistory => Set<Taf>();

    // NOTAM verileri
    public DbSet<Notam> Notams => Set<Notam>();

    // Admin ayarları
    public DbSet<ProviderSettingOverride> ProviderSettingOverrides => Set<ProviderSettingOverride>();

    // Kullanıcı verileri
    public DbSet<PilotProfile> PilotProfiles => Set<PilotProfile>();
    public DbSet<UserBookmark> UserBookmarks => Set<UserBookmark>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Identity tablolarını konfigure eder

        // Tüm entity konfigürasyonlarını bu assembly'den otomatik uygula
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
