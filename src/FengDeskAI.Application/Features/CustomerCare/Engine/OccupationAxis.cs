using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Trục nghề nghiệp (N3) — <c>ô</c> + trọng số <c>Wo</c>. Dựng MỘT lần cho một lượt chấm, không phụ
/// thuộc sản phẩm; cùng một <c>ô</c> dùng ở cả ba mặt: trang sản phẩm (<c>ô·p</c> thuần), luồng Carry
/// (<c>d = (1−Wo)·n̂ + Wo·ô</c>) và luồng phòng (<c>d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô</c>).
/// Xem <c>docs/adr/occupation-product-fit-v1.md</c> §3.
///
/// <para>
/// <c>ô</c> dựng từ hồ sơ Σ=1: <c>δ = profile − 0.2</c> (Σ=0, cùng hình dạng gap phòng) rồi chuẩn hoá
/// <b>nửa-L1</b> y hệt <c>ĝ</c> — mỗi trục ∈ [−1, +1]. Nhờ vậy <c>ô·p</c> có cùng thang với <c>ĝ·p</c>
/// và <c>r·p</c>, ba số hạng cộng thẳng được vào nhau.
/// </para>
/// </summary>
public sealed record OccupationAxis(
    string Code,
    string NameVi,

    /// <summary><c>ô</c> ĐÃ chặn hành khắc mệnh — thứ thật sự nhân với vector sản phẩm.</summary>
    ElementVector Direction,

    /// <summary><c>ô</c> TRƯỚC khi chặn — để breakdown nói ra phần nghề <i>không</i> kéo được.</summary>
    ElementVector RawDirection,

    /// <summary><c>Wo</c> đã kẹp bởi service.</summary>
    decimal Weight,

    /// <summary>Mã dòng <c>scoring_params</c> — luôn <c>OCCUPATION_WEIGHT</c>, giữ để breakdown chỉ đúng núm chỉnh.</summary>
    string WeightCode)
{
    /// <summary><c>Direction ≠ RawDirection</c> — nghề đã bị chặn ở ít nhất một hành khắc mệnh.</summary>
    public bool WasClamped => Direction != RawDirection;

    /// <summary>Các hành nghề muốn kéo lên (<c>Raw &gt; 0</c>) nhưng bị chặn về 0 vì khắc mệnh.</summary>
    public IReadOnlyList<FengShuiElement> ClampedElements =>
        RawDirection.Enumerate()
            .Where(x => x.Value > 0m && Direction[x.Element] <= 0m)
            .Select(x => x.Element)
            .ToList();

    /// <summary>
    /// Trả <c>null</c> khi: hồ sơ null · trọng số ≤ 0 · hồ sơ đều (|δ|₁ = 0, tức nghề <c>OTHER</c>).
    /// <c>null</c> ⇒ mọi công thức rơi về đúng v3.3 — đó là kill-switch, và cũng là lý do không cần
    /// nhánh <c>if</c> riêng cho "nghề trung tính".
    /// </summary>
    /// <param name="destiny">
    /// Bản mệnh để chặn: hành <c>BiKhac</c> bị kẹp về ≤ 0 — nghề không đổi được bản mệnh. <c>null</c>
    /// khi không biết mệnh (khách ẩn danh xem trang sản phẩm): khi đó <c>Direction == RawDirection</c>.
    /// </param>
    public static OccupationAxis? Build(
        ElementVector? profile,
        FengShuiElement? destiny,
        decimal weight,
        string weightCode,
        string code,
        string nameVi)
    {
        if (profile is not { } o || weight <= 0m)
            return null;

        var delta = o.Subtract(ElementVector.Uniform);
        decimal half = delta.L1() / 2m;
        if (half == 0m)
            return null;

        var raw = delta.Divide(half);
        var clamped = destiny is { } mine ? ClampAgainstDestiny(raw, mine) : raw;
        return new OccupationAxis(code, nameVi, clamped, raw, weight, weightCode);
    }

    /// <summary>
    /// <c>ô[e] = min(ô[e], 0)</c> với mọi hành <c>e</c> khắc bản mệnh. Không chuẩn hoá lại: phần bị chặn
    /// mất đi có chủ ý — mệnh Mộc làm Tài chính thì Kim vẫn khắc, nghề chỉ không được <i>cộng</i> vào đó,
    /// còn phần <i>trừ</i> đã có <c>r[Kim]</c> và <c>USER_CONFLICT_PENALTY</c> lo (ADR §3.2).
    /// </summary>
    private static ElementVector ClampAgainstDestiny(ElementVector raw, FengShuiElement destiny)
    {
        decimal Clamp(FengShuiElement e)
            => FengShuiCalculator.GetRelation(destiny, e) == FengShuiRelation.BiKhac
                ? Math.Min(raw[e], 0m)
                : raw[e];

        return new ElementVector(
            Tho: Clamp(FengShuiElement.Tho),
            Kim: Clamp(FengShuiElement.Kim),
            Thuy: Clamp(FengShuiElement.Thuy),
            Moc: Clamp(FengShuiElement.Moc),
            Hoa: Clamp(FengShuiElement.Hoa));
    }
}
