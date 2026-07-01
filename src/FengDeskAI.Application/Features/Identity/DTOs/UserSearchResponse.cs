namespace FengDeskAI.Application.Features.Identity.DTOs;

/// <summary>
/// Kết quả search user — chỉ field công khai tối thiểu (không lộ ngày sinh, balance...).
/// Phục vụ UI mời nhân viên kiểu GitHub combobox.
/// </summary>
public class UserSearchResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
}
