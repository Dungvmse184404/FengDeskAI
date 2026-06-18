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
    public string Role { get; set; } = null!;

    /// <summary>Ngày sinh (để tính mệnh Nạp Âm theo năm). Null nếu user chưa khai.</summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>0=Unspecified, 1=Male, 2=Female, 3=Other (cần cho Kua/hướng).</summary>
    public Gender Gender { get; set; }
}
