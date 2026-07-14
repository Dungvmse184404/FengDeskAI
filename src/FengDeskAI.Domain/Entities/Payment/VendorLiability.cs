using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.StateMachines;

namespace FengDeskAI.Domain.Entities.Payment;

/// <summary>
/// Công nợ vendor: số tiền nền tảng đã ứng hoàn cho khách, sẽ trừ vào payout kế tiếp của garden.
/// Được tạo tự động khi <see cref="Refund"/> đạt Completed. Vendor có thể dispute trong hạn;
/// Manager phán quyết. Toàn bộ diễn ra SAU khi khách đã nhận tiền, KHÔNG ảnh hưởng khách.
/// </summary>
public class VendorLiability : BaseEntity
{
    /// <summary>garden_id — ánh xạ sang GardenStore chịu khoản trừ.</summary>
    public Guid GardenStoreId { get; set; }

    public Guid ReturnRequestId { get; set; }
    public Guid? RefundId { get; set; }

    public decimal Amount { get; set; }
    public VendorLiabilityStatus Status { get; private set; } = VendorLiabilityStatus.Pending;

    public string? DisputeReason { get; private set; }
    public DateTime DisputeDeadline { get; set; }

    public Guid? ResolvedBy { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public Refund? Refund { get; set; }
    public GardenStore Garden { get; set; } = null!;

    // ===================== Transitions (đóng gói) =====================

    private void TransitionTo(VendorLiabilityStatus to)
    {
        if (!LiabilityStateMachine.CanTransition(Status, to))
            throw new InvalidStateTransitionException(nameof(VendorLiability), Status.ToString(), to.ToString());
        Status = to;
    }

    /// <summary>Vendor phản đối khoản trừ trong hạn dispute.</summary>
    public void Dispute(string reason)
    {
        TransitionTo(VendorLiabilityStatus.Disputed);
        DisputeReason = reason;
    }

    /// <summary>Chốt giữ khoản trừ (vendor chịu) — Manager phán quyết hoặc worker tự chốt khi quá hạn.</summary>
    public void Settle(Guid? managerId, DateTime nowUtc)
    {
        TransitionTo(VendorLiabilityStatus.Settled);
        ResolvedBy = managerId;
        ResolvedAt = nowUtc;
    }

    /// <summary>Miễn khoản trừ, hoàn lại cho vendor (Manager phán quyết vendor đúng).</summary>
    public void Waive(Guid managerId, DateTime nowUtc)
    {
        TransitionTo(VendorLiabilityStatus.Waived);
        ResolvedBy = managerId;
        ResolvedAt = nowUtc;
    }
}
