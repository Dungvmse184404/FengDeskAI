namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Sinh mã SKU cho biến thể sản phẩm. Phải nằm ở tầng có DB (khác với 2 generator cũ bên FE) —
/// chỉ nơi này mới kiểm tra được mã đã tồn tại chưa trước khi trả về.
/// Xem <c>docs/adr/platform-sku-generation.md</c>.
/// </summary>
public interface ISkuGenerator
{
    /// <summary>Sinh 1 mã chưa tồn tại trong <c>product_items</c>. Ném nếu không tìm được sau vài lần thử.</summary>
    Task<string> GenerateAsync(CancellationToken ct = default);
}
