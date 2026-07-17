using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.Identity.DTOs;

public class AuthResponse
{
    public string AccessToken { get; set; } = null!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserSummary User { get; set; } = null!;
}

public class UserSummary
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }

    /// <summary>Chuỗi role gộp (bit-flag ToString), vd "Customer, GardenOwner". Giữ cho tương thích cũ.</summary>
    public string Role { get; set; } = null!;

    /// <summary>Danh sách role tách rời để FE quyết định workspace, vd ["Customer","GardenOwner"].</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>Ngày sinh (để tính mệnh Nạp Âm theo năm). Null nếu user chưa khai.</summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Giờ sinh (HH:mm) — cần cho Tứ Trụ/Bát Tự đầy đủ. Null nếu chưa khai.</summary>
    public TimeOnly? BirthTime { get; set; }

    /// <summary>0=Unspecified, 1=Male, 2=Female, 3=Other (cần cho Kua/hướng).</summary>
    public Gender Gender { get; set; }

    /// <summary>
    /// Phong thủy ĐÃ TÍNH SẴN từ ngày sinh + giới tính (mệnh/cung/hướng). Null nếu chưa có ngày sinh.
    /// AI đọc thẳng các giá trị này, KHÔNG tự tính. Đây là giá trị phái sinh, không lưu DB.
    /// </summary>
    public UserFengShuiInfo? FengShui { get; set; }
}

/// <summary>Hồ sơ phong thủy cá nhân hiển thị cho client/AI (enum đưa dưới dạng chuỗi cho dễ đọc).</summary>
public class UserFengShuiInfo
{
    /// <summary>Mệnh Nạp Âm: Kim/Moc/Thuy/Hoa/Tho.</summary>
    public string Element { get; set; } = null!;

    /// <summary>Số Kua (1..9, không có 5). Null nếu giới tính không Nam/Nữ.</summary>
    public int? KuaNumber { get; set; }

    /// <summary>Nhóm trạch: "East" (Đông tứ trạch) hoặc "West" (Tây tứ trạch). Null nếu không tính được.</summary>
    public string? KuaGroup { get; set; }

    /// <summary>Các hướng tốt (Bắc/Đông Nam/...). Rỗng nếu chưa đủ dữ kiện tính Kua.</summary>
    public IReadOnlyList<string> FavorableDirections { get; set; } = Array.Empty<string>();
}
