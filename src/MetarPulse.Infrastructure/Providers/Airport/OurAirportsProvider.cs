using MetarPulse.Abstractions.Providers;
using MetarPulse.Abstractions.Repositories;
using MetarPulse.Infrastructure.AirportDb;
using MetarPulse.Core.Models;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using AirportModel = MetarPulse.Core.Models.Airport;

namespace MetarPulse.Infrastructure.Providers.AirportData;

/// <summary>
/// IAirportDataProvider implementasyonu — OurAirports verilerini kullanır.
/// Veri PostgreSQL'den okunur (AirportDbSeeder ile yüklenmiş).
/// </summary>
public class OurAirportsProvider : IAirportDataProvider
{
    public string ProviderName => "OurAirports";

    private readonly IAirportRepository _repo;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OurAirportsProvider> _logger;

    public OurAirportsProvider(
        IAirportRepository repo,
        IHttpClientFactory httpClientFactory,
        ILogger<OurAirportsProvider> logger)
    {
        _repo = repo;
        _httpClient = httpClientFactory.CreateClient("AirportSync");
        _logger = logger;
    }

    public async Task<AirportModel?> GetAirportAsync(string icaoCode, CancellationToken ct = default)
        => await _repo.GetByIcaoAsync(icaoCode, ct);

    public async Task<List<Runway>> GetRunwaysAsync(string icaoCode, CancellationToken ct = default)
    {
        var airport = await _repo.GetWithRunwaysAsync(icaoCode, ct);
        return airport?.Runways?.ToList() ?? new List<Runway>();
    }

    public async Task<List<AirportModel>> SearchAirportsAsync(
        string query, int limit = 20, CancellationToken ct = default)
        => await _repo.SearchAsync(query, limit, ct);

    public async Task<SyncResult> SyncAllAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("OurAirports manuel sync başlatıldı...");

            await using var airportStream = await _httpClient.GetStreamAsync(
                "https://davidmegginson.github.io/ourairports-data/airports.csv", ct);
            var airports = OurAirportsCsvParser.ParseAirports(airportStream).ToList();

            await _repo.BulkUpsertAsync(airports, ct);

            _logger.LogInformation("OurAirports sync tamamlandı: {Count} meydan.", airports.Count);
            return new SyncResult(airports.Count, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OurAirports sync hatası.");
            return new SyncResult(0, 0, 1, ex.Message);
        }
    }
}
