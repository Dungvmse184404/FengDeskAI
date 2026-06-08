namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>Cấu hình xác thực webhook nhà vận chuyển (shared secret).</summary>
public class ShippingWebhookSettings
{
    public const string SectionName = "ShippingWebhook";

    /// <summary>Secret mà provider phải gửi kèm trong header để được chấp nhận.</summary>
    public string Secret { get; set; } = null!;
}
