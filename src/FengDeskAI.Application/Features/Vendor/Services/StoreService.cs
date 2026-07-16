using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Announcement.DTOs;
using FengDeskAI.Application.Features.Announcement.Services;
using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Domain.Enums.Notification;
using FengDeskAI.Domain.Enums.Vendor;

namespace FengDeskAI.Application.Features.Vendor.Services;

public class StoreService : IStoreService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly INotificationService _notifications;

    public StoreService(IUnitOfWork uow, IMapper mapper, INotificationService notifications)
    {
        _uow = uow;
        _mapper = mapper;
        _notifications = notifications;
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
        return ServiceResult<List<StaffAssignmentResponse>>.Success(staff);
    }

    public async Task<IServiceResult<StaffAssignmentResponse>> AssignStaffAsync(Guid id, Guid actorUserId, bool isAdmin, AssignStaffRequest request, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Staff.AssignForbidden);

        // FE chính dùng /api/users/search → staffId. Vẫn chấp nhận email fallback cho client cũ.
        var email = request.StaffEmail?.Trim();
        var hasIdentifier = request.StaffId.HasValue || !string.IsNullOrEmpty(email);
        if (!hasIdentifier)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Staff.IdentifierRequired);

        var staff = request.StaffId.HasValue
            ? await _uow.Users.GetByIdAsync(request.StaffId.Value, ct)
            : await _uow.Users.GetByEmailAsync(email!, ct);
        if (staff is null)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Staff.StaffNotFound);

        // Không mời chính owner làm staff của store đó.
        if (await _uow.Stores.IsOwnerAsync(id, staff.Id, ct))
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Staff.CannotInviteOwner);

        var existing = await _uow.Stores.GetActiveAssignmentAsync(id, staff.Id, ct);
        if (existing is not null)
        {
            var key = existing.Status == InvitationStatus.Pending
                ? ApiStatusMessages.Staff.AlreadyInvited
                : ApiStatusMessages.Staff.AlreadyAssigned;
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.Conflict, key);
        }

        var assignment = new GardenStaffAssignment
        {
            GardenStoreId = id,
            StaffId = staff.Id,
            InvitedBy = actorUserId,
            Status = InvitationStatus.Pending,
            InvitedAt = DateTime.UtcNow,
        };
        await _uow.Stores.AddAssignmentAsync(assignment, ct);
        var actor = await _uow.Users.GetByIdAsync(actorUserId, ct);
        await _uow.SaveChangesAsync(ct);

        // Notification cho người được mời. Cùng transaction là lý tưởng nhưng NotificationService tự SaveChanges — chấp nhận as-is.
        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            UserId = staff.Id,
            Type = NotificationType.StaffInvited,
            Title = "Lời mời làm nhân viên",
            Message = $"Bạn được mời làm nhân viên của cửa hàng \"{store.Name}\".",
            ReferenceId = assignment.Id,
            ReferenceType = ReferenceType.StaffInvitation,
        }, ct);

        var response = new StaffAssignmentResponse
        {
            Id = assignment.Id,
            GardenStoreId = assignment.GardenStoreId,
            StaffId = staff.Id,
            StaffName = staff.FullName,
            StaffEmail = staff.Email,
            StaffPhone = staff.Phone,
            InvitedBy = actorUserId,
            InvitedByName = actor?.FullName,
            Status = assignment.Status,
            InvitedAt = assignment.InvitedAt,
            RespondedAt = assignment.RespondedAt,
            UnassignedAt = assignment.UnassignedAt,
        };
        return ServiceResult<StaffAssignmentResponse>.Success(response, ApiStatusMessages.Staff.Invited, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> UnassignStaffAsync(Guid id, Guid assignmentId, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        var store = await _uow.Stores.GetByIdAsync(id, ct);
        if (store is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        if (!await IsOwnerOrAdminAsync(store.Id, actorUserId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Staff.UnassignForbidden);

        var assignment = await _uow.Stores.GetAssignmentByIdAsync(assignmentId, id, ct);
        if (assignment is null
            || (assignment.Status != InvitationStatus.Pending && assignment.Status != InvitationStatus.Accepted))
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Staff.AssignmentNotFound);

        assignment.Status = InvitationStatus.Revoked;
        assignment.UnassignedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Staff.Unassigned);
    }

    public async Task<IServiceResult<List<InvitationResponse>>> GetMyInvitationsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await _uow.Stores.GetPendingInvitationsForUserAsync(userId, ct);
        return ServiceResult<List<InvitationResponse>>.Success(list);
    }

    public async Task<IServiceResult<StaffAssignmentResponse>> AcceptInvitationAsync(Guid assignmentId, Guid userId, CancellationToken ct = default)
    {
        var assignment = await _uow.Stores.GetAssignmentByIdForUserAsync(assignmentId, userId, ct);
        if (assignment is null)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Staff.InvitationNotFound);
        if (assignment.Status != InvitationStatus.Pending)
            return ServiceResult<StaffAssignmentResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Staff.InvitationNotPending);

        assignment.Status = InvitationStatus.Accepted;
        assignment.RespondedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        // Notify owner đã accept (tuỳ chọn — nhẹ nhàng, không phải lỗi nếu fail).
        var store = await _uow.Stores.GetByIdAsync(assignment.GardenStoreId, ct);
        var staff = await _uow.Users.GetByIdAsync(userId, ct);
        if (store is not null && staff is not null)
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = assignment.InvitedBy,
                Type = NotificationType.StaffInvitationAccepted,
                Title = "Lời mời được chấp nhận",
                Message = $"{staff.FullName} đã đồng ý làm nhân viên của \"{store.Name}\".",
                ReferenceId = assignment.Id,
                ReferenceType = ReferenceType.StaffInvitation,
            }, ct);
        }

        return await BuildAssignmentResponseAsync(assignment, ApiStatusMessages.Staff.InvitationAccepted, ct);
    }

    public async Task<IServiceResult> RejectInvitationAsync(Guid assignmentId, Guid userId, CancellationToken ct = default)
    {
        var assignment = await _uow.Stores.GetAssignmentByIdForUserAsync(assignmentId, userId, ct);
        if (assignment is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Staff.InvitationNotFound);
        if (assignment.Status != InvitationStatus.Pending)
            return ServiceResult.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Staff.InvitationNotPending);

        assignment.Status = InvitationStatus.Rejected;
        assignment.RespondedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        var store = await _uow.Stores.GetByIdAsync(assignment.GardenStoreId, ct);
        var staff = await _uow.Users.GetByIdAsync(userId, ct);
        if (store is not null && staff is not null)
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = assignment.InvitedBy,
                Type = NotificationType.StaffInvitationRejected,
                Title = "Lời mời bị từ chối",
                Message = $"{staff.FullName} đã từ chối lời mời làm nhân viên của \"{store.Name}\".",
                ReferenceId = assignment.Id,
                ReferenceType = ReferenceType.StaffInvitation,
            }, ct);
        }

        return ServiceResult.Success(ApiStatusMessages.Staff.InvitationRejected);
    }

    private async Task<IServiceResult<StaffAssignmentResponse>> BuildAssignmentResponseAsync(
        GardenStaffAssignment assignment, string message, CancellationToken ct)
    {
        var staff = await _uow.Users.GetByIdAsync(assignment.StaffId, ct);
        var inviter = await _uow.Users.GetByIdAsync(assignment.InvitedBy, ct);
        var res = new StaffAssignmentResponse
        {
            Id = assignment.Id,
            GardenStoreId = assignment.GardenStoreId,
            StaffId = assignment.StaffId,
            StaffName = staff?.FullName ?? string.Empty,
            StaffEmail = staff?.Email ?? string.Empty,
            StaffPhone = staff?.Phone,
            InvitedBy = assignment.InvitedBy,
            InvitedByName = inviter?.FullName,
            Status = assignment.Status,
            InvitedAt = assignment.InvitedAt,
            RespondedAt = assignment.RespondedAt,
            UnassignedAt = assignment.UnassignedAt,
        };
        return ServiceResult<StaffAssignmentResponse>.Success(res, message);
    }

    public async Task<IServiceResult<List<StoreResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        // Gồm cả store mà user là nhân viên đã Accepted — để garden staff vào được khu người bán.
        var stores = _mapper.Map<List<StoreResponse>>(await _uow.Stores.GetForUserAsync(userId, ct));
        // IsOwner để FE phân biệt "Chủ cửa hàng" vs "Nhân viên" + ẩn nút owner-only.
        foreach (var s in stores)
            s.IsOwner = s.Owners.Any(o => o.OwnerUserId == userId);
        return ServiceResult<List<StoreResponse>>.Success(stores);
    }

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
    // ===== Membership + thống kê =====

    public async Task<IServiceResult<StoreMembershipResponse>> GetMyMembershipAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await _uow.Stores.ExistsAsync(id, ct))
            return ServiceResult<StoreMembershipResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);

        var owner = await _uow.Stores.GetOwnerAsync(id, userId, ct);
        // Staff chỉ tính khi KHÔNG phải owner — owner luôn thắng.
        var isStaff = owner is null && await _uow.Stores.IsAcceptedStaffAsync(id, userId, ct);

        return ServiceResult<StoreMembershipResponse>.Success(new StoreMembershipResponse
        {
            IsPrimaryOwner = owner?.IsPrimary == true,
            IsOwner = owner is not null,
            IsStaff = isStaff,
            IsAdmin = isAdmin,
            CanManage = owner is not null || isStaff || isAdmin,
        });
    }

    public async Task<IServiceResult<StoreStatisticsResponse>> GetStatisticsAsync(Guid id, Guid actorUserId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await _uow.Stores.ExistsAsync(id, ct))
            return ServiceResult<StoreStatisticsResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);
        // Chỉ owner (chính/đồng sở hữu) hoặc admin — garden staff KHÔNG xem được thống kê.
        if (!await IsOwnerOrAdminAsync(id, actorUserId, isAdmin, ct))
            return ServiceResult<StoreStatisticsResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Store.StatisticsForbidden);

        return ServiceResult<StoreStatisticsResponse>.Success(await _uow.Stores.GetStatisticsAsync(id, ct));
    }

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
