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
}
