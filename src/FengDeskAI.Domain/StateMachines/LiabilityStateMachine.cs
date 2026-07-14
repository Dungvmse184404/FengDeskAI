using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Domain.StateMachines;

/// <summary>
/// Bảng transition hợp lệ của công nợ vendor (v2). Diễn ra SAU khi khách đã nhận tiền,
/// KHÔNG bao giờ đảo ngược khoản đã hoàn cho khách.
/// </summary>
public static class LiabilityStateMachine
{
    public static bool CanTransition(VendorLiabilityStatus from, VendorLiabilityStatus to) => from switch
    {
        VendorLiabilityStatus.Pending =>
            to is VendorLiabilityStatus.Disputed or VendorLiabilityStatus.Settled,

        VendorLiabilityStatus.Disputed =>
            to is VendorLiabilityStatus.Settled or VendorLiabilityStatus.Waived,

        _ => false, // Settled / Waived là terminal
    };

    public static bool IsTerminal(VendorLiabilityStatus status)
        => status is VendorLiabilityStatus.Settled or VendorLiabilityStatus.Waived;
}
