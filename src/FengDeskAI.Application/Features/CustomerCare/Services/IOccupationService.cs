using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

/// <summary>
/// Mặt công khai của nghề nghiệp — danh sách chọn nghề và "sản phẩm này hợp nghề nào" (mặt A của N3).
/// Tách khỏi <see cref="IScoringConfigAdminService"/> vì đây là dữ liệu công khai và <b>không kèm hồ sơ
/// thô</b>: danh sách chọn nghề mà hiện số thì người dùng chọn theo điểm thay vì chọn nghề thật của mình.
/// </summary>
public interface IOccupationService
{
    Task<IServiceResult<List<OccupationOptionDto>>> GetOptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// <c>ô · p</c> cho mọi nghề đang bật có hồ sơ (hoặc một nghề khi <paramref name="occupationCode"/> có),
    /// sắp giảm dần. Không cần user: không mệnh ⇒ dùng <c>ô</c> thô, không chặn — đây là tính chất của
    /// sản phẩm × nghề, không của một người.
    /// </summary>
    Task<IServiceResult<ProductOccupationFitResponse>> GetProductFitAsync(
        Guid productId, string? occupationCode, CancellationToken ct = default);
}
