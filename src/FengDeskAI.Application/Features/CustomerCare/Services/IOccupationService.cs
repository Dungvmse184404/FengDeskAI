using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

/// <summary>
/// Danh sách nghề cho màn hình hồ sơ người dùng. Tách khỏi <see cref="IScoringConfigAdminService"/>
/// vì đây là dữ liệu công khai và <b>không kèm delta</b>: con số delta là chuyện của engine, hiện ra
/// cho người chọn nghề chỉ khiến họ chọn theo điểm thay vì chọn theo nghề thật của mình.
/// </summary>
public interface IOccupationService
{
    Task<IServiceResult<List<OccupationOptionDto>>> GetOptionsAsync(CancellationToken ct = default);
}
