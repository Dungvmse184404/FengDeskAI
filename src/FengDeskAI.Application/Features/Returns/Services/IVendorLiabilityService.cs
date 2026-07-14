using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>
/// Công nợ vendor — cơ chế bù trừ payout SAU khi khách đã nhận tiền, KHÔNG ảnh hưởng khách.
/// Vendor dispute trong hạn; Manager phán quyết; quá hạn worker tự chốt.
/// </summary>
public interface IVendorLiabilityService
{
    /// <summary>Tạo công nợ khi refund đạt Completed (PHẢI gọi trong transaction hoàn tất refund).</summary>
    Task CreateForRefundAsync(ReturnRequest ticket, Refund refund, CancellationToken ct = default);

    Task<IServiceResult<PagedResult<VendorLiabilityResponse>>> GetByGardenAsync(Guid gardenId, RmaActor actor, PageRequest page, CancellationToken ct = default);

    /// <summary>Vendor phản đối khoản trừ (trong dispute_deadline).</summary>
    Task<IServiceResult<VendorLiabilityResponse>> DisputeAsync(Guid liabilityId, RmaActor actor, VendorDisputeRequest request, CancellationToken ct = default);

    /// <summary>Manager phán quyết dispute: vendor đúng → waived, vendor sai → settled.</summary>
    Task<IServiceResult<VendorLiabilityResponse>> ResolveAsync(Guid liabilityId, RmaActor actor, ResolveLiabilityRequest request, CancellationToken ct = default);

    /// <summary>Worker: auto-settle các công nợ Pending quá hạn dispute. Trả số đã xử lý.</summary>
    Task<int> AutoSettleOverdueAsync(CancellationToken ct = default);
}
