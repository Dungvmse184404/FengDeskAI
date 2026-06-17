namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>Bảng tra cứu code+name (Style, Vibe...) — cho phép service quản lý chung.</summary>
public interface ILookup
{
    string Code { get; set; }
    string Name { get; set; }
    bool IsActive { get; set; }
    int SortOrder { get; set; }
}
