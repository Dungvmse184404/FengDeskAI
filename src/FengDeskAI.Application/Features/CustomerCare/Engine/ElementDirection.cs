using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Bộ vector "hệ thống đang ưu tiên hành nào" — v3.2 §9/§10.3, thêm trục nghề N3 ở v3.4.
///
/// <para>
/// <b>Không phụ thuộc sản phẩm.</b> <c>ĝ</c> đến từ gap của PHÒNG, <c>r</c> đến từ BẢN MỆNH, <c>ô</c> đến
/// từ NGHỀ — chấm sản phẩm nào cũng ra cùng bộ này. Vì thế nó tách khỏi <see cref="RecommendationScorer"/>: radar phân
/// tích phòng (chưa có sản phẩm nào) cần đúng bộ vector này, và nếu tính lại ở chỗ khác thì sớm muộn
/// hai màn hình vẽ hai đa giác khác nhau cho cùng một căn phòng.
/// </para>
/// </summary>
public sealed record ElementDirection(
    /// <summary><c>ĝ</c> — mỗi trục ∈ [−1,+1] ở nhánh gap phòng; Σ=1 không âm ở nhánh dụng thần.</summary>
    ElementVector NormalizedGap,

    /// <summary>
    /// <c>r[e] = ruleScore(bản mệnh, e)</c> — điểm quan hệ với mệnh, CÓ DẤU. <c>null</c> khi trục cá nhân tắt.
    /// Từ v3.4 KHÔNG còn bị nghề bẻ (N1 gỡ): nghề đi trục riêng <see cref="Occupation"/>.
    /// </summary>
    ElementVector? RuleScoreVector,

    /// <summary>
    /// <c>d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô</c> (phòng) · <c>d = (1−Wo)·n̂ + Wo·ô</c> (Carry) — thứ nhân với
    /// vector sản phẩm để ra điểm.
    /// </summary>
    ElementVector CombinedDirection,

    ConflictResolution? ConflictResolution,

    /// <summary>Trục nghề đã áp — <c>null</c> khi trục tắt (kill-switch, chưa khai nghề, nghề OTHER).</summary>
    OccupationAxis? Occupation = null)
{
    /// <summary><c>ô</c> đã chặn hành khắc mệnh — lớp radar "Nghề cần". <c>null</c> khi trục tắt.</summary>
    public ElementVector? OccupationDirection => Occupation?.Direction;

    /// <summary>
    /// <c>normalize(max(d, 0))</c>, Σ=1 — chồng được lên <c>adjustedIdeal</c>/<c>current</c> vì cùng
    /// thang. Phần âm của <c>d</c> không mất đi: FE tô nhãn trục đỏ cho những hành đó.
    /// </summary>
    public ElementVector PriorityVector => CombinedDirection.Normalize();

    /// <summary>
    /// Nhánh chấm theo PHÒNG. <paramref name="ruleScoreOf"/> tra bảng <c>feng_shui_rules</c> (admin
    /// chỉnh được) chứ không dùng thẳng hằng số trong code.
    /// </summary>
    /// <param name="occupation">
    /// Trục nghề N3, đã kẹp <c>Wo ≤ 1 − Wp</c> bởi service. <c>null</c> ⇒ công thức y hệt v3.3. Áp
    /// <b>độc lập</b> với trục cá nhân: khách chưa có ngày sinh (<c>Wp = 0</c>) vẫn có <c>Wo·ô</c>.
    /// </param>
    public static ElementDirection ForWorkspaceGap(
        ElementVector gap,
        FengShuiElement? destiny,
        decimal personalWeight,
        Func<FengShuiElement, FengShuiElement, decimal> ruleScoreOf,
        OccupationAxis? occupation = null)
    {
        decimal gapL1 = gap.L1();

        // v3.2 §8 — chia NỬA chuẩn L1: gap có Σ=0 nên nửa dương đúng bằng |gap|₁/2, và tử số chỉ với
        // tới nửa đó. Chia cả |gap|₁ như v3.1 thì điểm kẹt trần ±0.5.
        decimal denom = gapL1 / 2m;
        var normalizedGap = denom == 0m ? ElementVector.Zero : gap.Divide(denom);

        decimal wo = occupation?.Weight ?? 0m;

        // Trục cá nhân tắt (Wp = 0, hoặc chưa có ngày sinh) ⇒ d = (1−Wo)·ĝ + Wo·ô, không có gì để hoá giải.
        if (destiny is not { } mine || personalWeight <= 0m)
        {
            var gapOnly = WithOccupation(normalizedGap.Scale(1m - wo), occupation);
            return new ElementDirection(normalizedGap, null, gapOnly, null, occupation);
        }

        var ruleScoreVector = new ElementVector(
            Tho: ruleScoreOf(mine, FengShuiElement.Tho),
            Kim: ruleScoreOf(mine, FengShuiElement.Kim),
            Thuy: ruleScoreOf(mine, FengShuiElement.Thuy),
            Moc: ruleScoreOf(mine, FengShuiElement.Moc),
            Hoa: ruleScoreOf(mine, FengShuiElement.Hoa));

        var combined = WithOccupation(
            normalizedGap.Scale(1m - personalWeight - wo).Add(ruleScoreVector.Scale(personalWeight)),
            occupation);

        // DetectConflict đọc `ruleScoreOf` gốc: "hành này khắc bản mệnh" là một PHẠM TRÙ do mệnh quyết
        // định, nghề nghiệp không được phép xóa một cảnh báo xung khắc chỉ vì nó cộng dương vào hành đó.
        return new ElementDirection(
            normalizedGap, ruleScoreVector, combined,
            DetectConflict(normalizedGap, mine, ruleScoreOf),
            occupation);
    }

    /// <summary><c>baseDirection + Wo·ô</c>; giữ nguyên <c>baseDirection</c> khi trục nghề tắt.</summary>
    private static ElementVector WithOccupation(ElementVector baseDirection, OccupationAxis? occupation)
        => occupation is { } o ? baseDirection.Add(o.Direction.Scale(o.Weight)) : baseDirection;

    /// <summary>
    /// <b>Mục tiêu đã tính bản mệnh</b>: <c>T = (1−Wp)·adjustedIdeal + Wp·personalVector</c>.
    ///
    /// <para>
    /// Đây là "tầng 1 — trộn MỤC TIÊU" của §10.1. Cả hai toán hạng đều Σ=1 và <c>Wp ∈ [0,1]</c> nên tổ
    /// hợp lồi <b>tự động Σ=1</b> — không phải chuẩn hoá lại, và vì thế nó nằm gọn cùng thang với
    /// <c>adjustedIdeal</c>/<c>current</c> trên radar thay vì nhọn ra ngoài như <c>priorityVector</c>.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>CHỈ để hiển thị, KHÔNG dùng chấm điểm.</b> <c>personalVector</c> không có phần tử âm nên nó
    /// chỉ nâng mục tiêu lên, không bao giờ phản đối được hành khắc mệnh — chấm theo <c>T</c> thì sản
    /// phẩm khắc mệnh lên đầu bảng còn sản phẩm sinh mệnh tụt xuống (§10.2, Q3 đã chốt tầng CHÊNH LỆCH).
    /// Điểm số vẫn đi đường <see cref="CombinedDirection"/>.
    /// </para>
    /// </summary>
    public static ElementVector PersonalTargetOf(
        ElementVector adjustedIdeal, ElementVector personalVector, decimal personalWeight)
        => adjustedIdeal.Scale(1m - personalWeight).Add(personalVector.Scale(personalWeight));

    /// <summary>
    /// Nhánh vật mang theo người: mục tiêu là vector dụng thần (Σ=1, không âm) nên mẫu số GIỮ
    /// <c>|target|₁</c> — không có "hai nửa" để chia đôi. <c>Wp</c> không áp: mục tiêu vốn đã 100% cá nhân.
    /// Trục nghề (N3) vào bằng <c>d = (1−Wo)·n̂ + Wo·ô</c> — dùng <c>ô</c> CÓ DẤU chứ không trộn hồ sơ
    /// Σ=1 thẳng vào <c>n</c>, để <c>OCCUPATION_SCORE</c> ở Carry và ở phòng cùng một thang (ADR §3.3).
    /// </summary>
    public static ElementDirection ForPersonalNeed(ElementVector personalNeed, OccupationAxis? occupation = null)
    {
        decimal l1 = personalNeed.L1();
        var normalized = l1 == 0m ? ElementVector.Zero : personalNeed.Divide(l1);
        decimal wo = occupation?.Weight ?? 0m;
        var combined = WithOccupation(normalized.Scale(1m - wo), occupation);
        return new ElementDirection(normalized, null, combined, null, occupation);
    }

    /// <summary>
    /// §13 — phòng thiếu nhất đúng hành KHẮC bản mệnh. Engine đã tự giải (hai lực triệt tiêu ở hành
    /// xung, hành trung gian nổi lên trong <c>d</c>); việc còn lại là NÓI RA để user không tưởng hệ
    /// thống bỏ sót nhu cầu của phòng.
    /// <para>A khắc B thì <b>con của A = mẹ của B</b>, nên hành hoá giải luôn tồn tại và luôn duy nhất.</para>
    /// </summary>
    private static ConflictResolution? DetectConflict(
        ElementVector normalizedGap,
        FengShuiElement destiny,
        Func<FengShuiElement, FengShuiElement, decimal> ruleScoreOf)
    {
        var roomNeed = normalizedGap.Dominant();

        // Dominant() của vector toàn 0 rơi về Thổ — phải chắc phòng THẬT SỰ đang thiếu hành đó,
        // nếu không phòng cân bằng hoàn hảo sẽ bị báo xung khắc ma.
        if (normalizedGap[roomNeed] <= 0m || ruleScoreOf(destiny, roomNeed) >= 0m)
            return null;

        var bridge = FengShuiCalculator.GetGeneratedElement(roomNeed);
        // Câu này hiện thẳng cho khách — dùng tên có dấu, không dùng enum name (Thuy/Moc).
        var (needVi, destinyVi, bridgeVi) =
            (ElementSemantics.ElementName(roomNeed), ElementSemantics.ElementName(destiny), ElementSemantics.ElementName(bridge));
        return new ConflictResolution(roomNeed, destiny, bridge,
            $"Phòng đang thiếu {needVi}, nhưng {needVi} khắc bản mệnh {destinyVi} của bạn. "
            + $"Hệ thống ưu tiên vật hành {bridgeVi} - {needVi} sinh {bridgeVi}, {bridgeVi} sinh {destinyVi} - "
            + $"bù cho phòng mà vẫn nuôi bản mệnh.");
    }
}
