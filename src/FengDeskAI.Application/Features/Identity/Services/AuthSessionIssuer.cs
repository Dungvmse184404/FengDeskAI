using AutoMapper;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Features.Identity.Services;

public class AuthSessionIssuer : IAuthSessionIssuer
{
    private readonly IUnitOfWork _uow;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;

    public AuthSessionIssuer(IUnitOfWork uow, ITokenService tokenService, IMapper mapper)
    {
        _uow = uow;
        _tokenService = tokenService;
        _mapper = mapper;
    }

    public async Task<AuthResponse> IssueAsync(User user, CancellationToken ct = default, RefreshToken? replacedFromToken = null)
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
