namespace FengDeskAI.Application.Features.Geography.DTOs;

public class UserAddressResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDefault { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateUserAddressRequest
{
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDefault { get; set; }
    public string? Label { get; set; }
}

public class UpdateUserAddressRequest
{
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Label { get; set; }
}
