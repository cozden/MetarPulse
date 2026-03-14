using MetarPulse.Abstractions.Repositories;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Repositories;

public class AirportRepository : GenericRepository<Airport>, IAirportRepository
{
    public AirportRepository(AppDbContext context) : base(context) { }

    public async Task<Airport?> GetByIcaoAsync(string icaoCode, CancellationToken ct = default)
        => await _dbSet
            .FirstOrDefaultAsync(a => a.Ident == icaoCode.ToUpper(), ct);

    public async Task<Airport?> GetWithRunwaysAsync(string icaoCode, CancellationToken ct = default)
        => await _dbSet
            .Include(a => a.Runways.Where(r => !r.IsClosed))
            .FirstOrDefaultAsync(a => a.Ident == icaoCode.ToUpper(), ct);

    public async Task<List<Airport>> SearchAsync(
        string query, int limit = 20, CancellationToken ct = default)
    {
        var upper = query.ToUpper();
        return await _dbSet
            .Where(a =>
                (a.Type == "large_airport" || a.Type == "medium_airport" || a.Type == "small_airport") &&
                (a.Ident.Contains(upper) ||
                 a.Name.ToUpper().Contains(upper) ||
                 (a.IataCode != null && a.IataCode.Contains(upper)) ||
                 (a.Municipality != null && a.Municipality.ToUpper().Contains(upper))))
            .OrderByDescending(a =>
                a.Type == "large_airport" ? 3 :
                a.Type == "medium_airport" ? 2 : 1)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<Airport>> GetByCountryAsync(
        string isoCountry, CancellationToken ct = default)
        => await _dbSet
            .Where(a => a.IsoCountry == isoCountry.ToUpper())
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

    public async Task BulkUpsertAsync(
        IEnumerable<Airport> airports, CancellationToken ct = default)
    {
        foreach (var airport in airports)
        {
            var existing = await _dbSet
                .FirstOrDefaultAsync(a => a.Ident == airport.Ident, ct);

            if (existing == null)
                await _dbSet.AddAsync(airport, ct);
            else
            {
                existing.Name = airport.Name;
                existing.Type = airport.Type;
                existing.LatitudeDeg = airport.LatitudeDeg;
                existing.LongitudeDeg = airport.LongitudeDeg;
                existing.ElevationFt = airport.ElevationFt;
                existing.IsoCountry = airport.IsoCountry;
                existing.Municipality = airport.Municipality;
                existing.IataCode = airport.IataCode;
                existing.MagneticVariation = airport.MagneticVariation;
                existing.LastSynced = DateTime.UtcNow;
                _dbSet.Update(existing);
            }
        }
    }
}
