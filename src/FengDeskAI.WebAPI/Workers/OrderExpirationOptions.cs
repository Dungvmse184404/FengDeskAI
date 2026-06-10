namespace FengDeskAI.WebAPI.Workers;

public class OrderExpirationOptions
{
    public const string SectionName = "OrderExpiration";

    /// <summary>Đơn online quá số phút này chưa thanh toán sẽ chuyển Expired.</summary>
    public int PendingTimeoutMinutes { get; set; } = 15;

    /// <summary>Chu kỳ quét đơn quá hạn.</summary>
    public int ScanIntervalSeconds { get; set; } = 60;
}
