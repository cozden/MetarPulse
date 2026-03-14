using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MetarPulse.Infrastructure.AirportDb;

/// <summary>
/// İlk deploy'da OurAirports CSV verilerini PostgreSQL'e yükler.
/// airports tablosu boşsa indirip toplu INSERT yapar (~60K meydan, ~42K pist).
/// </summary>
public class AirportDbSeeder
{
    private const string AirportsCsvUrl = "https://davidmegginson.github.io/ourairports-data/airports.csv";
    private const string RunwaysCsvUrl = "https://davidmegginson.github.io/ourairports-data/runways.csv";
    private const int BulkBatchSize = 1000;

    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AirportDbSeeder> _logger;

    public AirportDbSeeder(AppDbContext db, HttpClient httpClient, ILogger<AirportDbSeeder> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// DB boşsa seed işlemi yapar. Dolu ise atlar.
    /// </summary>
    public async Task SeedIfEmptyAsync(CancellationToken ct = default)
    {
        var hasAirports = await _db.Airports.AnyAsync(ct);
        var hasRunways  = await _db.Runways.AnyAsync(ct);

        if (hasAirports && hasRunways)
        {
            _logger.LogInformation("Meydan ve pist veritabanı zaten dolu, seed atlanıyor.");
            return;
        }

        if (!hasAirports)
        {
            _logger.LogInformation("Meydan veritabanı boş — airports.csv indiriliyor...");
            await SeedAirportsAsync(ct);
        }

        if (!hasRunways)
        {
            _logger.LogInformation("Pist veritabanı boş — runways.csv indiriliyor...");
            await SeedRunwaysAsync(ct);
        }

        _logger.LogInformation("Seed işlemi tamamlandı.");
    }

    private async Task SeedAirportsAsync(CancellationToken ct)
    {
        _logger.LogInformation("airports.csv indiriliyor: {Url}", AirportsCsvUrl);

        await using var stream = await _httpClient.GetStreamAsync(AirportsCsvUrl, ct);
        var airports = OurAirportsCsvParser.ParseAirports(stream).ToList();

        _logger.LogInformation("{Count} meydan parse edildi, DB'ye yazılıyor...", airports.Count);

        for (int i = 0; i < airports.Count; i += BulkBatchSize)
        {
            var batch = airports.Skip(i).Take(BulkBatchSize);
            await _db.Airports.AddRangeAsync(batch, ct);
            await _db.SaveChangesAsync(ct);

            if (i % 10000 == 0)
                _logger.LogInformation("  {Done}/{Total} meydan yazıldı...", i, airports.Count);
        }

        _logger.LogInformation("airports tablosu seed tamamlandı: {Count} kayıt.", airports.Count);
    }

    private async Task SeedRunwaysAsync(CancellationToken ct)
    {
        _logger.LogInformation("runways.csv indiriliyor: {Url}", RunwaysCsvUrl);

        await using var stream = await _httpClient.GetStreamAsync(RunwaysCsvUrl, ct);
        var runways = OurAirportsCsvParser.ParseRunways(stream).ToList();

        // Sadece DB'deki meydanların pistlerini ekle
        var validIdents = await _db.Airports.Select(a => a.Ident).ToHashSetAsync(ct);
        var validRunways = runways.Where(r => validIdents.Contains(r.AirportIdent)).ToList();

        _logger.LogInformation("{Count}/{Total} pist DB'ye yazılıyor...", validRunways.Count, runways.Count);

        for (int i = 0; i < validRunways.Count; i += BulkBatchSize)
        {
            var batch = validRunways.Skip(i).Take(BulkBatchSize);
            await _db.Runways.AddRangeAsync(batch, ct);
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("runways tablosu seed tamamlandı: {Count} kayıt.", validRunways.Count);
    }
}
