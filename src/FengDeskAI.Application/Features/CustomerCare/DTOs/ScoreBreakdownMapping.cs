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
                BaseRuleScore = b.BaseRuleScoreVector is { } br ? Rows(br) : null,
                OccupationShift = b.OccupationShift is { } shift ? Rows(shift) : null,
                CombinedDirection = Rows(b.CombinedDirection),
                PriorityVector = Rows(b.PriorityVector),
                PersonalNeed = b.PersonalNeedVector is { } need ? Rows(need) : null,
                PersonalVector = b.PersonalVector is { } pv ? Rows(pv) : null,
                PersonalTarget = b.PersonalTarget is { } pt ? Rows(pt) : null,
            },
            DestinyElement = b.DestinyElement?.ToString(),
            DestinyLabelVi = DestinyLabel(b.DestinyElement, dateOfBirth),
            Occupation = b.OccupationCode is { } occCode && b.OccupationNameVi is { } occName && b.OccupationShift is { } occShift
                ? new OccupationInfluenceResponse
                {
                    Code = occCode,
                    NameVi = occName,
                    Share = b.OccupationShare,
                    ReasonVi = OccupationReason(occName, occShift),
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
    /// Câu giải thích cho lớp nghề nghiệp — nêu ĐÍCH DANH hành nào được kéo lên, hành nào bị đẩy xuống.
    ///
    /// <para>
    /// Đọc từ mức dịch THẬT (<c>r' − r</c>) chứ không từ delta khai trong bảng: hành khắc bản mệnh bị
    /// chặn nên mức dịch thật của nó gần 0, và user cần thấy đúng điều đó — nói "nghề của bạn hợp Kim"
    /// trong khi Kim vẫn khắc mệnh là nói dối bằng con số danh nghĩa.
    /// </para>
    /// </summary>
    public static string OccupationReason(string occupationNameVi, ElementVector shift)
    {
        const decimal visible = 0.005m; // dưới ngưỡng này thì làm tròn hiển thị đã về 0.00
        var up = shift.Enumerate().Where(x => x.Value >= visible)
            .OrderByDescending(x => x.Value).Select(x => x.Element.ToString()).ToList();
        var down = shift.Enumerate().Where(x => x.Value <= -visible)
            .OrderBy(x => x.Value).Select(x => x.Element.ToString()).ToList();

        if (up.Count == 0 && down.Count == 0)
            return $"Nghề {occupationNameVi} không đổi mức hợp của hành nào - hoặc hệ số đang rất nhỏ, "
                 + "hoặc những hành nghề này ưa đều đang khắc bản mệnh của bạn nên bị chặn lại.";

        var parts = new List<string>();
        if (up.Count > 0) parts.Add($"nâng {string.Join(", ", up)}");
        if (down.Count > 0) parts.Add($"hạ {string.Join(", ", down)}");
        return $"Nghề {occupationNameVi} {string.Join(" và ", parts)}. "
             + "Nghề nghiệp chỉ đổi mức ƯA THÍCH, không đổi bản mệnh: hành đang khắc mệnh vẫn ở lại phía âm.";
    }

    /// <summary>
    /// Cùng công thức với <c>scorePercent()</c> của FE: <c>(clamp(score) + 1) / 2 × 100</c>. BE tính sẵn
    /// để hai bên không bao giờ lệch cách làm tròn — user thấy 81% ở badge thì waterfall cũng cộng ra 81%.
    /// </summary>
    public static int DisplayPercentOf(decimal score)
        => (int)Math.Round(100m * (Math.Clamp(score, -1m, 1m) + 1m) / 2m, MidpointRounding.AwayFromZero);

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
