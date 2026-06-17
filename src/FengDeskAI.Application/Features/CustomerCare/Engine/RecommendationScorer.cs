using FengDeskAI.Domain.Enums;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>Mã vibe canonical mà thuật toán quan tâm (khớp vibes.code đã seed).</summary>
internal static class VibeCodes
{
    public const string Focus = "Focus";
    public const string Relax = "Relax";
    public const string Creative = "Creative";
    public const string Calm = "Calm";
    public const string Energize = "Energize";
}

/// <inheritdoc />
public sealed class RecommendationScorer : IRecommendationScorer
{
    // Ngưỡng diện tích mặt bàn (cm²) → sức chứa kích thước sản phẩm.
    private const int SmallDeskMax = 3000;
    private const int MediumDeskMax = 8000;

    public PersonalProfile? BuildPersonalProfile(DateTime? dateOfBirth, Gender gender)
    {
        if (dateOfBirth is null || gender is not (Gender.Male or Gender.Female))
            return null;

        int year = dateOfBirth.Value.Year;
        var element = FengShuiCalculator.GetNapAmElement(year);
        int kua = FengShuiCalculator.GetKuaNumber(year, gender);
        var group = FengShuiCalculator.GetKuaGroup(kua);
        var favorable = FengShuiCalculator.GetFavorableDirections(group);

        return new PersonalProfile(element, kua, group, favorable);
    }

    public IReadOnlyList<ScoredProduct> Score(ScoringContext context, IReadOnlyList<ProductFacts> candidates)
    {
        var results = new List<ScoredProduct>(candidates.Count);
        foreach (var product in candidates)
            results.Add(ScoreOne(context, product));

        return results
            .OrderByDescending(r => r.Score)
            .ToList();
    }

    private ScoredProduct ScoreOne(ScoringContext ctx, ProductFacts product)
    {
        var facts = new List<string>();
        var cautions = new List<string>();
        var w = ctx.Weights;

        // ----- Phần cá nhân (mệnh + hướng) — nhân PersonalWeight -----
        decimal personalRaw = 0m;
        if (ctx.Personal is { } personal)
        {
            personalRaw += w.Element * ScoreElement(ctx, personal, product, facts, cautions);
            personalRaw += w.Direction * ScoreDirection(personal, product, ctx.DeskOrientation, facts);
        }
        else
        {
            cautions.Add("Chưa xác định giới tính Nam/Nữ — bỏ qua yếu tố mệnh & hướng, chỉ xét công năng.");
        }

        decimal personalScore = ctx.PersonalWeight * personalRaw;

        // ----- Phần chức năng — luôn tính nguyên -----
        decimal functionalScore =
              w.Purpose * ScorePurpose(ctx.Purpose, product, facts)
            + w.Style * ScoreStyle(ctx.Style, product, facts)
            + w.Lighting * ScoreLighting(ctx.Lighting, product, facts)
            + w.Size * ScoreSize(ctx.DeskArea, product, facts, cautions);

        decimal total = Math.Round(personalScore + functionalScore, 3);
        return new ScoredProduct(product.ProductId, total, facts, cautions);
    }

    private static decimal ScoreElement(
        ScoringContext ctx, PersonalProfile personal, ProductFacts product,
        List<string> facts, List<string> cautions)
    {
        if (product.PrimaryElement is not { } primary)
            return 0m;

        decimal value = ResolveElementScore(ctx, personal.Element, primary);
        var relation = FengShuiCalculator.GetRelation(personal.Element, primary);

        // Hành phụ đóng góp nửa trọng số.
        if (product.SecondaryElement is { } secondary)
            value += 0.5m * ResolveElementScore(ctx, personal.Element, secondary);

        string phrase = RelationPhrase(relation);
        if (value >= 0)
            facts.Add($"Hành {primary} {phrase} (bản mệnh {personal.Element}).");
        else
            cautions.Add($"Hành {primary} {phrase} (bản mệnh {personal.Element}).");

        return value;
    }

    private static decimal ResolveElementScore(ScoringContext ctx, FengShuiElement subject, FengShuiElement obj)
        => ctx.ElementScores.TryGetValue((subject, obj), out var s)
            ? s
            : FengShuiCalculator.DefaultScore(FengShuiCalculator.GetRelation(subject, obj));

    private static decimal ScoreDirection(
        PersonalProfile personal, ProductFacts product, CompassDirection desk, List<string> facts)
    {
        if (product.PrimaryElement is not { } primary)
            return 0m;

        if (personal.FavorableDirections.Contains(desk))
        {
            // Bàn đã quay hướng tốt → ưu tiên vật phẩm cùng hành với hướng để cộng hưởng.
            if (primary == FengShuiCalculator.GetDirectionElement(desk))
            {
                facts.Add($"Hành {primary} cộng hưởng với hướng bàn {desk} (hướng tốt theo Kua {personal.KuaNumber}).");
                return 1.0m;
            }
            return 0m;
        }

        // Bàn quay hướng chưa tốt → vật phẩm mang hành của các hướng tốt giúp cân bằng.
        var cureElements = personal.FavorableDirections
            .Select(FengShuiCalculator.GetDirectionElement)
            .ToHashSet();
        if (cureElements.Contains(primary))
        {
            facts.Add($"Bàn quay hướng {desk} chưa thuộc nhóm tốt; hành {primary} giúp dẫn năng lượng về hướng hợp mệnh.");
            return 1.0m;
        }
        return 0m;
    }

    private static decimal ScorePurpose(WorkPurpose purpose, ProductFacts product, List<string> facts)
    {
        var target = TargetVibe(purpose);
        if (target is { } vibe && product.Vibes.Contains(vibe))
        {
            facts.Add($"Tạo cảm giác {VibePhrase(vibe)} — hợp mục đích {purpose}.");
            return 1.0m;
        }
        return 0m;
    }

    // ── Vibe giờ là mã chuỗi (vibes.code) thay cho enum ──

    private static decimal ScoreStyle(string style, ProductFacts product, List<string> facts)
    {
        if (!string.IsNullOrEmpty(style) && product.Styles.Contains(style))
        {
            facts.Add($"Phong cách {style} đồng bộ với không gian.");
            return 1.0m;
        }
        return 0m;
    }

    private static decimal ScoreLighting(LightingType lighting, ProductFacts product, List<string> facts)
    {
        switch (lighting)
        {
            case LightingType.Dim:
                if (product.PrimaryElement == FengShuiElement.Hoa || product.Vibes.Contains(VibeCodes.Energize))
                {
                    facts.Add("Bổ sung sinh khí/ánh sáng cho không gian thiếu sáng.");
                    return 1.0m;
                }
                break;
            case LightingType.Natural:
                if (product.PrimaryElement is FengShuiElement.Thuy or FengShuiElement.Moc
                    || product.Vibes.Contains(VibeCodes.Calm))
                {
                    facts.Add("Làm dịu, cân bằng không gian nhiều ánh sáng tự nhiên.");
                    return 1.0m;
                }
                break;
        }
        return 0m;
    }

    private static decimal ScoreSize(int deskArea, ProductFacts product, List<string> facts, List<string> cautions)
    {
        var capacity = deskArea < SmallDeskMax ? SizeClass.Small
            : deskArea < MediumDeskMax ? SizeClass.Medium
            : SizeClass.Large;

        int diff = (int)product.SizeClass - (int)capacity;
        if (diff <= 0)
        {
            facts.Add($"Kích thước {product.SizeClass} vừa vặn mặt bàn ({deskArea} cm²).");
            return 1.0m;
        }

        cautions.Add($"Kích thước {product.SizeClass} có thể quá khổ so với mặt bàn ({deskArea} cm²).");
        return -0.5m * diff;
    }

    private static string? TargetVibe(WorkPurpose purpose) => purpose switch
    {
        WorkPurpose.Office => VibeCodes.Focus,
        WorkPurpose.Study => VibeCodes.Focus,
        WorkPurpose.Reading => VibeCodes.Calm,
        WorkPurpose.Creative => VibeCodes.Creative,
        WorkPurpose.Gaming => VibeCodes.Energize,
        WorkPurpose.Mixed => VibeCodes.Focus,
        _ => null,
    };

    private static string RelationPhrase(FengShuiRelation relation) => relation switch
    {
        FengShuiRelation.TuongHoa => "tương hòa, hợp bản mệnh",
        FengShuiRelation.TuongSinh => "tương sinh, nuôi dưỡng bản mệnh",
        FengShuiRelation.TuongKhac => "được bản mệnh chế ngự, dùng tốt",
        FengShuiRelation.TietKhi => "khiến bản mệnh hao khí nhẹ",
        FengShuiRelation.BiKhac => "khắc bản mệnh, nên cân nhắc",
        _ => "",
    };

    private static string VibePhrase(string vibe) => vibe switch
    {
        VibeCodes.Focus => "tập trung",
        VibeCodes.Relax => "thư giãn",
        VibeCodes.Creative => "khơi gợi sáng tạo",
        VibeCodes.Calm => "tĩnh tại",
        VibeCodes.Energize => "tràn năng lượng",
        _ => vibe,
    };
}
