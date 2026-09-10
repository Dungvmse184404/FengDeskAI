using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Sản phẩm user đã mua và đặt vào phòng, quy ra nguồn ngũ hành — thuần (không I/O).
///
/// <para>
/// <b>Vì sao phải dùng chung.</b> Trước đây phần này nằm riêng trong <c>WorkspaceProfileService</c>,
/// nên radar trang Workspace tính sản phẩm đã đặt vào <c>current</c> còn <c>GenerateAsync</c> (bộ gợi
/// ý) thì không. Cùng một căn phòng ra hai <c>current</c> khác nhau: đo trên "Phòng họp tổng" thấy
/// radar bảo phòng thiếu <b>Thổ</b> (ĝ = +1.00) trong khi bộ gợi ý bảo thiếu <b>Mộc</b> (ĝ = +0.58) —
/// vì user đã đặt một cây tùng thơm thuần Mộc mà bộ gợi ý không nhìn thấy. Điểm một sản phẩm thuần
/// Mộc lệch <b>0.694</b> trên thang ±1 giữa hai đường.
/// </para>
///
/// <para>
/// <see cref="WorkspaceElementAnalyzer"/> sinh ra đúng để chặn chuyện đó ("bảo đảm Gap giống hệt
/// nhau"), nhưng hai bên truyền tham số khác nhau nên vẫn lệch. Gom luật dựng vector + đếm phiếu +
/// phân biệt đã giao/đang giao về một chỗ để không thể lệch thêm lần nữa.
/// </para>
/// </summary>
public static class PlacedProductBuilder
{
    /// <summary>
    /// Dựng vector cho từng placement. Bỏ qua sản phẩm chưa có dữ liệu ngũ hành (vector rỗng) —
    /// đưa vào sẽ kéo <c>current</c> về phía không có căn cứ nào.
    /// </summary>
    public static List<PlacedProductVector> Build(
        IEnumerable<WorkspaceProductPlacement> placements,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<ProductElementInput>> inputsByProduct,
        ElementInputResolver resolver,
        ScoringParameters prms)
    {
        var result = new List<PlacedProductVector>();

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
            if (vector.L1() <= 0m) continue;

            // Phiếu = Σ weight các mã DecorItem của sản phẩm trong element_input_map — đồng bộ với tag
            // hiện trạng cùng tên, nên chỉnh weight một chỗ trong seed là cả hai đổi theo. Không gắn
            // DecorItem thì mặc định 1 phiếu, ngang một tag user khai.
            var decorCodes = inputs.Where(i => i.InputKind == ElementInputKind.DecorItem).ToList();
            decimal voteWeight = decorCodes.Count > 0
                ? decorCodes.Sum(c => resolver.Resolve(c.InputKind, c.InputCode).Sum(kv => kv.Value))
                : 1.0m;
            if (voteWeight < 0m) voteWeight = 0m; // admin cố tình cho weight 0 → sản phẩm không ảnh hưởng

            result.Add(new PlacedProductVector(
                placement.Id,
                placement.ProductId,
                placement.OrderItem.ProductName,
                vector,
                voteWeight,
                placement.OrderItem.Delivery?.Status == DeliveryStatus.Delivered));
        }

        return result;
    }

    /// <summary>
    /// Sản phẩm ĐÃ GIAO — thứ có mặt thật trong phòng, nên vào <c>current</c> dùng cho <b>cả</b> radar
    /// <b>lẫn</b> chấm điểm. Hàng đang giao chỉ vào lớp preview (xem <see cref="PreviewOf"/>).
    /// </summary>
    public static List<ProductContribution> DeliveredOf(IEnumerable<PlacedProductVector> rows)
        => rows.Where(r => r.IsDelivered).Select(r => r.ToContribution()).ToList();

    /// <summary>Đã giao + đang giao — lớp "xem trước" trên radar.</summary>
    public static List<ProductContribution> PreviewOf(IEnumerable<PlacedProductVector> rows)
        => rows.Select(r => r.ToContribution()).ToList();
}

/// <summary>Một sản phẩm đã đặt trong phòng, đã quy ra vector + số phiếu.</summary>
public sealed record PlacedProductVector(
    Guid PlacementId,
    Guid ProductId,
    string ProductName,
    ElementVector Vector,
    decimal VoteWeight,
    bool IsDelivered)
{
    public ProductContribution ToContribution() => new(ProductId, ProductName, Vector, VoteWeight);
}
