using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Vendor;

namespace FengDeskAI.Application.Features.Vendor.Services;

public class StoreService : IStoreService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public StoreService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<List<StoreResponse>>> GetActiveAsync(CancellationToken ct = default)
        => ServiceResult<List<StoreResponse>>.Success(
            _mapper.Map<List<StoreResponse>>(await _uow.Stores.GetActiveAsync(ct)));

    public async Task<IServiceResult<StoreResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetDetailAsync(id, ct);
        if (store is null)
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy cửa hàng.");
        return ServiceResult<StoreResponse>.Success(_mapper.Map<StoreResponse>(store));
    }

    public async Task<IServiceResult<StoreResponse>> CreateAsync(Guid actorUserId, CreateStoreRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.BadRequest, "Tên cửa hàng không được để trống.");
        if (string.IsNullOrWhiteSpace(request.Hotline))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.BadRequest, "Hotline không được để trống.");
        if (!await _uow.Users.AnyAsync(u => u.Id == request.OwnerUserId, ct))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.BadRequest, "Chủ cửa hàng (owner) không tồn tại.");

        var entity = _mapper.Map<GardenStore>(request);
        entity.Name = request.Name.Trim();
        entity.IsActive = true;

        await _uow.Stores.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<StoreResponse>.Success(
            _mapper.Map<StoreResponse>(entity), "Tạo cửa hàng thành công.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<StoreResponse>> UpdateAsync(Guid id, Guid actorUserId, bool isAdmin, UpdateStoreRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy cửa hàng.");
        if (!IsOwnerOrAdmin(store, actorUserId, isAdmin))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền sửa cửa hàng này.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.BadRequest, "Tên cửa hàng không được để trống.");

        store.Name = request.Name.Trim();
        store.Description = request.Description;
        store.Hotline = request.Hotline;
        store.OpeningHours = request.OpeningHours;
        store.IsActive = request.IsActive;
        _uow.Stores.Update(store);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<StoreResponse>.Success(_mapper.Map<StoreResponse>(store), "Cập nhật cửa hàng thành công.");
    }

    public async Task<IServiceResult<StoreAddressResponse>> UpsertAddressAsync(Guid id, Guid actorUserId, bool isAdmin, UpsertStoreAddressRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy cửa hàng.");
        if (!IsOwnerOrAdmin(store, actorUserId, isAdmin))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền sửa cửa hàng này.");
        if (string.IsNullOrWhiteSpace(request.StreetAddress))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.BadRequest, "Địa chỉ chi tiết không được để trống.");
        if (!await _uow.Locations.WardExistsAsync(request.WardId, ct))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.BadRequest, "Phường/xã không hợp lệ.");

        var address = await _uow.Stores.GetAddressAsync(id, ct);
        if (address is null)
        {
            address = new StoreAddress { StoreId = id };
            await _uow.Stores.AddAddressAsync(address, ct);
        }
        address.WardId = request.WardId;
        address.StreetAddress = request.StreetAddress.Trim();
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;
        address.IsActive = true;

        await _uow.SaveChangesAsync(ct);
        return ServiceResult<StoreAddressResponse>.Success(_mapper.Map<StoreAddressResponse>(address), "Cập nhật địa chỉ cửa hàng thành công.");
    }

    public async Task<IServiceResult<List<StaffAssignmentResponse>>> GetStaffAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<List<StaffAssignmentResponse>>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy cửa hàng.");
        if (!IsOwnerOrAdmin(store, actorUserId, isAdmin))
            return ServiceResult<List<StaffAssignmentResponse>>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền xem nhân viên cửa hàng này.");

        var staff = await _uow.Stores.GetStaffAsync(id, ct);
        return ServiceResult<List<StaffAssignmentResponse>>.Success(_mapper.Map<List<StaffAssignmentResponse>>(staff));
    }

    public async Task<IServiceResult<StaffAssignmentResponse>> AssignStaffAsync(Guid id, Guid actorUserId, bool isAdmin, AssignStaffRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy cửa hàng.");
        if (!IsOwnerOrAdmin(store, actorUserId, isAdmin))
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền phân công nhân viên cho cửa hàng này.");
        if (!await _uow.Users.AnyAsync(u => u.Id == request.StaffId, ct))
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.BadRequest, "Nhân viên (user) không tồn tại.");
        if (await _uow.Stores.GetActiveAssignmentAsync(id, request.StaffId, ct) is not null)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.Conflict, "Nhân viên đã được phân công cho cửa hàng này.");

        var assignment = new GardenStaffAssignment
        {
            GardenStoreId = id,
            StaffId = request.StaffId,
            AssignedBy = actorUserId,
            IsActive = true,
            AssignedAt = DateTime.UtcNow,
        };
        await _uow.Stores.AddAssignmentAsync(assignment, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<StaffAssignmentResponse>.Success(
            _mapper.Map<StaffAssignmentResponse>(assignment), "Phân công nhân viên thành công.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> UnassignStaffAsync(Guid id, Guid assignmentId, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy cửa hàng.");
        if (!IsOwnerOrAdmin(store, actorUserId, isAdmin))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền gỡ phân công nhân viên cửa hàng này.");

        var assignment = await _uow.Stores.GetAssignmentByIdAsync(assignmentId, id, ct);
        if (assignment is null || !assignment.IsActive)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy phân công đang hiệu lực.");

        assignment.IsActive = false;
        assignment.UnassignedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã gỡ phân công nhân viên.");
    }

    private static bool IsOwnerOrAdmin(GardenStore store, Guid userId, bool isAdmin)
        => isAdmin || store.OwnerUserId == userId;
}
