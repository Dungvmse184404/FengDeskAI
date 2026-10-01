namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>Một dòng trong nhóm sản phẩm đang cân nhắc mua — cùng productId lặp lại thì cộng dồn số lượng.</summary>
public sealed record BundlePreviewItemRequest
{
    public Guid ProductId { get; init; }

    /// <summary>Số món; kẹp về [1, 99].</summary>
    public int Quantity { get; init; } = 1;
}

/// <summary>
/// <c>POST /recommendations/fit/bundle</c> — "phòng sẽ ra sao nếu đặt TẤT CẢ các món này (kèm số lượng)".
/// Chỉ đọc, không lưu gì — dùng POST vì danh sách có thể dài.
/// </summary>
public sealed record BundlePreviewRequest
{
    public Guid WorkspaceProfileId { get; init; }
    public List<BundlePreviewItemRequest> Items { get; init; } = new();
}

public sealed record BundlePreviewResponse
{
    public Guid WorkspaceProfileId { get; init; }

    /// <summary>
    /// Cùng hình dạng <c>gap</c> của <c>GET /recommendations/fit</c>: <c>current</c> là phòng như đang có,
    /// <c>previewCurrent</c>/<c>previewGap</c> là phòng sau khi thêm cả nhóm.
    /// </summary>
    public List<ElementAnalysisRow> Gap { get; init; } = new();

    /// <summary>Sản phẩm không tính vào (không tồn tại / ngừng bán / chưa gắn ngũ hành).</summary>
    public List<Guid> SkippedProductIds { get; init; } = new();
}
