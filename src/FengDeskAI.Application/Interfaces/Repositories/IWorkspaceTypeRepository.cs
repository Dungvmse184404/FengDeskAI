using FengDeskAI.Domain.Entities.Workspace;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IWorkspaceTypeRepository : IGenericRepository<WorkspaceType>
{
    /// <summary>Loại không gian khả dụng cho user: hệ thống seed sẵn + loại do chính user tạo.</summary>
    Task<List<WorkspaceType>> GetAvailableForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>True nếu loại tồn tại và user được dùng (system hoặc do user tạo).</summary>
    Task<bool> IsAvailableToUserAsync(Guid id, Guid userId, CancellationToken ct = default);
}
