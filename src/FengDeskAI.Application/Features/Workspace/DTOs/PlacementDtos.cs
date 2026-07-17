namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>
/// Một order item user đã mua, đủ điều kiện đặt vào workspace (delivery không Cancelled/Returned).
/// <see cref="IsDelivered"/> false → khi đặt vào phòng chỉ tính vector PREVIEW.
/// </summary>
public class PurchasedItemResponse
{
    public Guid OrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string? ProductImage { get; set; }
    public int Quantity { get; set; }
    public string DeliveryStatus { get; set; } = null!;
    public bool IsDelivered { get; set; }

    /// <summary>Workspace đang đặt (null = chưa đặt đâu cả).</summary>
    public Guid? PlacedWorkspaceProfileId { get; set; }
    public string? PlacedWorkspaceName { get; set; }
}

/// <summary>Body cho PUT /api/workspace/{id}/placements — đặt (hoặc CHUYỂN từ phòng khác) 1 order item vào phòng.</summary>
public class PlaceProductRequest
{
    public Guid OrderItemId { get; set; }
}

/// <summary>Sản phẩm đang đặt trong phòng — trả kèm element-analysis để FE render danh sách + radar.</summary>
public class PlacedProductResponse
{
    public Guid PlacementId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string? ProductImage { get; set; }
    public string DeliveryStatus { get; set; } = null!;
    /// <summary>false = hàng chưa giao tới → chỉ nằm trong vector preview (nét đứt trên radar).</summary>
    public bool IsDelivered { get; set; }
    /// <summary>Phiếu đóng góp vào vector phòng (mặc định 1.0; scale theo DecorItem code trong element_input_map).</summary>
    public decimal VoteWeight { get; set; }
}
