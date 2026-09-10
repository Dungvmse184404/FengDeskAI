using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>Mã tham số engine v3 (khớp cột <c>scoring_params.code</c>). Xem PHẦN F của spec.</summary>
/// <summary>
/// §19 — tỉ trọng ba khối nguồn trong <c>current</c>. Σ luôn = 1; <c>Evidence</c> gộp cả tag user khai
/// lẫn sản phẩm đã đặt trong phòng, vì cả hai đều là QUAN SÁT về căn phòng, đối lập với hai prior.
/// </summary>
public readonly record struct ElementBudget(decimal Interior, decimal Person, decimal Evidence);

public static class ScoringParamCodes
{
    public const string SelfShare = "SELF_SHARE";
    public const string SupportShare = "SUPPORT_SHARE";
    public const string ChildShare = "CHILD_SHARE";
    public const string MaterialShare = "MATERIAL_SHARE";
    public const string ColorShare = "COLOR_SHARE";
    public const string UserConflictPenalty = "USER_CONFLICT_PENALTY";
    public const string DirectionPenalty = "DIRECTION_PENALTY";
    public const string FallbackPrimary = "FALLBACK_PRIMARY";
    public const string FallbackSecondary = "FALLBACK_SECONDARY";
    public const string CarryPrimaryShare = "CARRY_PRIMARY_SHARE";
    public const string CarrySecondaryShare = "CARRY_SECONDARY_SHARE";
    public const string VibeMismatchPenalty = "VIBE_MISMATCH_PENALTY";
    public const string VibeUnknownPenalty = "VIBE_UNKNOWN_PENALTY";
    public const string VibeFilterHard = "VIBE_FILTER_HARD";
    public const string MinScoreThreshold = "MIN_SCORE_THRESHOLD";

    // v3.1 — trọng số trục cá nhân theo WorkspaceScope (xem personalized-recommendation-v3.1.md §3.2).
    public const string PersonalWeightPrivate = "PERSONAL_WEIGHT_PRIVATE";
    public const string PersonalWeightShared = "PERSONAL_WEIGHT_SHARED";
    public const string PersonalWeightPublic = "PERSONAL_WEIGHT_PUBLIC";

    // v3.2 — số PHIẾU trong mô hình dựng vector hiện trạng phòng (xem §10.8).
    public const string InteriorPriorVotes = "INTERIOR_PRIOR_VOTES";
    public const string PersonPresenceVotesPrivate = "PERSON_PRESENCE_VOTES_PRIVATE";
    public const string PersonPresenceVotesShared = "PERSON_PRESENCE_VOTES_SHARED";
    public const string PersonPresenceVotesPublic = "PERSON_PRESENCE_VOTES_PUBLIC";

    // P5 — nghề nghiệp bẻ vector điểm quan hệ `r` (xem §11.3, phương án N1).
    public const string OccupationShare = "OCCUPATION_SHARE";

    // §17 — nén tương phản khi dựng `current` (định luật luỹ thừa Stevens).
    public const string EvidenceSaturationAlpha = "EVIDENCE_SATURATION_ALPHA";

    // §18 — phạt phần hành khắc mệnh KHÔNG trội, chỉ ở luồng vật mang theo người.
    public const string MinorClashPenalty = "MINOR_CLASH_PENALTY";

    // §19 — ngân sách tỉ trọng ba khối nguồn khi dựng `current`. Evidence = 1 − Interior − Person,
    // suy ra chứ không seed: Σ=1 thành bất biến của công thức, không phải thứ trông chờ seed đúng.
    public const string BudgetInteriorPrivate = "BUDGET_INTERIOR_PRIVATE";
    public const string BudgetPersonPrivate = "BUDGET_PERSON_PRIVATE";
    public const string BudgetInteriorShared = "BUDGET_INTERIOR_SHARED";
    public const string BudgetPersonShared = "BUDGET_PERSON_SHARED";
    public const string BudgetInteriorPublic = "BUDGET_INTERIOR_PUBLIC";
    public const string BudgetPersonPublic = "BUDGET_PERSON_PUBLIC";
    public const string BudgetInteriorNoEvidence = "BUDGET_INTERIOR_NO_EVIDENCE";

    /// <summary>Số phiếu của chủ nhân phòng theo scope — phòng càng riêng tư, chủ nhân càng nặng.</summary>
    public static string PersonPresenceVotesFor(WorkspaceScope scope) => scope switch
    {
        WorkspaceScope.Shared => PersonPresenceVotesShared,
        WorkspaceScope.Public => PersonPresenceVotesPublic,
        _ => PersonPresenceVotesPrivate,
    };

    /// <summary>Mã tham số quyết định <c>Wp</c> của một scope — để breakdown chỉ đúng dòng cấu hình.</summary>
    public static string PersonalWeightFor(WorkspaceScope scope) => scope switch
    {
        WorkspaceScope.Shared => PersonalWeightShared,
        WorkspaceScope.Public => PersonalWeightPublic,
        _ => PersonalWeightPrivate,
    };
}

/// <summary>
/// Bộ tham số phẳng của engine (nạp từ <c>scoring_params</c>, thiếu row → default trong code).
/// </summary>
public sealed record ScoringParameters
{
    public decimal SelfShare { get; init; } = 0.60m;
    public decimal SupportShare { get; init; } = 0.30m;
    public decimal ChildShare { get; init; } = 0.10m;
    public decimal MaterialShare { get; init; } = 0.60m;
    public decimal ColorShare { get; init; } = 0.40m;
    public decimal UserConflictPenalty { get; init; } = 0.60m;
    public decimal DirectionPenalty { get; init; } = 0.30m;
    public decimal FallbackPrimary { get; init; } = 0.70m;
    public decimal FallbackSecondary { get; init; } = 0.30m;

    /// <summary>Trọng số dụng thần CHÍNH khi dựng vector mục tiêu cho vật phẩm mang theo người.</summary>
    public decimal CarryPrimaryShare { get; init; } = 0.60m;

    /// <summary>Trọng số dụng thần PHỤ (và các hành sau) cho vật phẩm mang theo người.</summary>
    public decimal CarrySecondaryShare { get; init; } = 0.40m;

    /// <summary>Trừ điểm khi sản phẩm CÓ vibe nhưng không chứa vibe hợp mục đích phòng.</summary>
    public decimal VibeMismatchPenalty { get; init; } = 0.40m;

    /// <summary>
    /// Trừ điểm khi sản phẩm CHƯA khai vibe nào. Nhẹ hơn <see cref="VibeMismatchPenalty"/>:
    /// "chưa biết" là thiếu dữ liệu, không phải bằng chứng lệch mục đích.
    /// </summary>
    public decimal VibeUnknownPenalty { get; init; } = 0.10m;

    /// <summary>
    /// Kill-switch: ≥ 0.5 → giữ nguyên hành vi v3 (lệch vibe bị LOẠI khỏi candidates);
    /// &lt; 0.5 → chuyển sang trừ điểm mềm. Seed 1.0 để merge không đổi ranking; hạ về 0
    /// qua API scoring-config sau khi đã đối chiếu golden set.
    /// </summary>
    public decimal VibeFilterHard { get; init; } = 1.00m;

    /// <summary>
    /// Điểm cuối dưới ngưỡng này thì loại khỏi danh sách gợi ý (chỉ mode Rank). Mặc định −1.0 =
    /// KHÔNG cắt (điểm đã clamp về [−1,1]). Đây là lưới an toàn thay cho các bộ lọc theo thuộc tính
    /// đơn lẻ — nâng lên 0.0 khi tắt <see cref="VibeFilterHard"/>.
    /// </summary>
    public decimal MinScoreThreshold { get; init; } = -1.00m;

    /// <summary>
    /// Tỉ trọng <c>personalScore</c> trong điểm cuối ở không gian <see cref="WorkspaceScope.Private"/>
    /// (phần còn lại <c>1 − Wp</c> dành cho gap phòng). <b>Seed 0</b> = kill-switch: giữ nguyên hành vi
    /// trước v3.1 (hard-filter khắc mệnh + <see cref="UserConflictPenalty"/>). Giá trị đích 0.50.
    /// </summary>
    public decimal PersonalWeightPrivate { get; init; } = 0.50m;

    /// <summary>Như trên, cho <see cref="WorkspaceScope.Shared"/> (phòng khách, bếp, phòng họp). Đích 0.30.</summary>
    public decimal PersonalWeightShared { get; init; } = 0.30m;

    /// <summary>
    /// Như trên, cho <see cref="WorkspaceScope.Public"/> (lễ tân, khu mở). Luôn 0 — không gian chung
    /// không được neo vào bản mệnh của một người.
    /// </summary>
    public decimal PersonalWeightPublic { get; init; } = 0.00m;

    /// <summary>
    /// <c>Wp</c> thực tế cho một phiên: <b>0 khi user chưa có ngày sinh</b>, bất kể scope — không có bản
    /// mệnh thì không có gì để trộn. Gom vào đây thay vì để mỗi service tự nhớ điều kiện đó.
    /// </summary>
    public decimal PersonalWeightFor(WorkspaceScope scope, DateTime? dateOfBirth)
        => dateOfBirth is null ? 0m : PersonalWeightFor(scope);

    /// <summary>Tỉ trọng cá nhân thuần theo scope, chưa xét user đã khai ngày sinh chưa.</summary>
    public decimal PersonalWeightFor(WorkspaceScope scope) => scope switch
    {
        WorkspaceScope.Private => PersonalWeightPrivate,
        WorkspaceScope.Shared => PersonalWeightShared,
        WorkspaceScope.Public => PersonalWeightPublic,
        _ => PersonalWeightPrivate,
    };

    /// <summary>
    /// Hệ số nhân cho delta nghề nghiệp: <c>r'[e] = clamp(r[e] + delta[e]·OccupationShare, −1, 1)</c>.
    ///
    /// <para>
    /// <b>Seed 0.00</b> = kill-switch: mọi delta × 0 ⇒ kết quả <b>y hệt</b> khi chưa có P5, kể cả khi bảng
    /// nghề đã có dữ liệu. Bật lên sau khi chuyên gia phong thủy duyệt bảng delta — cùng quy trình
    /// <c>PERSONAL_WEIGHT_*</c> và <c>VIBE_FILTER_HARD</c> đã đi.
    /// </para>
    /// </summary>
    public decimal OccupationShare { get; init; } = 0.00m;

    /// <summary>Tỉ trọng NỀN PHÒNG trong <c>current</c> ở không gian riêng tư.</summary>
    public decimal BudgetInteriorPrivate { get; init; } = 0.30m;

    /// <summary>Tỉ trọng BẢN MỆNH chủ nhân ở không gian riêng tư — phòng của bạn thì bạn nặng nhất.</summary>
    public decimal BudgetPersonPrivate { get; init; } = 0.40m;

    /// <summary>Tỉ trọng NỀN PHÒNG ở không gian dùng chung.</summary>
    public decimal BudgetInteriorShared { get; init; } = 0.40m;

    /// <summary>Tỉ trọng BẢN MỆNH ở không gian dùng chung — nhẹ hơn Private vì phòng còn của người khác.</summary>
    public decimal BudgetPersonShared { get; init; } = 0.30m;

    /// <summary>Tỉ trọng NỀN PHÒNG ở không gian công cộng.</summary>
    public decimal BudgetInteriorPublic { get; init; } = 0.60m;

    /// <summary>
    /// Tỉ trọng BẢN MỆNH ở không gian công cộng — <b>0</b>. Lễ tân không thuộc về ai, neo nó vào bản
    /// mệnh của MỘT người là sai bản chất (Q12 · §14.3, cùng lý do <c>PERSONAL_WEIGHT_PUBLIC = 0</c>).
    /// </summary>
    public decimal BudgetPersonPublic { get; init; } = 0.00m;

    /// <summary>
    /// Tỉ trọng NỀN PHÒNG khi phòng <b>chưa có bằng chứng nào</b> (không tag, không sản phẩm đã đặt).
    /// Phần còn lại thuộc bản mệnh. Không suy ra từ ngân sách của scope vì tỉ lệ mong muốn khác hẳn:
    /// Private muốn 60/40 chứ không phải 30/40 chuẩn hoá thành 43/57.
    /// </summary>
    public decimal BudgetInteriorNoEvidence { get; init; } = 0.60m;

    /// <summary>
    /// §19 — chia <c>current</c> thành ba khối theo TỈ TRỌNG CỐ ĐỊNH thay vì theo phiếu.
    ///
    /// <para>
    /// Vì sao đổi: mô hình phiếu (§12) để khối bằng chứng nuốt dần hai prior — khai 20 tag thì nền
    /// phòng còn 12% và bản mệnh còn 8%, tức người dùng càng chăm khai càng tự xoá bản mệnh của mình
    /// khỏi phân tích. Ngân sách cố định chặn đúng chuyện đó.
    /// </para>
    ///
    /// <para>
    /// Đánh đổi đã biết và chấp nhận: một tag duy nhất nay gánh trọn ngân sách bằng chứng (30%) thay
    /// vì 1/6 như mô hình phiếu. Tỉ lệ <b>bên trong</b> khối bằng chứng vẫn chia theo phiếu, nên tag
    /// nặng/nhẹ và sản phẩm <c>voteWeight</c> vẫn có tác dụng tương đối như cũ.
    /// </para>
    ///
    /// <para>Ba luật điều chỉnh, theo đúng thứ tự:</para>
    /// <list type="number">
    /// <item>Chưa có bằng chứng nào ⇒ <c>(BudgetInteriorNoEvidence, phần còn lại, 0)</c>.</item>
    /// <item>User chưa có ngày sinh ⇒ phần bản mệnh về 0, chia lại cho hai khối kia theo tỉ lệ.</item>
    /// <item>Chuẩn hoá để Σ = 1 trong mọi trường hợp còn lại.</item>
    /// </list>
    /// </summary>
    public ElementBudget ElementBudgetFor(WorkspaceScope scope, bool hasPerson, bool hasEvidence)
    {
        var (interior, person) = scope switch
        {
            WorkspaceScope.Shared => (BudgetInteriorShared, BudgetPersonShared),
            WorkspaceScope.Public => (BudgetInteriorPublic, BudgetPersonPublic),
            _ => (BudgetInteriorPrivate, BudgetPersonPrivate),
        };
        if (!hasPerson) person = 0m;
        decimal evidence = Math.Max(0m, 1m - interior - person);

        if (!hasEvidence)
        {
            // Không có gì để đổ vào khối bằng chứng. Public thì person vốn đã 0 nên ra nền phòng 100%,
            // đúng ý: phòng chung chưa khai gì thì chỉ còn biết nó là loại phòng gì.
            // Xét phần mệnh SAU khi scope đã ép (Public luôn 0), không xét cờ hasPerson của caller:
            // truyền chủ nhân vào một phòng Public rồi rơi vào luật 60/40 là lễ tân mọc ra 40% bản
            // mệnh của một người — đúng thứ Q12 cấm.
            if (person <= 0m) return new ElementBudget(1m, 0m, 0m);
            return new ElementBudget(BudgetInteriorNoEvidence, 1m - BudgetInteriorNoEvidence, 0m);
        }

        decimal sum = interior + person + evidence;
        return sum <= 0m
            ? new ElementBudget(1m, 0m, 0m)
            : new ElementBudget(interior / sum, person / sum, evidence / sum);
    }

    /// <summary>
    /// Phạt phần hành KHẮC bản mệnh <b>không phải hành trội</b> của vật mang theo người, tính theo
    /// đúng tỉ trọng: <c>penalty = MinorClashPenalty × Σ product[e] (e khắc mệnh)</c>.
    ///
    /// <para>
    /// Bịt một lỗ hổng chỉ có ở nhánh <see cref="ScoringTarget.PersonalNeed"/>: vector dụng thần
    /// <b>Σ=1 và không âm</b> nên không có trục nào mang dấu trừ, còn bộ lọc xung khắc lại chỉ so
    /// hành TRỘI. Kết quả là một vật 50% Kim / 20% Hỏa đeo trên người mệnh Kim lọt qua cả hai: hành
    /// trội Kim tỷ hòa nên không bị lọc, còn 20% Hỏa nhân với <c>dụngThần[Hỏa] = 0</c> nên biến mất
    /// không dấu vết.
    /// </para>
    ///
    /// <para>
    /// <b>KHÔNG áp cho luồng phòng.</b> Ở đó <c>d = (1−Wp)·ĝ + Wp·r</c> với <c>r</c> có dấu đã tính
    /// phần khắc theo đúng tỉ trọng rồi; cộng thêm penalty này nữa là đếm hai lần.
    /// </para>
    ///
    /// <para>
    /// Hành trội mà khắc mệnh thì <b>vẫn đi đường cũ</b> (loại cứng / phạt đủ
    /// <see cref="UserConflictPenalty"/>), không rơi vào công thức tỉ trọng — cố ý giữ nguyên hành vi
    /// đã có thay vì nới lỏng đúng nhóm cần phạt nặng nhất. Đổi lại là một bậc nhảy quanh ngưỡng trội,
    /// chấp nhận được vì luật v3 vốn đã nhị phân theo hành trội.
    /// </para>
    ///
    /// <para><b>Seed 0.60</b> — ngang <see cref="UserConflictPenalty"/>. Đặt <c>0</c> = tắt hẳn, kết quả
    /// byte-identical như trước §18.</para>
    /// </summary>
    public decimal MinorClashPenalty { get; init; } = 0.60m;

    /// <summary>
    /// Số mũ nén tương phản khi dựng <c>current</c>: <c>current = normalize(khốiLượng^α)</c>.
    ///
    /// <para>
    /// Sửa đúng một tật: khai 12 tag Mộc thì Mộc chiếm ~60% hiện trạng và nuốt gần hết bốn hành còn
    /// lại, dù 12 tag đó chỉ nói "phòng nhiều gỗ" chứ không nói "phòng gấp 12 lần gỗ". Cảm nhận về số
    /// lượng vốn <b>không tuyến tính</b> (định luật luỹ thừa Stevens).
    /// </para>
    ///
    /// <para>
    /// <b>Seed 0.60</b> — khoảng số mũ thực nghiệm cho cảm nhận về DIỆN TÍCH/SỐ LƯỢNG nhìn thấy.
    /// <c>α = 1</c> là kill-switch (tuyến tính, hành vi trước §17); <c>α → 0</c> đẩy mọi hành về đều nhau.
    /// Default ở đây <b>phải khớp seed</b>: lệch nhau thì môi trường thiếu row sẽ chấm khác prod —
    /// đúng vết <c>PERSONAL_WEIGHT_*</c> đã vấp.
    /// </para>
    ///
    /// <para>
    /// Chọn luỹ thừa chứ không phải <c>log</c> vì luỹ thừa <b>bất biến theo tỉ lệ</b>: khai 6 tag hay
    /// khai đúng 6 tag đó nhân đôi thành 12 đều ra cùng một hình. <c>log</c> thì không — người khai kỹ
    /// hơn bị đẩy về phía cân bằng, tức bị phạt vì cẩn thận.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Đây là bộ hãm MỀM, không phải trần cứng: ở 80 tag Mộc, <c>α = 0.6</c> vẫn cho Mộc 69%. Nó
    /// làm một hành cần <b>2–3 lần</b> số tag mới đạt cùng mức áp đảo, và giữ nguyên thứ tự giữa các
    /// hành — thứ mà trần cứng sẽ đánh mất (vượt trần rồi thì 20 tag và 200 tag trông y hệt).
    /// </para>
    /// </summary>
    public decimal EvidenceSaturationAlpha { get; init; } = 0.60m;

    /// <summary>
    /// Số phiếu quy ước của nền phòng — prior Dirichlet: nền phòng LUÔN có mặt, nặng ngang 3 tag thật,
    /// và loãng dần khi user khai thêm tag. Trước đây hard-code trong <c>ElementVectorBuilders</c>;
    /// đưa vào bảng để cùng họ với số phiếu chủ nhân.
    /// </summary>
    public decimal InteriorPriorVotes { get; init; } = 3.00m;

    /// <summary>
    /// Số phiếu của CHỦ NHÂN phòng ở <see cref="WorkspaceScope.Private"/> — bản mệnh của họ cũng là một
    /// nguồn ngũ hành trong không gian.
    /// <para>
    /// Dùng <b>số phiếu</b> chứ không phải tỉ trọng cố định: bản mệnh là <i>prior</i> nên nó phải loãng
    /// dần khi user khai thêm tag, đúng cách <see cref="InteriorPriorVotes"/> cư xử. Tỉ trọng cố định
    /// thì khai 20 tag thật mà bản mệnh vẫn giữ nguyên phần — ngược triết lý "bằng chứng lấn át suy đoán".
    /// </para>
    /// <para>Mặc định 3 = <b>ngang nền phòng</b>: một mốc giải thích được, thay vì một con số tự chọn.</para>
    /// </summary>
    public decimal PersonPresenceVotesPrivate { get; init; } = 3.00m;

    /// <summary>Như trên, cho <see cref="WorkspaceScope.Shared"/> — nhẹ hơn vì phòng chia với người khác.</summary>
    public decimal PersonPresenceVotesShared { get; init; } = 2.00m;

    /// <summary>
    /// Như trên, cho <see cref="WorkspaceScope.Public"/>. Luôn 0 — không gian chung không có "chủ nhân"
    /// nào để đưa bản mệnh vào, cùng lý lẽ với <see cref="PersonalWeightPublic"/>.
    /// </summary>
    public decimal PersonPresenceVotesPublic { get; init; } = 0.00m;

    /// <summary>Số phiếu chủ nhân ứng với một scope; <c>0</c> khi user chưa có ngày sinh.</summary>
    public decimal PersonPresenceVotesFor(WorkspaceScope scope, DateTime? dateOfBirth) => dateOfBirth is null
        ? 0m
        : scope switch
        {
            WorkspaceScope.Shared => PersonPresenceVotesShared,
            WorkspaceScope.Public => PersonPresenceVotesPublic,
            _ => PersonPresenceVotesPrivate,
        };

    public static ScoringParameters Default { get; } = new();

    /// <summary>Dựng từ các row DB; code lạ bị bỏ qua, code thiếu giữ default.</summary>
    public static ScoringParameters FromRows(IEnumerable<ScoringParam> rows)
    {
        var map = rows.ToDictionary(r => r.Code, r => r.Value, StringComparer.OrdinalIgnoreCase);
        var d = Default;
        decimal V(string code, decimal fallback) => map.TryGetValue(code, out var v) ? v : fallback;
        return new ScoringParameters
        {
            SelfShare = V(ScoringParamCodes.SelfShare, d.SelfShare),
            SupportShare = V(ScoringParamCodes.SupportShare, d.SupportShare),
            ChildShare = V(ScoringParamCodes.ChildShare, d.ChildShare),
            MaterialShare = V(ScoringParamCodes.MaterialShare, d.MaterialShare),
            ColorShare = V(ScoringParamCodes.ColorShare, d.ColorShare),
            UserConflictPenalty = V(ScoringParamCodes.UserConflictPenalty, d.UserConflictPenalty),
            DirectionPenalty = V(ScoringParamCodes.DirectionPenalty, d.DirectionPenalty),
            FallbackPrimary = V(ScoringParamCodes.FallbackPrimary, d.FallbackPrimary),
            FallbackSecondary = V(ScoringParamCodes.FallbackSecondary, d.FallbackSecondary),
            CarryPrimaryShare = V(ScoringParamCodes.CarryPrimaryShare, d.CarryPrimaryShare),
            CarrySecondaryShare = V(ScoringParamCodes.CarrySecondaryShare, d.CarrySecondaryShare),
            VibeMismatchPenalty = V(ScoringParamCodes.VibeMismatchPenalty, d.VibeMismatchPenalty),
            VibeUnknownPenalty = V(ScoringParamCodes.VibeUnknownPenalty, d.VibeUnknownPenalty),
            VibeFilterHard = V(ScoringParamCodes.VibeFilterHard, d.VibeFilterHard),
            MinScoreThreshold = V(ScoringParamCodes.MinScoreThreshold, d.MinScoreThreshold),
            InteriorPriorVotes = V(ScoringParamCodes.InteriorPriorVotes, d.InteriorPriorVotes),
            PersonPresenceVotesPrivate = V(ScoringParamCodes.PersonPresenceVotesPrivate, d.PersonPresenceVotesPrivate),
            PersonPresenceVotesShared = V(ScoringParamCodes.PersonPresenceVotesShared, d.PersonPresenceVotesShared),
            PersonPresenceVotesPublic = V(ScoringParamCodes.PersonPresenceVotesPublic, d.PersonPresenceVotesPublic),
            PersonalWeightPrivate = V(ScoringParamCodes.PersonalWeightPrivate, d.PersonalWeightPrivate),
            PersonalWeightShared = V(ScoringParamCodes.PersonalWeightShared, d.PersonalWeightShared),
            PersonalWeightPublic = V(ScoringParamCodes.PersonalWeightPublic, d.PersonalWeightPublic),
            OccupationShare = V(ScoringParamCodes.OccupationShare, d.OccupationShare),
            EvidenceSaturationAlpha = V(ScoringParamCodes.EvidenceSaturationAlpha, d.EvidenceSaturationAlpha),
            MinorClashPenalty = V(ScoringParamCodes.MinorClashPenalty, d.MinorClashPenalty),
            BudgetInteriorPrivate = V(ScoringParamCodes.BudgetInteriorPrivate, d.BudgetInteriorPrivate),
            BudgetPersonPrivate = V(ScoringParamCodes.BudgetPersonPrivate, d.BudgetPersonPrivate),
            BudgetInteriorShared = V(ScoringParamCodes.BudgetInteriorShared, d.BudgetInteriorShared),
            BudgetPersonShared = V(ScoringParamCodes.BudgetPersonShared, d.BudgetPersonShared),
            BudgetInteriorPublic = V(ScoringParamCodes.BudgetInteriorPublic, d.BudgetInteriorPublic),
            BudgetPersonPublic = V(ScoringParamCodes.BudgetPersonPublic, d.BudgetPersonPublic),
            BudgetInteriorNoEvidence = V(ScoringParamCodes.BudgetInteriorNoEvidence, d.BudgetInteriorNoEvidence),
        };
    }
}

/// <summary>
/// Hồ sơ phong thủy cá nhân (mệnh Nạp Âm + Kua) — GIỮ cho hiển thị hồ sơ &amp; AI diễn giải.
/// Điểm v3 không dùng Kua; chỉ mệnh (qua <see cref="Element"/>) tham gia bộ lọc.
/// </summary>
public sealed record PersonalProfile(
    FengShuiElement Element,
    int? KuaNumber,
    KuaGroup? Group,
    IReadOnlySet<CompassDirection> FavorableDirections);

/// <summary>
/// Bối cảnh 1 phiên chấm điểm v3. Mọi thực thể quy về <see cref="ElementVector"/>.
/// </summary>
public sealed record ScoringContext
{
    /// <summary>Null → bỏ qua bộ lọc mệnh (thiếu ngày sinh).</summary>
    public ElementVector? PersonalVector { get; init; }

    /// <summary>Vector lý tưởng đã bẻ theo Intent.</summary>
    public required ElementVector AdjustedIdeal { get; init; }

    /// <summary>Vector hiện trạng phòng (màu/vật liệu, hoặc Interior mặc định).</summary>
    public required ElementVector CurrentVector { get; init; }

    /// <summary>
    /// Vector mục tiêu của NGƯỜI (dụng thần Tứ Trụ, fallback Nạp Âm) — chỉ dùng cho
    /// <see cref="ProductPlacement.Carry"/>. Null ở luồng workspace.
    /// </summary>
    public ElementVector? PersonalNeedVector { get; init; }

    public required WorkspaceScope Scope { get; init; }
    public required WorkPurpose Purpose { get; init; }

    /// <summary>Hướng bị chắn (cửa vào ∪ WC ∪ góc tối) — dùng ở Directional Validation.</summary>
    public IReadOnlySet<CompassDirection> ViolatedDirections { get; init; } = new HashSet<CompassDirection>();

    /// <summary>
    /// Tỉ trọng <c>personalScore</c> trong điểm cuối của nhánh <see cref="ScoringTarget.WorkspaceGap"/>
    /// (v3.1). <c>0</c> = tắt trục cá nhân → giữ nguyên hành vi trước v3.1 (hard-filter khắc mệnh +
    /// <see cref="ScoringParameters.UserConflictPenalty"/>). Không áp cho nhánh
    /// <see cref="ScoringTarget.PersonalNeed"/> — nhánh đó vốn đã 100% cá nhân.
    /// </summary>
    public decimal PersonalWeight { get; init; }

    /// <summary>
    /// Bảng điểm quan hệ ngũ hành nạp từ <c>feng_shui_rules</c> (admin chỉnh được): khóa
    /// <c>(mệnh user, hành sản phẩm)</c>. Null/thiếu khóa → rơi về
    /// <see cref="FengShuiCalculator.DefaultScore"/>.
    /// </summary>
    public IReadOnlyDictionary<(FengShuiElement Subject, FengShuiElement Object), decimal>? RuleScores { get; init; }

    /// <summary>
    /// Delta ngũ hành theo NGHỀ NGHIỆP của user (P5/N1) — cộng vào <c>r</c>, không cộng vào
    /// <see cref="PersonalVector"/>. <c>null</c> = user chưa khai nghề hoặc nghề đó chưa có dòng delta nào.
    /// <para>Không áp cho nhánh <see cref="ScoringTarget.PersonalNeed"/>: nhánh đó không có <c>r</c> để bẻ.</para>
    /// </summary>
    public ElementVector? OccupationDelta { get; init; }

    /// <summary>Mã nghề đã áp, vd <c>"IT"</c> — để breakdown chỉ đúng dòng cấu hình. <c>null</c> khi không áp.</summary>
    public string? OccupationCode { get; init; }

    /// <summary>Tên nghề tiếng Việt — hiển thị cho user trong breakdown.</summary>
    public string? OccupationNameVi { get; init; }

    /// <summary>Mục tiêu người dùng nêu trong phiên này (đã dùng để lọc ứng viên). Null = không nêu.</summary>
    public Aspiration? Aspiration { get; init; }

    /// <summary>
    /// Hướng Bát Trạch ưu tiên theo <see cref="Aspiration"/>, tốt nhất trước. Rỗng → <c>placementHint</c>
    /// giữ hành vi cũ (lấy hướng hợp đầu tiên).
    /// </summary>
    public IReadOnlyList<AspirationDirection> AspirationDirections { get; init; } = Array.Empty<AspirationDirection>();

    public ScoringParameters Params { get; init; } = ScoringParameters.Default;

    /// <summary>
    /// Nghề nghiệp có đang tác động không: cần cả delta lẫn hệ số &gt; 0. Delta bẻ <c>r</c>, mà <c>r</c>
    /// chỉ tồn tại khi trục cá nhân bật — nên nghề nghiệp kèm theo điều kiện đó.
    /// </summary>
    public bool OccupationActive => OccupationDelta is not null && Params.OccupationShare > 0m && PersonalBlendActive;

    /// <summary>Trục cá nhân có đang bật không: cần cả trọng số &gt; 0 lẫn vector mệnh (user có ngày sinh).</summary>
    public bool PersonalBlendActive => PersonalWeight > 0m && PersonalVector is not null;

    /// <summary>
    /// Điểm quan hệ từ mệnh user tới một hành, có dấu: tỷ hòa +1.0 … bị khắc −1.0.
    /// Ưu tiên giá trị admin cấu hình, thiếu thì dùng default trong code.
    /// </summary>
    public decimal RuleScoreOf(FengShuiElement subject, FengShuiElement obj)
        => RuleScores is not null && RuleScores.TryGetValue((subject, obj), out var v)
            ? v
            : FengShuiCalculator.DefaultScore(FengShuiCalculator.GetRelation(subject, obj));
}

/// <summary>Một hướng Bát Trạch kèm tên cung, đã lọc theo mục tiêu người dùng (vd Đông Nam — Sinh Khí).</summary>
public sealed record AspirationDirection(CompassDirection Direction, string CungName);

/// <summary>Thuộc tính phong thủy của 1 sản phẩm ứng viên (đã rút &amp; dựng vector từ DB).</summary>
public sealed record ProductFacts(
    Guid ProductId,
    ElementVector Vector,
    IReadOnlySet<string> Vibes,
    ProductPlacement Placement = ProductPlacement.Desk);

/// <summary>Nguồn vector mục tiêu khi chấm điểm 1 sản phẩm.</summary>
public enum ScoringTarget
{
    /// <summary>Gap của phòng: <c>AdjustedIdeal − Current</c>.</summary>
    WorkspaceGap,

    /// <summary>Vector dụng thần/bản mệnh của người dùng (<see cref="ScoringContext.PersonalNeedVector"/>).</summary>
    PersonalNeed,
}

/// <summary>Cách xử lý hướng đặt vật phẩm.</summary>
public enum DirectionMode
{
    /// <summary>Directional Validation của engine v3: hết hướng hợp → phạt + caution.</summary>
    Soft,

    /// <summary>Không xét hướng (vật di chuyển theo người, hoặc cây đặt theo ánh sáng).</summary>
    None,
}

/// <summary>Mức độ nghiêm khắc của bộ lọc khắc bản mệnh.</summary>
public enum PersonalConflictMode
{
    /// <summary>Loại cứng khi <see cref="WorkspaceScope.Private"/>, còn lại chỉ trừ điểm (luật v3).</summary>
    ByScope,

    /// <summary>Luôn loại cứng — vật đeo trên người là riêng tư tuyệt đối.</summary>
    AlwaysHard,

    /// <summary>
    /// Không loại, không trừ: dùng ở <see cref="WorkspaceScope.Public"/> — không gian chung không được
    /// neo vào bản mệnh của MỘT người, nên không lọc cũng không phạt theo mệnh
    /// (<c>score-explainability-v3.2.md</c> §14.3 · Q12).
    /// </summary>
    None,

    /// <summary>
    /// v3.2 (L2) — không loại, nhưng trừ <c>USER_CONFLICT_PENALTY × Wp</c> khi hành trội sản phẩm
    /// <c>BiKhac</c> bản mệnh. Thay <see cref="None"/> của v3.1 ở nhánh workspace: khi phòng cần đúng
    /// hành khắc mệnh, hai lực trong <c>d</c> triệt tiêu nhau và sản phẩm hiện "Trung tính" — sai bản
    /// chất. <c>personalScore</c> đo <b>mức độ hợp</b> (liên tục), "bị khắc" là một <b>phạm trù kiêng
    /// kỵ</b>; hai đại lượng khác loại nên tách hai số hạng, không phải tính phạt hai lần.
    /// Chỉ dùng khi <see cref="ScoringContext.PersonalBlendActive"/>. Xem §14.2.
    /// </summary>
    Scaled,
}

/// <summary>
/// Luật chấm điểm theo <see cref="ProductPlacement"/> — khai báo dạng BẢNG thay vì nhánh <c>switch</c>
/// rải trong <c>RecommendationScorer</c>. Thêm placement mới = thêm một dòng ở <see cref="For"/>.
/// Xem <c>docs/adr/product-placement-personal-recommendation.md</c> §3.
/// </summary>
public sealed record PlacementPolicy(
    bool IsRecommendable,
    ScoringTarget Target,
    DirectionMode Direction,
    PersonalConflictMode Conflict,
    bool UsePurposeVibe)
{
    /// <summary>
    /// Luật dùng cho trang chi tiết sản phẩm (<c>ScoreSingle</c>): luôn chấm theo phòng và KHÔNG BAO GIỜ
    /// loại — placement chỉ sinh caution. Giữ đúng hợp đồng "fit luôn có kết quả".
    /// </summary>
    public static PlacementPolicy WorkspaceFit(bool personalBlendActive, WorkspaceScope scope) =>
        new(true, ScoringTarget.WorkspaceGap, DirectionMode.Soft, WorkspaceConflict(personalBlendActive, scope), false);

    /// <summary>
    /// Cách xử lý khắc bản mệnh cho nhánh chấm theo phòng — ba trạng thái, xét theo đúng thứ tự này:
    /// <list type="number">
    /// <item><see cref="WorkspaceScope.Public"/> → <see cref="PersonalConflictMode.None"/>: không gian
    /// chung không lọc/phạt theo bản mệnh một người (v3.2 §14.3 · Q12). Thắng cả hai nhánh dưới vì
    /// <c>PERSONAL_WEIGHT_PUBLIC</c> cố định 0 ⇒ nếu không chặn ở đây, Public sẽ rơi vào
    /// <see cref="PersonalConflictMode.ByScope"/> và bị trừ ĐỦ penalty — đúng cái bất nhất cần bỏ.</item>
    /// <item>Trục cá nhân BẬT → <see cref="PersonalConflictMode.Scaled"/> (L2, v3.2 §14.2).</item>
    /// <item>Trục cá nhân TẮT (<c>Wp = 0</c>) → <see cref="PersonalConflictMode.ByScope"/>, luật v3
    /// nguyên bản. Đứt gãy tại <c>Wp = 0</c> là CÓ CHỦ ĐÍCH — xem §14.3(a).</item>
    /// </list>
    /// </summary>
    private static PersonalConflictMode WorkspaceConflict(bool personalBlendActive, WorkspaceScope scope)
        => scope == WorkspaceScope.Public ? PersonalConflictMode.None
        : personalBlendActive ? PersonalConflictMode.Scaled
        : PersonalConflictMode.ByScope;

    public static PlacementPolicy For(
        ProductPlacement placement,
        bool personalBlendActive = false,
        WorkspaceScope scope = WorkspaceScope.Private) => placement switch
    {
        ProductPlacement.Desk =>
            new(true, ScoringTarget.WorkspaceGap, DirectionMode.Soft, WorkspaceConflict(personalBlendActive, scope), true),

        // Cây/vật sống: vị trí do ánh sáng quyết định, không theo la bàn.
        ProductPlacement.Living =>
            new(true, ScoringTarget.WorkspaceGap, DirectionMode.None, WorkspaceConflict(personalBlendActive, scope), true),

        // Carry KHÔNG chịu ảnh hưởng của PersonalWeight lẫn Scope: mục tiêu của nó vốn đã 100% cá nhân,
        // và vật đeo trên người là riêng tư tuyệt đối nên giữ AlwaysHard kể cả ở Public (§14.6 #4).
        ProductPlacement.Carry =>
            new(true, ScoringTarget.PersonalNeed, DirectionMode.None, PersonalConflictMode.AlwaysHard, false),

        // Hàng tiêu hao: không vào bất kỳ luồng gợi ý nào.
        ProductPlacement.Consumable =>
            new(false, ScoringTarget.WorkspaceGap, DirectionMode.None, PersonalConflictMode.ByScope, false),

        _ => new(true, ScoringTarget.WorkspaceGap, DirectionMode.Soft, WorkspaceConflict(personalBlendActive, scope), true),
    };
}

/// <summary>Kết quả chấm điểm 1 sản phẩm + các "sự thật" để AI diễn giải + gợi ý vị trí đặt.</summary>
public sealed record ScoredProduct(
    Guid ProductId,
    decimal Score,
    IReadOnlyList<string> MatchFacts,
    IReadOnlyList<string> CautionFacts,
    string? PlacementHint,
    ScoreBreakdown? Breakdown = null);

// ─────────────────────────── v3.2 §9 — giải thích điểm số ───────────────────────────

/// <summary>Mã thành phần CỘNG vào điểm. FE map sang i18n/icon theo mã, không parse nhãn.</summary>
public static class ScoreComponentCodes
{
    public const string GapScore = "GAP_SCORE";
    public const string PersonalScore = "PERSONAL_SCORE";

    /// <summary>Nhánh <see cref="ScoringTarget.PersonalNeed"/> (vật mang theo người) — thành phần DUY NHẤT.</summary>
    public const string PersonalNeedScore = "PERSONAL_NEED_SCORE";
}

/// <summary>
/// Một số hạng CỘNG vào điểm: <c>Contribution = Value × Weight</c>. Tổng mọi
/// <see cref="Contribution"/> = <see cref="ScoreBreakdown.Blended"/>.
/// </summary>
public sealed record ScoreComponent(
    string Code,
    string LabelVi,
    decimal Value,
    decimal Weight,
    decimal Contribution,
    string ReasonVi);

/// <summary>
/// Một số hạng TRỪ khỏi điểm. Luôn phát đủ mọi loại penalty kể cả khi
/// <see cref="Applied"/> = <c>false</c> — user cần thấy "cái này đã được xét và không bị trừ",
/// khác hẳn "cái này không tồn tại".
/// </summary>
public sealed record ScorePenalty(
    string Code,
    string LabelVi,
    decimal Value,
    bool Applied,
    string ReasonVi);

/// <summary>
/// v3.2 §13 — phòng đang thiếu đúng hành KHẮC bản mệnh. Engine tự giải bằng
/// <b>hành hoá giải</b> (con của hành phòng cần = mẹ của bản mệnh), việc còn lại chỉ là NÓI RA.
/// <c>null</c> khi không có xung khắc.
/// </summary>
public sealed record ConflictResolution(
    FengShuiElement RoomNeed,
    FengShuiElement Destiny,
    FengShuiElement Bridge,
    string ReasonVi);

/// <summary>
/// Toàn bộ số liệu trung gian đã tạo ra <see cref="ScoredProduct.Score"/> — v3.2 §9.1.
/// Trước đây chúng là biến cục bộ trong <c>ScoreOne</c> nên màn hình không giải thích được gì.
///
/// <para><b>Bất biến (khoá bằng test <c>SCORE-BD-*</c>):</b></para>
/// <code>
/// Σ Components[i].Contribution                            == Blended
/// ProductVector · CombinedDirection                       ≈  Blended     (sai số chia decimal)
/// Blended − UserPenalty − DirectionPenalty − VibePenalty  == RawScore
/// round(clamp(RawScore, −1, 1), 3)                        == Score
/// </code>
/// </summary>
public sealed record ScoreBreakdown(
    string FormulaVersion,
    ScoringTarget Target,
    ProductPlacement Placement,

    decimal GapScore,
    decimal? PersonalScore,
    decimal PersonalWeight,

    /// <summary>Mã tham số đã quyết định <see cref="PersonalWeight"/>, vd <c>PERSONAL_WEIGHT_PRIVATE</c>.</summary>
    string? PersonalWeightCode,

    decimal Blended,
    decimal UserPenalty,
    decimal DirectionPenalty,
    decimal VibePenalty,

    /// <summary>Điểm TRƯỚC khi clamp và làm tròn.</summary>
    decimal RawScore,

    /// <summary><c>true</c> khi <see cref="RawScore"/> nằm ngoài [−1,1] và đã bị cắt.</summary>
    bool Clamped,

    /// <summary>Vector ngũ hành của sản phẩm, Σ=1.</summary>
    ElementVector ProductVector,

    /// <summary>
    /// <c>ĝ = gap / (|gap|₁/2)</c>, mỗi trục ∈ [−1,+1] — nhánh <see cref="ScoringTarget.WorkspaceGap"/>.
    /// Ở nhánh <see cref="ScoringTarget.PersonalNeed"/> đây là vector dụng thần đã chuẩn hoá
    /// (Σ=1, không âm), KHÔNG phải gap.
    /// </summary>
    ElementVector NormalizedGap,

    /// <summary>
    /// <c>r'[e]</c> — điểm quan hệ ĐÃ tính nghề nghiệp, CÓ DẤU. <c>null</c> khi trục cá nhân tắt.
    /// Bằng <see cref="BaseRuleScoreVector"/> khi nghề nghiệp không áp.
    /// </summary>
    ElementVector? RuleScoreVector,

    /// <summary>
    /// <c>d = (1−Wp)·ĝ + Wp·r'</c> — thứ thật sự nhân với <see cref="ProductVector"/>.
    /// <b>Đây là vector radar cần vẽ</b> (§10.3), không phải <c>personalVector</c>.
    /// </summary>
    ElementVector CombinedDirection,

    /// <summary>Vector dụng thần — chỉ luồng <see cref="ProductPlacement.Carry"/>.</summary>
    ElementVector? PersonalNeedVector,

    /// <summary>
    /// Vector bản mệnh Σ=1 (self 60 / hành sinh mệnh 30 / hành mệnh sinh 10). <c>null</c> khi user chưa
    /// có ngày sinh. Engine KHÔNG chấm điểm bằng vector này (§5 — nó chỉ lấy <c>.Dominant()</c>); trả ra
    /// để FE dựng lớp "Mục tiêu của bạn" trên radar.
    /// </summary>
    ElementVector? PersonalVector,

    /// <summary>
    /// <c>T = (1−Wp)·adjustedIdeal + Wp·personalVector</c>, Σ=1 — lớp radar "Mục tiêu của bạn".
    /// <b>Chỉ để hiển thị</b>, xem <see cref="ElementDirection.PersonalTargetOf"/>.
    /// </summary>
    ElementVector? PersonalTarget,

    IReadOnlyList<ScoreComponent> Components,
    IReadOnlyList<ScorePenalty> Penalties,
    ConflictResolution? ConflictResolution,

    /// <summary>Hành Nạp Âm của user. <c>null</c> khi chưa có ngày sinh.</summary>
    FengShuiElement? DestinyElement,

    /// <summary>
    /// <c>r</c> TRƯỚC khi cộng delta nghề nghiệp (P5). <c>null</c> khi nghề nghiệp không áp — khi đó
    /// <see cref="RuleScoreVector"/> đã là <c>r</c> gốc rồi.
    /// </summary>
    ElementVector? BaseRuleScoreVector = null,

    /// <summary>Mã nghề đã áp, vd <c>"IT"</c>. <c>null</c> khi user chưa khai nghề hoặc kill-switch đang tắt.</summary>
    string? OccupationCode = null,

    /// <summary>Tên nghề tiếng Việt — nhãn cho lớp radar nghề nghiệp.</summary>
    string? OccupationNameVi = null,

    /// <summary><c>OCCUPATION_SHARE</c> đã áp. <c>0</c> khi nghề nghiệp không tác động.</summary>
    decimal OccupationShare = 0m)
{
    /// <summary>
    /// <c>r' − r</c> — phần nghề nghiệp THẬT SỰ dịch được sau khi đã chặn hành khắc mệnh. <c>null</c>
    /// khi nghề nghiệp không áp. Hiển thị con số này chứ không phải <c>delta·share</c>: user cần thấy
    /// nghề của mình không kéo nổi một hành kiêng kỵ lên, chứ không phải con số danh nghĩa.
    /// </summary>
    public ElementVector? OccupationShift =>
        BaseRuleScoreVector is { } b && RuleScoreVector is { } r ? r.Subtract(b) : null;

    /// <summary>
    /// <c>normalize(max(d, 0))</c> — Σ=1, vẽ chồng được lên radar cùng thang với
    /// <c>adjustedIdeal</c>/<c>current</c> (§10.3). Trả lời *"sau khi tính bản mệnh của bạn, hệ thống
    /// đang ưu tiên bù hành nào"*. Phần âm của <c>d</c> không mất đi — FE hiện thành nhãn trục đỏ.
    /// </summary>
    public ElementVector PriorityVector => CombinedDirection.Normalize();
}
