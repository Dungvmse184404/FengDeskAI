using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Identity;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Identity.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow,
        IPasswordService passwordService,
        ITokenService tokenService,
        IMapper mapper,
        ILogger<AuthService> logger)
    {
        _uow = uow;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Users.GetByEmailAsync(email, ct);

        if (user is null || !_passwordService.Verify(request.Password, user.PasswordHash))
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Auth.InvalidCredentials);

        if (!user.IsActive)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Auth.AccountDisabled);

        var response = await IssueTokensAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<AuthResponse>.Success(response, ApiStatusMessages.Auth.LoginSuccess);
    }

    public async Task<IServiceResult<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var existing = await _uow.RefreshTokens.GetByTokenAsync(request.RefreshToken, ct);
        if (existing is null || !existing.IsActive)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Auth.InvalidRefreshToken);

        var user = await _uow.Users.GetByIdAsync(existing.UserId, ct);
        if (user is null || !user.IsActive)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Auth.AccountUnavailable);

        existing.IsRevoked = true;
        existing.RevokedAt = DateTime.UtcNow;
        _uow.RefreshTokens.Update(existing);

        var response = await IssueTokensAsync(user, ct, existing);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<AuthResponse>.Success(response, ApiStatusMessages.Auth.RefreshSuccess);
    }

    public async Task<IServiceResult> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await _uow.RefreshTokens.GetByTokenAsync(refreshToken, ct);
        if (existing is null)
            return ServiceResult.Success(ApiStatusMessages.Auth.LoggedOut);

        if (!existing.IsRevoked)
        {
            existing.IsRevoked = true;
            existing.RevokedAt = DateTime.UtcNow;
            _uow.RefreshTokens.Update(existing);
            await _uow.SaveChangesAsync(ct);
        }

        return ServiceResult.Success(ApiStatusMessages.Auth.LoggedOut);
    }

    public async Task<IServiceResult<UserSummary>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<UserSummary>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);
        if (!user.IsActive)
            return ServiceResult<UserSummary>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Auth.AccountDisabled);

        return ServiceResult<UserSummary>.Success(_mapper.Map<UserSummary>(user));
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct, RefreshToken? replacedFromToken = null)
    {
        var (access, accessExp) = _tokenService.GenerateAccessToken(user);
        var (refresh, refreshExp) = _tokenService.GenerateRefreshToken();

        await _uow.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = refreshExp,
        }, ct);

        if (replacedFromToken is not null)
            replacedFromToken.ReplacedByToken = refresh;

        return new AuthResponse
        {
            AccessToken = access,
            AccessTokenExpiresAt = accessExp,
            RefreshToken = refresh,
            RefreshTokenExpiresAt = refreshExp,
            User = _mapper.Map<UserSummary>(user),
        };
    }
}
