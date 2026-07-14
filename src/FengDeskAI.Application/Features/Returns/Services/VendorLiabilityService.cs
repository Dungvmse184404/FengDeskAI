using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.StateMachines;

namespace FengDeskAI.Application.Features.Returns.Services;

public class VendorLiabilityService : IVendorLiabilityService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public VendorLiabilityService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task CreateForRefundAsync(ReturnRequest ticket, Refund refund, CancellationToken ct = default)
    {
        // Đã có công nợ cho ticket này thì bỏ qua (idempotent — tránh trùng khi webhook lặp).
        if (ticket.VendorLiability is not null) return;

        var liability = new VendorLiability
        {
            GardenStoreId = ticket.Delivery.GardenStoreId,
            ReturnRequestId = ticket.Id,
            RefundId = refund.Id,
            Amount = refund.Amount,
            DisputeDeadline = DateTime.UtcNow.AddDays(ReturnWorkflow.LiabilityDisputeWindowDays),
        };
        ticket.VendorLiability = liability;
        await _uow.Returns.AddVendorLiabilityAsync(liability, ct);
    }

    public async Task<IServiceResult<PagedResult<VendorLiabilityResponse>>> GetByGardenAsync(Guid gardenId, RmaActor actor, PageRequest page, CancellationToken ct = default)
    {
        if (!actor.CanManageRefund && !(actor.IsGardenOwner && await _uow.Stores.CanManageAsync(gardenId, actor.UserId, ct)))
            return ServiceResult<PagedResult<VendorLiabilityResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.LiabilityForbidden);

        var (items, total) = await _uow.Returns.GetLiabilitiesByGardenAsync(gardenId, page.Skip, page.PageSize, ct);
        return ServiceResult<PagedResult<VendorLiabilityResponse>>.Success(
            new PagedResult<VendorLiabilityResponse>(_mapper.Map<List<VendorLiabilityResponse>>(items), page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<VendorLiabilityResponse>> DisputeAsync(Guid liabilityId, RmaActor actor, VendorDisputeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.DisputeReasonRequired);

        var liability = await _uow.Returns.GetVendorLiabilityAsync(liabilityId, ct);
        if (liability is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.LiabilityNotFound);

        // Chỉ vendor sở hữu garden mới được phản đối.
        if (!(actor.IsGardenOwner && await _uow.Stores.CanManageAsync(liability.GardenStoreId, actor.UserId, ct)))
            return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.LiabilityForbidden);
        if (DateTime.UtcNow > liability.DisputeDeadline)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.LiabilityDisputeExpired);
        if (!LiabilityStateMachine.CanTransition(liability.Status, Domain.Enums.Payment.VendorLiabilityStatus.Disputed))
            return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.LiabilityNotDisputable);

        liability.Dispute(request.Reason);
        await _uow.SaveChangesAsync(ct);
        return Ok(liability);
    }

    public async Task<IServiceResult<VendorLiabilityResponse>> ResolveAsync(Guid liabilityId, RmaActor actor, ResolveLiabilityRequest request, CancellationToken ct = default)
    {
        if (!actor.CanManageRefund)
            return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManagerOnly);

        var liability = await _uow.Returns.GetVendorLiabilityAsync(liabilityId, ct);
        if (liability is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.LiabilityNotFound);

        var now = DateTime.UtcNow;
        if (request.VendorWins)
        {
            if (!LiabilityStateMachine.CanTransition(liability.Status, Domain.Enums.Payment.VendorLiabilityStatus.Waived))
                return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.LiabilityNotResolvable);
            liability.Waive(actor.UserId, now);
        }
        else
        {
            if (!LiabilityStateMachine.CanTransition(liability.Status, Domain.Enums.Payment.VendorLiabilityStatus.Settled))
                return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.LiabilityNotResolvable);
            liability.Settle(actor.UserId, now);
        }
        await _uow.SaveChangesAsync(ct);
        return Ok(liability);
    }

    public async Task<int> AutoSettleOverdueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await _uow.Returns.GetOverdueLiabilitiesAsync(now, 100, ct);
        foreach (var liability in overdue)
            liability.Settle(null, now); // hết hạn không phản đối → vendor chịu khoản trừ
        if (overdue.Count > 0) await _uow.SaveChangesAsync(ct);
        return overdue.Count;
    }

    private IServiceResult<VendorLiabilityResponse> Ok(VendorLiability liability)
        => ServiceResult<VendorLiabilityResponse>.Success(_mapper.Map<VendorLiabilityResponse>(liability));

    private static ServiceResult<VendorLiabilityResponse> Fail(int code, string message)
        => ServiceResult<VendorLiabilityResponse>.Failure(code, message);
}
