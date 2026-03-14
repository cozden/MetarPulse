using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Repositories;

public interface IAirportRepository : IRepository<Airport>
{
    Task<Airport?> GetByIcaoAsync(string icaoCode, CancellationToken ct = default);
    Task<Airport?> GetWithRunwaysAsync(string icaoCode, CancellationToken ct = default);
    Task<List<Airport>> SearchAsync(string query, int limit = 20, CancellationToken ct = default);
    Task<List<Airport>> GetByCountryAsync(string isoCountry, CancellationToken ct = default);
    Task BulkUpsertAsync(IEnumerable<Airport> airports, CancellationToken ct = default);
}
