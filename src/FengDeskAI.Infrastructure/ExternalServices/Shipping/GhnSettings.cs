namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Cấu hình tích hợp GHN (Giao Hàng Nhanh) — giao toàn quốc theo mã quận/phường.
/// Token là API key dài hạn (không hết hạn). Secret lấy từ env (<c>Ghn__Token</c>…),
/// appsettings chỉ để default rỗng. Xem Documents/GHN_INTEGRATION.md §2.
/// </summary>
public class GhnSettings
{
    public const string SectionName = "Ghn";

    /// <summary>Base URL. Staging: https://dev-online-gateway.ghn.vn — Prod: https://online-gateway.ghn.vn</summary>
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn";

    /// <summary>API token (header <c>Token</c>) — long-lived, lấy từ portal 5sao.ghn.dev.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>ShopId mặc định (header <c>ShopId</c>) khi store chưa có GhnShopId riêng.</summary>
    public int DefaultShopId { get; set; }

    /// <summary>Loại dịch vụ mặc định: 2 = chuyển phát nhanh (nhẹ), 5 = chuyển phát thường (nặng).</summary>
    public int DefaultServiceTypeId { get; set; } = 2;
}
