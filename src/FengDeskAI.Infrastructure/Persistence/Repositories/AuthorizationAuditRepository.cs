using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public sealed class AuthorizationAuditRepository : IAuthorizationAuditRepository
{
    private readonly AppDbContext _context;
    public AuthorizationAuditRepository(AppDbContext context) => _context = context;

    public Task AddAsync(AuthorizationAuditLog log, CancellationToken ct = default)
        => _context.AuthorizationAuditLogs.AddAsync(log, ct).AsTask();

    public async Task<(List<AuthorizationAuditLog> Items, int Total)> GetByResourceAsync(
        string resourceType, Guid resourceId, int skip, int take, CancellationToken ct = default)
    {
        var query = _context.AuthorizationAuditLogs.AsNoTracking()
            .Where(x => x.ResourceType == resourceType && x.ResourceId == resourceId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }
}
