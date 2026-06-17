namespace FengDeskAI.Application.Interfaces.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Name { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
