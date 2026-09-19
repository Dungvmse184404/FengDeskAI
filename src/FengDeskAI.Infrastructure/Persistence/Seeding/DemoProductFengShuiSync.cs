using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Đồng bộ phong thủy của MỘT sản phẩm demo theo file seed: hành chính/phụ (<c>product_element</c>),
/// tín hiệu chất liệu/màu/hình (<c>product_element_inputs</c>) và cache vector 5 cột. Dùng chung cho
/// <see cref="ProductFengShuiDemoSeeder"/>, <see cref="ProductElementInputDemoSeeder"/> và
/// <see cref="PlacementProductDemoSeeder"/> để ba seeder không mỗi nơi một kiểu "đã có thì bỏ qua".
///
/// <para>
/// <b>File seed là nguồn sự thật cho sản phẩm demo</b> (khớp theo tên đầy đủ) — khác
/// <c>element_input_map</c> nơi admin chỉnh runtime và seeder phải tránh đè. Sản phẩm demo tồn tại để
/// engine có ứng viên đúng; dữ liệu cũ lệch file (vd trước 2026-09-19 cây chỉ khai <c>Wood + Green</c>
/// ⇒ 100% Mộc) phải được kéo về, không thì DB dev/test cứ giữ vector sai mãi dù file đã sửa.
/// </para>
///
/// <para>
/// <b>Vì sao không có vật nào một hành thuần.</b> Ngũ hành gán hành cho vật qua nhiều <i>kênh</i>:
/// chất liệu, màu, hình dáng, công năng. Cây xanh là Mộc nhưng sống trong chậu sứ và đất (Thổ), tưới
/// nước (Thủy); tượng đồng là Kim nhưng đứng trên đế đá (Thổ) và ánh đồng đỏ (Hỏa). Hành <i>chủ</i> có,
/// hành <i>duy nhất</i> thì không. Vector 1.0 chỉ xuất hiện khi mọi kênh khai trỏ về cùng một hành
/// (<c>Wood + Green</c>), tức là <b>khai thiếu kênh</b>, không phải vật đó thuần. Hệ quả kỹ thuật: với
/// <c>p</c> thuần, <c>d·p = d[e]</c> chạm biên ±1 ⇒ điểm 100% / 0% — engine nói quá lời.
/// <see cref="Audit"/> cảnh báo hai lỗi khai đó để file seed không trượt lại.
/// </para>
/// </summary>
public static class DemoProductFengShuiSync
{
    /// <summary>Vector có một hành ≥ ngưỡng này coi như "thuần" — khai thiếu kênh.</summary>
    public const decimal PureElementThreshold = 0.95m;

    /// <summary>
    /// Đưa <c>product.Elements</c> về đúng (primary, secondaries) của file. Trả về <c>true</c> nếu có đổi.
    /// <c>ProductElement</c> không soft-delete (không kế thừa <c>BaseEntity</c>) nên gỡ là gỡ thật.
    /// </summary>
    public static bool SyncElements(
        Product product, FengShuiElement? primary, IEnumerable<FengShuiElement> secondaries)
    {
        if (primary is not { } prim) return false;

        var desired = new List<(FengShuiElement Element, bool IsPrimary)> { (prim, true) };
        desired.AddRange(secondaries.Where(s => s != prim).Distinct().Select(s => (s, false)));

        var currentSet = product.Elements.Select(e => (e.Element, e.IsPrimary)).ToHashSet();
        if (currentSet.SetEquals(desired)) return false;

        product.Elements.Clear();
        foreach (var (element, isPrimary) in desired)
            product.Elements.Add(new ProductElement { ProductId = product.Id, Element = element, IsPrimary = isPrimary });
        return true;
    }

    /// <summary>
    /// Đưa <c>product_element_inputs</c> của sản phẩm về đúng tập (kind, code) trong file — chỉ nhận code
    /// có trong <c>element_input_map</c>. Dòng thừa bị xoá mềm, dòng thiếu được thêm. Trả về danh sách
    /// input SAU khi đồng bộ (để dựng vector) và cờ có đổi hay không.
    /// </summary>
    public static (IReadOnlyList<ProductElementInput> Inputs, bool Changed) SyncInputs(
        Product product,
        IReadOnlyList<(ElementInputKind Kind, string Code)> desired,
        List<ProductElementInput> existing,
        DbSet<ProductElementInput> set,
        ElementInputResolver resolver,
        ILogger logger,
        string fileName)
    {
        var accepted = new List<(ElementInputKind Kind, string Code)>();
        foreach (var d in desired.Distinct())
        {
            if (!resolver.Resolve(d.Kind, d.Code).Any())
            {
                logger.LogWarning("{File}: ({Kind}, {Code}) không có trong element_input_map ('{Name}') — bỏ qua.",
                    fileName, d.Kind, d.Code, product.Name);
                continue;
            }
            accepted.Add(d);
        }

        var wanted = accepted.ToHashSet();
        var have = existing.Select(i => (i.InputKind, i.InputCode)).ToHashSet();
        if (wanted.SetEquals(have)) return (existing, false);

        var stale = existing.Where(i => !wanted.Contains((i.InputKind, i.InputCode))).ToList();
        set.RemoveRange(stale); // AppDbContext đổi Deleted → IsDeleted = true

        var kept = existing.Except(stale).ToList();
        foreach (var (kind, code) in accepted.Where(a => !have.Contains(a)))
        {
            var row = new ProductElementInput { ProductId = product.Id, InputKind = kind, InputCode = code };
            set.Add(row);
            kept.Add(row);
        }
        return (kept, true);
    }

    /// <summary>Tính lại vector (đúng 3 tầng của engine) và ghi vào 5 cột cache. Trả về vector.</summary>
    public static ElementVector CacheVector(
        Product product, IReadOnlyCollection<ProductElementInput> inputs,
        ElementInputResolver resolver, ScoringParameters prms)
    {
        ElementVector? overridden = product is
            { ElementTho: { } t, ElementKim: { } k, ElementThuy: { } w, ElementMoc: { } m, ElementHoa: { } h }
            ? new ElementVector(t, k, w, m, h)
            : null;

        var vector = ProductVectorProvider.Build(
            product.IsVectorOverridden, overridden, inputs, resolver,
            product.Elements.Select(e => (e.Element, e.IsPrimary)), prms);

        // Cột cache là numeric(4,3): làm tròn TRƯỚC khi gán, nếu không mỗi lần seed EF lại thấy
        // 0.5666… ≠ 0.567 và ghi UPDATE vô nghĩa (seeder mất idempotent).
        product.ElementTho = Math.Round(vector.Tho, 3);
        product.ElementKim = Math.Round(vector.Kim, 3);
        product.ElementThuy = Math.Round(vector.Thuy, 3);
        product.ElementMoc = Math.Round(vector.Moc, 3);
        product.ElementHoa = Math.Round(vector.Hoa, 3);
        return vector;
    }

    /// <summary>
    /// Hai lỗi khai dữ liệu hay gặp, cảnh báo chứ không chặn (seed không được làm app không lên):
    /// (1) vector thuần một hành — khai thiếu kênh; (2) hành trội của vector ≠ hành chính khai —
    /// trang sản phẩm ghi một hành, engine chấm theo hành khác.
    /// </summary>
    public static void Audit(Product product, ElementVector vector, ILogger logger, string fileName)
    {
        if (vector.L1() <= 0m) return;

        var (dominant, share) = vector.Enumerate().MaxBy(x => x.Value);
        if (share >= PureElementThreshold)
        {
            logger.LogWarning(
                "{File}: '{Name}' là một hành thuần ({Element} {Share:P0}) — khai thêm kênh (chất liệu đế/chậu, màu, hình) để vector có hành phụ.",
                fileName, product.Name, dominant, share);
        }

        var primary = product.Elements.FirstOrDefault(e => e.IsPrimary)?.Element;
        if (primary is { } prim && prim != dominant)
        {
            logger.LogWarning(
                "{File}: '{Name}' khai hành chính {Primary} nhưng vector tính ra trội {Dominant} — sửa primaryElement hoặc elementInputs cho khớp.",
                fileName, product.Name, prim, dominant);
        }
    }

    public static FengShuiElement? ParseElement(string? code)
        => Enum.TryParse<FengShuiElement>(code, ignoreCase: true, out var e) ? e : null;

    public static IReadOnlyList<(ElementInputKind Kind, string Code)> ParseInputs(
        IEnumerable<(string Kind, string Code)> rows, ILogger logger, string fileName, string productName)
    {
        var result = new List<(ElementInputKind, string)>();
        foreach (var (kind, code) in rows)
        {
            if (!Enum.TryParse<ElementInputKind>(kind, ignoreCase: true, out var parsed))
            {
                logger.LogWarning("{File}: inputKind '{Kind}' không hợp lệ ('{Name}') — bỏ qua.", fileName, kind, productName);
                continue;
            }
            if (!string.IsNullOrWhiteSpace(code)) result.Add((parsed, code.Trim()));
        }
        return result;
    }
}
