using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums;

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
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        return ServiceResult<StoreResponse>.Success(_mapper.Map<StoreResponse>(store));
    }

    public async Task<IServiceResult<StoreResponse>> CreateAsync(Guid actorUserId, CreateStoreRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Store.NameRequired);
        if (string.IsNullOrWhiteSpace(request.Hotline))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Store.HotlineRequired);

        var entity = _mapper.Map<GardenStore>(request);
        entity.Name = request.Name.Trim();
        entity.IsActive = true;

        // Self-service: người tạo trở thành owner chính (primary). EF gắn GardenStoreId qua quan hệ.
        entity.Owners.Add(new GardenStoreOwner
        {
            OwnerUserId = actorUserId,
            IsPrimary = true,
            AssignedAt = DateTime.UtcNow,
        });

        await _uow.Stores.AddAsync(entity, ct);
        await GrantGardenOwnerRoleAsync(actorUserId, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<StoreResponse>.Success(
            _mapper.Map<StoreResponse>(entity), ApiStatusMessages.Store.Created, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<StoreResponse>> UpdateAsync(Guid id, Guid actorUserId, bool isAdmin, UpdateStoreRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.EditForbidden);
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<StoreResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Store.NameRequired);

        store.Name = request.Name.Trim();
        store.Description = request.Description;
        store.Hotline = request.Hotline;
        store.OpeningHours = request.OpeningHours;
        store.IsActive = request.IsActive;
        _uow.Stores.Update(store);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<StoreResponse>.Success(_mapper.Map<StoreResponse>(store), ApiStatusMessages.Store.Updated);
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null || store.IsDeleted)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.DeleteForbidden);

        // Soft-delete địa chỉ kèm theo (nếu có) rồi soft-delete store.
        var address = await _uow.Stores.GetAddressAsync(id, ct);
        if (address is not null) address.IsDeleted = true;
        _uow.Stores.Remove(store); // interceptor SaveChanges chuyển Deleted → soft-delete
        await _uow.SaveChangesAsync(ct);

        return ServiceResult.Success(ApiStatusMessages.Store.Deleted);
    }

    public async Task<IServiceResult> HardDeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _uow.Stores.ExistsAsync(id, ct))
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);

        try
        {
            await _uow.ExecuteInTransactionAsync(async c =>
            {
                await _uow.Stores.HardDeleteAsync(id, c);
                return true;
            }, ct);
        }
        catch (Exception)
        {
            // Thường do FK còn dữ liệu liên quan (sản phẩm, đơn hàng...).
            return ServiceResult.Failure(ApiStatusCodes.Conflict,
                ApiStatusMessages.Store.HardDeleteConflict);
        }

        return ServiceResult.Success(ApiStatusMessages.Store.HardDeleted);
    }

    public async Task<IServiceResult<StoreAddressResponse>> AddAddressAsync(Guid id, Guid actorUserId, bool isAdmin, CreateStoreAddressRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null || store.IsDeleted)
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.EditForbidden);
        if (string.IsNullOrWhiteSpace(request.StreetAddress))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.StoreAddress.StreetRequired);
        if (!await _uow.Locations.WardExistsAsync(request.WardId, ct))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.StoreAddress.WardInvalid);

        // StoreId là unique (1-1). Nếu đã có bản ghi đã soft-delete thì hồi sinh thay vì insert mới (tránh đụng unique).
        var address = await _uow.Stores.GetAddressIncludingDeletedAsync(id, ct);
        if (address is not null && !address.IsDeleted)
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.StoreAddress.AlreadyExists);

        if (address is null)
        {
            address = new StoreAddress { StoreId = id };
            await _uow.Stores.AddAddressAsync(address, ct);
        }
        else
        {
            address.IsDeleted = false; // hồi sinh bản ghi đã soft-delete
        }
        address.WardId = request.WardId;
        address.StreetAddress = request.StreetAddress.Trim();
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;
        address.IsActive = true;

        await _uow.SaveChangesAsync(ct);
        return ServiceResult<StoreAddressResponse>.Success(
            _mapper.Map<StoreAddressResponse>(address), ApiStatusMessages.StoreAddress.Created, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<StoreAddressResponse>> UpdateAddressAsync(Guid id, Guid actorUserId, bool isAdmin, UpdateStoreAddressRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null || store.IsDeleted)
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.EditForbidden);
        if (string.IsNullOrWhiteSpace(request.StreetAddress))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.StoreAddress.StreetRequired);
        if (!await _uow.Locations.WardExistsAsync(request.WardId, ct))
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.StoreAddress.WardInvalid);

        var address = await _uow.Stores.GetAddressAsync(id, ct);
        if (address is null)
            return ServiceResult<StoreAddressResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.StoreAddress.StoreHasNoAddress);

        address.WardId = request.WardId;
        address.StreetAddress = request.StreetAddress.Trim();
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;

        await _uow.SaveChangesAsync(ct);
        return ServiceResult<StoreAddressResponse>.Success(_mapper.Map<StoreAddressResponse>(address), ApiStatusMessages.StoreAddress.Updated);
    }

    public async Task<IServiceResult> DeleteAddressAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null || store.IsDeleted)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.EditForbidden);

        var address = await _uow.Stores.GetAddressAsync(id, ct);
        if (address is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.StoreAddress.NotFoundForStore);

        address.IsDeleted = true; // tracked → soft-delete
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.StoreAddress.Deleted);
    }

    public async Task<IServiceResult> HardDeleteAddressAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _uow.Stores.AddressExistsAsync(id, ct))
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.StoreAddress.NotFound);

        await _uow.Stores.HardDeleteAddressAsync(id, ct);
        return ServiceResult.Success(ApiStatusMessages.StoreAddress.HardDeleted);
    }

    public async Task<IServiceResult<List<StaffAssignmentResponse>>> GetStaffAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<List<StaffAssignmentResponse>>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult<List<StaffAssignmentResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Staff.ViewForbidden);

        var staff = await _uow.Stores.GetStaffAsync(id, ct);
        return ServiceResult<List<StaffAssignmentResponse>>.Success(_mapper.Map<List<StaffAssignmentResponse>>(staff));
    }

    public async Task<IServiceResult<StaffAssignmentResponse>> AssignStaffAsync(Guid id, Guid actorUserId, bool isAdmin, AssignStaffRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Staff.AssignForbidden);
        if (!await _uow.Users.AnyAsync(u => u.Id == request.StaffId, ct))
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Staff.StaffNotFound);
        if (await _uow.Stores.GetActiveAssignmentAsync(id, request.StaffId, ct) is not null)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Staff.AlreadyAssigned);

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
            _mapper.Map<StaffAssignmentResponse>(assignment), ApiStatusMessages.Staff.Assigned, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> UnassignStaffAsync(Guid id, Guid assignmentId, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Staff.UnassignForbidden);

        var assignment = await _uow.Stores.GetAssignmentByIdAsync(assignmentId, id, ct);
        if (assignment is null || !assignment.IsActive)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Staff.AssignmentNotFound);

        assignment.IsActive = false;
        assignment.UnassignedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Staff.Unassigned);
    }

    public async Task<IServiceResult<List<StoreResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default)
        => ServiceResult<List<StoreResponse>>.Success(
            _mapper.Map<List<StoreResponse>>(await _uow.Stores.GetByOwnerAsync(userId, ct)));

    // ===== Owner (đồng sở hữu — marketplace) =====

    public async Task<IServiceResult<List<StoreOwnerResponse>>> GetOwnersAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _uow.Stores.ExistsAsync(id, ct))
            return ServiceResult<List<StoreOwnerResponse>>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);

        var owners = await _uow.Stores.GetOwnersAsync(id, ct);
        return ServiceResult<List<StoreOwnerResponse>>.Success(_mapper.Map<List<StoreOwnerResponse>>(owners));
    }

    public async Task<IServiceResult<StoreOwnerResponse>> AddOwnerAsync(Guid id, Guid actorUserId, bool isAdmin, AddOwnerRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null || store.IsDeleted)
            return ServiceResult<StoreOwnerResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(id, actorUserId, isAdmin, ct))
            return ServiceResult<StoreOwnerResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.ManageOwnersForbidden);
        if (!await _uow.Users.AnyAsync(u => u.Id == request.OwnerUserId, ct))
            return ServiceResult<StoreOwnerResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Store.OwnerNotFound);
        if (await _uow.Stores.IsOwnerAsync(id, request.OwnerUserId, ct))
            return ServiceResult<StoreOwnerResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Store.AlreadyOwner);

        var owner = new GardenStoreOwner
        {
            GardenStoreId = id,
            OwnerUserId = request.OwnerUserId,
            IsPrimary = false,
            AssignedAt = DateTime.UtcNow,
        };
        await _uow.Stores.AddOwnerAsync(owner, ct);
        await GrantGardenOwnerRoleAsync(request.OwnerUserId, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<StoreOwnerResponse>.Success(
            _mapper.Map<StoreOwnerResponse>(owner), ApiStatusMessages.Store.OwnerAdded, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> RemoveOwnerAsync(Guid id, Guid ownerUserId, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null || store.IsDeleted)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(id, actorUserId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.ManageOwnersForbidden);

        var owner = await _uow.Stores.GetOwnerAsync(id, ownerUserId, ct);
        if (owner is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.OwnerNotInStore);
        // Store luôn cần owner chính — không cho gỡ owner primary (muốn đổi thì cần luồng chuyển nhượng riêng).
        if (owner.IsPrimary)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Store.CannotRemoveLastPrimary);

        owner.IsDeleted = true; // tracked → soft-delete
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Store.OwnerRemoved);
    }

    /// <summary>Owner của store hoặc Admin (KHÔNG gồm nhân viên — staff không được sửa store).</summary>
    private async Task<bool> IsOwnerOrAdminAsync(Guid storeId, Guid userId, bool isAdmin, CancellationToken ct)
        => isAdmin || await _uow.Stores.IsOwnerAsync(storeId, userId, ct);

    /// <summary>Cấp flag <see cref="UserRole.GardenOwner"/> cho user nếu chưa có (không SaveChanges).</summary>
    private async Task GrantGardenOwnerRoleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is not null && !user.Role.Has(UserRole.GardenOwner))
        {
            user.Role = user.Role.Add(UserRole.GardenOwner);
            _uow.Users.Update(user);
        }
    }
}
