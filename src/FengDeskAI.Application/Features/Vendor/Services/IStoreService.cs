using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Vendor.DTOs;

namespace FengDeskAI.Application.Features.Vendor.Services;

public interface IStoreService
{
    Task<IServiceResult<List<StoreResponse>>> GetActiveAsync(CancellationToken ct = default);
    Task<IServiceResult<StoreResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IServiceResult<StoreResponse>> CreateAsync(Guid actorUserId, CreateStoreRequest request, CancellationToken ct = default);
    Task<IServiceResult<StoreResponse>> UpdateAsync(Guid id, Guid actorUserId, bool isAdmin, UpdateStoreRequest request, CancellationToken ct = default);

    /// <summary>Soft-delete store (owner hoặc admin). Đồng thời soft-delete địa chỉ kèm theo.</summary>
    Task<IServiceResult> DeleteAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Hard-delete store + địa chỉ + phân công (vật lý). Chỉ Staff/Admin — gate ở controller.</summary>
    Task<IServiceResult> HardDeleteAsync(Guid id, CancellationToken ct = default);

    // ===== Địa chỉ store (1-1) =====
    Task<IServiceResult<StoreAddressResponse>> AddAddressAsync(Guid id, Guid actorUserId, bool isAdmin, CreateStoreAddressRequest request, CancellationToken ct = default);
    Task<IServiceResult<StoreAddressResponse>> UpdateAddressAsync(Guid id, Guid actorUserId, bool isAdmin, UpdateStoreAddressRequest request, CancellationToken ct = default);
    /// <summary>Soft-delete địa chỉ (owner hoặc admin).</summary>
    Task<IServiceResult> DeleteAddressAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default);
    /// <summary>Hard-delete địa chỉ (vật lý). Chỉ Staff/Admin — gate ở controller.</summary>
    Task<IServiceResult> HardDeleteAddressAsync(Guid id, CancellationToken ct = default);

    /// <summary>Các store user hiện tại đồng sở hữu (kênh người bán).</summary>
    Task<IServiceResult<List<StoreResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default);

    // ===== Owner (đồng sở hữu — marketplace) =====
    Task<IServiceResult<List<StoreOwnerResponse>>> GetOwnersAsync(Guid id, CancellationToken ct = default);
    /// <summary>Thêm đồng sở hữu (owner hiện tại hoặc Admin). Tự cấp flag GardenOwner cho user được thêm.</summary>
    Task<IServiceResult<StoreOwnerResponse>> AddOwnerAsync(Guid id, Guid actorUserId, bool isAdmin, AddOwnerRequest request, CancellationToken ct = default);
    /// <summary>Gỡ đồng sở hữu (owner hiện tại hoặc Admin). Không gỡ được owner primary.</summary>
    Task<IServiceResult> RemoveOwnerAsync(Guid id, Guid ownerUserId, Guid actorUserId, bool isAdmin, CancellationToken ct = default);

    Task<IServiceResult<List<StaffAssignmentResponse>>> GetStaffAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default);
    Task<IServiceResult<StaffAssignmentResponse>> AssignStaffAsync(Guid id, Guid actorUserId, bool isAdmin, AssignStaffRequest request, CancellationToken ct = default);
    Task<IServiceResult> UnassignStaffAsync(Guid id, Guid assignmentId, Guid actorUserId, bool isAdmin, CancellationToken ct = default);
}
