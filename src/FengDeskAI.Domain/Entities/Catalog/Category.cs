using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>Danh mục sản phẩm — có thể phân cấp cha-con qua <see cref="ParentId"/>.</summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
}
