namespace MetarPulse.Abstractions.Repositories;

/// <summary>
/// Transaction yönetimi — birden fazla repository operasyonunu atomik hale getirir.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IMetarRepository Metars { get; }
    IAirportRepository Airports { get; }
    IBookmarkRepository Bookmarks { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
