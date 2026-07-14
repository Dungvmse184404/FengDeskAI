using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Domain.StateMachines;

/// <summary>
/// Bảng transition hợp lệ của refund sub-saga (v2). <see cref="RefundStatus.Failed"/> KHÔNG dead-end:
/// luôn có đường ra (retry → Processing, hoặc escalate → ManagerReview).
/// </summary>
public static class RefundStateMachine
{
    public static bool CanTransition(RefundStatus from, RefundStatus to) => from switch
    {
        RefundStatus.Pending =>
            to is RefundStatus.Processing or RefundStatus.Cancelled,

        RefundStatus.Processing =>
            to is RefundStatus.Completed or RefundStatus.Failed,

        RefundStatus.Failed =>
            to is RefundStatus.Processing or RefundStatus.ManagerReview,

        RefundStatus.ManagerReview =>
            to is RefundStatus.Processing or RefundStatus.Completed,

        _ => false, // Completed / Cancelled là terminal
    };

    public static bool IsTerminal(RefundStatus status)
        => status is RefundStatus.Completed or RefundStatus.Cancelled;
}
