namespace FengDeskAI.Domain.Enums.Promotion;

/// <summary>
/// Ai trả tiền cho khoản giảm. Sàn tài trợ thì khoản giảm trừ vào phí sàn của từng delivery và không vượt
/// phí sàn đó (docs/adr/voucher-freeship.md). Nhà vườn tự tài trợ: chưa hỗ trợ.
/// </summary>
public enum VoucherFundingSource
{
    /// <summary>Sàn chịu — khoản giảm trừ vào phí sàn, không bao giờ vượt phần sàn thu.</summary>
    Platform,

    /// <summary>Nhà vườn chịu — khoản giảm trừ thẳng vào tiền hàng họ nhận, hoa hồng sàn giữ nguyên.</summary>
    Seller,

    /// <summary>[DEMO] Chia cho cả hai bên theo thứ tự ship → sàn → người bán.</summary>
    Mixed,
}
