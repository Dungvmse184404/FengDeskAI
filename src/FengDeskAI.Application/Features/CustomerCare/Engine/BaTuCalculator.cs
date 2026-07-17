using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>Một trụ trong Tứ Trụ: can chi + hành của can + tàng can trong chi.</summary>
public sealed record BaTuPillar(
    string Pillar,          // "Năm" / "Tháng" / "Ngày" / "Giờ"
    string Can,
    string Chi,
    string CanElement,      // hành của thiên can (tên VN)
    IReadOnlyList<string> HiddenStems); // tàng can trong địa chi (tên can)

/// <summary>
/// Kết quả Bát Tự (Tứ Trụ / Tử Bình) rút gọn. <see cref="HourPillar"/> null khi thiếu giờ sinh —
/// các phần còn lại vẫn tính trên 3 trụ (graceful degradation).
/// </summary>
public sealed record BaTuChart(
    BaTuPillar YearPillar,
    BaTuPillar MonthPillar,
    BaTuPillar DayPillar,
    BaTuPillar? HourPillar,
    string NhatChu,                                   // can ngày — "chính mình"
    string DayMasterElement,                          // hành nhật chủ (tên VN)
    IReadOnlyDictionary<string, decimal> ElementDistribution, // "Kim"/"Mộc"/... → trọng số
    IReadOnlyList<string> MissingElements,            // hành khuyết (trọng số 0)
    string BodyStrength,                              // "Thân vượng" / "Thân nhược"
    IReadOnlyList<string> FavorableElements,          // dụng thần gợi ý (tên VN)
    IReadOnlyList<string> FavorableElementCodes,      // enum code (Kim/Moc/Thuy/Hoa/Tho) — filter sản phẩm
    string MethodNote);

/// <summary>
/// Tính Tứ Trụ (Bát Tự) rút gọn — deterministic, AI chỉ diễn giải kết quả:
/// trụ năm theo Lập Xuân, trụ tháng theo TIẾT KHÍ (kinh độ mặt trời, không phải tháng âm),
/// trụ ngày theo số ngày Julian, trụ giờ theo ngũ thử độn.
/// Đơn giản hóa có chủ đích (ghi trong MethodNote): 23h tính là Tý của ngày hiện tại (không tách dạ Tý);
/// thân vượng/nhược đánh giá theo trọng số sinh-trợ + lệnh tháng, chưa xét hợp-xung-hình-hại.
/// </summary>
public static class BaTuCalculator
{
    private static readonly string[] Can =
        { "Giáp", "Ất", "Bính", "Đinh", "Mậu", "Kỷ", "Canh", "Tân", "Nhâm", "Quý" };

    private static readonly string[] Chi =
        { "Tý", "Sửu", "Dần", "Mão", "Thìn", "Tỵ", "Ngọ", "Mùi", "Thân", "Dậu", "Tuất", "Hợi" };

    // Hành của 10 thiên can (index khớp mảng Can).
    private static readonly FengShuiElement[] CanElements =
    {
        FengShuiElement.Moc, FengShuiElement.Moc,   // Giáp, Ất
        FengShuiElement.Hoa, FengShuiElement.Hoa,   // Bính, Đinh
        FengShuiElement.Tho, FengShuiElement.Tho,   // Mậu, Kỷ
        FengShuiElement.Kim, FengShuiElement.Kim,   // Canh, Tân
        FengShuiElement.Thuy, FengShuiElement.Thuy, // Nhâm, Quý
    };

    // Tàng can trong 12 địa chi (index khớp mảng Chi) — can đầu là chính khí.
    private static readonly int[][] HiddenStemIdx =
    {
        new[] { 9 },        // Tý: Quý
        new[] { 5, 9, 7 },  // Sửu: Kỷ, Quý, Tân
        new[] { 0, 2, 4 },  // Dần: Giáp, Bính, Mậu
        new[] { 1 },        // Mão: Ất
        new[] { 4, 1, 9 },  // Thìn: Mậu, Ất, Quý
        new[] { 2, 6, 4 },  // Tỵ: Bính, Canh, Mậu
        new[] { 3, 5 },     // Ngọ: Đinh, Kỷ
        new[] { 5, 3, 1 },  // Mùi: Kỷ, Đinh, Ất
        new[] { 6, 8, 4 },  // Thân: Canh, Nhâm, Mậu
        new[] { 7 },        // Dậu: Tân
        new[] { 4, 7, 3 },  // Tuất: Mậu, Tân, Đinh
        new[] { 8, 0 },     // Hợi: Nhâm, Giáp
    };

    // Trọng số tàng can theo số lượng: chính khí nặng nhất.
    private static readonly decimal[][] HiddenWeights =
    {
        new[] { 1.0m },
        new[] { 0.7m, 0.3m },
        new[] { 0.6m, 0.3m, 0.1m },
    };

    /// <summary>Tên tiếng Việt của hành.</summary>
    public static string ElementVn(FengShuiElement e) => e switch
    {
        FengShuiElement.Kim => "Kim",
        FengShuiElement.Moc => "Mộc",
        FengShuiElement.Thuy => "Thủy",
        FengShuiElement.Hoa => "Hỏa",
        FengShuiElement.Tho => "Thổ",
        _ => e.ToString(),
    };

    /// <summary>Tính Tứ Trụ. <paramref name="birthTime"/> null → bỏ trụ giờ, tính trên 3 trụ.</summary>
    public static BaTuChart Compute(DateOnly birthDate, TimeOnly? birthTime)
    {
        long jd = LunarCalendarConverter.JdFromDate(birthDate.Day, birthDate.Month, birthDate.Year);
        double sunLong = LunarCalendarConverter.SunLongitudeDeg(jd);

        // ── Trụ NĂM: đổi năm tại Lập Xuân (kinh độ mặt trời 315°) — tháng 1/đầu tháng 2 trước Lập Xuân thuộc năm trước.
        int pillarYear = birthDate.Year;
        if (birthDate.Month <= 2 && sunLong is >= 270 and < 315) pillarYear -= 1;
        int yearCan = ((pillarYear + 6) % 10 + 10) % 10;
        int yearChi = ((pillarYear + 8) % 12 + 12) % 12;

        // ── Trụ THÁNG theo tiết khí: tháng Dần bắt đầu tại 315°, mỗi tháng 30°.
        int monthIndex = (int)(((sunLong - 315 + 360) % 360) / 30); // 0 = Dần
        int monthChi = (2 + monthIndex) % 12;
        // Ngũ hổ độn: năm Giáp/Kỷ khởi Bính Dần; Ất/Canh → Mậu Dần; Bính/Tân → Canh Dần; Đinh/Nhâm → Nhâm Dần; Mậu/Quý → Giáp Dần.
        int monthCan = ((yearCan % 5) * 2 + 2 + monthIndex) % 10;

        // ── Trụ NGÀY theo JDN.
        int dayCan = (int)((jd + 9) % 10);
        int dayChi = (int)((jd + 1) % 12);

        // ── Trụ GIỜ theo ngũ thử độn (Giáp/Kỷ khởi Giáp Tý...). 23h–1h = Tý.
        BaTuPillar? hourPillar = null;
        if (birthTime is { } t)
        {
            int hourChi = ((t.Hour + 1) % 24) / 2 % 12;
            int hourCan = ((dayCan % 5) * 2 + hourChi) % 10;
            hourPillar = MakePillar("Giờ", hourCan, hourChi);
        }

        var yearPillar = MakePillar("Năm", yearCan, yearChi);
        var monthPillar = MakePillar("Tháng", monthCan, monthChi);
        var dayPillar = MakePillar("Ngày", dayCan, dayChi);

        // ── Phân bố ngũ hành: mỗi thiên can 1.0; tàng can trong chi theo trọng số chính/trung/dư khí.
        var dist = new Dictionary<FengShuiElement, decimal>();
        foreach (var e in FengShuiCalculator.AllElements) dist[e] = 0m;

        var pillars = new List<(int Can, int Chi)> { (yearCan, yearChi), (monthCan, monthChi), (dayCan, dayChi) };
        if (birthTime is { } t2)
        {
            int hc = ((t2.Hour + 1) % 24) / 2 % 12;
            pillars.Add((((dayCan % 5) * 2 + hc) % 10, hc));
        }

        foreach (var (c, chi) in pillars)
        {
            dist[CanElements[c]] += 1.0m;
            var hidden = HiddenStemIdx[chi];
            var weights = HiddenWeights[hidden.Length - 1];
            for (int i = 0; i < hidden.Length; i++)
                dist[CanElements[hidden[i]]] += weights[i];
        }

        var dayMaster = CanElements[dayCan];

        // ── Thân vượng/nhược rút gọn: (đồng hành + hành sinh nhật chủ) so với tổng, KHÔNG tính chính can ngày;
        //    được lệnh tháng (chính khí chi tháng sinh/trợ nhật chủ) cộng thêm.
        var supporter = FengShuiCalculator.GetGeneratingElement(dayMaster);
        decimal support = dist[dayMaster] + dist[supporter] - 1.0m; // trừ chính nhật chủ
        decimal total = dist.Values.Sum() - 1.0m;
        var monthMainElement = CanElements[HiddenStemIdx[monthChi][0]];
        bool seasonSupports = monthMainElement == dayMaster || monthMainElement == supporter;
        decimal ratio = total <= 0 ? 0 : (support + (seasonSupports ? 0.5m : 0m)) / total;
        bool strong = ratio >= 0.5m;

        // ── Dụng thần rút gọn: thân nhược → sinh-trợ (ấn + tỷ kiếp); thân vượng → tiết-hao (thực thương + tài).
        var favorable = strong
            ? new[] { FengShuiCalculator.GetGeneratedElement(dayMaster), FengShuiCalculator.GetControlledElement(dayMaster) }
            : new[] { supporter, dayMaster };

        var missing = dist.Where(kv => kv.Value == 0m).Select(kv => ElementVn(kv.Key)).ToList();

        return new BaTuChart(
            yearPillar, monthPillar, dayPillar, hourPillar,
            NhatChu: Can[dayCan],
            DayMasterElement: ElementVn(dayMaster),
            ElementDistribution: dist.ToDictionary(kv => ElementVn(kv.Key), kv => Math.Round(kv.Value, 2)),
            MissingElements: missing,
            BodyStrength: strong ? "Thân vượng" : "Thân nhược",
            FavorableElements: favorable.Select(ElementVn).ToList(),
            FavorableElementCodes: favorable.Select(e => e.ToString()).ToList(),
            MethodNote: (birthTime is null
                ? "Thiếu giờ sinh — chỉ tính 3 trụ (năm/tháng/ngày); kết quả vượng nhược và dụng thần là sơ bộ. "
                : "") +
                "Phương pháp rút gọn: trụ năm theo Lập Xuân, trụ tháng theo tiết khí; 23h gộp vào giờ Tý cùng ngày; " +
                "chưa xét hợp-xung-hình-hại giữa các chi.");
    }

    private static BaTuPillar MakePillar(string name, int canIdx, int chiIdx)
        => new(name, Can[canIdx], Chi[chiIdx],
            ElementVn(CanElements[canIdx]),
            HiddenStemIdx[chiIdx].Select(i => Can[i]).ToList());
}
