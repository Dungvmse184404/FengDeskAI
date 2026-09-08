using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Interfaces.Repositories;

/// <summary>
/// Đọc dữ liệu cấu hình cho engine chấm điểm v3 (tham số, map ngũ hành, vector loại phòng,
/// modifier intent, input phòng/sản phẩm). Chỉ đọc — seed &amp; admin CRUD nằm nơi khác.
/// </summary>
public interface IScoringConfigRepository
{
    Task<List<ScoringParam>> GetScoringParamsAsync(CancellationToken ct = default);
    Task<List<ElementInputMap>> GetElementInputMapAsync(CancellationToken ct = default);
    Task<List<WorkspaceTypeElement>> GetWorkspaceTypeElementsAsync(Guid workspaceTypeId, CancellationToken ct = default);
    Task<List<WorkPurposeElementModifier>> GetWorkPurposeModifiersAsync(WorkPurpose purpose, CancellationToken ct = default);
    Task<List<WorkspaceProfileInput>> GetWorkspaceProfileInputsAsync(Guid workspaceProfileId, CancellationToken ct = default);
    Task<List<ProductElementInput>> GetProductElementInputsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    /// <summary>
    /// Delta ngũ hành của một nghề (P5). Rỗng khi nghề chưa được chuyên gia nhập delta — engine coi
    /// như không có nghề nghiệp, đúng ý: thiếu dữ liệu thì đừng đoán.
    /// </summary>
    Task<List<OccupationElementModifier>> GetOccupationModifiersAsync(Guid occupationId, CancellationToken ct = default);

    /// <summary>Danh sách nghề đang bật, kèm delta — cho màn hình chọn nghề và màn quản trị.</summary>
    Task<List<Occupation>> GetOccupationsAsync(bool includeInactive = false, CancellationToken ct = default);

    /// <summary>Một nghề theo mã bất biến, kèm delta. <c>null</c> khi không có.</summary>
    Task<Occupation?> GetOccupationByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Thay toàn bộ input (màu/vật liệu/hình khối) của 1 sản phẩm. Chưa commit — caller lưu qua UoW.</summary>
    Task ReplaceProductElementInputsAsync(Guid productId, IEnumerable<ProductElementInput> inputs, CancellationToken ct = default);

    /// <summary>Thay toàn bộ input (màu/vật liệu/hình khối) của 1 workspace profile. Chưa commit — caller lưu qua UoW.</summary>
    Task ReplaceWorkspaceProfileInputsAsync(Guid workspaceProfileId, IEnumerable<WorkspaceProfileInput> inputs, CancellationToken ct = default);
}
