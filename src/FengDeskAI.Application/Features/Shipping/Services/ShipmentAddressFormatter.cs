using FengDeskAI.Domain.Entities.Geography;

namespace FengDeskAI.Application.Features.Shipping.Services;

/// <summary>
/// Ghép địa chỉ đầy đủ cho nhà vận chuyển. AhaMove cần chuỗi tìm được trên Google Maps
/// dạng "đường, phường, quận, tỉnh" — bỏ qua phần thiếu.
/// </summary>
public static class ShipmentAddressFormatter
{
    public static string Compose(string? street, Ward? ward)
    {
        var parts = new[]
        {
            street,
            ward?.Name,
            ward?.District?.Name,
            ward?.District?.Province?.Name,
        };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
