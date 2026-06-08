namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ Tag (nhiều-nhiều). Junction thuần: composite key,
/// không kế thừa BaseEntity (không audit/soft-delete).
/// </summary>
public class ProductTag
{
    public Guid ProductId { get; set; }
    public Guid TagId { get; set; }

    public Product Product { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
