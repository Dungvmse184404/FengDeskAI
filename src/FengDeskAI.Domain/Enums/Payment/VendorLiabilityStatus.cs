namespace FengDeskAI.Domain.Enums.Payment;

/// <summary>
/// Trạng thái khoản công nợ vendor (trừ vào payout kế tiếp). Lưu DB dạng string.
/// Máy trạng thái ở <c>Domain.StateMachines.LiabilityStateMachine</c>. Toàn bộ diễn ra
/// SAU khi khách đã nhận tiền và KHÔNG ảnh hưởng tới khách.
///
///   Pending  → Disputed (vendor phản đối trong hạn) | Settled (quá hạn → tự chốt)
///   Disputed → Settled (Manager: vendor sai) | Waived (Manager: vendor đúng)
/// (terminal: Settled, Waived)
/// </summary>
public enum VendorLiabilityStatus
{
    Pending,   // đã ghi nhận, chờ hết hạn dispute hoặc vendor phản đối
    Disputed,  // vendor đã phản đối, chờ Manager phán quyết
    Settled,   // giữ nguyên khoản trừ (vendor chịu)
    Waived,    // miễn khoản trừ (hoàn lại cho vendor)
}
