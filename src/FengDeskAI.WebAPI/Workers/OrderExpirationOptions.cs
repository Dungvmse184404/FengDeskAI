namespace FengDeskAI.WebAPI.Workers;

public class OrderExpirationOptions
{
    public const string SectionName = "OrderExpiration";

    /// <summary>Bật/tắt quét đơn quá hạn. Set false để tạm ngưng expire đơn (worker vẫn chạy nhưng skip xử lý).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Đơn online quá số phút này chưa thanh toán sẽ chuyển Expired.</summary>
    public int PendingTimeoutMinutes { get; set; } = 15;

    /// <summary>Chu kỳ quét đơn quá hạn.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;
}
