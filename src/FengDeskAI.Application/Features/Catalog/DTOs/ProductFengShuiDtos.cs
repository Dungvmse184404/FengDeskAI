using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Catalog.DTOs;

/// <summary>Khai báo/ cập nhật thuộc tính phong thủy cho 1 sản phẩm (làm sản phẩm thành ứng viên gợi ý).</summary>
public class SetProductFengShuiRequest
{
    /// <summary>Hành chính (IsPrimary). Bắt buộc.</summary>
    public FengShuiElement PrimaryElement { get; set; }

    /// <summary>Các hành phụ (0..n). Trùng hành chính sẽ bị bỏ qua.</summary>
    public List<FengShuiElement> SecondaryElements { get; set; } = new();

    public SizeClass SizeClass { get; set; }
    public List<Vibe> Vibes { get; set; } = new();
    public List<WorkspaceStyle> Styles { get; set; } = new();
}

public class ProductFengShuiResponse
{
    public Guid ProductId { get; set; }
    public FengShuiElement PrimaryElement { get; set; }
    public List<FengShuiElement> SecondaryElements { get; set; } = new();
    public SizeClass SizeClass { get; set; }
    public List<Vibe> Vibes { get; set; } = new();
    public List<WorkspaceStyle> Styles { get; set; } = new();
}
