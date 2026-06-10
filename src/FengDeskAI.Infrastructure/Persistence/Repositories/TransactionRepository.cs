using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
{
    public TransactionRepository(AppDbContext context) : base(context) { }

    public Task<Transaction?> GetByOrderCodeAsync(long orderCode, CancellationToken ct = default)
        => _set.Include(t => t.Order).ThenInclude(o => o.Deliveries)
               .Include(t => t.Order).ThenInclude(o => o.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product)
               .FirstOrDefaultAsync(t => t.OrderCode == orderCode, ct);

    public Task<Transaction?> GetLatestByOrderAsync(Guid orderId, CancellationToken ct = default)
        => _set.Where(t => t.OrderId == orderId)
               .OrderByDescending(t => t.CreatedAt)
               .FirstOrDefaultAsync(ct);

    public Task<List<Transaction>> GetPendingByOrderAsync(Guid orderId, CancellationToken ct = default)
        => _set.Where(t => t.OrderId == orderId && t.Status == PaymentStatus.Pending)
               .ToListAsync(ct);

    public Task<bool> HasPaidAsync(Guid orderId, CancellationToken ct = default)
        => _set.AnyAsync(t => t.OrderId == orderId && t.Status == PaymentStatus.Paid, ct);
}
