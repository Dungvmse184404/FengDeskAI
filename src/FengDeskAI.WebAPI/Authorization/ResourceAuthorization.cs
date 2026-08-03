using System.Security.Claims;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace FengDeskAI.WebAPI.Authorization;

public enum ResourceOperation
{
    ManageStore,
    ManageProduct,
    AssignDelivery,
    ViewDelivery,
    UpdateDelivery,
}

public sealed record ResourceAccessRequirement(ResourceOperation Operation) : IAuthorizationRequirement;
public sealed record ResourceReference(Guid Id);

public sealed class ResourceAccessHandler : AuthorizationHandler<ResourceAccessRequirement, ResourceReference>
{
    private readonly IUnitOfWork _uow;
    public ResourceAccessHandler(IUnitOfWork uow) => _uow = uow;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceAccessRequirement requirement,
        ResourceReference resource)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return;
        if (context.User.IsInRole(Roles.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        var allowed = requirement.Operation switch
        {
            ResourceOperation.ManageStore =>
                await _uow.Stores.IsOwnerAsync(resource.Id, userId),
            ResourceOperation.ManageProduct =>
                await CanManageProductAsync(resource.Id, userId),
            ResourceOperation.AssignDelivery =>
                await CanAssignDeliveryAsync(resource.Id, userId),
            ResourceOperation.ViewDelivery =>
                await CanAccessDeliveryAsync(resource.Id, userId, requireAssignment: true, allowCustomer: true),
            ResourceOperation.UpdateDelivery =>
                await CanAccessDeliveryAsync(resource.Id, userId, requireAssignment: true, allowCustomer: false),
            _ => false,
        };
        if (allowed) context.Succeed(requirement);
    }

    private async Task<bool> CanManageProductAsync(Guid productId, Guid userId)
    {
        var product = await _uow.Products.GetByIdAsync(productId);
        return product is not null && await _uow.Stores.CanManageAsync(product.GardenStoreId, userId);
    }

    private async Task<bool> CanAccessDeliveryAsync(
        Guid deliveryId, Guid userId, bool requireAssignment, bool allowCustomer)
    {
        var delivery = await _uow.Orders.GetDeliveryWithOrderAsync(deliveryId);
        if (delivery is null) return false;
        if (allowCustomer && delivery.Order.CustomerId == userId) return true;
        if (await _uow.Stores.IsOwnerAsync(delivery.GardenStoreId, userId)) return true;
        if (!await _uow.Stores.IsAcceptedStaffAsync(delivery.GardenStoreId, userId)) return false;
        return !requireAssignment || delivery.AssignedStaffId == userId;
    }

    private async Task<bool> CanAssignDeliveryAsync(Guid deliveryId, Guid userId)
    {
        var delivery = await _uow.Orders.GetDeliveryWithOrderAsync(deliveryId);
        return delivery is not null
            && await _uow.Stores.IsOwnerAsync(delivery.GardenStoreId, userId);
    }
}
