namespace FengDeskAI.Domain.Enums.Promotion;

/// <summary>
/// Ai trả tiền cho khoản giảm. Sàn tài trợ thì khoản giảm trừ vào phí sàn của từng delivery và không vượt
/// phí sàn đó (docs/adr/voucher-freeship.md). Nhà vườn tự tài trợ: chưa hỗ trợ.
/// </summary>
public enum VoucherFundingSource
{
    Platform,
}
