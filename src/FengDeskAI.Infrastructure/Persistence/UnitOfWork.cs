using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Infrastructure.Persistence.Contexts;

namespace FengDeskAI.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IWorkspaceProfileRepository workspaceProfiles)
    {
        _context = context;
        Users = users;
        RefreshTokens = refreshTokens;
        WorkspaceProfiles = workspaceProfiles;
    }

    public IUserRepository Users { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IWorkspaceProfileRepository WorkspaceProfiles { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
