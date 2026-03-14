using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Repositories;

public interface IMetarRepository : IRepository<Metar>
{
    Task<Metar?> GetLatestAsync(string icaoCode, CancellationToken ct = default);
    Task<List<Metar>> GetHistoryAsync(string icaoCode, int hours = 24, CancellationToken ct = default);
    Task<List<Metar>> GetLatestForStationsAsync(IEnumerable<string> icaoCodes, CancellationToken ct = default);
}
