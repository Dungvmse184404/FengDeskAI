namespace FengDeskAI.WebAPI.Workers;

/// <summary>Cấu hình worker SLA cho luồng RMA (auto-reject / auto-retry / auto-settle).</summary>
public class ReturnSlaOptions
{
    public const string SectionName = "ReturnSla";

    /// <summary>Bật/tắt worker SLA (worker vẫn chạy nhưng skip xử lý khi false).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Chu kỳ quét (giây).</summary>
    public int ScanIntervalSeconds { get; set; } = 120;
}
