using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Thuần (không I/O): phân loại phòng vào 1 trong 3 case A/B/C theo Gap rồi dựng 3 dòng nhận định
/// (status/detail/action). Case B (cân bằng) và C (toxic) loại trừ lẫn nhau và ưu tiên hơn A.
/// </summary>
public static class SpaceInsightBuilder
{
    private const decimal Epsilon = 0.05m; // |gap| ≤ ε coi như cân bằng
    private const decimal Strong = 0.10m;  // ngưỡng thừa mạnh cho case Toxic

    public static SpaceInsights Build(
        IReadOnlyList<ElementAnalysisRow> rows,
        WorkPurpose? purpose,
        IReadOnlyList<WorkPurposeElementModifier> purposeModifiers,
        int? birthYear)
    {
        var gaps = rows.ToDictionary(r => Enum.Parse<FengShuiElement>(r.Element), r => r.Gap);

        var deficits = gaps.Where(kv => kv.Value > Epsilon)
            .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
            .ToList();
        var surpluses = gaps.Where(kv => kv.Value < -Epsilon)
            .OrderBy(kv => kv.Value).ThenBy(kv => kv.Key)
            .ToList();

        if (deficits.Count == 0 && surpluses.Count == 0)
            return BuildBalanced(rows, purpose, birthYear);

        var toxicPair = FindToxicPair(gaps, surpluses, purposeModifiers);
        if (toxicPair is { } pair)
            return BuildToxic(pair.X, pair.Y, purpose);

        return BuildImbalanced(deficits, surpluses, purpose);
    }

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

    private static SpaceInsights BuildImbalanced(
        List<KeyValuePair<FengShuiElement, decimal>> deficits,
        List<KeyValuePair<FengShuiElement, decimal>> surpluses,
        WorkPurpose? purpose)
    {
        var deficitNames = deficits.Select(d => ElementSemantics.ElementName(d.Key)).ToList();
        var surplusNames = surpluses.Select(s => ElementSemantics.ElementName(s.Key)).ToList();
        bool hasDeficit = deficits.Count > 0;
        bool hasSurplus = surpluses.Count > 0;

        string status, detail, action;

        if (hasDeficit && hasSurplus)
        {
            var x = surpluses[0].Key;
            var y = deficits[0].Key;
            status = $"Phòng đang có quá nhiều hành {JoinVi(surplusNames)}, nhưng lại thiếu hụt hành {JoinVi(deficitNames)}.";
            detail = $"{ElementSemantics.Trait(x, purpose)} (hành {ElementSemantics.ElementName(x)}) đang quá trội, dìm {ElementSemantics.Trait(y, purpose)} của hành {ElementSemantics.ElementName(y)} xuống mức suy hạn.";
            action = $"Bổ sung ngay {ElementSemantics.Items(y)} (thuộc {ElementSemantics.ElementName(y)}) để cân bằng lại luồng khí.";
        }
        else if (hasDeficit)
        {
            var y = deficits[0].Key;
            status = $"Phòng đang thiếu hụt hành {JoinVi(deficitNames)}.";
            detail = $"{ElementSemantics.Trait(y, purpose)} (hành {ElementSemantics.ElementName(y)}) đang suy — phòng chưa đủ nguồn năng lượng này.";
            action = $"Bổ sung ngay {ElementSemantics.Items(y)} (thuộc {ElementSemantics.ElementName(y)}) để cân bằng lại luồng khí.";
        }
        else
        {
            var x = surpluses[0].Key;
            var s = FengShuiCalculator.GetGeneratedElement(x);
            status = $"Phòng đang có quá nhiều hành {JoinVi(surplusNames)}.";
            detail = $"{ElementSemantics.Trait(x, purpose)} (hành {ElementSemantics.ElementName(x)}) đang quá trội — phòng dư thừa nguồn năng lượng này.";
            action = $"Đặt thêm {ElementSemantics.Items(s)} (thuộc {ElementSemantics.ElementName(s)}) để hút bớt hành {ElementSemantics.ElementName(x)} đang dư thừa.";
        }

        return new SpaceInsights("Imbalanced", new List<SpaceInsightLine>
        {
            new("status", "Lệch chuẩn ngũ hành", status),
            new("detail", "Ảnh hưởng đến không gian", detail),
            new("action", "Gợi ý cân bằng", action),
        });
    }

    private static SpaceInsights BuildBalanced(
        IReadOnlyList<ElementAnalysisRow> rows, WorkPurpose? purpose, int? birthYear)
    {
        string detail;
        if (birthYear is { } year)
        {
            var m = FengShuiCalculator.GetNapAmElement(year);
            var napAmName = FengShuiCalculator.GetNapAmName(year);
            detail = $"Sự cân bằng này đang trợ lực rất tốt cho bản mệnh {napAmName} của bạn, giúp duy trì {ElementSemantics.Trait(m, purpose)}.";
        }
        else
        {
            var purposeVi = ElementSemantics.PurposeVi(purpose ?? WorkPurpose.Other);
            detail = $"Bố cục hiện tại đạt chuẩn lý tưởng cho mục đích {purposeVi}.";
        }

        var topRow = rows.OrderByDescending(r => r.Current).First();
        var k = Enum.Parse<FengShuiElement>(topRow.Element);
        var action = $"Giữ nguyên bố cục hiện tại, hạn chế nhồi thêm {ElementSemantics.Items(k)} (tính {ElementSemantics.ElementName(k)}) để tránh phá vỡ cấu trúc cân bằng.";

        return new SpaceInsights("Balanced", new List<SpaceInsightLine>
        {
            new("status", "Ngũ hành cân bằng", "Ngũ hành không gian hiện đang đạt trạng thái cân bằng, không có năng lượng nào bị lệch chuẩn."),
            new("detail", "Lợi ích hiện tại", detail),
            new("action", "Duy trì bố cục", action),
        });
    }

    private static SpaceInsights BuildToxic(FengShuiElement x, FengShuiElement y, WorkPurpose? purpose)
    {
        var s = FengShuiCalculator.GetGeneratedElement(x);
        var status = $"Xuất hiện năng lượng xung khắc trực tiếp: phòng đang dư thừa cục bộ hành {ElementSemantics.ElementName(x)}.";
        var detail = $"{ElementSemantics.Trait(x, purpose)} (hành {ElementSemantics.ElementName(x)}) quá nhiều đang triệt tiêu {ElementSemantics.Trait(y, purpose)} của hành {ElementSemantics.ElementName(y)} trong phòng.";
        var action = $"Đặt thêm {ElementSemantics.Items(y)} ({ElementSemantics.ElementName(y)}) hoặc {ElementSemantics.Items(s)} ({ElementSemantics.ElementName(s)}) để hút bớt tính {ElementSemantics.ElementName(x)} độc hại.";

        return new SpaceInsights("Toxic", new List<SpaceInsightLine>
        {
            new("status", "Xung khắc ngũ hành", status),
            new("detail", "Vì sao nguy hại", detail),
            new("action", "Cách hóa giải", action),
        });
    }

    private static string JoinVi(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return string.Empty;
        if (items.Count == 1) return items[0];
        return string.Join(", ", items.Take(items.Count - 1)) + " và " + items[^1];
    }
}
