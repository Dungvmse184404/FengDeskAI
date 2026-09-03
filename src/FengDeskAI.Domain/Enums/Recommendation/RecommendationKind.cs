namespace FengDeskAI.Domain.Enums.Recommendation;

/// <summary>Loại phiên gợi ý — quyết định phiên có gắn workspace hay không (xem <c>recommendations.kind</c>).</summary>
public enum RecommendationKind
{
    /// <summary>Gợi ý cho một hồ sơ không gian: chấm theo gap ngũ hành của phòng.</summary>
    Workspace,

    /// <summary>Gợi ý vật phẩm mang theo người: chấm theo bản mệnh, không gắn workspace.</summary>
    PersonalCarry,
}
