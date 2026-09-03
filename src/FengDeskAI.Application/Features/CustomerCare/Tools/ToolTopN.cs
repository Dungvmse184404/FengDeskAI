namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Số lượng gợi ý cho các tool CHAT. Hẹp hơn hẳn API REST (<c>RecommendationService</c> vẫn 8/20 cho
/// FE grid): trong hội thoại, một danh sách dài khiến câu trả lời loãng và model hay bịa thêm sản phẩm.
/// </summary>
public static class ToolTopN
{
    public const int Min = 2;
    public const int Max = 5;
    public const int Default = 3;

    /// <summary>Kẹp về [<see cref="Min"/>, <see cref="Max"/>]; null → <see cref="Default"/>.</summary>
    public static int Clamp(int? requested) => Math.Clamp(requested ?? Default, Min, Max);
}
