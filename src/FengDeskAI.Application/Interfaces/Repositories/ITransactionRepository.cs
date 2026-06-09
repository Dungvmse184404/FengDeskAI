using FengDeskAI.Domain.Entities.Payment;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    /// <summary>Giao dịch theo orderCode PayOS, kèm Order + Deliveries (để tạo shipment khi paid).</summary>
    Task<Transaction?> GetByOrderCodeAsync(long orderCode, CancellationToken ct = default);

    Task<Transaction?> GetLatestByOrderAsync(Guid orderId, CancellationToken ct = default);

    Task<bool> HasPaidAsync(Guid orderId, CancellationToken ct = default);
}
