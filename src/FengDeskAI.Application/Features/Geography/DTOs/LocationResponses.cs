namespace FengDeskAI.Application.Features.Geography.DTOs;

public class ProvinceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int Code { get; set; }
}

public class DistrictResponse
{
    public Guid Id { get; set; }
    public Guid ProvinceId { get; set; }
    public string Name { get; set; } = null!;
    public int Code { get; set; }
}

public class WardResponse
{
    public Guid Id { get; set; }
    public Guid DistrictId { get; set; }
    public string Name { get; set; } = null!;
    public int Code { get; set; }
}
