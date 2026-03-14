using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MetarPulse.Infrastructure.AirportDb;

/// <summary>
/// Aylık arka plan servisi — OurAirports verilerini periyodik günceller.
/// Delta update: sadece değişen meydanlar güncellenir.
/// </summary>
public class AirportSyncService : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromDays(30);
    private const string AirportsCsvUrl = "https://davidmegginson.github.io/ourairports-data/airports.csv";
    private const string RunwaysCsvUrl = "https://davidmegginson.github.io/ourairports-data/runways.csv";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AirportSyncService> _logger;
    private readonly HttpClient _httpClient;

    public AirportSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<AirportSyncService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AirportSync");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Uygulama başladığında seed kontrolü yap
        await SeedOnStartupAsync(stoppingToken);

        // Sonra aylık döngüye gir
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(SyncInterval, stoppingToken);

            if (!stoppingToken.IsCancellationRequested)
                await SyncAsync(stoppingToken);
        }
    }

    private async Task SeedOnStartupAsync(CancellationToken stoppingToken)
    {
        // Seed için bağımsız CTS kullan — uygulama durdurma sinyali büyük CSV
        // indirmelerini yarıda kesmemeli. 10 dakika yeterli üst sınır.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var ct = cts.Token;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seeder = new AirportDbSeeder(db, _httpClient,
                scope.ServiceProvider.GetRequiredService<ILogger<AirportDbSeeder>>());

            await seeder.SeedIfEmptyAsync(ct);

            // MagneticVariation sonradan eklendi — NULL olan meydanları güncelle
            var needsBackfill = await db.Airports.AnyAsync(a => a.MagneticVariation == null, ct);
            if (needsBackfill)
            {
                _logger.LogInformation("MagneticVariation backfill başlıyor...");
                await BackfillMagneticVariationAsync(db, ct);
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Meydan seed işlemi 10 dakika zaman aşımına uğradı. Uygulama yeniden başlatıldığında seed tekrar denenecek.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meydan seed işlemi sırasında hata oluştu.");
        }
    }

    private async Task BackfillMagneticVariationAsync(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await using var stream = await _httpClient.GetStreamAsync(AirportsCsvUrl, ct);
            var incoming = OurAirportsCsvParser.ParseAirports(stream)
                .Where(a => a.MagneticVariation.HasValue)
                .ToDictionary(a => a.Ident);

            var airports = await db.Airports
                .Where(a => a.MagneticVariation == null)
                .ToListAsync(ct);

            int updated = 0;
            foreach (var airport in airports)
            {
                if (incoming.TryGetValue(airport.Ident, out var src) && src.MagneticVariation.HasValue)
                {
                    airport.MagneticVariation = src.MagneticVariation;
                    updated++;
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("MagneticVariation backfill tamamlandı: {Count} meydan güncellendi.", updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MagneticVariation backfill sırasında hata oluştu.");
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("Aylık meydan senkronizasyonu başlıyor...");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await SyncAirportsAsync(db, ct);
            await SyncRunwaysAsync(db, ct);

            _logger.LogInformation("Aylık meydan senkronizasyonu tamamlandı.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meydan senkronizasyonu sırasında hata oluştu.");
        }
    }

    private async Task SyncAirportsAsync(AppDbContext db, CancellationToken ct)
    {
        await using var stream = await _httpClient.GetStreamAsync(AirportsCsvUrl, ct);
        var incoming = OurAirportsCsvParser.ParseAirports(stream).ToDictionary(a => a.Ident);

        var existing = await db.Airports
            .ToDictionaryAsync(a => a.Ident, ct);

        int added = 0, updated = 0;

        foreach (var (ident, airport) in incoming)
        {
            if (!existing.TryGetValue(ident, out var dbAirport))
            {
                await db.Airports.AddAsync(airport, ct);
                added++;
            }
            else if (IsAirportChanged(dbAirport, airport))
            {
                dbAirport.Name = airport.Name;
                dbAirport.Type = airport.Type;
                dbAirport.LatitudeDeg = airport.LatitudeDeg;
                dbAirport.LongitudeDeg = airport.LongitudeDeg;
                dbAirport.ElevationFt = airport.ElevationFt;
                dbAirport.Municipality = airport.Municipality;
                dbAirport.IataCode = airport.IataCode;
                dbAirport.MagneticVariation = airport.MagneticVariation;
                dbAirport.LastSynced = DateTime.UtcNow;
                updated++;
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Airport sync: {Added} eklendi, {Updated} güncellendi.", added, updated);
    }

    private async Task SyncRunwaysAsync(AppDbContext db, CancellationToken ct)
    {
        await using var stream = await _httpClient.GetStreamAsync(RunwaysCsvUrl, ct);
        var incoming = OurAirportsCsvParser.ParseRunways(stream).ToList();

        // Var olan tüm pistleri sil ve yeniden ekle (pistler nadiren değişir ama basit yol)
        var validIdents = await db.Airports.Select(a => a.Ident).ToHashSetAsync(ct);
        var validRunways = incoming.Where(r => validIdents.Contains(r.AirportIdent)).ToList();

        db.Runways.RemoveRange(db.Runways);
        await db.SaveChangesAsync(ct);

        for (int i = 0; i < validRunways.Count; i += 1000)
        {
            var batch = validRunways.Skip(i).Take(1000);
            await db.Runways.AddRangeAsync(batch, ct);
            await db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Runway sync: {Count} pist yenilendi.", validRunways.Count);
    }

    private static bool IsAirportChanged(Core.Models.Airport existing, Core.Models.Airport incoming)
        => existing.Name != incoming.Name
        || existing.Type != incoming.Type
        || Math.Abs(existing.LatitudeDeg - incoming.LatitudeDeg) > 0.0001
        || existing.IataCode != incoming.IataCode;
}
