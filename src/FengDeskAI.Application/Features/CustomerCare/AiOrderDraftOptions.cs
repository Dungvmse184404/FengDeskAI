namespace FengDeskAI.Application.Features.CustomerCare;

/// <summary>
/// Cấu hình draft đơn hàng của trợ lý AI (bind từ section "AiOrderDraft").
/// Tool đọc <see cref="DraftTtlMinutes"/>; <c>AiOrderDraftCleanupWorker</c> đọc phần còn lại
/// qua IOptionsMonitor nên bật/tắt được mà không cần restart.
/// </summary>
public sealed class AiOrderDraftOptions
{
    public const string SectionName = "AiOrderDraft";

    /// <summary>Bật/tắt worker dọn draft (worker vẫn chạy nhưng bỏ qua lượt quét).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Draft không được đụng tới quá số phút này thì hết hạn. Đặt lại mỗi lần user sửa draft.</summary>
    public int DraftTtlMinutes { get; set; } = 60;

    /// <summary>Chu kỳ quét của worker dọn dẹp.</summary>
    public int ScanIntervalSeconds { get; set; } = 300;

    /// <summary>Draft kẹt ở Confirming (process chết giữa lúc tạo đơn) quá số phút này thì worker xóa.</summary>
    public int ConfirmingGraceMinutes { get; set; } = 5;
}
