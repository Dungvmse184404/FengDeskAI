using FengDeskAI.Domain.Enums.Vendor;

namespace FengDeskAI.Application.Features.Vendor.DTOs;

public class StoreAddressResponse
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; }
}

public class StoreResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; }
    /// <summary>True nếu user gọi /stores/mine là owner của store này; false = chỉ là nhân viên (Accepted). Chỉ set ở GetMine.</summary>
    public bool IsOwner { get; set; }
    public StoreAddressResponse? Address { get; set; }
    public List<StoreOwnerResponse> Owners { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StoreOwnerResponse
{
    public Guid OwnerUserId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime AssignedAt { get; set; }
}

public class CreateStoreRequest
{
    // Owner = người gọi (self-service); không nhận OwnerUserId từ client.
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
}

public class AddOwnerRequest
{
    public Guid OwnerUserId { get; set; }
}

public class UpdateStoreRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStoreAddressRequest
{
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class UpdateStoreAddressRequest
{
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class AssignStaffRequest
{
    /// <summary>ID user lấy từ /api/users/search. Required.</summary>
    public Guid? StaffId { get; set; }
    /// <summary>Tra cứu theo email (cách phụ — vẫn hỗ trợ để FE cũ dùng).</summary>
    public string? StaffEmail { get; set; }
}

public class StaffAssignmentResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = null!;
    public string StaffEmail { get; set; } = null!;
    public string? StaffPhone { get; set; }
    public Guid InvitedBy { get; set; }
    public string? InvitedByName { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
}

/// <summary>Lời mời gửi cho user hiện tại — để hiển thị ở MyInvitationsPage.</summary>
public class InvitationResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public string StoreName { get; set; } = null!;
    public Guid InvitedBy { get; set; }
    public string? InvitedByName { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime InvitedAt { get; set; }
}

/// <summary>
/// Vai trò của user hiện tại đối với 1 store — nguồn sự thật để FE ẩn/hiện tab.
/// Owner (chính/đồng sở hữu) full quyền; Staff (Accepted) chỉ xử lý đơn/ship,
/// KHÔNG sửa hồ sơ, KHÔNG xem thống kê/nhân viên.
/// </summary>
public class StoreMembershipResponse
{
    /// <summary>Owner chính (IsPrimary) của store.</summary>
    public bool IsPrimaryOwner { get; set; }
    /// <summary>Owner chính hoặc đồng sở hữu.</summary>
    public bool IsOwner { get; set; }
    /// <summary>Garden staff với assignment Accepted (chỉ tính khi không phải owner).</summary>
    public bool IsStaff { get; set; }
    /// <summary>Platform admin.</summary>
    public bool IsAdmin { get; set; }
    /// <summary>Được thao tác nghiệp vụ trên store (owner | staff | admin).</summary>
    public bool CanManage { get; set; }
}

/// <summary>Thống kê cửa hàng cho dashboard vendor (chỉ owner/admin).</summary>
public class StoreStatisticsResponse
{
    /// <summary>Tổng doanh thu hàng (Subtotal) của các delivery đã Delivered.</summary>
    public decimal TotalRevenue { get; set; }
    /// <summary>Tổng phí ship của các delivery đã Delivered.</summary>
    public decimal TotalShippingFee { get; set; }
    public int TotalDeliveries { get; set; }
    /// <summary>Đếm delivery theo trạng thái (Pending/Preparing/Shipped/Delivered/…).</summary>
    public Dictionary<string, int> DeliveriesByStatus { get; set; } = new();
    public int ProductCount { get; set; }
    /// <summary>Số nhân viên Accepted.</summary>
    public int StaffCount { get; set; }
    /// <summary>Doanh thu 6 tháng gần nhất (gồm tháng hiện tại).</summary>
    public List<MonthlyRevenuePoint> RevenueByMonth { get; set; } = new();
}

public class MonthlyRevenuePoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
    public int DeliveredCount { get; set; }
}
