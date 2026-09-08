using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Bộ vector "hệ thống đang ưu tiên hành nào" — v3.2 §9/§10.3.
///
/// <para>
/// <b>Không phụ thuộc sản phẩm.</b> <c>ĝ</c> đến từ gap của PHÒNG, <c>r</c> đến từ BẢN MỆNH — chấm sản
/// phẩm nào cũng ra cùng bộ này. Vì thế nó tách khỏi <see cref="RecommendationScorer"/>: radar phân
/// tích phòng (chưa có sản phẩm nào) cần đúng bộ vector này, và nếu tính lại ở chỗ khác thì sớm muộn
/// hai màn hình vẽ hai đa giác khác nhau cho cùng một căn phòng.
/// </para>
/// </summary>
public sealed record ElementDirection(
    /// <summary><c>ĝ</c> — mỗi trục ∈ [−1,+1] ở nhánh gap phòng; Σ=1 không âm ở nhánh dụng thần.</summary>
    ElementVector NormalizedGap,

    /// <summary>
    /// <c>r'[e]</c> — điểm quan hệ <b>đã tính nghề nghiệp</b>, CÓ DẤU, là thứ thật sự nhân với vector
    /// sản phẩm. <c>null</c> khi trục cá nhân tắt. Bằng <see cref="BaseRuleScoreVector"/> khi nghề
    /// nghiệp không áp.
    /// </summary>
    ElementVector? RuleScoreVector,

    /// <summary><c>d = (1−Wp)·ĝ + Wp·r'</c> — thứ nhân với vector sản phẩm để ra điểm.</summary>
    ElementVector CombinedDirection,

    ConflictResolution? ConflictResolution,

    /// <summary>
    /// <c>r[e] = ruleScore(bản mệnh, e)</c> — <b>trước</b> khi cộng delta nghề nghiệp. Giữ lại để
    /// breakdown nói được "nghề của bạn đã dịch hành này bao nhiêu" thay vì chỉ đưa ra con số cuối.
    /// </summary>
    ElementVector? BaseRuleScoreVector = null)
{
    /// <summary>
    /// <c>r' − r</c> — phần nghề nghiệp thật sự dịch được, <b>sau</b> khi đã clamp. Khác
    /// <c>delta·OccupationShare</c> ở những hành bị chặn — và đó chính là thứ cần hiển thị: user phải
    /// thấy nghề của mình <i>không</i> kéo nổi một hành khắc mệnh lên, chứ không phải con số danh nghĩa.
    /// <c>null</c> khi nghề nghiệp không áp.
    /// </summary>
    public ElementVector? OccupationShift =>
        BaseRuleScoreVector is { } b && RuleScoreVector is { } r ? r.Subtract(b) : null;

    /// <summary>
    /// <c>normalize(max(d, 0))</c>, Σ=1 — chồng được lên <c>adjustedIdeal</c>/<c>current</c> vì cùng
    /// thang. Phần âm của <c>d</c> không mất đi: FE tô nhãn trục đỏ cho những hành đó.
    /// </summary>
    public ElementVector PriorityVector => CombinedDirection.Normalize();

    /// <summary>
    /// Nhánh chấm theo PHÒNG. <paramref name="ruleScoreOf"/> tra bảng <c>feng_shui_rules</c> (admin
    /// chỉnh được) chứ không dùng thẳng hằng số trong code.
    /// </summary>
    public static ElementDirection ForWorkspaceGap(
        ElementVector gap,
        FengShuiElement? destiny,
        decimal personalWeight,
        Func<FengShuiElement, FengShuiElement, decimal> ruleScoreOf,
        ElementVector? occupationDelta = null,
        decimal occupationShare = 0m)
    {
        decimal gapL1 = gap.L1();

        // v3.2 §8 — chia NỬA chuẩn L1: gap có Σ=0 nên nửa dương đúng bằng |gap|₁/2, và tử số chỉ với
        // tới nửa đó. Chia cả |gap|₁ như v3.1 thì điểm kẹt trần ±0.5.
        decimal denom = gapL1 / 2m;
        var normalizedGap = denom == 0m ? ElementVector.Zero : gap.Divide(denom);

        // Trục cá nhân tắt (Wp = 0, hoặc chưa có ngày sinh) ⇒ d ≡ ĝ, và không có gì để hoá giải.
        if (destiny is not { } mine || personalWeight <= 0m)
            return new ElementDirection(normalizedGap, null, normalizedGap, null);

        var baseRuleScore = new ElementVector(
            Tho: ruleScoreOf(mine, FengShuiElement.Tho),
            Kim: ruleScoreOf(mine, FengShuiElement.Kim),
            Thuy: ruleScoreOf(mine, FengShuiElement.Thuy),
            Moc: ruleScoreOf(mine, FengShuiElement.Moc),
            Hoa: ruleScoreOf(mine, FengShuiElement.Hoa));

        var ruleScoreVector = ApplyOccupation(baseRuleScore, mine, occupationDelta, occupationShare);

        // Chỉ khai báo "có nghề nghiệp" khi nó DỊCH ĐƯỢC thật. Kill-switch bằng 0, delta toàn 0, hay
        // delta chỉ trỏ vào những hành đang khắc mệnh (bị chặn hết) đều ra r' ≡ r — khi đó phải trả
        // null để `OccupationShift` không đẻ ra một lớp radar phẳng lì và một dòng breakdown vô nghĩa.
        bool occupationApplied = ruleScoreVector != baseRuleScore;

        var combined = normalizedGap
            .Scale(1m - personalWeight)
            .Add(ruleScoreVector.Scale(personalWeight));

        // DetectConflict đọc `ruleScoreOf` GỐC chứ không đọc r': "hành này khắc bản mệnh" là một PHẠM
        // TRÙ do mệnh quyết định, nghề nghiệp không được phép xóa một cảnh báo xung khắc chỉ vì nó cộng dương.
        return new ElementDirection(
            normalizedGap, ruleScoreVector, combined,
            DetectConflict(normalizedGap, mine, ruleScoreOf),
            occupationApplied ? baseRuleScore : null);
    }

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
    /// </summary>
    public static ElementDirection ForPersonalNeed(ElementVector personalNeed)
    {
        decimal l1 = personalNeed.L1();
        var normalized = l1 == 0m ? ElementVector.Zero : personalNeed.Divide(l1);
        return new ElementDirection(normalized, null, normalized, null);
    }

    /// <summary>
    /// <b>N1</b> — nghề nghiệp bẻ <c>r</c>: <c>r'[e] = clamp(r[e] + delta[e]·share, −1, 1)</c>.
    ///
    /// <para>
    /// Bẻ vào đây chứ không bẻ vào <c>personalVector</c>: engine chỉ đọc <c>.Dominant()</c> của
    /// <c>personalVector</c> nên delta nhỏ sẽ bị nuốt mất, còn delta đủ lớn thì <i>lật đỉnh</i> — nhảy
    /// bậc, không mượt. <c>r</c> mới là thứ nhân thật với vector sản phẩm, nên tác động ở đây tuyến
    /// tính và liên tục.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Nghề nghiệp không đổi được bản mệnh.</b> Hành đang <c>BiKhac</c> bị chặn trần ở <c>−0.1</c>:
    /// người mệnh Mộc làm nghề cần Kim thì Kim vẫn khắc mệnh — chỉ bớt khó chịu, không thành hợp. Không
    /// có chặn này thì một dòng delta gõ sai có thể làm hệ thống gợi ý đúng thứ người dùng phải kiêng.
    /// </para>
    ///
    /// <para>Phạt điểm khi sản phẩm khắc mệnh đi đường <c>GetRelation</c> riêng, không đọc <c>r'</c> — xem §14.4.</para>
    /// </summary>
    private static ElementVector ApplyOccupation(
        ElementVector ruleScore,
        FengShuiElement destiny,
        ElementVector? delta,
        decimal share)
    {
        if (delta is not { } d || share <= 0m) return ruleScore;

        decimal Adjust(FengShuiElement e)
        {
            decimal v = Math.Clamp(ruleScore[e] + d[e] * share, -1m, 1m);
            return FengShuiCalculator.GetRelation(destiny, e) == FengShuiRelation.BiKhac
                ? Math.Min(v, -0.1m)
                : v;
        }

        return new ElementVector(
            Tho: Adjust(FengShuiElement.Tho),
            Kim: Adjust(FengShuiElement.Kim),
            Thuy: Adjust(FengShuiElement.Thuy),
            Moc: Adjust(FengShuiElement.Moc),
            Hoa: Adjust(FengShuiElement.Hoa));
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
        return new ConflictResolution(roomNeed, destiny, bridge,
            $"Phòng đang thiếu {roomNeed}, nhưng {roomNeed} khắc bản mệnh {destiny} của bạn. "
            + $"Hệ thống ưu tiên vật hành {bridge} — {roomNeed} sinh {bridge}, {bridge} sinh {destiny} — "
            + $"bù cho phòng mà vẫn nuôi bản mệnh.");
    }
}
