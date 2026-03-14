using MetarPulse.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Infrastructure.Persistence.SQLite;

/// <summary>
/// Cihaz üzerinde çalışan hafif SQLite bağlamı.
/// Offline cache + hızlı erişim için kullanılır.
/// Backend API'den çekilen veriler buraya yazılır.
/// </summary>
public class LocalDbContext : DbContext
{
    // Arama + pist bilgileri
    public DbSet<Airport> AirportsCache => Set<Airport>();
    public DbSet<Runway> RunwaysCache => Set<Runway>();

    // Son bilinen METAR (meydan başına tek kayıt)
    public DbSet<CachedMetar> LastMetarCache => Set<CachedMetar>();

    // Kullanıcı tercihleri (offline, senkronize edilmez)
    public DbSet<LocalPreference> UserPreferences => Set<LocalPreference>();

    // Bookmark listesi (backend sync öncesi yerel kopya)
    public DbSet<LocalBookmark> BookmarksCache => Set<LocalBookmark>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            // MAUI: FileSystem.AppDataDirectory
            // Test: geçici yol
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "metarpulse_local.db");
            options.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Airport>(b =>
        {
            b.ToTable("airports_cache");
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.Ident).IsUnique();
            b.HasIndex(a => a.IsoCountry);
            b.HasMany(a => a.Runways)
             .WithOne(r => r.Airport)
             .HasForeignKey(r => r.AirportIdent)
             .HasPrincipalKey(a => a.Ident);
        });

        modelBuilder.Entity<Runway>(b =>
        {
            b.ToTable("runways_cache");
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.AirportIdent);
        });

        modelBuilder.Entity<CachedMetar>(b =>
        {
            b.ToTable("last_metar_cache");
            b.HasKey(m => m.StationIcao);
        });

        modelBuilder.Entity<LocalBookmark>(b =>
        {
            b.ToTable("bookmarks_cache");
            b.HasKey(bm => bm.StationIcao);
        });

        modelBuilder.Entity<LocalPreference>(b =>
        {
            b.ToTable("user_preferences");
            b.HasKey(p => p.Key);
        });
    }
}

/// <summary>Son bilinen METAR — meydan başına bir kayıt.</summary>
public class CachedMetar
{
    public string StationIcao { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public DateTime ObservationTime { get; set; }
    public string CategoryName { get; set; } = string.Empty;    // "VFR", "IFR" vb.
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Offline bookmark — sync beklerken yerel kopya.</summary>
public class LocalBookmark
{
    public string StationIcao { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPendingSync { get; set; } = false;
}

/// <summary>Cihaz üzerindeki kullanıcı tercihleri (key-value).</summary>
public class LocalPreference
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
