using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.Identity.DTOs;

/// <summary>
/// Body cho <c>PUT /api/auth/me</c> — các field hồ sơ user tự sửa được.
/// Email KHÔNG nằm ở đây: đổi email đi qua luồng 2 bước OTP riêng (xem <see cref="VerifyCurrentEmailRequest"/>).
/// </summary>
public class UpdateProfileRequest
{
    public string FullName { get; set; } = null!;

    /// <summary>Null/rỗng = xóa số điện thoại.</summary>
    public string? Phone { get; set; }

    public Gender Gender { get; set; }

    /// <summary>
    /// Null = xóa ngày sinh. Đổi giá trị này làm ĐỔI mệnh Nạp Âm/cung Kua đã tính — FE phải xác nhận
    /// với user trước khi gửi.
    /// </summary>
    public DateTime? DateOfBirth { get; set; }
}

/// <summary>Bước 2 — xác thực OTP đã gửi tới email HIỆN TẠI, đổi lấy token cho các bước sau.</summary>
public class VerifyCurrentEmailRequest
{
    public string Otp { get; set; } = null!;
}

/// <summary>Kết quả bước 2 — token ngắn hạn buộc 3 bước của luồng đổi email vào cùng một phiên.</summary>
public class ChangeEmailTokenResponse
{
    public string ChangeEmailToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>Bước 3 — khai email mới, hệ thống gửi OTP tới hòm thư đó.</summary>
public class RequestNewEmailRequest
{
    public string ChangeEmailToken { get; set; } = null!;
    public string NewEmail { get; set; } = null!;
}

/// <summary>Bước 4 — xác thực OTP của email mới, áp dụng thay đổi.</summary>
public class ConfirmNewEmailRequest
{
    public string ChangeEmailToken { get; set; } = null!;
    public string Otp { get; set; } = null!;
}
