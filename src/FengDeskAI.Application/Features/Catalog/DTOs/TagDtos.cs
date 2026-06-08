namespace FengDeskAI.Application.Features.Catalog.DTOs;

public class TagResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class CreateTagRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class UpdateTagRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
