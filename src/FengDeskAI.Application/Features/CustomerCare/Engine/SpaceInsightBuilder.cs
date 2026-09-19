using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Thuần (không I/O): dựng 3 dòng nhận định cho một workspace, theo đúng cấu trúc kể chuyện:
/// <list type="number">
/// <item><b>trait</b> — ĐẶC TÍNH: loại phòng này cần hành nào trội thì thuận lợi.</item>
/// <item><b>status</b> — HIỆN TRẠNG: đang lệch hành nào, và NGUYÊN DO là tag/sản phẩm cụ thể nào
/// (đọc từ <see cref="CurrentBreakdown"/> — chính những tag user đã khai ở bước intake).</item>
/// <item><b>action</b> — ĐỀ XUẤT: thêm vật phẩm hành nào để kéo lại.</item>
/// </list>
/// Phòng chưa khai tag nào thì dòng 2 NÓI RÕ là đang ước tính theo nền loại phòng — không bịa nguyên do.
/// </summary>
public static class SpaceInsightBuilder
{
    private const decimal Epsilon = 0.05m; // |gap| ≤ ε coi như cân bằng
    private const decimal Strong = 0.10m;  // ngưỡng thừa mạnh cho case Toxic

    /// <summary>Số nguồn tối đa được nêu tên làm dẫn chứng trong dòng hiện trạng.</summary>
    private const int MaxEvidence = 2;

    public static SpaceInsights Build(
        IReadOnlyList<ElementAnalysisRow> rows,
        WorkPurpose? purpose,
        IReadOnlyList<WorkPurposeElementModifier> purposeModifiers,
        int? birthYear,
        CurrentBreakdown? breakdown = null,
        string? workspaceTypeName = null)
    {
        var gaps = rows.ToDictionary(r => Enum.Parse<FengShuiElement>(r.Element), r => r.Gap);

        var deficits = gaps.Where(kv => kv.Value > Epsilon)
            .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .ToList();
        var surpluses = gaps.Where(kv => kv.Value < -Epsilon)
            .OrderBy(kv => kv.Value).ThenBy(kv => kv.Key)
            .ToList();

        var trait = BuildTraitLine(rows, purpose, workspaceTypeName);

        if (deficits.Count == 0 && surpluses.Count == 0)
            return Compose("Balanced", trait, BuildBalancedStatus(purpose, birthYear, breakdown),
                BuildBalancedAction(rows, purpose));

        var toxicPair = FindToxicPair(gaps, surpluses, purposeModifiers);
        if (toxicPair is { } pair)
            return Compose("Toxic",
                trait,
                BuildStatusLine(pair.X, new[] { pair.Y }, purpose, breakdown, isToxic: true),
                BuildActionLine(pair.Y, FengShuiCalculator.GetGeneratedElement(pair.X), pair.X));

        if (surpluses.Count > 0)
        {
            var x = surpluses[0].Key;
            var suppressed = deficits.Select(d => d.Key).ToList();
            var drain = FengShuiCalculator.GetGeneratedElement(x);
            var boost = deficits.Count > 0 ? deficits[0].Key : drain;
            return Compose("Imbalanced",
                trait,
                BuildStatusLine(x, suppressed, purpose, breakdown, isToxic: false),
                BuildActionLine(boost, drain, x));
        }

        // Chỉ thiếu, không hành nào thừa rõ rệt.
        var missing = deficits[0].Key;
        return Compose("Imbalanced",
            trait,
            BuildDeficitOnlyStatus(deficits.Select(d => d.Key).ToList(), purpose, breakdown),
            BuildActionLine(missing, missing, null));
    }

    // ── Dòng 1: đặc tính loại phòng ────────────────────────────────────────

    /// <summary>"Đối với &lt;loại phòng&gt; (&lt;mục đích&gt;), hành &lt;A, B&gt; trội hơn sẽ thuận lợi hơn cho công việc."</summary>
    private static string BuildTraitLine(
        IReadOnlyList<ElementAnalysisRow> rows, WorkPurpose? purpose, string? workspaceTypeName)
    {
        // 2 hành có tỉ trọng lý tưởng (đã bẻ theo mục đích) cao nhất = đặc tính phòng nên có.
        var top = rows.OrderByDescending(r => r.AdjustedIdeal).ThenBy(r => r.Element)
            .Take(2)
            .Select(r => Enum.Parse<FengShuiElement>(r.Element))
            .ToList();

        var subject = string.IsNullOrWhiteSpace(workspaceTypeName)
            ? "không gian này"
            : $"không gian {workspaceTypeName.Trim()}";
        var purposeVi = ElementSemantics.PurposeVi(purpose ?? WorkPurpose.Other);
        var names = JoinVi(top.Select(ElementSemantics.ElementName).ToList());
        var benefit = ElementSemantics.Trait(top[0], purpose);

        return $"Đối với {subject} dùng để {purposeVi}, hành {names} trội hơn sẽ thuận lợi hơn - "
             + $"đây là nguồn {benefit} mà không gian cần.";
    }

    // ── Dòng 2: hiện trạng + NGUYÊN DO ─────────────────────────────────────

    /// <summary>"Hiện tại, do có &lt;tag A, tag B&gt;, hành X đang lấn át &lt;Y, Z&gt;."</summary>
    private static string BuildStatusLine(
        FengShuiElement surplus,
        IReadOnlyList<FengShuiElement> suppressed,
        WorkPurpose? purpose,
        CurrentBreakdown? breakdown,
        bool isToxic)
    {
        var cause = DescribeCause(breakdown, surplus);
        var surplusName = ElementSemantics.ElementName(surplus);

        var target = suppressed.Count > 0
            ? JoinVi(suppressed.Select(ElementSemantics.ElementName).ToList())
            : null;

        var head = cause is not null
            ? $"Hiện tại, do có {cause}, hành {surplusName} đang chiếm ưu thế"
            : HasEvidence(breakdown)
                // Có khai tag nhưng không tag nào sinh ra hành này → lệch đến từ nền phòng.
                ? $"Hiện tại hành {surplusName} đang chiếm ưu thế trong phòng"
                : $"Phòng chưa khai báo nội thất/vật trang trí nào nên hệ thống ước tính theo nền chung "
                  + $"của loại phòng - theo đó hành {surplusName} đang chiếm ưu thế";

        if (target is null)
            return head + $" - vượt mức {ElementSemantics.Trait(surplus, purpose)} mà phòng cần.";

        var verb = isToxic ? "triệt tiêu" : "lấn át";
        return head + $" và {verb} hành {target} "
             + $"({ElementSemantics.Trait(suppressed[0], purpose)} bị kéo xuống mức suy hạn).";
    }

    /// <summary>Chỉ thiếu, không thừa: nêu rõ phòng chưa có nguồn nào sinh ra hành đó.</summary>
    private static string BuildDeficitOnlyStatus(
        IReadOnlyList<FengShuiElement> deficits, WorkPurpose? purpose, CurrentBreakdown? breakdown)
    {
        var names = JoinVi(deficits.Select(ElementSemantics.ElementName).ToList());
        var trait = ElementSemantics.Trait(deficits[0], purpose);

        if (!HasEvidence(breakdown))
            return $"Hiện tại phòng chưa khai báo nội thất/vật trang trí nào, hệ thống đang ước tính theo "
                 + $"nền chung của loại phòng - theo đó hành {names} còn thiếu ({trait} chưa đủ nguồn).";

        var declared = DescribeDeclared(breakdown!);
        return $"Hiện tại, với {declared}, phòng chưa có nguồn nào sinh hành {names} "
             + $"- {trait} vì thế còn thiếu.";
    }

    private static string BuildBalancedStatus(WorkPurpose? purpose, int? birthYear, CurrentBreakdown? breakdown)
    {
        if (!HasEvidence(breakdown))
            return "Hiện tại phòng chưa khai báo nội thất/vật trang trí nào - hệ thống ước tính theo nền chung "
                 + "của loại phòng và chưa thấy hành nào lệch chuẩn.";

        var declared = DescribeDeclared(breakdown!);
        if (birthYear is { } year)
        {
            var m = FengShuiCalculator.GetNapAmElement(year);
            return $"Hiện tại, với {declared}, ngũ hành phòng đang cân bằng - trợ lực tốt cho bản mệnh "
                 + $"{FengShuiCalculator.GetNapAmName(year)} của bạn, duy trì {ElementSemantics.Trait(m, purpose)}.";
        }

        return $"Hiện tại, với {declared}, ngũ hành phòng đang cân bằng - không hành nào lệch chuẩn.";
    }

    // ── Dòng 3: đề xuất ────────────────────────────────────────────────────

    /// <summary>"Đặt thêm &lt;vật phẩm Y&gt; (Y) hoặc &lt;vật phẩm S&gt; (S) để hút bớt tính X."</summary>
    private static string BuildActionLine(FengShuiElement boost, FengShuiElement drain, FengShuiElement? surplus)
    {
        if (surplus is not { } x)
            return $"Bổ sung {ElementSemantics.Items(boost)} (thuộc {ElementSemantics.ElementName(boost)}) "
                 + "để cân bằng lại luồng khí.";

        var suffix = $" để hút bớt tính {ElementSemantics.ElementName(x)} đang dư thừa.";
        if (boost == drain)
            return $"Đặt thêm {ElementSemantics.Items(boost)} ({ElementSemantics.ElementName(boost)})" + suffix;

        return $"Đặt thêm {ElementSemantics.Items(boost)} ({ElementSemantics.ElementName(boost)}) hoặc "
             + $"{ElementSemantics.Items(drain)} ({ElementSemantics.ElementName(drain)})" + suffix;
    }

    private static string BuildBalancedAction(IReadOnlyList<ElementAnalysisRow> rows, WorkPurpose? purpose)
    {
        var topRow = rows.OrderByDescending(r => r.Current).First();
        var k = Enum.Parse<FengShuiElement>(topRow.Element);
        return $"Giữ nguyên bố cục hiện tại, hạn chế nhồi thêm {ElementSemantics.Items(k)} "
             + $"(tính {ElementSemantics.ElementName(k)}) để tránh phá vỡ cấu trúc cân bằng.";
    }

    // ── Dẫn chứng lấy từ breakdown ─────────────────────────────────────────

    private static bool HasEvidence(CurrentBreakdown? breakdown)
        => breakdown is { } b && b.EvidenceCount > 0;

    /// <summary>
    /// Tên 1-2 nguồn (tag user khai / sản phẩm đã đặt) đóng góp nhiều nhất vào hành đang thừa.
    /// Null khi phòng chưa khai gì — để câu văn không bịa nguyên do.
    /// </summary>
    private static string? DescribeCause(CurrentBreakdown? breakdown, FengShuiElement element)
    {
        if (breakdown is not { } b || b.TotalVotes <= 0m) return null;

        var culprits = b.Contributions
            .Where(c => c.Source != CurrentSourceKind.Interior)
            .Where(c => !string.IsNullOrWhiteSpace(c.Label))
            .Select(c => (c.Label, Amount: c.Vector[element] * c.Votes))
            .Where(c => c.Amount > 0m)
            .OrderByDescending(c => c.Amount)
            .ThenBy(c => c.Label, StringComparer.CurrentCulture)
            .Take(MaxEvidence)
            .Select(c => c.Label)
            .ToList();

        return culprits.Count == 0 ? null : JoinVi(culprits).ToLowerInvariant();
    }

    /// <summary>Liệt kê những gì user đã khai (không gắn với hành cụ thể) — dùng cho câu không có hành thừa.</summary>
    private static string DescribeDeclared(CurrentBreakdown breakdown)
    {
        var declared = breakdown.Contributions
            .Where(c => c.Source != CurrentSourceKind.Interior)
            .Where(c => !string.IsNullOrWhiteSpace(c.Label))
            .OrderByDescending(c => c.Votes)
            .Take(MaxEvidence)
            .Select(c => c.Label)
            .ToList();

        return declared.Count == 0
            ? "hiện trạng đã khai"
            : JoinVi(declared).ToLowerInvariant();
    }

    // ── Toxic detection (giữ nguyên logic cũ) ──────────────────────────────

    /// <summary>Tìm cặp (X thừa mạnh, Y bị khắc) toxic có |gap_X|+|gap_Y| lớn nhất — chỉ 1 cặp.</summary>
    private static (FengShuiElement X, FengShuiElement Y)? FindToxicPair(
        Dictionary<FengShuiElement, decimal> gaps,
        List<KeyValuePair<FengShuiElement, decimal>> surpluses,
        IReadOnlyList<WorkPurposeElementModifier> purposeModifiers)
    {
        FengShuiElement? king = purposeModifiers
            .Where(m => m.Delta > 0)
            .OrderByDescending(m => m.Delta).ThenBy(m => m.Element)
            .Select(m => (FengShuiElement?)m.Element)
            .FirstOrDefault();

        (FengShuiElement X, FengShuiElement Y, decimal Severity)? best = null;
        foreach (var (x, xGap) in surpluses)
        {
            if (xGap > -Strong) continue; // chưa thừa mạnh

            var y = FengShuiCalculator.GetControlledElement(x);
            var yGap = gaps.TryGetValue(y, out var g) ? g : 0m;
            bool yIsDeficit = yGap > Epsilon;
            bool yIsKing = king == y;
            if (!yIsDeficit && !yIsKing) continue;

            decimal severity = Math.Abs(xGap) + Math.Abs(yGap);
            if (best is null || severity > best.Value.Severity)
                best = (x, y, severity);
        }

        return best is { } b ? (b.X, b.Y) : null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static SpaceInsights Compose(string caseName, string trait, string status, string action)
        => new(caseName, new List<SpaceInsightLine>
        {
            new("trait", "Đặc tính không gian", trait),
            new("status", "Hiện trạng", status),
            new("action", "Gợi ý cân bằng", action),
        });

    private static string JoinVi(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return string.Empty;
        if (items.Count == 1) return items[0];
        return string.Join(", ", items.Take(items.Count - 1)) + " và " + items[^1];
    }
}
