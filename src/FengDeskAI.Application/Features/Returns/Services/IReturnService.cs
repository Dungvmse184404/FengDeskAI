using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Returns.DTOs;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>
/// Luồng RMA v2 (ticket trả hàng / hoàn tiền / đổi trả).
/// - Customer: tạo ticket + bằng chứng, bổ sung bằng chứng, hủy, theo dõi.
/// - Staff (nền tảng): tiếp nhận & RA QUYẾT ĐỊNH (duyệt hoàn/đổi, từ chối, yêu cầu bổ sung). Vendor KHÔNG quyết.
/// - Vendor (garden owner): góp ý trong SLA (acknowledge/dispute, non-blocking) + xác nhận đã nhận hàng.
/// Quy tắc chuyển trạng thái ở <c>Domain.StateMachines.ReturnStateMachine</c>; transition sai → 409.
/// </summary>
public interface IReturnService
{
    // ----- Customer -----
    /// <summary>Tạo ticket RMA. Ảnh bằng chứng truyền qua <c>request.ImageUrls</c> (upload trước bằng POST /api/uploads) — bắt buộc ≥1.</summary>
    Task<IServiceResult<ReturnDetailResponse>> CreateAsync(Guid userId, CreateReturnRequest request, CancellationToken ct = default);
    Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> GetByIdAsync(Guid id, RmaActor actor, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> CancelAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ResubmitEvidenceAsync(Guid id, Guid userId, IReadOnlyList<ReturnImageFile> files, CancellationToken ct = default);

    Task<IServiceResult<ReturnDetailResponse>> UploadImagesAsync(Guid id, Guid userId, IReadOnlyList<ReturnImageFile> files, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> DeleteImageAsync(Guid id, Guid imageId, Guid userId, CancellationToken ct = default);

    // ----- Vendor (non-blocking) -----
    Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetForStoreAsync(Guid storeId, RmaActor actor, PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> VendorAcknowledgeAsync(Guid id, RmaActor actor, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> VendorDisputeAsync(Guid id, RmaActor actor, VendorDisputeRequest request, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ConfirmItemReceivedAsync(Guid id, RmaActor actor, CancellationToken ct = default);

    // ----- Staff (decision) -----
    Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetPendingForStaffAsync(PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetAllAsync(PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> AcceptAsync(Guid id, RmaActor actor, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> RequestMoreEvidenceAsync(Guid id, RmaActor actor, RequestMoreEvidenceRequest request, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ApproveRefundAsync(Guid id, RmaActor actor, ApproveRefundRequest request, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> ApproveExchangeAsync(Guid id, RmaActor actor, ApproveExchangeRequest request, CancellationToken ct = default);
    Task<IServiceResult<ReturnDetailResponse>> RejectAsync(Guid id, RmaActor actor, RejectReturnRequest request, CancellationToken ct = default);

    /// <summary>Worker: auto-reject các ticket ở NeedMoreEvidence quá evidence_deadline. Trả số đã xử lý.</summary>
    Task<int> AutoRejectOverdueEvidenceAsync(CancellationToken ct = default);
}
