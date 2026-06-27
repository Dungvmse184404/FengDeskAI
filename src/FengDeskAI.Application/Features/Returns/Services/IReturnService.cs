using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Returns.DTOs;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>
/// Luồng trả hàng / hoàn tiền / đổi trả (RMA). Customer tạo & theo dõi; Vendor (store) duyệt &amp;
/// xử lý các yêu cầu của delivery thuộc store mình; Admin giám sát toàn bộ &amp; xác nhận hoàn tiền.
/// </summary>
public interface IReturnService
{
    // ----- Customer -----
    Task<IServiceResult<ReturnDetailResponse>> CreateAsync(Guid userId, CreateReturnRequest request, CancellationToken ct = default);
    Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> GetByIdAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> CancelAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ShipBackAsync(Guid id, Guid userId, ShipBackRequest request, CancellationToken ct = default);

    /// <summary>Khách tải ảnh bằng chứng (tệp) lên yêu cầu của mình — upload storage rồi gắn URL.</summary>
    Task<IServiceResult<ReturnDetailResponse>> UploadImagesAsync(Guid id, Guid userId, IReadOnlyList<ReturnImageFile> files, CancellationToken ct = default);

    /// <summary>Khách xóa một ảnh bằng chứng — chỉ khi yêu cầu còn ở trạng thái chờ duyệt (Requested).</summary>
    Task<IServiceResult<ReturnDetailResponse>> DeleteImageAsync(Guid id, Guid imageId, Guid userId, CancellationToken ct = default);

    // ----- Vendor / Admin -----
    Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetForStoreAsync(Guid storeId, Guid userId, bool isAdmin, PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetAllAsync(PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ApproveAsync(Guid id, Guid userId, bool isAdmin, ApproveReturnRequest request, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> RejectAsync(Guid id, Guid userId, bool isAdmin, RejectReturnRequest request, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ReceiveAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ResolveAsync(Guid id, Guid userId, bool isAdmin, ResolveReturnRequest request, CancellationToken ct = default);

    // ----- Admin / Finance -----
    Task<IServiceResult<ReturnDetailResponse>> CompleteRefundAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);
}
