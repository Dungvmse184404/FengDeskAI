using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Interfaces.Security;

public interface ITokenService
{
    (string token, DateTime expiresAt) GenerateAccessToken(User user);
    (string token, DateTime expiresAt) GenerateRefreshToken();
}
