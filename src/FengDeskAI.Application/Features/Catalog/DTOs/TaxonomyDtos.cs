namespace FengDeskAI.Application.Features.Catalog.DTOs;

/// <summary>Một mục tra cứu (style / vibe).</summary>
public sealed record LookupItemResponse(string Code, string Name, bool IsActive, int SortOrder);

/// <summary>Tạo mục tra cứu mới (admin thêm style/vibe không cần deploy).</summary>
public sealed class CreateLookupRequest
{
    /// <summary>Mã bất biến (vd "Industrial"). Không trùng.</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
}

/// <summary>Cập nhật mục tra cứu (đổi tên hiển thị / bật-tắt / thứ tự). Code không đổi.</summary>
public sealed class UpdateLookupRequest
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
