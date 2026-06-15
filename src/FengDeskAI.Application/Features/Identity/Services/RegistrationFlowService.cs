using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Enums;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Identity.Services;

public class RegistrationFlowService : IRegistrationFlowService
{
    private static readonly TimeSpan RegistrationTokenTtl = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWork _uow;
    private readonly IOtpService _otpService;
    private readonly IRegistrationTokenService _registrationTokenService;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly ILogger<RegistrationFlowService> _logger;

    public RegistrationFlowService(
        IUnitOfWork uow,
        IOtpService otpService,
        IRegistrationTokenService registrationTokenService,
        IPasswordService passwordService,
        ITokenService tokenService,
        IMapper mapper,
        ILogger<RegistrationFlowService> logger)
    {
        _uow = uow;
        _otpService = otpService;
        _registrationTokenService = registrationTokenService;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IServiceResult> InitiateAsync(InitiateRegisterRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);

        if (await _uow.Users.EmailExistsAsync(email, ct))
            return ServiceResult.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Registration.EmailInUse);

        var sendResult = await _otpService.SendOtpAsync(email, OtpPurpose.Register, ct);
        if (!sendResult.IsSuccess) return sendResult;

        return ServiceResult.Success(ApiStatusMessages.Registration.OtpSent);
    }

    public async Task<IServiceResult<VerifyRegisterResponse>> VerifyAsync(VerifyRegisterRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var verifyResult = await _otpService.VerifyOtpAsync(email, request.Otp, OtpPurpose.Register, ct);

        switch (verifyResult)
        {
            case OtpVerifyResult.Invalid:
                return ServiceResult<VerifyRegisterResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Registration.OtpIncorrect);
            case OtpVerifyResult.Expired:
                return ServiceResult<VerifyRegisterResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Registration.OtpExpired);
            case OtpVerifyResult.TooManyAttempts:
                return ServiceResult<VerifyRegisterResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Registration.OtpTooManyAttempts);
        }

        var token = await _registrationTokenService.IssueAsync(email, RegistrationTokenTtl, ct);
        var response = new VerifyRegisterResponse
        {
            RegistrationToken = token,
            ExpiresAt = DateTime.UtcNow.Add(RegistrationTokenTtl),
        };

        return ServiceResult<VerifyRegisterResponse>.Success(response, ApiStatusMessages.Registration.VerifySuccess);
    }

    public async Task<IServiceResult<AuthResponse>> FinalizeAsync(FinalizeRegisterRequest request, CancellationToken ct = default)
    {
        var email = await _registrationTokenService.ConsumeAsync(request.RegistrationToken, ct);
        if (email is null)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Registration.InvalidSession);

        if (await _uow.Users.EmailExistsAsync(email, ct))
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Registration.EmailInUse);

        if (!string.IsNullOrWhiteSpace(request.Phone) && await _uow.Users.PhoneExistsAsync(request.Phone.Trim(), ct))
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Registration.PhoneInUse);

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordService.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Role = UserRole.Customer,
            IsActive = true,
        };

        await _uow.Users.AddAsync(user, ct);

        var (access, accessExp) = _tokenService.GenerateAccessToken(user);
        var (refresh, refreshExp) = _tokenService.GenerateRefreshToken();

        await _uow.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = refreshExp,
        }, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User registered (finalize): {UserId} ({Email})", user.Id, user.Email);

        var response = new AuthResponse
        {
            AccessToken = access,
            AccessTokenExpiresAt = accessExp,
            RefreshToken = refresh,
            RefreshTokenExpiresAt = refreshExp,
            User = _mapper.Map<UserSummary>(user),
        };

        return ServiceResult<AuthResponse>.Success(response, ApiStatusMessages.Registration.RegisterSuccess, ApiStatusCodes.Created);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
