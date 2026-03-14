using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Providers;

/// <summary>
/// Tüm meydan veri kaynakları bu interface'i implement eder.
/// OurAirports, AirportDB.io veya custom CSV — hepsi aynı interface.
/// </summary>
public interface IAirportDataProvider
{
    string ProviderName { get; }        // "OurAirports", "AirportDB", "CustomCSV"

    Task<Airport?> GetAirportAsync(string icaoCode, CancellationToken ct = default);
    Task<List<Runway>> GetRunwaysAsync(string icaoCode, CancellationToken ct = default);
    Task<List<Airport>> SearchAirportsAsync(string query, int limit = 20, CancellationToken ct = default);
    Task<SyncResult> SyncAllAsync(CancellationToken ct = default);
}

public record SyncResult(int Added, int Updated, int Errors, string? ErrorMessage = null);
