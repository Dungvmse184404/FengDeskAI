using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IReturnRepository : IGenericRepository<ReturnRequest>
{
    /// <summary>Yêu cầu trả hàng (tracked) kèm Items→OrderItem, Delivery, Order, Refund, StatusLogs — để cập nhật trạng thái.</summary>
    Task<ReturnRequest?> GetWithGraphAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chi tiết yêu cầu (AsNoTracking) kèm Items→OrderItem, Images, StatusLogs, Refund. Lọc theo customer nếu truyền.</summary>
    Task<ReturnRequest?> GetDetailAsync(Guid id, Guid? customerId, CancellationToken ct = default);

    Task<(List<ReturnRequest> Items, int Total)> GetByCustomerAsync(Guid customerId, int skip, int take, CancellationToken ct = default);

    /// <summary>Yêu cầu trả của các delivery thuộc một store (màn vendor).</summary>
    Task<(List<ReturnRequest> Items, int Total)> GetForStoreAsync(Guid storeId, int skip, int take, CancellationToken ct = default);

    /// <summary>Tất cả yêu cầu (màn admin).</summary>
    Task<(List<ReturnRequest> Items, int Total)> GetAllPagedAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>Delivery kèm Order + Items (OrderItems, tracked) — kiểm tra điều kiện trả & quyền sở hữu.</summary>
    Task<Delivery?> GetDeliveryForReturnAsync(Guid deliveryId, CancellationToken ct = default);

    /// <summary>Tổng số lượng đã yêu cầu trả (loại trừ yêu cầu đã Hủy/Từ chối) theo từng OrderItem — chặn trả vượt số đã mua.</summary>
    Task<Dictionary<Guid, int>> GetReturnedQuantitiesAsync(IEnumerable<Guid> orderItemIds, CancellationToken ct = default);

    /// <summary>ProductItem kèm Product (để biết store/giá/tồn) — dùng cho đổi hàng & hoàn kho.</summary>
    Task<List<ProductItem>> GetProductItemsWithProductAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>Thêm lệnh hoàn tiền tường minh vào context (đảm bảo Added → INSERT).</summary>
    Task AddRefundAsync(Refund refund, CancellationToken ct = default);

    /// <summary>Thêm status log tường minh qua DbSet (Added → INSERT) — tránh EF đánh Modified khi
    /// add qua navigation vào ReturnRequest đã-tracked (BaseEntity set sẵn Id).</summary>
    void AddStatusLog(ReturnStatusLog log);

    /// <summary>Thêm ảnh bằng chứng tường minh qua DbSet (Added → INSERT).</summary>
    Task AddImageAsync(ReturnRequestImage image, CancellationToken ct = default);

    /// <summary>Ảnh (tracked) theo id, scope theo yêu cầu — để xóa.</summary>
    Task<ReturnRequestImage?> GetImageAsync(Guid returnRequestId, Guid imageId, CancellationToken ct = default);

    /// <summary>Xóa ảnh (soft-delete qua SaveChanges override).</summary>
    void RemoveImage(ReturnRequestImage image);
}
