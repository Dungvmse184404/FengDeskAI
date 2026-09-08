using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>Yêu cầu tạo gợi ý cho một workspace đã lưu.</summary>
public sealed record GenerateRecommendationRequest
{
    public Guid WorkspaceProfileId { get; init; }

    /// <summary>Số sản phẩm muốn gợi ý (mặc định 8, kẹp 1..20).</summary>
    public int? TopN { get; init; }

    /// <summary>
    /// Mục tiêu người dùng nêu trong phiên này (Tài lộc / Sức khỏe…). Có giá trị → LỌC ứng viên theo thẻ
    /// <c>product_aspirations</c> đã duyệt TRƯỚC khi chấm điểm, và chọn hướng đặt theo cung Bát Trạch
    /// tương ứng. Null = không lọc. Không lưu trên <c>User</c> — xem ADR personalized-recommendation-v3.1 §4.
    /// </summary>
    public Aspiration? Aspiration { get; init; }
}

/// <summary>
/// Yêu cầu gợi ý vật phẩm MANG THEO NGƯỜI (vòng tay, mặt dây, charm treo xe…) — chấm theo bản mệnh,
/// không gắn workspace nên không có <c>WorkspaceProfileId</c>.
/// </summary>
public sealed record GeneratePersonalRecommendationRequest
{
    /// <summary>Số sản phẩm muốn gợi ý (mặc định 8, kẹp 1..20).</summary>
    public int? TopN { get; init; }

    /// <inheritdoc cref="GenerateRecommendationRequest.Aspiration"/>
    public Aspiration? Aspiration { get; init; }
}

public sealed record RecommendationResponse
{
    public Guid Id { get; init; }

    /// <summary>Workspace | PersonalCarry — FE dựa vào đây để render đúng loại phiên.</summary>
    public string Kind { get; init; } = null!;

    public string? CustomerElement { get; init; }
    public int? KuaNumber { get; init; }
    public string? KuaGroup { get; init; }

    /// <summary>
    /// LEGACY (engine v2) — engine v3 dùng <c>WorkspaceScope</c> thay cho trọng số này. Giữ trong response
    /// REST để không phá FE; KHÔNG gửi cho LLM nữa (model đọc được sẽ diễn giải sai).
    /// </summary>
    public decimal PersonalWeight { get; init; }

    public string Status { get; init; } = null!;
    public string? Summary { get; init; }

    /// <summary>Chênh lệch ngũ hành lý tưởng vs hiện trạng phòng (engine v3). Null với phiên PersonalCarry.</summary>
    public GapBreakdownResponse? Gap { get; init; }

    /// <summary>Căn cứ ngũ hành cá nhân đã dùng để chấm. Chỉ có ở phiên PersonalCarry.</summary>
    public PersonalTargetResponse? PersonalTarget { get; init; }

    /// <summary>
    /// Ghi chú của engine về cách danh sách được dựng — vd đã phải BỎ bộ lọc mục tiêu vì chưa sản phẩm nào
    /// gắn thẻ đó. Null = không có gì bất thường. AI nên nhắc lại ý này cho người dùng.
    /// </summary>
    public string? Note { get; init; }

    public List<RecommendationItemResponse> Items { get; init; } = new();
}

/// <summary>Vector mục tiêu cá nhân + căn cứ (Tứ Trụ hay Nạp Âm) — để AI diễn giải đúng cơ sở, không đoán.</summary>
public sealed record PersonalTargetResponse
{
    /// <summary>TuTru | NapAm.</summary>
    public string Source { get; init; } = null!;

    /// <summary>Các hành đang cần được bồi (tên tiếng Việt), theo thứ tự ưu tiên.</summary>
    public List<string> Elements { get; init; } = new();

    public string Note { get; init; } = null!;
}

/// <summary>Breakdown Gap = adjustedIdeal − current cho từng hành, để FE hiển thị phòng đang thiếu/thừa gì.</summary>
public sealed record GapBreakdownResponse
{
    public List<GapElementRow> Elements { get; init; } = new();
}

public sealed record GapElementRow
{
    public string Element { get; init; } = null!;
    public decimal Ideal { get; init; }
    public decimal Current { get; init; }
    public decimal Gap { get; init; }
}

public sealed record RecommendationItemResponse
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = null!;
    public decimal? Price { get; init; }
    public string? ImageUrl { get; init; }
    public decimal Score { get; init; }
    public int Rank { get; init; }
    public List<string> MatchFacts { get; init; } = new();
    public List<string> CautionFacts { get; init; } = new();

    /// <summary>Gợi ý hướng đặt vật phẩm (engine v3, Directional Validation). Null nếu không có.</summary>
    public string? PlacementHint { get; init; }

    public string? Explanation { get; init; }
}

/// <summary>
/// Độ phù hợp của 1 sản phẩm với 1 workspace (trang chi tiết sản phẩm). Khác <see cref="RecommendationResponse"/>:
/// KHÔNG loại sản phẩm — xung mệnh/lệch vibe chỉ phản ánh vào score/cautionFacts, luôn có kết quả.
/// </summary>
public sealed record ProductFitResponse
{
    public Guid ProductId { get; init; }
    public Guid WorkspaceProfileId { get; init; }

    /// <summary>Điểm phù hợp ∈ [-1,1]. Âm = xung khắc/lệch nhu cầu phòng, dương = phù hợp.</summary>
    public decimal Score { get; init; }

    public List<string> MatchFacts { get; init; } = new();
    public List<string> CautionFacts { get; init; } = new();
    public string? PlacementHint { get; init; }

    /// <summary>Vector ngũ hành của phòng (ideal/adjustedIdeal/current/gap) — cùng shape với element-analysis.</summary>
    public List<ElementAnalysisRow> Gap { get; init; } = new();

    /// <summary>Vector ngũ hành của sản phẩm (Σ=1) — để FE so sánh cạnh Gap (sản phẩm cấp gì vs phòng cần gì).</summary>
    public List<ProductElementRow> ProductVector { get; init; } = new();

    /// <summary>
    /// v3.2 §9 — mọi thành phần đã tạo ra <see cref="Score"/>: waterfall, penalty có/không áp, và các
    /// vector để vẽ radar. Trước đây chúng là biến cục bộ trong engine nên màn hình chỉ có một con số
    /// trần không giải thích được.
    /// </summary>
    public ScoreBreakdownResponse? Breakdown { get; init; }

    /// <summary>
    /// Nguồn nào tạo ra vector <c>Current</c> của phòng và chiếm bao nhiêu % — cùng dữ liệu tooltip đã
    /// có ở <c>element-analysis</c>, mang sang đây để user hiểu <b>vì sao phòng được cho là đang thiếu
    /// hành đó</b>, chứ không chỉ biết là nó thiếu.
    /// </summary>
    public List<CurrentContributionRow> Contributions { get; init; } = new();

    /// <summary>Số bằng chứng THẬT (tag user khai + sản phẩm đã đặt). 0 = hiện trạng hoàn toàn suy từ loại phòng.</summary>
    public int EvidenceCount { get; init; }

    /// <summary>Tỉ lệ <c>Current</c> đến từ dữ liệu user khai thay vì nền phòng (0..1).</summary>
    public decimal Confidence { get; init; }
}

/// <summary>
/// Độ phù hợp của 1 sản phẩm <see cref="ProductPlacement.Carry"/> với BẢN MỆNH người dùng — không gắn
/// với phòng nào. Khác <see cref="ProductFitResponse"/>: không có gap, không có hướng đặt, waterfall chỉ
/// một thành phần (§PHẦN E #6), nên FE phải dùng component riêng chứ không tái sử dụng panel của phòng.
/// </summary>
public sealed record PersonalFitResponse
{
    public Guid ProductId { get; init; }

    /// <summary>Điểm phù hợp ∈ [-1,1] so với dụng thần của user.</summary>
    public decimal Score { get; init; }

    public List<string> MatchFacts { get; init; } = new();
    public List<string> CautionFacts { get; init; } = new();
    public string? PlacementHint { get; init; }

    public ScoreBreakdownResponse? Breakdown { get; init; }

    /// <summary>Vector "người đang cần hành gì" (dụng thần Tứ Trụ, fallback Nạp Âm) — Σ=1.</summary>
    public List<ProductElementRow> PersonalNeedVector { get; init; } = new();

    /// <summary>Vector ngũ hành của sản phẩm (Σ=1).</summary>
    public List<ProductElementRow> ProductVector { get; init; } = new();

    /// <summary>Hành Nạp Âm của user, vd <c>"Moc"</c>.</summary>
    public string DestinyElement { get; init; } = null!;

    /// <summary>vd <c>"Mộc — Đại Lâm Mộc (1988)"</c>.</summary>
    public string DestinyLabelVi { get; init; } = null!;
}

public sealed record ProductElementRow
{
    /// <summary>Kim / Moc / Thuy / Hoa / Tho.</summary>
    public string Element { get; init; } = null!;
    public decimal Value { get; init; }
}
