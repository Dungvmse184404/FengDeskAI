using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Vendor.DTOs;

namespace FengDeskAI.Application.Features.Vendor.Services;

public interface IStoreService
{
    Task<IServiceResult<List<StoreResponse>>> GetActiveAsync(CancellationToken ct = default);
    Task<IServiceResult<StoreResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IServiceResult<StoreResponse>> CreateAsync(Guid actorUserId, CreateStoreRequest request, CancellationToken ct = default);
    Task<IServiceResult<StoreResponse>> UpdateAsync(Guid id, Guid actorUserId, bool isAdmin, UpdateStoreRequest request, CancellationToken ct = default);
    Task<IServiceResult<StoreAddressResponse>> UpsertAddressAsync(Guid id, Guid actorUserId, bool isAdmin, UpsertStoreAddressRequest request, CancellationToken ct = default);

    Task<IServiceResult<List<StaffAssignmentResponse>>> GetStaffAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default);
    Task<IServiceResult<StaffAssignmentResponse>> AssignStaffAsync(Guid id, Guid actorUserId, bool isAdmin, AssignStaffRequest request, CancellationToken ct = default);
    Task<IServiceResult> UnassignStaffAsync(Guid id, Guid assignmentId, Guid actorUserId, bool isAdmin, CancellationToken ct = default);
}
