using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;

namespace FengDeskAI.Application.Features.Identity.Services;

public interface IUserService
{
    /// <summary>
    /// Tìm user theo email / tên (có dấu hoặc không) / phone. Tối thiểu 3 ký tự;
    /// chỉ trả field tối thiểu (xem <see cref="UserSearchResponse"/>). Loại chính người đang tìm (<paramref name="searcherId"/>).
    /// </summary>
    Task<IServiceResult<List<UserSearchResponse>>> SearchAsync(Guid searcherId, string? q, int? limit, CancellationToken ct = default);
}
