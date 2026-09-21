using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>Một nghề trong danh sách chọn của user — KHÔNG kèm delta (user không cần biết con số).</summary>
public sealed record OccupationOptionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string NameVi { get; init; } = null!;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Một nghề ở màn quản trị — kèm bảng delta để chuyên gia soát.</summary>
public sealed record OccupationAdminDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string NameVi { get; init; } = null!;
    public string? Description { get; init; }
    public bool IsActive { get; init; }

    /// <summary>Do seeder tạo hay admin thêm tay — seeder không đụng vào row admin tạo.</summary>
    public bool IsSystemSeeded { get; init; }

    public int SortOrder { get; init; }

    /// <summary>
    /// Hồ sơ ngũ hành Σ=1. <b>Rỗng = nghề chưa có hồ sơ</b> — engine bỏ qua nghề đó thay vì đoán, nên
    /// nghề vẫn dùng được cho thống kê mà không tác động điểm.
    /// </summary>
    public List<OccupationProfileEntryDto> Profile { get; init; } = new();
}

public sealed record OccupationProfileEntryDto
{
    public string Element { get; init; } = null!;
    public decimal Share { get; init; }
}

public sealed record UpsertOccupationRequest
{
    /// <summary>Bắt buộc khi tạo mới; bỏ qua khi cập nhật (mã là khoá bất biến, đổi mã = tạo nghề khác).</summary>
    public string? Code { get; init; }

    public string NameVi { get; init; } = null!;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public int SortOrder { get; init; }
}

/// <summary>
/// Thay <b>toàn bộ</b> hồ sơ ngũ hành của một nghề — hành có trong DB mà thiếu ở đây coi như 0 và bị xóa.
/// Ghi đè trọn gói thay vì upsert từng dòng vì hồ sơ là một phân bố trọn vẹn (Σ=1): sửa lẻ từng hành
/// là phá tổng mà không ai nhận ra.
/// </summary>
public sealed record ReplaceOccupationProfileRequest
{
    public List<OccupationProfileEntryInput> Entries { get; init; } = new();
}

public sealed record OccupationProfileEntryInput
{
    public FengShuiElement Element { get; init; }

    /// <summary>Tỉ trọng ∈ [0, 1]. Σ toàn bộ entries phải = 1 ± 0.001.</summary>
    public decimal Share { get; init; }
}
