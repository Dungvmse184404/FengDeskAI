using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Domain.StateMachines;

/// <summary>
/// Bảng transition hợp lệ của ticket RMA (v2). Nguồn chân lý duy nhất cho việc chuyển trạng thái —
/// entity <c>ReturnRequest</c> gọi vào đây, KHÔNG rẽ nhánh trạng thái rải rác ở service.
/// Nhánh từ <see cref="ReturnRequestStatus.UnderReview"/> phụ thuộc <paramref name="reason"/>:
/// cây chết (<see cref="ReturnReason.PlantHealth"/>) đi thẳng Reviewing, không thu hồi hàng.
/// </summary>
public static class ReturnStateMachine
{
    public static bool CanTransition(ReturnRequestStatus from, ReturnRequestStatus to, ReturnReason reason) => from switch
    {
        ReturnRequestStatus.Requested =>
            to is ReturnRequestStatus.NeedMoreEvidence
                or ReturnRequestStatus.UnderReview
                or ReturnRequestStatus.Cancelled,

        ReturnRequestStatus.NeedMoreEvidence =>
            to is ReturnRequestStatus.Requested
                or ReturnRequestStatus.Rejected,

        // Cây chết → bỏ qua thu hồi; hàng vật lý → thu hồi.
        ReturnRequestStatus.UnderReview =>
            (to is ReturnRequestStatus.Reviewing && reason == ReturnReason.PlantHealth)
            || (to is ReturnRequestStatus.ReturnInTransit && reason != ReturnReason.PlantHealth),

        ReturnRequestStatus.ReturnInTransit =>
            to is ReturnRequestStatus.ItemReceived,

        ReturnRequestStatus.ItemReceived =>
            to is ReturnRequestStatus.Reviewing,

        ReturnRequestStatus.Reviewing =>
            to is ReturnRequestStatus.Exchanging
                or ReturnRequestStatus.Refunding
                or ReturnRequestStatus.Rejected,

        ReturnRequestStatus.Exchanging =>
            to is ReturnRequestStatus.Completed
                or ReturnRequestStatus.Refunding, // fallback hết hàng thay thế

        ReturnRequestStatus.Refunding =>
            to is ReturnRequestStatus.Completed,

        _ => false, // Completed / Cancelled / Rejected là terminal
    };

    /// <summary>True nếu là trạng thái kết thúc — không transition ra được nữa.</summary>
    public static bool IsTerminal(ReturnRequestStatus status)
        => status is ReturnRequestStatus.Completed
            or ReturnRequestStatus.Cancelled
            or ReturnRequestStatus.Rejected;
}
