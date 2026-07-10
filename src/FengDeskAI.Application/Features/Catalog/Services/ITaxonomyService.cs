using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Quản lý bảng tra cứu Style / Vibe — admin thêm/sửa tên không cần đụng code.
/// Code bất biến (thuật toán tham chiếu); chỉ Name/IsActive/SortOrder được sửa.
/// </summary>
public interface ITaxonomyService
{
    Task<IServiceResult<List<LookupItemResponse>>> GetStylesAsync(bool includeInactive, CancellationToken ct = default);
    Task<IServiceResult<LookupItemResponse>> CreateStyleAsync(CreateLookupRequest request, CancellationToken ct = default);
    Task<IServiceResult<LookupItemResponse>> UpdateStyleAsync(string code, UpdateLookupRequest request, CancellationToken ct = default);

    Task<IServiceResult<List<LookupItemResponse>>> GetVibesAsync(bool includeInactive, CancellationToken ct = default);
    Task<IServiceResult<LookupItemResponse>> CreateVibeAsync(CreateLookupRequest request, CancellationToken ct = default);
    Task<IServiceResult<LookupItemResponse>> UpdateVibeAsync(string code, UpdateLookupRequest request, CancellationToken ct = default);

    // Element: ngũ hành cố định 5 — chỉ đọc + sửa tên hiển thị (không thêm mới).
    Task<IServiceResult<List<LookupItemResponse>>> GetElementsAsync(bool includeInactive, CancellationToken ct = default);
    Task<IServiceResult<LookupItemResponse>> UpdateElementAsync(string code, UpdateLookupRequest request, CancellationToken ct = default);

    /// <summary>Vocabulary code hợp lệ theo kind (Material/Color/Shape) cho form vendor — không lộ weight/element.</summary>
    Task<IServiceResult<List<ElementInputCodesResponse>>> GetElementInputCodesAsync(CancellationToken ct = default);
}
