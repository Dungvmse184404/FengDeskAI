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

    /// <summary>Ticket đang chờ Staff xử lý (Requested/UnderReview/Reviewing) — hàng đợi của Staff.</summary>
    Task<(List<ReturnRequest> Items, int Total)> GetPendingForStaffAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>Ticket ở NeedMoreEvidence đã quá evidence_deadline — worker auto-reject.</summary>
    Task<List<ReturnRequest>> GetOverdueEvidenceTicketsAsync(DateTime nowUtc, int max, CancellationToken ct = default);

    // ----- Refund (sub-saga) -----

    /// <summary>Lệnh hoàn tiền (tracked) kèm ReturnRequest — để cập nhật trạng thái/ hoàn tất.</summary>
    Task<Refund?> GetRefundByIdAsync(Guid refundId, CancellationToken ct = default);

    /// <summary>Lệnh hoàn tiền theo idempotency key (chống tạo trùng).</summary>
    Task<Refund?> GetRefundByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>Lệnh hoàn tiền theo mã tham chiếu cổng (xử lý webhook).</summary>
    Task<Refund?> GetRefundByProviderRefAsync(string providerRefundId, CancellationToken ct = default);

    /// <summary>Refund cần Manager để mắt (Failed hoặc ManagerReview) — màn Manager.</summary>
    Task<(List<Refund> Items, int Total)> GetRefundsForManagerAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>Refund Failed còn lượt retry (&lt; maxRetry) — worker auto-retry.</summary>
    Task<List<Refund>> GetRetryableFailedRefundsAsync(int maxRetry, int max, CancellationToken ct = default);

    // ----- Vendor liability (công nợ) -----

    Task AddVendorLiabilityAsync(VendorLiability liability, CancellationToken ct = default);

    /// <summary>Công nợ (tracked) kèm ReturnRequest — để phán quyết dispute.</summary>
    Task<VendorLiability?> GetVendorLiabilityAsync(Guid id, CancellationToken ct = default);

    Task<(List<VendorLiability> Items, int Total)> GetLiabilitiesByGardenAsync(Guid gardenId, int skip, int take, CancellationToken ct = default);

    /// <summary>Công nợ Pending đã quá dispute_deadline — worker auto-settle.</summary>
    Task<List<VendorLiability>> GetOverdueLiabilitiesAsync(DateTime nowUtc, int max, CancellationToken ct = default);

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
