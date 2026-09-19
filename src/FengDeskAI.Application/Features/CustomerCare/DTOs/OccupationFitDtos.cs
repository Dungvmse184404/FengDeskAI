namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>
/// Mặt A — "sản phẩm này hợp NGHỀ NÀO, bao nhiêu %": <c>ô · p</c> cho từng nghề đang bật có hồ sơ,
/// KHÔNG cần đăng nhập, phòng hay ngày sinh. Cùng con số với dòng <c>OCCUPATION_SCORE</c> trong
/// breakdown gợi ý — user thấy 88% ở đây thì waterfall cũng 88%. ADR <c>occupation-product-fit-v1.md</c> §4.3.
/// </summary>
public sealed record ProductOccupationFitResponse
{
    public Guid ProductId { get; init; }

    public string FormulaVersion { get; init; } = null!;

    /// <summary>Desk / Living / Carry / Consumable — FE dùng để chọn nhãn ("đặt trong phòng" vs "mang theo").</summary>
    public string Placement { get; init; } = null!;

    /// <summary>Vector ngũ hành của sản phẩm (Σ=1) — cùng shape với breakdown để vẽ radar.</summary>
    public List<ProductElementRow> ProductVector { get; init; } = new();

    /// <summary>Sắp giảm dần theo <see cref="OccupationFitRow.Score"/>. Rỗng khi hàng tiêu hao hoặc chưa nghề nào có hồ sơ.</summary>
    public List<OccupationFitRow> Fits { get; init; } = new();

    /// <summary>Lý do danh sách rỗng, nếu có (vd hàng tiêu hao không xét phong thủy).</summary>
    public string? NoteVi { get; init; }
}

public sealed record OccupationFitRow
{
    public string Code { get; init; } = null!;
    public string NameVi { get; init; } = null!;

    /// <summary><c>ô · p</c> ∈ [−1, 1], làm tròn 3 chữ số.</summary>
    public decimal Score { get; init; }

    /// <summary><c>(score + 1) / 2 × 100</c> — cùng công thức <c>DisplayPercentOf</c>; 50% = trung tính.</summary>
    public int DisplayPercent { get; init; }

    /// <summary>Rất hợp / Phù hợp / Trung tính / Cân nhắc — cùng ngưỡng <c>ScoreBadge</c> của FE.</summary>
    public string TierVi { get; init; } = null!;

    /// <summary><c>ô</c> — mỗi trục ∈ [−1, 1]. Mặt A không có mệnh nên đây là hướng THÔ, chưa chặn.</summary>
    public List<ProductElementRow> Direction { get; init; } = new();

    public string ReasonVi { get; init; } = null!;
}
