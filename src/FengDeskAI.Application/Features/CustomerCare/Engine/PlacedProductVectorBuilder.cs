using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Một sản phẩm user đã mua và đặt vào phòng, đã quy ra đại lượng engine hiểu.
/// </summary>
/// <param name="VoteWeight">
/// Σ weight các mã <see cref="ElementInputKind.DecorItem"/> của sản phẩm trong
/// <c>element_input_map</c> — đồng bộ tuyệt đối với tag hiện trạng cùng tên. Không gắn
/// <c>DecorItem</c> nào thì mặc định 1 phiếu, ngang một tag.
/// </param>
/// <param name="IsDelivered">
/// Đã giao tới tay user chưa. <b>Chỉ hàng ĐÃ GIAO mới là hiện trạng thật của phòng</b>; hàng đang
/// giao chỉ vào vector "xem trước".
/// </param>
public sealed record PlacedProductVector(
    Guid PlacementId,
    Guid OrderItemId,
    Guid ProductId,
    string ProductName,
    ElementVector Vector,
    decimal VoteWeight,
    bool IsDelivered,
    string DeliveryStatus);

/// <summary>
/// Quy <c>workspace_product_placements</c> ra vector + số phiếu. <b>Thuần, không I/O</b> — caller nạp
/// dữ liệu rồi truyền vào.
///
/// <para>
/// Tách ra vì <b>ba</b> đường cần đúng cùng con số: radar trang Workspace
/// (<c>element-analysis</c>), engine xếp hạng gợi ý (<c>GenerateAsync</c>), và trang chấm điểm một
/// sản phẩm (<c>GetProductFitAsync</c>). Trước khi tách, chỉ radar tính sản phẩm đã đặt còn hai
/// đường kia thì không — cùng một căn phòng ra hai kết luận "thiếu hành gì" trái nhau, và bộ gợi ý
/// cứ đẩy tiếp đúng hành mà user vừa mua về đặt vào phòng. Xem ADR v3.2 §19.
/// </para>
/// </summary>
public static class PlacedProductVectorBuilder
{
    public static IReadOnlyList<PlacedProductVector> Build(
        IEnumerable<WorkspaceProductPlacement> placements,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<ProductElementInput>> inputsByProduct,
        ElementInputResolver resolver,
        ScoringParameters prms)
    {
        var rows = new List<PlacedProductVector>();

        foreach (var placement in placements)
        {
            var product = placement.Product;
            ElementVector? overridden = product is
                { ElementTho: { } t, ElementKim: { } k, ElementThuy: { } w, ElementMoc: { } m, ElementHoa: { } h }
                ? new ElementVector(t, k, w, m, h)
                : null;

            var inputs = inputsByProduct.TryGetValue(product.Id, out var list)
                ? list
                : Array.Empty<ProductElementInput>();

            var vector = ProductVectorProvider.Build(
                product.IsVectorOverridden, overridden, inputs, resolver,
                product.Elements.Select(e => (e.Element, e.IsPrimary)), prms);

            // Sản phẩm chưa có dữ liệu ngũ hành thì không phải bằng chứng về căn phòng — bỏ qua hẳn
            // thay vì đưa vào với vector 0 (sẽ ngốn một suất phiếu mà không nói gì).
            if (vector.L1() <= 0m) continue;

            var decorCodes = inputs.Where(i => i.InputKind == ElementInputKind.DecorItem).ToList();
            decimal voteWeight = decorCodes.Count > 0
                ? decorCodes.Sum(c => resolver.Resolve(c.InputKind, c.InputCode).Sum(kv => kv.Value))
                : 1.0m;
            if (voteWeight < 0m) voteWeight = 0m; // admin cố tình cho weight 0 → sản phẩm không ảnh hưởng

            rows.Add(new PlacedProductVector(
                placement.Id,
                placement.OrderItemId,
                placement.ProductId,
                placement.OrderItem.ProductName,
                vector,
                voteWeight,
                placement.OrderItem.Delivery?.Status == DeliveryStatus.Delivered,
                placement.OrderItem.Delivery?.Status.ToString() ?? "Unknown"));
        }

        return rows;
    }

    /// <summary>
    /// Hiện trạng THẬT của phòng: chỉ hàng đã giao. Đây là thứ đi vào <c>current</c> — cả trên radar
    /// lẫn trong gap chấm điểm.
    /// </summary>
    public static IReadOnlyCollection<ProductContribution> DeliveredContributions(
        this IReadOnlyList<PlacedProductVector> rows)
        => rows.Where(r => r.IsDelivered).Select(ToContribution).ToList();

    /// <summary>Vector "xem trước": tính cả hàng đang giao tới.</summary>
    public static IReadOnlyCollection<ProductContribution> PreviewContributions(
        this IReadOnlyList<PlacedProductVector> rows)
        => rows.Select(ToContribution).ToList();

    private static ProductContribution ToContribution(PlacedProductVector r)
        => new(r.ProductId, r.ProductName, r.Vector, r.VoteWeight);
}
