using MetarPulse.Abstractions.Repositories;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Repositories;

public class BookmarkRepository : GenericRepository<UserBookmark>, IBookmarkRepository
{
    public BookmarkRepository(AppDbContext context) : base(context) { }

    public async Task<List<UserBookmark>> GetByUserIdAsync(
        string userId, CancellationToken ct = default)
        => await _dbSet
            .Where(b => b.UserId == userId)
            .Include(b => b.Airport)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public async Task<UserBookmark?> GetByUserAndStationAsync(
        string userId, string icaoCode, CancellationToken ct = default)
        => await _dbSet
            .FirstOrDefaultAsync(b =>
                b.UserId == userId && b.StationIcao == icaoCode.ToUpper(), ct);

    public async Task<bool> ExistsAsync(
        string userId, string icaoCode, CancellationToken ct = default)
        => await _dbSet.AnyAsync(b =>
            b.UserId == userId && b.StationIcao == icaoCode.ToUpper(), ct);

    public async Task<List<string>> GetStationIcaosAsync(
        string userId, CancellationToken ct = default)
        => await _dbSet
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.SortOrder)
            .Select(b => b.StationIcao)
            .ToListAsync(ct);
}
