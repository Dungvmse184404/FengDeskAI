using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Domain.Entities.Identity;

public class User : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime? DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
