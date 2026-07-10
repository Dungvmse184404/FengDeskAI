using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Catalog.DTOs;

/// <summary>1 tín hiệu vật lý của sản phẩm (màu/vật liệu/hình khối).</summary>
public sealed record ProductElementInputDto
{
    public ElementInputKind Kind { get; init; }
    public string Code { get; init; } = null!;
}

/// <summary>Khai chất liệu/màu/hình khối cho sản phẩm → auto-calc vector (tầng 2). Rỗng = xóa hết input.</summary>
public sealed record SetProductElementInputsRequest
{
    public List<ProductElementInputDto> Inputs { get; init; } = new();
}

/// <summary>Ghi đè vector ngũ hành thủ công (tầng 1). Yêu cầu Σ ≈ 1 (sẽ chuẩn hóa lại).</summary>
public sealed record SetProductVectorOverrideRequest
{
    public decimal Tho { get; init; }
    public decimal Kim { get; init; }
    public decimal Thuy { get; init; }
    public decimal Moc { get; init; }
    public decimal Hoa { get; init; }
}

/// <summary>Trạng thái vector hiện tại của sản phẩm.</summary>
public sealed record ProductVectorResponse
{
    public Guid ProductId { get; init; }
    public bool IsVectorOverridden { get; init; }
    public decimal? Tho { get; init; }
    public decimal? Kim { get; init; }
    public decimal? Thuy { get; init; }
    public decimal? Moc { get; init; }
    public decimal? Hoa { get; init; }
    public List<ProductElementInputDto> Inputs { get; init; } = new();
}

/// <summary>
/// Danh sách code hợp lệ theo từng kind (vd Material → Wood/Metal/...) cho form vendor render dropdown/chip.
/// KHÔNG trả weight/element — vendor không cần biết mapping ngũ hành đằng sau.
/// </summary>
public sealed record ElementInputCodesResponse
{
    public ElementInputKind Kind { get; init; }
    public List<string> Codes { get; init; } = new();
}
