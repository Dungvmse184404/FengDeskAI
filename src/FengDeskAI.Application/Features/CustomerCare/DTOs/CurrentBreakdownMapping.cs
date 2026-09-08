using FengDeskAI.Application.Features.CustomerCare.Engine;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>
/// Quy <see cref="CurrentBreakdown"/> của engine ra DTO "nguồn nào chiếm bao nhiêu %".
///
/// <para>
/// Tách ra khỏi <c>WorkspaceProfileService</c> vì <b>hai</b> màn hình cần cùng con số: radar phân tích
/// phòng (<c>GET /workspace-profiles/{id}/element-analysis</c>) và panel giải thích điểm sản phẩm
/// (<c>GET /recommendations/fit</c>, v3.2 §9.2). Chép sang chỗ thứ hai thì sớm muộn hai bản lệch nhau
/// và user thấy cùng một tag chiếm hai tỉ lệ khác nhau trên hai màn hình.
/// </para>
/// </summary>
public static class CurrentBreakdownMapping
{
    /// <summary>
    /// Phần của nguồn <c>i</c> trong <c>Current[e]</c> = <c>Vector_i[e] × Votes_i / TotalVotes</c>.
    /// Tổng <see cref="CurrentContributionRow.SharePercent"/> mọi nguồn = 100 → FE xếp chồng thẳng lên
    /// radar, không phải tính lại.
    /// </summary>
    public static List<CurrentContributionRow> ToContributionRows(CurrentBreakdown breakdown)
    {
        if (breakdown.TotalVotes <= 0m) return new List<CurrentContributionRow>();

        return breakdown.Contributions
            .Select(c =>
            {
                var share = c.Votes / breakdown.TotalVotes;
                return new CurrentContributionRow
                {
                    Source = c.Source.ToString(),
                    Label = c.Label,
                    SharePercent = Math.Round(100m * share, 2),
                    Votes = Math.Round(c.Votes, 3),
                    Elements = c.Vector.Enumerate()
                        .Where(x => x.Value > 0m)
                        .Select(x => new ContributionElementShare(
                            x.Element.ToString(), Math.Round(100m * x.Value * share, 2)))
                        .OrderByDescending(x => x.Percent)
                        .ToList(),
                    InputKind = c.InputKind?.ToString(),
                    InputCode = c.InputCode,
                    ProductId = c.ProductId,
                };
            })
            .OrderByDescending(r => r.SharePercent)
            .ToList();
    }

    /// <summary>
    /// Tỉ lệ <c>Current</c> đến từ dữ liệu user khai thay vì suy ra từ nền loại phòng (0..1).
    /// <c>0</c> = chưa khai gì, toàn bộ hiện trạng là phỏng đoán theo loại phòng — FE hạ badge độ tin cậy
    /// và mời user khai thêm tag thay vì để họ tin vào một con số không có bằng chứng.
    /// </summary>
    public static decimal ConfidenceOf(CurrentBreakdown breakdown)
        => breakdown.TotalVotes <= 0m
            ? 0m
            : Math.Round(
                breakdown.Contributions
                    // Loại MỌI nguồn prior (nền phòng + chủ nhân), khớp với EvidenceCount. Chủ nhân
                    // nằm ở MẪU SỐ nhưng không ở tử số ⇒ thêm chủ nhân làm confidence GIẢM: phần của
                    // current đến từ quan sát thật đúng là nhỏ đi. Tính chủ nhân là bằng chứng thì
                    // phòng chưa khai gì vẫn báo độ tin cậy cao — sai hẳn bản chất.
                    .Where(c => !CurrentBreakdown.IsPrior(c.Source))
                    .Sum(c => c.Votes) / breakdown.TotalVotes,
                3);
}
