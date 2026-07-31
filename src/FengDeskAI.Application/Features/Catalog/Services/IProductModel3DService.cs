using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Quản lý model 3D "hiện tại" của sản phẩm (<c>ProductModel3D</c>, 1–1) + tạo yêu cầu sinh/tạo lại
/// (<c>Model3DRequest</c>, n–1, xem <see cref="IModel3DRequestService"/> cho phần staff sàn xử lý
/// thủ công). Xem docs/adr/refactor-model3d-request-flow.md.
/// </summary>
public interface IProductModel3DService
{
    /// <summary>Đọc trạng thái/kết quả model 3D hiện tại của sản phẩm (public).</summary>
    Task<IServiceResult<ProductModel3DResponse>> GetAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Tạo yêu cầu sinh model 3D. Product chưa có model → Initial (tự động, worker xử lý ngay).
    /// Product đã có model → Regenerate (vào hàng chờ, chỉ staff sàn xử lý thủ công). Chặn nếu
    /// đang có request khác chưa xử lý xong (409).
    /// </summary>
    Task<IServiceResult<Model3DRequestResponse>> RequestAsync(
        Guid productId, Guid userId, bool isAdmin, RequestModel3DRequest request, CancellationToken ct = default);

    /// <summary>Lịch sử request của 1 sản phẩm (owner/garden staff) — trạng thái đã che giấu lỗi hết credit.</summary>
    Task<IServiceResult<List<Model3DRequestResponse>>> ListRequestsAsync(
        Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Bật/tắt hiển thị model 3D trên trang sản phẩm — độc lập với dữ liệu model đã sinh.</summary>
    Task<IServiceResult> ToggleAsync(Guid productId, Guid userId, bool isAdmin, bool isEnabled, CancellationToken ct = default);

    /// <summary>Xóa model 3D của sản phẩm (kèm xóa file trên storage best-effort).</summary>
    Task<IServiceResult> DeleteAsync(Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Worker nền gọi định kỳ: gửi Meshy cho các request Initial đang <c>Queued</c> đến hạn thử
    /// (kể cả retry sau backoff do hết credit), rồi poll các request đang <c>Processing</c> để
    /// hoàn tất (tải GLB → re-host storage → ghi <c>ProductModel3D</c>) hoặc đánh dấu Failed.
    /// </summary>
    Task ProcessInitialQueueAsync(CancellationToken ct = default);
}
