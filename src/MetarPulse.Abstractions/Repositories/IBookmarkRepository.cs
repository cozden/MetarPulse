using MetarPulse.Core.Models;

namespace MetarPulse.Abstractions.Repositories;

public interface IBookmarkRepository : IRepository<UserBookmark>
{
    Task<List<UserBookmark>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<UserBookmark?> GetByUserAndStationAsync(string userId, string icaoCode, CancellationToken ct = default);
    Task<bool> ExistsAsync(string userId, string icaoCode, CancellationToken ct = default);
    Task<List<string>> GetStationIcaosAsync(string userId, CancellationToken ct = default);
}
