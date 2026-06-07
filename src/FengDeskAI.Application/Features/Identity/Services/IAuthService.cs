using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;

namespace FengDeskAI.Application.Features.Identity.Services;

public interface IAuthService
{
    Task<IServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<IServiceResult<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<IServiceResult> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<IServiceResult<UserSummary>> GetMeAsync(Guid userId, CancellationToken ct = default);
}
