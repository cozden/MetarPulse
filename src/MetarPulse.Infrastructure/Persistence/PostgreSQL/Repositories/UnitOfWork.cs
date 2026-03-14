using MetarPulse.Abstractions.Repositories;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore.Storage;

namespace MetarPulse.Infrastructure.Persistence.PostgreSQL.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    private IMetarRepository? _metars;
    private IAirportRepository? _airports;
    private IBookmarkRepository? _bookmarks;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IMetarRepository Metars
        => _metars ??= new MetarRepository(_context);

    public IAirportRepository Airports
        => _airports ??= new AirportRepository(_context);

    public IBookmarkRepository Bookmarks
        => _bookmarks ??= new BookmarkRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _context.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
