using System.Globalization;
using System.Text.Json;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Tool xem mệnh STATELESS cho người bất kỳ (chính user hoặc người thân/bạn — không lưu DB):
/// graceful theo dữ liệu có — chỉ ngày sinh → nạp âm/can chi/3 trụ; +giới tính → Cung Mệnh Bát Trạch
/// (4 hướng cát + 4 hung); +giờ sinh → Tứ Trụ đầy đủ + dụng thần. Field <c>missing</c> báo AI biết
/// còn thiếu gì để hỏi bổ sung nhưng vẫn trả lời ngay phần đã tính.
/// </summary>
public sealed class ComputeDestinyChartTool : IAiTool
{
    public string Name => "compute_destiny_chart";

    public string Description =>
        "Compute the Vietnamese feng shui destiny chart for ANY person (the current user, their friend, family member...) " +
        "from birth info. Returns: can-chi year, zodiac animal, nạp âm element/mệnh with meaning; " +
        "PLUS (if gender given) Bát Trạch cung mệnh with 4 favorable directions (Sinh Khí/Diên Niên/Thiên Y/Phục Vị) " +
        "and 4 unfavorable ones; PLUS (if birthTime given) the full Tứ Trụ/Bát Tự four pillars with element distribution " +
        "and 'favorableElementCodes' — use those codes with search_products/recommend_products element filter to suggest products. " +
        "The 'missing' field lists what extra info would unlock deeper reading — answer with what you have first, then ask for it. " +
        "NEVER calculate mệnh/cung/tứ trụ yourself — always call this tool. Results are for reference/entertainment; say so briefly.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["birthDate"] = new("string", "Birth date in yyyy-MM-dd. Solar (dương lịch) by default; set isLunar=true if the person gave a lunar date.", Required: true),
        ["birthTime"] = new("string", "Birth time HH:mm (24h clock time). Optional — unlocks the full four-pillar Bát Tự reading."),
        ["gender"] = new("string", "Biological gender for Bát Trạch cung mệnh. Optional.", Enum: new[] { "Male", "Female" }),
        ["isLunar"] = new("boolean", "true if birthDate is a lunar-calendar date (âm lịch). Default false."),
    };

    public Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        // ── Parse input (chịu lỗi tốt: model hay gửi định dạng lệch chuẩn).
        var rawDate = ToolArgs.GetString(arguments, "birthDate");
        if (string.IsNullOrWhiteSpace(rawDate) ||
            !DateOnly.TryParseExact(rawDate, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            return Task.FromResult(ToolArgs.Error("birthDate is required in yyyy-MM-dd (or dd/MM/yyyy) format."));

        TimeOnly? birthTime = null;
        var rawTime = ToolArgs.GetString(arguments, "birthTime");
        if (!string.IsNullOrWhiteSpace(rawTime))
        {
            if (TimeOnly.TryParseExact(rawTime, new[] { "HH:mm", "H:mm", "HH:mm:ss" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                birthTime = t;
            else
                return Task.FromResult(ToolArgs.Error("birthTime must be HH:mm (24h). Omit it if unknown."));
        }

        Gender? gender = ToolArgs.GetString(arguments, "gender") switch
        {
            "Male" or "male" => Gender.Male,
            "Female" or "female" => Gender.Female,
            _ => null,
        };

        bool isLunar = arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("isLunar", out var lv)
            && lv.ValueKind == JsonValueKind.True;

        // ── Ngày âm → đổi sang dương trước khi tính (engine làm việc trên dương lịch).
        var solarDate = birthDate;
        if (isLunar)
        {
            var (d, m, y) = LunarCalendarConverter.Lunar2Solar(birthDate.Day, birthDate.Month, birthDate.Year);
            if (d == 0)
                return Task.FromResult(ToolArgs.Error("Could not convert this lunar date — please double-check day/month/year."));
            solarDate = new DateOnly(y, m, d);
        }

        // ── Toàn bộ logic "có gì tính nấy" + missing nằm trong facade FortuneCalculator.
        return Task.FromResult(ToolArgs.Json(
            FortuneCalculator.Compute(solarDate, birthTime, gender, wasLunarInput: isLunar)));
    }
}
