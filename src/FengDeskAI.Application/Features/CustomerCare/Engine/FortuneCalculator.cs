using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Kết quả xem mệnh tổng hợp — các phần null theo dữ liệu đầu vào (có gì tính nấy):
/// thiếu gender → Destiny không có block Bát Trạch; thiếu birthTime → BaTu không có trụ giờ.
/// <see cref="Missing"/> liệt kê dữ liệu còn thiếu để mở khóa phần sâu hơn.
/// </summary>
public sealed record FortuneChart(
    DateOnly SolarBirthDate,
    string? BirthTime,
    string? Gender,
    bool WasLunarInput,
    DestinyProfile Destiny,
    BaTuChart BaTu,
    IReadOnlyList<string> Missing,
    string Disclaimer);

/// <summary>
/// Facade DUY NHẤT cho việc xem mệnh: gom DestinyCalculator (nạp âm + Bát Trạch) và
/// BaTuCalculator (Tứ Trụ) với input nullable — mọi caller (AI tool, REST sau này, get_my_profile)
/// đi qua đây để logic "có gì tính nấy" + danh sách missing nằm một chỗ.
/// </summary>
public static class FortuneCalculator
{
    /// <summary>
    /// Tính từ ngày sinh DƯƠNG lịch (đã quy đổi nếu khách nhập âm — dùng
    /// <see cref="LunarCalendarConverter.Lunar2Solar"/> trước khi gọi).
    /// </summary>
    public static FortuneChart Compute(
        DateOnly solarBirthDate,
        TimeOnly? birthTime = null,
        Gender? gender = null,
        bool wasLunarInput = false)
    {
        var destiny = DestinyCalculator.ComputeFromSolar(solarBirthDate, gender);
        var baTu = BaTuCalculator.Compute(solarBirthDate, birthTime);

        var missing = new List<string>();
        if (gender is not (Gender.Male or Gender.Female))
            missing.Add("gender — needed for Bát Trạch cung mệnh + favorable directions");
        if (birthTime is null)
            missing.Add("birthTime — needed for the hour pillar to complete the Tứ Trụ reading");

        return new FortuneChart(
            solarBirthDate,
            birthTime?.ToString("HH:mm"),
            gender is Gender.Male or Gender.Female ? gender.ToString() : null,
            wasLunarInput,
            destiny,
            baTu,
            missing,
            "Thông tin phong thủy mang tính tham khảo.");
    }
}
