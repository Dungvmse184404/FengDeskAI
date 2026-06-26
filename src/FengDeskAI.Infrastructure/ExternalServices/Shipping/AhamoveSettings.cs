namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Cấu hình tích hợp AhaMove (giao hàng nội thành). Secret lấy từ env (<c>Ahamove__ApiKey</c>…),
/// appsettings chỉ để default rỗng. Xem Documents/AHAMOVE_INTEGRATION.md.
/// </summary>
public class AhamoveSettings
{
    public const string SectionName = "Ahamove";

    /// <summary>Base URL partner API. Staging: https://partner-apistg.ahamove.com</summary>
    public string BaseUrl { get; set; } = "https://partner-apistg.ahamove.com";

    /// <summary>API key partner (cấp qua email sau khi đăng ký).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>SĐT tài khoản tạo đơn đã đăng ký dưới API key.</summary>
    public string Mobile { get; set; } = string.Empty;
}
