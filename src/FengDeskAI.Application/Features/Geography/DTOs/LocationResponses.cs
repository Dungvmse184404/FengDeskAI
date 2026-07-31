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

/// <summary>
/// Đường dẫn hành chính đầy đủ của một phường (phường → quận → tỉnh).
/// Địa chỉ đã lưu chỉ giữ WardId; FE cần cả 3 cấp để dựng lại các dropdown
/// khi mở form sửa địa chỉ.
/// </summary>
public class WardPathResponse
{
    public Guid WardId { get; set; }
    public string WardName { get; set; } = null!;
    public Guid DistrictId { get; set; }
    public string DistrictName { get; set; } = null!;
    public Guid ProvinceId { get; set; }
    public string ProvinceName { get; set; } = null!;
}
