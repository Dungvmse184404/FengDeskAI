using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;

namespace FengDeskAI.Application.Features.Identity.Services;

public interface IAuthService
{
    Task<IServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>
    /// Đăng nhập/đăng ký bằng Google. Nếu GoogleId đã link → login trực tiếp.
    /// Nếu chưa nhưng email trùng tài khoản Local đã verify → tự động link.
    /// Nếu email chưa tồn tại → tạo user mới (không có mật khẩu).
    /// </summary>
    Task<IServiceResult<AuthResponse>> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken ct = default);

    Task<IServiceResult<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<IServiceResult> LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<IServiceResult<UserSummary>> GetMeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Cập nhật giờ sinh (nullable — null để xóa). Dùng cho Tứ Trụ/Bát Tự.</summary>
    Task<IServiceResult<UserSummary>> UpdateBirthTimeAsync(Guid userId, TimeOnly? birthTime, CancellationToken ct = default);

    /// <summary>Cập nhật họ tên / SĐT / giới tính / ngày sinh. Email đổi qua luồng OTP riêng bên dưới.</summary>
    Task<IServiceResult<UserSummary>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);

    // ===== Đổi email — 4 bước, mỗi bước phải qua bước trước (ràng bằng changeEmailToken) =====

    /// <summary>B1: gửi OTP tới email HIỆN TẠI để xác nhận đúng chủ tài khoản.</summary>
    Task<IServiceResult> InitiateEmailChangeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>B2: xác thực OTP của email hiện tại → cấp changeEmailToken mở khóa các bước sau.</summary>
    Task<IServiceResult<ChangeEmailTokenResponse>> VerifyCurrentEmailAsync(
        Guid userId, VerifyCurrentEmailRequest request, CancellationToken ct = default);

    /// <summary>B3: khai email mới (kiểm tra trùng) → gửi OTP tới hòm thư đó.</summary>
    Task<IServiceResult> RequestNewEmailAsync(Guid userId, RequestNewEmailRequest request, CancellationToken ct = default);

    /// <summary>B4: xác thực OTP của email mới → ghi email mới + cấp lại token cho phiên hiện tại.</summary>
    Task<IServiceResult<AuthResponse>> ConfirmNewEmailAsync(
        Guid userId, ConfirmNewEmailRequest request, CancellationToken ct = default);
}
