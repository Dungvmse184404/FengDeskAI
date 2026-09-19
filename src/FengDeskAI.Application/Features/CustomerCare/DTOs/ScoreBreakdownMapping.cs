using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>
/// Quy <see cref="ScoreBreakdown"/> của engine ra JSON cho FE — v3.2 §9.2.
///
/// <para>
/// Engine cố tình KHÔNG biết gì về ngày sinh hay tên Nạp Âm (nó chỉ nhận vector), nên phần nhãn
/// <c>destinyLabelVi</c> được ghép ở đây, nơi có <c>DateOfBirth</c>.
/// </para>
/// </summary>
public static class ScoreBreakdownMapping
{
    public static ScoreBreakdownResponse ToResponse(ScoreBreakdown b, decimal score, DateTime? dateOfBirth) =>
        new()
        {
            FormulaVersion = b.FormulaVersion,
            Target = b.Target.ToString(),
            Placement = b.Placement.ToString(),
            DisplayPercent = DisplayPercentOf(score),
            Components = b.Components.Select(c => new ScoreComponentRow
            {
                Code = c.Code,
                LabelVi = c.LabelVi,
                Value = Math.Round(c.Value, 3),
                Weight = Math.Round(c.Weight, 3),
                Contribution = Math.Round(c.Contribution, 3),
                ReasonVi = c.ReasonVi,
            }).ToList(),
            Penalties = b.Penalties.Select(p => new ScorePenaltyRow
            {
                Code = p.Code,
                LabelVi = p.LabelVi,
                Value = Math.Round(p.Value, 3),
                Applied = p.Applied,
                ReasonVi = p.ReasonVi,
                ParamValue = Math.Round(p.ParamValue, 3),
                Factor = p.Factor is { } f ? Math.Round(f, 3) : null,
                FactorLabelVi = p.FactorLabelVi,
            }).ToList(),
            Blended = Math.Round(b.Blended, 3),
            RawScore = Math.Round(b.RawScore, 3),
            Clamped = b.Clamped,
            Score = score,
            PersonalWeight = b.PersonalWeightCode is null ? null : new PersonalWeightInfo
            {
                Value = Math.Round(b.PersonalWeight, 3),
                Code = b.PersonalWeightCode,
                Scope = ScopeOf(b.PersonalWeightCode).ToString(),
                ReasonVi = PersonalWeightReason(ScopeOf(b.PersonalWeightCode), b.PersonalWeight, b.DestinyElement),
            },
            Vectors = new ScoreVectorsResponse
            {
                Product = Rows(b.ProductVector),
                NormalizedGap = Rows(b.NormalizedGap),
                RuleScore = b.RuleScoreVector is { } r ? Rows(r) : null,
                OccupationDirection = b.OccupationDirection is { } od ? Rows(od) : null,
                OccupationRawDirection = b.OccupationRawDirection is { } ord ? Rows(ord) : null,
                CombinedDirection = Rows(b.CombinedDirection),
                PriorityVector = Rows(b.PriorityVector),
                PersonalNeed = b.PersonalNeedVector is { } need ? Rows(need) : null,
                PersonalVector = b.PersonalVector is { } pv ? Rows(pv) : null,
                PersonalTarget = b.PersonalTarget is { } pt ? Rows(pt) : null,
            },
            DestinyElement = b.DestinyElement?.ToString(),
            PersonalAvoidElements = b.PersonalAvoidElements?.Select(e => e.ToString()).ToList(),
            DestinyLabelVi = DestinyLabel(b.DestinyElement, dateOfBirth),
            Occupation = b.OccupationCode is { } occCode && b.OccupationNameVi is { } occName
                         && b.OccupationDirection is { } occDir && b.OccupationRawDirection is { } occRaw
                ? new OccupationInfluenceResponse
                {
                    Code = occCode,
                    NameVi = occName,
                    Weight = Math.Round(b.OccupationWeight, 3),
                    WeightCode = b.OccupationWeightCode ?? ScoringParamCodes.OccupationWeight,
                    ReasonVi = OccupationReason(occName, occDir, occRaw, b.DestinyElement),
                }
                : null,
            ConflictResolution = b.ConflictResolution is { } c ? new ConflictResolutionResponse
            {
                RoomNeed = c.RoomNeed.ToString(),
                Destiny = c.Destiny.ToString(),
                Bridge = c.Bridge.ToString(),
                ReasonVi = c.ReasonVi,
            } : null,
        };

    /// <summary>
    /// Câu giải thích cho trục nghề — nêu ĐÍCH DANH hành nghề cần, hành nghề tránh, và hành nghề muốn
    /// nâng nhưng bị chặn vì khắc mệnh. Đọc từ <c>ô</c> ĐÃ chặn: nói "nghề của bạn hợp Kim" trong khi
    /// Kim vẫn khắc mệnh là nói dối bằng con số danh nghĩa.
    /// </summary>
    public static string OccupationReason(
        string occupationNameVi, ElementVector direction, ElementVector rawDirection, FengShuiElement? destiny)
    {
        const decimal visible = 0.005m;
        var up = direction.Enumerate().Where(x => x.Value >= visible)
            .OrderByDescending(x => x.Value).Select(x => x.Element.ToString()).ToList();
        var down = direction.Enumerate().Where(x => x.Value <= -visible)
            .OrderBy(x => x.Value).Select(x => x.Element.ToString()).ToList();
        var blocked = rawDirection.Enumerate()
            .Where(x => x.Value >= visible && direction[x.Element] < visible)
            .Select(x => x.Element.ToString()).ToList();

        var parts = new List<string>();
        if (up.Count > 0) parts.Add($"cần {string.Join(", ", up)}");
        if (down.Count > 0) parts.Add($"tránh {string.Join(", ", down)}");

        string head = parts.Count > 0
            ? $"Nghề {occupationNameVi} {string.Join(" và ", parts)}."
            : $"Nghề {occupationNameVi} không nghiêng về hành nào sau khi chặn.";

        if (blocked.Count > 0 && destiny is { } mine)
            head += $" Nghề còn cần {string.Join(", ", blocked)} nhưng hành đó khắc bản mệnh {mine} nên không được cộng"
                  + " - nghề đổi mức ưa thích, không đổi bản mệnh.";

        return head;
    }

    /// <summary>
    /// Cùng công thức với <c>scorePercent()</c> của FE: <c>(clamp(score) + 1) / 2 × 100</c>. BE tính sẵn
    /// để hai bên không bao giờ lệch cách làm tròn — user thấy 81% ở badge thì waterfall cũng cộng ra 81%.
    /// </summary>
    public static int DisplayPercentOf(decimal score)
        => (int)Math.Round(100m * (Math.Clamp(score, -1m, 1m) + 1m) / 2m, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Cùng ngưỡng với <c>ScoreBadge.tierFor</c> của FE (≥0.6 · ≥0.2 · ≥−0.2). BE tính sẵn cho những chỗ
    /// FE không có badge (bảng "hợp nghề nào" ở trang sản phẩm) để hai bên không lệch nhãn.
    /// </summary>
    public static string TierVi(decimal score) => score switch
    {
        >= 0.6m => "Rất hợp",
        >= 0.2m => "Phù hợp",
        >= -0.2m => "Trung tính",
        _ => "Cân nhắc",
    };

    public static List<ProductElementRow> Rows(ElementVector v)
        => v.Enumerate()
            .Select(x => new ProductElementRow { Element = x.Element.ToString(), Value = Math.Round(x.Value, 3) })
            .ToList();

    public static WorkspaceScope ScopeOf(string personalWeightCode) => personalWeightCode switch
    {
        ScoringParamCodes.PersonalWeightShared => WorkspaceScope.Shared,
        ScoringParamCodes.PersonalWeightPublic => WorkspaceScope.Public,
        _ => WorkspaceScope.Private,
    };

    /// <summary>
    /// Vì sao trọng số là con số đó. Phân biệt rõ <b>ba lý do khác nhau</b> cùng dẫn tới <c>Wp = 0</c>
    /// (§10.7): không gian chung · chưa có ngày sinh · tham số đang tắt. Gộp chung ba thứ này thành một
    /// câu là lấy mất của user hành động cần làm — người thiếu ngày sinh chỉ cần khai ngày sinh.
    /// </summary>
    public static string PersonalWeightReason(WorkspaceScope scope, decimal wp, FengShuiElement? destiny)
    {
        if (scope == WorkspaceScope.Public)
            return "Không gian chung - điểm không neo vào bản mệnh của riêng ai.";

        if (destiny is null)
            return "Chưa có ngày sinh nên chưa tính được bản mệnh - điểm hiện chỉ dựa trên nhu cầu của phòng. "
                + "Thêm ngày sinh để nhận gợi ý hợp bản mệnh.";

        if (wp <= 0m)
            return "Trục cá nhân đang tắt trong cấu hình - điểm hiện chỉ dựa trên nhu cầu của phòng.";

        int percent = (int)Math.Round(wp * 100m, MidpointRounding.AwayFromZero);
        return scope == WorkspaceScope.Private
            ? $"Phòng riêng tư - {percent}% điểm đến từ bản mệnh của bạn, phần còn lại từ nhu cầu của phòng."
            : $"Không gian dùng chung - chỉ {percent}% điểm đến từ bản mệnh của bạn, "
                + "phần lớn vẫn là nhu cầu của phòng.";
    }

    /// <summary>vd <c>"Mộc — Đại Lâm Mộc (1988)"</c>. Năm hiển thị là năm ÂM lịch, đúng năm engine đã dùng.</summary>
    public static string? DestinyLabel(FengShuiElement? destiny, DateTime? dateOfBirth)
    {
        if (destiny is not { } element) return null;
        if (dateOfBirth is not { } dob) return element.ToString();

        int lunarYear = FengShuiCalculator.GetLunarYear(dob);
        return $"{element} - {FengShuiCalculator.GetNapAmName(lunarYear)} ({lunarYear})";
    }
}
