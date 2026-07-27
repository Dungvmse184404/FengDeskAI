namespace FengDeskAI.Application.Interfaces.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Name { get; }
    string? Role { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
}
