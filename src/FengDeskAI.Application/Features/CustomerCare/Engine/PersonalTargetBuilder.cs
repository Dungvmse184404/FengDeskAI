using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>Căn cứ dựng vector mục tiêu cá nhân — ghi vào log &amp; response để AI nói đúng cơ sở.</summary>
public enum PersonalTargetSource
{
    /// <summary>Dụng thần rút từ Tứ Trụ (cần giờ sinh) — chuẩn hơn cho vật phẩm mang theo người.</summary>
    TuTru,

    /// <summary>Nạp Âm theo năm sinh (bản mệnh + hành sinh + hành được sinh) — dùng khi thiếu giờ sinh.</summary>
    NapAm,
}

/// <summary>
/// Vector mục tiêu của một người + căn cứ đã dùng để dựng ra nó.
/// <para>
/// <paramref name="Avoid"/> (v3.6): <b>kỵ thần</b> — hành nên tránh. Tứ Trụ chia hành thành dụng/hỷ (bồi),
/// kỵ (tránh) và nhàn (trung tính); trước v3.6 engine chỉ giữ nhóm đầu nên vật toàn kỵ thần vẫn "trung
/// tính". Nạp Âm chỉ suy được một hành kỵ (khắc mệnh). Xem ADR <c>personal-need-v3.6.md</c>.
/// </para>
/// </summary>
public sealed record PersonalTarget(
    ElementVector Vector,
    PersonalTargetSource Source,
    IReadOnlyList<string> Elements,
    string Note,
    IReadOnlySet<FengShuiElement>? Avoid = null)
{
    /// <summary>Kỵ thần — rỗng khi không suy được.</summary>
    public IReadOnlySet<FengShuiElement> AvoidSet => Avoid ?? new HashSet<FengShuiElement>();

    /// <summary>Tên tiếng Việt của kỵ thần, thứ tự enum.</summary>
    public IReadOnlyList<string> AvoidElements
        => Enum.GetValues<FengShuiElement>().Where(AvoidSet.Contains).Select(BaTuCalculator.ElementVn).ToList();
}

/// <summary>
/// Dựng vector "người đang cần hành gì" cho luồng gợi ý vật phẩm mang theo người
/// (<see cref="Domain.Enums.Catalog.ProductPlacement.Carry"/>). Thuần logic, không chạm DB.
/// Hybrid theo dữ liệu có: đủ giờ sinh → dụng thần Tứ Trụ; thiếu → Nạp Âm; thiếu cả ngày sinh → null.
/// Xem <c>docs/adr/product-placement-personal-recommendation.md</c> §4.
/// </summary>
public static class PersonalTargetBuilder
{
    public static PersonalTarget? Build(DateTime? dateOfBirth, TimeOnly? birthTime, ScoringParameters prms)
    {
        if (dateOfBirth is not { } dob)
            return null;

        var birthDate = DateOnly.FromDateTime(dob);

        // ── Ưu tiên dụng thần Tứ Trụ: chỉ đủ căn cứ khi có giờ sinh (thiếu trụ giờ thì vượng/nhược là sơ bộ).
        if (birthTime is { } time)
        {
            var chart = BaTuCalculator.Compute(birthDate, time);

            // FavorableElementCodes chính là enum.ToString() từ BaTuCalculator — parse ngược lại là lossless,
            // rẻ hơn nhiều so với nhân bản logic dụng thần ở đây.
            var favorable = chart.FavorableElementCodes
                .Select(code => Enum.TryParse<FengShuiElement>(code, out var e) ? e : (FengShuiElement?)null)
                .Where(e => e is not null)
                .Select(e => e!.Value)
                .Distinct()
                .ToList();

            if (favorable.Count > 0)
            {
                var vector = ElementVector.Zero;
                for (int i = 0; i < favorable.Count; i++)
                {
                    decimal share = i == 0 ? prms.CarryPrimaryShare : prms.CarrySecondaryShare;
                    vector = vector.Add(ElementVector.Single(favorable[i]).Scale(share));
                }

                var avoid = (chart.UnfavorableElementCodes ?? Array.Empty<string>())
                    .Select(code => Enum.TryParse<FengShuiElement>(code, out var e) ? e : (FengShuiElement?)null)
                    .OfType<FengShuiElement>()
                    .Where(e => !favorable.Contains(e))
                    .ToHashSet();

                return new PersonalTarget(
                    vector.Normalize(),
                    PersonalTargetSource.TuTru,
                    favorable.Select(BaTuCalculator.ElementVn).ToList(),
                    $"Dụng thần theo Tứ Trụ ({chart.BodyStrength}, nhật chủ {chart.NhatChu} hành {chart.DayMasterElement}).",
                    avoid);
            }
        }

        // ── Fallback Nạp Âm: chỉ cần năm sinh, dùng chung công thức với bộ lọc mệnh của luồng workspace.
        //    Đi qua overload nhận DateOnly để lấy năm ÂM — dùng birthDate.Year (dương) sẽ lệch mệnh
        //    với người sinh tháng 1–2 trước Tết, và lệch luôn so với BuildPersonalProfile.
        var napAmElement = FengShuiCalculator.GetNapAmElement(birthDate);
        var napAmVector = FengShuiCalculator.BuildPersonalVector(
            birthDate, prms.SelfShare, prms.SupportShare, prms.ChildShare);

        // Nạp Âm chỉ biết bản mệnh ⇒ kỵ duy nhất suy được là hành khắc mệnh. Ít thông tin hơn Tứ Trụ nên
        // ít hành bị trừ hơn — đúng với mức chắc chắn của dữ liệu.
        return new PersonalTarget(
            napAmVector,
            PersonalTargetSource.NapAm,
            new[] { BaTuCalculator.ElementVn(napAmElement) },
            birthTime is null
                ? "Chưa có giờ sinh nên dùng bản mệnh Nạp Âm; bổ sung giờ sinh sẽ tính được dụng thần Tứ Trụ chính xác hơn."
                : "Dùng bản mệnh Nạp Âm.",
            new HashSet<FengShuiElement> { FengShuiCalculator.GetControllingElement(napAmElement) });
    }
}
