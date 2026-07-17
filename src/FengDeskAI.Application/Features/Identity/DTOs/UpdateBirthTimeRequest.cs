namespace FengDeskAI.Application.Features.Identity.DTOs;

/// <summary>
/// Body cho <c>PUT /api/auth/me/birth-time</c>.
/// <see cref="BirthTime"/> nullable CÓ CHỦ ĐÍCH: "HH:mm" để đặt giờ sinh, null/rỗng để XÓA —
/// engine xem mệnh vẫn tính được bằng các dữ liệu còn lại (ngày sinh + giới tính), chỉ thiếu trụ giờ.
/// </summary>
public class UpdateBirthTimeRequest
{
    public string? BirthTime { get; set; }
}
