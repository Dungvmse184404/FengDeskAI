using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Domain.Entities.Identity;

public class User : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Giờ sinh (nullable) — nhập 1 lần để AI tính đủ Tứ Trụ/Bát Tự, không bắt buộc.</summary>
    public TimeOnly? BirthTime { get; set; }

    public Gender Gender { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// Tăng mỗi khi tài khoản bị khóa, đổi role hoặc thu hồi toàn bộ phiên.
    /// Access token chỉ hợp lệ khi claim token_version khớp giá trị hiện tại.
    /// </summary>
    public int TokenVersion { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<WorkspaceProfile> WorkspaceProfiles { get; set; } = new List<WorkspaceProfile>();
}
