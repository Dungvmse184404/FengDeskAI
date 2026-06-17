namespace FengDeskAI.Contracts.Recommendation;

// ─────────────────────────────────────────────────────────────────────────────
// Hợp đồng dữ liệu giữa .NET monolith và AI microservice (Python).
// Xem CONTRACT.md cùng thư mục để biết LUẬT bắt buộc AI phải tuân thủ.
// Quy ước: enum gửi dưới dạng STRING (vd "Moc", "East") để khớp closed-set và dễ đọc.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Payload .NET gửi sang AI: đã chấm điểm + facts; AI chỉ diễn giải/đảo thứ tự.</summary>
public sealed record AiRecommendationRequest
{
    public required AiCustomerInfo Customer { get; init; }
    public required AiWorkspaceInfo Workspace { get; init; }
    public required IReadOnlyList<AiCandidate> Candidates { get; init; }
}

public sealed record AiCustomerInfo
{
    /// <summary>Mệnh Nạp Âm: Kim/Moc/Thuy/Hoa/Tho. Null nếu giới tính không Nam/Nữ (đã bỏ qua phần cá nhân).</summary>
    public string? Element { get; init; }
    public int? KuaNumber { get; init; }
    /// <summary>"East" (Đông tứ mệnh) hoặc "West" (Tây tứ mệnh). Null nếu không tính.</summary>
    public string? KuaGroup { get; init; }
    public IReadOnlyList<string> FavorableDirections { get; init; } = Array.Empty<string>();
}

public sealed record AiWorkspaceInfo
{
    public required string Type { get; init; }
    public bool IsPublic { get; init; }
    public required string Purpose { get; init; }
    public required string Style { get; init; }
    public required string Lighting { get; init; }
    public required string DeskOrientation { get; init; }
    public int DeskArea { get; init; }
    /// <summary>Trọng số cá nhân đã áp (1.0 riêng tư, 0.5 công cộng) — để AI hiểu mức nhấn mệnh.</summary>
    public decimal PersonalWeight { get; init; }
}

/// <summary>Một sản phẩm ứng viên đã được engine .NET chấm điểm.</summary>
public sealed record AiCandidate
{
    public required Guid ProductId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public decimal Score { get; init; }
    public int BaseRank { get; init; }
    /// <summary>Các "sự thật" tích cực engine đã tính — AI BẮT BUỘC chỉ giải thích dựa trên đây.</summary>
    public IReadOnlyList<string> MatchFacts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CautionFacts { get; init; } = Array.Empty<string>();
}

/// <summary>Kết quả AI trả về: diễn giải + thứ tự cuối. KHÔNG được thêm/bớt sản phẩm.</summary>
public sealed record AiRecommendationResponse
{
    public string? Summary { get; init; }
    public required IReadOnlyList<AiExplainedItem> Items { get; init; }
}

public sealed record AiExplainedItem
{
    public required Guid ProductId { get; init; }
    /// <summary>Thứ hạng cuối (1 = ưu tiên cao nhất). Chỉ được hoán vị trong tập đã nhận.</summary>
    public int FinalRank { get; init; }
    /// <summary>Đoạn văn thuyết phục, suy ra từ MatchFacts. Không bịa luật phong thủy mới.</summary>
    public required string Explanation { get; init; }
}
