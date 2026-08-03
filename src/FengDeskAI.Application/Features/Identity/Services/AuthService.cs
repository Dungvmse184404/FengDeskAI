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

public class AuthService : IAuthService
{
    /// <summary>Thời gian sống của phiên đổi email — đủ để nhận 2 OTP mà không treo quá lâu.</summary>
    private static readonly TimeSpan ChangeEmailTokenTtl = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWork _uow;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IOtpService _otpService;
    private readonly IChangeEmailTokenService _changeEmailTokens;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow,
        IPasswordService passwordService,
        ITokenService tokenService,
        IGoogleTokenValidator googleTokenValidator,
        IOtpService otpService,
        IChangeEmailTokenService changeEmailTokens,
        IMapper mapper,
        ILogger<AuthService> logger)
    {
        _uow = uow;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _googleTokenValidator = googleTokenValidator;
        _otpService = otpService;
        _changeEmailTokens = changeEmailTokens;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Users.GetByEmailAsync(email, ct);

        // PasswordHash null = user chỉ đăng ký qua Google, chưa từng đặt mật khẩu.
        if (user is null || user.PasswordHash is null || !_passwordService.Verify(request.Password, user.PasswordHash))
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Auth.InvalidCredentials);

        if (!user.IsActive)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Auth.AccountDisabled);

        var response = await IssueTokensAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<AuthResponse>.Success(response, ApiStatusMessages.Auth.LoginSuccess);
    }

    public async Task<IServiceResult<AuthResponse>> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken ct = default)
    {
        var googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken, ct);
        if (googleUser is null)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Auth.GoogleTokenInvalid);

        if (!googleUser.EmailVerified)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Auth.GoogleEmailNotVerified);

        // 1) Đã từng đăng nhập Google trước đó → tài khoản đã link sẵn.
        var user = await _uow.Users.GetByGoogleIdAsync(googleUser.GoogleId, ct);

        if (user is null)
        {
            // 2) Chưa link Google nhưng email trùng tài khoản Local đã có → tự động link
            //    (an toàn vì Google đã xác thực quyền sở hữu email — email_verified=true).
            user = await _uow.Users.GetByEmailAsync(googleUser.Email, ct);
            if (user is not null)
            {
                user.GoogleId = googleUser.GoogleId;
            }
            else
            {
                // 3) User hoàn toàn mới → tạo tài khoản, không có mật khẩu.
                user = new User
                {
                    Email = googleUser.Email,
                    PasswordHash = null,
                    FullName = string.IsNullOrWhiteSpace(googleUser.FullName) ? googleUser.Email : googleUser.FullName,
                    GoogleId = googleUser.GoogleId,
                    AuthProvider = AuthProvider.Google,
                    Role = UserRole.Customer,
                    IsActive = true,
                };
                await _uow.Users.AddAsync(user, ct);
            }
        }

        if (!user.IsActive)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Auth.AccountDisabled);

        var response = await IssueTokensAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<AuthResponse>.Success(response, ApiStatusMessages.Auth.GoogleLoginSuccess);
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

    public async Task<IServiceResult<UserSummary>> UpdateBirthTimeAsync(Guid userId, TimeOnly? birthTime, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<UserSummary>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);

        user.BirthTime = birthTime;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<UserSummary>.Success(_mapper.Map<UserSummary>(user), "Đã cập nhật giờ sinh.");
    }

    public async Task<IServiceResult<UserSummary>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<UserSummary>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return ServiceResult<UserSummary>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Profile.FullNameRequired);

        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        if (phone is not null)
        {
            if (!IsValidVietnamesePhone(phone))
                return ServiceResult<UserSummary>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Profile.PhoneInvalid);

            // PhoneExistsAsync không loại trừ chính mình → so tay để user lưu lại số cũ vẫn được.
            if (!string.Equals(phone, user.Phone, StringComparison.Ordinal)
                && await _uow.Users.PhoneExistsAsync(phone, ct))
                return ServiceResult<UserSummary>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Profile.PhoneInUse);
        }

        // Chặn ngày sinh tương lai / quá xa: engine phong thủy tra can-chi theo năm, dữ liệu rác sẽ ra mệnh sai.
        if (request.DateOfBirth is { } dob && (dob.Date > DateTime.UtcNow.Date || dob.Year < 1900))
            return ServiceResult<UserSummary>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Profile.DateOfBirthInvalid);

        user.FullName = fullName;
        user.Phone = phone;
        user.Gender = request.Gender;
        user.DateOfBirth = request.DateOfBirth;

        await _uow.SaveChangesAsync(ct);
        return ServiceResult<UserSummary>.Success(_mapper.Map<UserSummary>(user), ApiStatusMessages.Profile.Updated);
    }

    public async Task<IServiceResult> InitiateEmailChangeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);

        var sendResult = await _otpService.SendOtpAsync(user.Email, OtpPurpose.ChangeEmail, ct);
        if (!sendResult.IsSuccess) return sendResult;

        return ServiceResult.Success(ApiStatusMessages.Profile.CurrentEmailOtpSent);
    }

    public async Task<IServiceResult<ChangeEmailTokenResponse>> VerifyCurrentEmailAsync(
        Guid userId, VerifyCurrentEmailRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<ChangeEmailTokenResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);

        var otpFailure = MapOtpFailure(await _otpService.VerifyOtpAsync(user.Email, request.Otp, OtpPurpose.ChangeEmail, ct));
        if (otpFailure is not null)
            return ServiceResult<ChangeEmailTokenResponse>.Failure(ApiStatusCodes.BadRequest, otpFailure);

        var token = await _changeEmailTokens.IssueAsync(new ChangeEmailSession(userId, null), ChangeEmailTokenTtl, ct);
        var response = new ChangeEmailTokenResponse
        {
            ChangeEmailToken = token,
            ExpiresAt = DateTime.UtcNow.Add(ChangeEmailTokenTtl),
        };

        return ServiceResult<ChangeEmailTokenResponse>.Success(response, ApiStatusMessages.Profile.CurrentEmailVerified);
    }

    public async Task<IServiceResult> RequestNewEmailAsync(Guid userId, RequestNewEmailRequest request, CancellationToken ct = default)
    {
        var session = await _changeEmailTokens.PeekAsync(request.ChangeEmailToken, ct);
        if (session is null || session.UserId != userId)
            return ServiceResult.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Profile.ChangeEmailSessionInvalid);

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);

        var newEmail = request.NewEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(newEmail) || !IsValidEmail(newEmail))
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Profile.EmailInvalid);

        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Profile.EmailUnchanged);

        if (await _uow.Users.EmailExistsAsync(newEmail, ct))
            return ServiceResult.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Profile.EmailInUse);

        var sendResult = await _otpService.SendOtpAsync(newEmail, OtpPurpose.ChangeEmail, ct);
        if (!sendResult.IsSuccess) return sendResult;

        // Chỉ ghi email đang chờ vào phiên SAU khi gửi OTP thành công, tránh khóa phiên vào địa chỉ chưa gửi được.
        await _changeEmailTokens.UpdateAsync(
            request.ChangeEmailToken, new ChangeEmailSession(userId, newEmail), ChangeEmailTokenTtl, ct);

        return ServiceResult.Success(ApiStatusMessages.Profile.NewEmailOtpSent);
    }

    public async Task<IServiceResult<AuthResponse>> ConfirmNewEmailAsync(
        Guid userId, ConfirmNewEmailRequest request, CancellationToken ct = default)
    {
        var session = await _changeEmailTokens.PeekAsync(request.ChangeEmailToken, ct);
        if (session is null || session.UserId != userId || string.IsNullOrEmpty(session.PendingNewEmail))
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.Profile.ChangeEmailSessionInvalid);

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);

        var newEmail = session.PendingNewEmail;
        var otpFailure = MapOtpFailure(await _otpService.VerifyOtpAsync(newEmail, request.Otp, OtpPurpose.ChangeEmail, ct));
        if (otpFailure is not null)
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.BadRequest, otpFailure);

        // Kiểm tra lại ngay trước khi ghi: giữa lúc gửi OTP và lúc xác nhận có thể có người khác đăng ký email này.
        if (await _uow.Users.EmailExistsAsync(newEmail, ct))
            return ServiceResult<AuthResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Profile.EmailInUse);

        var previousEmail = user.Email;
        user.Email = newEmail;

        // Access token mang claim email → cấp lại ngay để FE không phải đăng nhập lại.
        var response = await IssueTokensAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        await _changeEmailTokens.RevokeAsync(request.ChangeEmailToken, ct);

        _logger.LogInformation("User {UserId} changed email from {OldEmail} to {NewEmail}", userId, previousEmail, newEmail);

        return ServiceResult<AuthResponse>.Success(response, ApiStatusMessages.Profile.EmailChanged);
    }

    /// <summary>Đổi kết quả verify OTP thành message lỗi; null = hợp lệ.</summary>
    private static string? MapOtpFailure(OtpVerifyResult result) => result switch
    {
        OtpVerifyResult.Invalid => ApiStatusMessages.Registration.OtpIncorrect,
        OtpVerifyResult.Expired => ApiStatusMessages.Registration.OtpExpired,
        OtpVerifyResult.TooManyAttempts => ApiStatusMessages.Registration.OtpTooManyAttempts,
        _ => null,
    };

    /// <summary>SĐT Việt Nam: 10 số, bắt đầu bằng 0 — cùng quy tắc FE đang validate.</summary>
    private static bool IsValidVietnamesePhone(string phone)
        => phone.Length == 10 && phone[0] == '0' && phone.All(char.IsAsciiDigit);

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new System.Net.Mail.MailAddress(email).Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
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
