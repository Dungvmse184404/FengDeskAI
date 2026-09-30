using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Features.Identity.Services;

/// <summary>
/// Cấp cặp access + refresh token cho một user. Dùng chung giữa đăng nhập và những thao tác làm đổi claim của
/// chính người đang gọi (đổi email, tự mở cửa hàng → thêm role GardenOwner) để FE nhận phiên mới ngay, không
/// phải đăng nhập lại.
/// </summary>
public interface IAuthSessionIssuer
{
    /// <summary>
    /// Tạo phiên mới theo trạng thái HIỆN TẠI của <paramref name="user"/> (role, <c>TokenVersion</c>). Chỉ thêm
    /// refresh token vào UnitOfWork — caller tự <c>SaveChangesAsync</c>, cùng transaction với thay đổi của mình.
    /// </summary>
    Task<AuthResponse> IssueAsync(User user, CancellationToken ct = default, RefreshToken? replacedFromToken = null);
}
