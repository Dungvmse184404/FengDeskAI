namespace FengDeskAI.Application.Features.Catalog.DTOs;

public class CategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}
