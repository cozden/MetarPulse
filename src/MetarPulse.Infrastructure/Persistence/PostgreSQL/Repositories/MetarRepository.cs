using MetarPulse.Abstractions.Repositories;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Repositories;

public class MetarRepository : GenericRepository<Metar>, IMetarRepository
{
    public MetarRepository(AppDbContext context) : base(context) { }

    public async Task<Metar?> GetLatestAsync(string icaoCode, CancellationToken ct = default)
        => await _dbSet
            .Where(m => m.StationId == icaoCode.ToUpper())
            .OrderByDescending(m => m.ObservationTime)
            .FirstOrDefaultAsync(ct);

    public async Task<List<Metar>> GetHistoryAsync(
        string icaoCode, int hours = 24, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddHours(-hours);
        return await _dbSet
            .Where(m => m.StationId == icaoCode.ToUpper() && m.ObservationTime >= since)
            .OrderByDescending(m => m.ObservationTime)
            .ToListAsync(ct);
    }

    public async Task<List<Metar>> GetLatestForStationsAsync(
        IEnumerable<string> icaoCodes, CancellationToken ct = default)
    {
        var codes = icaoCodes.Select(c => c.ToUpper()).ToList();

        // Her istasyon için en son METAR'ı çek
        return await _dbSet
            .Where(m => codes.Contains(m.StationId))
            .GroupBy(m => m.StationId)
            .Select(g => g.OrderByDescending(m => m.ObservationTime).First())
            .ToListAsync(ct);
    }
}
