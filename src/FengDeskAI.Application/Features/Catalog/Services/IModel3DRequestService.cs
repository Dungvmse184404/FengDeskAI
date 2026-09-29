using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Xử lý thủ công hàng chờ Regenerate — chỉ staff sàn (<c>UserRole.Staff</c> trở lên,
/// policy <c>StaffOrAbove</c>). KHÔNG có bước claim/khóa: bất kỳ staff nào cũng gọi được mọi
/// action, <c>AssignedStaffId</c> chỉ mang tính audit "ai làm gần nhất".
/// </summary>
public interface IModel3DRequestService
{
    /// <summary>Hàng chờ cho staff sàn — lọc theo trạng thái và/hoặc lý do lỗi nội bộ.</summary>
    Task<IServiceResult<Model3DRequestQueueResponse>> GetQueueAsync(
        Model3DRequestStatus? status, Model3DFailureReason? reason, int skip, int take, CancellationToken ct = default);

    /// <summary>Chọn ảnh (có sẵn + upload mới, 1–4 ảnh) rồi gửi task tới Meshy lần đầu cho 1 request Regenerate.</summary>
    Task<IServiceResult<Model3DRequestQueueItemResponse>> GenerateAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, CancellationToken ct = default);

    /// <summary>Chưa ưng ý kết quả trước — chọn lại ảnh, gửi lại Meshy. Không giới hạn số lần.</summary>
    Task<IServiceResult<Model3DRequestQueueItemResponse>> RetryAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, CancellationToken ct = default);

    /// <summary>Xem trước kết quả Meshy hiện tại (live poll, không lưu) — để staff quyết định accept hay retry.</summary>
    Task<IServiceResult<Model3DPreviewResponse>> PreviewAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>Download a completed preview through the staff API without accepting or storing it.</summary>
    Task<IServiceResult<Stream>> DownloadPreviewAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>Ưng ý kết quả — tải GLB từ Meshy, re-host storage, ghi đè <c>ProductModel3D</c> hiện tại.</summary>
    Task<IServiceResult<ProductModel3DResponse>> AcceptAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default);

    /// <summary>Từ chối xử lý hẳn request này.</summary>
    Task<IServiceResult> RejectAsync(Guid requestId, Guid staffUserId, string reason, CancellationToken ct = default);
}
