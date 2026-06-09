namespace FengDeskAI.Infrastructure.ExternalServices.Payment;

public class PayOsSettings
{
    public const string SectionName = "PayOS";

    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ClientId { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string ChecksumKey { get; set; } = null!;

    /// <summary>URL client được redirect về sau khi thanh toán thành công.</summary>
    public string ReturnUrl { get; set; } = null!;
    /// <summary>URL client được redirect về khi hủy thanh toán.</summary>
    public string CancelUrl { get; set; } = null!;
}
