using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Quản lý model 3D của sản phẩm (sinh từ ảnh qua Meshy AI). Sinh bất đồng bộ:
/// <see cref="GenerateAsync"/> gửi job + trả Processing ngay; worker nền gọi
/// <see cref="PollPendingAsync"/> định kỳ để hoàn tất (tải GLB → re-host storage).
/// </summary>
public interface IProductModel3DService
{
    /// <summary>Đọc trạng thái/kết quả model 3D của sản phẩm (public).</summary>
    Task<IServiceResult<ProductModel3DResponse>> GetAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Gửi yêu cầu sinh model 3D từ một ảnh của sản phẩm (owner/staff/admin).</summary>
    Task<IServiceResult<ProductModel3DResponse>> GenerateAsync(
        Guid productId, Guid userId, bool isAdmin, GenerateModel3DRequest request, CancellationToken ct = default);

    /// <summary>Xóa model 3D của sản phẩm (kèm xóa file trên storage best-effort).</summary>
    Task<IServiceResult> DeleteAsync(Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Worker nền gọi: poll mọi job đang Processing, hoàn tất hoặc đánh dấu Failed.</summary>
    Task PollPendingAsync(CancellationToken ct = default);
}
