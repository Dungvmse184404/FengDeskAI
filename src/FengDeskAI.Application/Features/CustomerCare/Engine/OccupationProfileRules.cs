using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Luật hợp lệ của hồ sơ ngũ hành nghề — dùng chung bởi service admin (PUT <c>/profile</c>) và seeder,
/// để hai cửa nhập dữ liệu không thể lệch nhau về định nghĩa "hồ sơ đúng". Thuần, không I/O.
/// </summary>
public static class OccupationProfileRules
{
    /// <summary>Sai số cho phép của Σ share — 5 số numeric(4,3) làm tròn không thể cộng đúng 1.000 mọi lúc.</summary>
    public const decimal SumTolerance = 0.001m;

    /// <summary>
    /// Trả thông báo lỗi tiếng Việt, hoặc <c>null</c> khi hợp lệ. Danh sách rỗng là hợp lệ (= xóa hồ sơ).
    /// </summary>
    public static string? Validate(IReadOnlyCollection<(FengShuiElement Element, decimal Share)> entries)
    {
        if (entries.Count == 0) return null;

        if (entries.Select(e => e.Element).Distinct().Count() != entries.Count)
            return "Mỗi hành chỉ được khai một lần.";

        if (entries.Any(e => e.Share < 0m || e.Share > 1m))
            return "Share phải nằm trong [0, 1].";

        decimal sum = entries.Sum(e => e.Share);
        if (Math.Abs(sum - 1m) > SumTolerance)
            return $"Tổng share phải bằng 1 (đang là {sum:0.000}).";

        return null;
    }

    /// <summary>
    /// Hồ sơ → vector Σ=1. Dựng tay chứ KHÔNG qua <c>Normalize</c>: hồ sơ đã Σ=1 từ lúc nhập (đã qua
    /// <see cref="Validate"/>); chuẩn hoá lại chỉ che mất dữ liệu lệch tổng nếu có ai ghi thẳng vào DB.
    /// </summary>
    public static ElementVector ToVector(IEnumerable<(FengShuiElement Element, decimal Share)> entries)
    {
        var v = ElementVector.Zero;
        foreach (var (element, share) in entries)
            v = v.Add(ElementVector.Single(element).Scale(share));
        return v;
    }
}
