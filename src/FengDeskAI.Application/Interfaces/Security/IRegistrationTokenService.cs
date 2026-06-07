namespace FengDeskAI.Application.Interfaces.Security;

public interface IRegistrationTokenService
{
    Task<string> IssueAsync(string email, TimeSpan ttl, CancellationToken ct = default);
    Task<string?> ConsumeAsync(string token, CancellationToken ct = default);
}
