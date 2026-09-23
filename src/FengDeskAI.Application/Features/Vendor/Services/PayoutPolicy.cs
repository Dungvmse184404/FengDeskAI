namespace FengDeskAI.Application.Features.Vendor.Services;

/// <summary>
/// Chính sách đối soát tiền hàng của nhà vườn — MỘT nguồn sự thật cho mọi nơi nói về "khi nào rút được".
///
/// <para>
/// Tiền của một đơn chỉ "sạch" sau khi giao thành công và qua hết <see cref="HoldDays"/> ngày giữ.
/// Khoảng giữ này tồn tại vì đơn vừa giao vẫn có thể quay đầu: khách mở ticket đổi/trả
/// (<c>ReturnWorkflow.ReturnWindowDays</c>) và nền tảng sẽ ứng hoàn cho khách rồi trừ lại vào kỳ chi kế tiếp
/// (<see cref="FengDeskAI.Domain.Entities.Payment.VendorLiability"/>).
/// </para>
///
/// <para>
/// Khoảng giữ đặt <b>bằng đúng</b> cửa sổ đổi trả (<c>ReturnWorkflow.ReturnWindowDays</c> = 7): tiền chỉ
/// rời khỏi sàn sau khi khách hết quyền mở ticket, nên không có cảnh đã chi rồi mới phải đi đòi lại qua
/// công nợ. Rút ngắn số này = chấp nhận rủi ro tín dụng với vendor — đổi thì rà lại
/// <c>ReturnWindowDays</c> cùng lúc, hai số này phải đi với nhau.
/// </para>
/// </summary>
public static class PayoutPolicy
{
    /// <summary>Số ngày giữ tiền tính từ lúc giao thành công — khớp cửa sổ đổi trả.</summary>
    public const int HoldDays = 7;

    /// <summary>
    /// TẮT (24/09/2026): việc cộng tiền vào số dư chủ vườn đang SAI nghiệp vụ, đừng bật lại khi chưa sửa:
    /// <list type="number">
    /// <item>đơn đã cộng vào số dư vẫn tiếp tục được tính vào "có thể rút" ở thống kê — một khoản tiền
    /// hiện ở hai nơi;</item>
    /// <item>công nợ hoàn hàng (<c>VendorLiability</c>) KHÔNG bị trừ khỏi số dư, nên đơn bị trả sau khi
    /// đã cộng là sàn mất trắng phần đã ứng cho khách;</item>
    /// <item>chạy nhiều instance sẽ cộng đôi vì không có khoá, chỉ dựa vào cờ <c>PayoutCreditedAt</c>.</item>
    /// </list>
    /// Bật lại khi có sổ cái (<c>docs/adr/vendor-payout.md</c> bước 2). Các con số "chờ đối soát / có thể
    /// rút" ở API vẫn tính bình thường — chỉ không còn ghi vào <c>users.balance</c> nữa.
    /// </summary>
    public const bool CreditToBalanceEnabled = false;
}
