namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ Category (nhiều-nhiều). Junction thuần: composite key,
/// không kế thừa BaseEntity (không audit/soft-delete).
/// </summary>
public class ProductCategory
{
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }

    public Product Product { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
