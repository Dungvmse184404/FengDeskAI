using FengDeskAI.Domain.Entities.Payment;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    /// <summary>Giao dịch theo orderCode PayOS, kèm Order + Deliveries + Items.ProductItem.Product
    /// (để tạo delivery theo store + shipment khi paid).</summary>
    Task<Transaction?> GetByOrderCodeAsync(long orderCode, CancellationToken ct = default);

    Task<Transaction?> GetLatestByOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Các giao dịch còn Pending của một order (tracked) — dùng khi hủy / hết hạn đơn.</summary>
    Task<List<Transaction>> GetPendingByOrderAsync(Guid orderId, CancellationToken ct = default);

    Task<bool> HasPaidAsync(Guid orderId, CancellationToken ct = default);
}
