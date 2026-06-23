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
    public Guid StaffId { get; set; }
}

public class StaffAssignmentResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public Guid StaffId { get; set; }
    public Guid AssignedBy { get; set; }
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
}
