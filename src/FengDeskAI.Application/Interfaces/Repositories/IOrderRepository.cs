using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IOrderRepository : IGenericRepository<Order>
{
    /// <summary>Load product items (tracked) theo danh sách id — dùng để hoàn kho khi hủy đơn.</summary>
    Task<List<ProductItem>> GetProductItemsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>Thêm deliveries tường minh vào context (đảm bảo trạng thái Added → INSERT).</summary>
    Task AddDeliveriesAsync(IEnumerable<Delivery> deliveries, CancellationToken ct = default);

    /// <summary>Order của user kèm Items→ProductItem→Product + Deliveries (tracked) — để xử lý thanh toán.</summary>
    Task<Order?> GetForPaymentAsync(Guid id, Guid customerId, CancellationToken ct = default);

    Task<(List<Order> Items, int Total)> GetByCustomerAsync(Guid customerId, int skip, int take, CancellationToken ct = default);

    /// <summary>Tất cả đơn (mọi customer), paged, mới nhất trước — màn admin.</summary>
    Task<(List<Order> Items, int Total)> GetAllAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>Order chi tiết (Items + Deliveries.Store + StatusLogs). Lọc theo customer nếu truyền.</summary>
    Task<Order?> GetDetailAsync(Guid id, Guid? customerId, CancellationToken ct = default);

    /// <summary>Order kèm Deliveries + Items (tracked) — dùng cho hủy đơn / cập nhật.</summary>
    Task<Order?> GetWithGraphAsync(Guid id, Guid? customerId, CancellationToken ct = default);

    /// <summary>Đơn online (không COD) còn Pending tạo trước <paramref name="cutoffUtc"/>, tracked kèm Items + Deliveries — quét hết hạn thanh toán.</summary>
    Task<List<Order>> GetOverduePendingAsync(DateTime cutoffUtc, int take, CancellationToken ct = default);

    /// <summary>Delivery kèm Store + Order.Deliveries (tracked) — vendor cập nhật trạng thái + rollup.</summary>
    Task<Delivery?> GetDeliveryWithOrderAsync(Guid deliveryId, CancellationToken ct = default);

    /// <summary>Danh sách delivery của một store (kèm Order) — màn vendor.</summary>
    Task<(List<Delivery> Items, int Total)> GetDeliveriesForStoreAsync(Guid storeId, int skip, int take, CancellationToken ct = default);
}
