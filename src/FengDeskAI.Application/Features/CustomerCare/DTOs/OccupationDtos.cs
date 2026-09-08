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
    /// Delta theo hành. <b>Rỗng = nghề chưa được chuyên gia duyệt delta</b> — engine bỏ qua nghề đó
    /// thay vì đoán, nên nghề vẫn dùng được cho thống kê mà không tác động điểm.
    /// </summary>
    public List<OccupationModifierDto> Modifiers { get; init; } = new();
}

public sealed record OccupationModifierDto
{
    public string Element { get; init; } = null!;
    public decimal Delta { get; init; }
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
/// Thay <b>toàn bộ</b> bảng delta của một nghề — hành có trong DB mà thiếu ở đây sẽ bị xóa.
/// Ghi đè trọn gói thay vì upsert từng dòng vì bảng delta là một phát biểu phong thủy trọn vẹn: sửa
/// lẻ từng hành dễ để lại một bộ nửa cũ nửa mới mà không ai nhận ra.
/// </summary>
public sealed record ReplaceOccupationModifiersRequest
{
    public List<OccupationModifierInput> Modifiers { get; init; } = new();
}

public sealed record OccupationModifierInput
{
    public FengShuiElement Element { get; init; }

    /// <summary>Độ dịch điểm quan hệ, CÓ THỂ ÂM. Miền hợp lệ [−1, 1].</summary>
    public decimal Delta { get; init; }
}
