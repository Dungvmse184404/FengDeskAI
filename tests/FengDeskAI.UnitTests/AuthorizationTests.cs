using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Infrastructure.Authentication;
using FengDeskAI.WebAPI.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FengDeskAI.UnitTests;

public sealed class AuthorizationTests
{
    [Fact]
    public void AccessToken_ContainsEveryRoleAndTokenVersion()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            FullName = "Owner",
            Role = UserRole.Customer | UserRole.GardenOwner,
            TokenVersion = 7,
        };
        var service = new TokenService(Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 64),
            Issuer = "tests",
            Audience = "tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        }));

        var (token, _) = service.GenerateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("7", jwt.Claims.Single(x => x.Type == "token_version").Value);
        Assert.Contains(jwt.Claims, x => x.Type == ClaimTypes.Role && x.Value == Roles.Customer);
        Assert.Contains(jwt.Claims, x => x.Type == ClaimTypes.Role && x.Value == Roles.GardenOwner);
    }

    [Fact]
    public async Task GardenStaff_CanManageProductOfAcceptedStore()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var uow = new Mock<IUnitOfWork>();
        var products = new Mock<IProductRepository>();
        var stores = new Mock<IStoreRepository>();
        uow.SetupGet(x => x.Products).Returns(products.Object);
        uow.SetupGet(x => x.Stores).Returns(stores.Object);
        products.Setup(x => x.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Id = productId, GardenStoreId = storeId });
        stores.Setup(x => x.CanManageAsync(storeId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new AuthorizationHandlerContext(
            new[] { new ResourceAccessRequirement(ResourceOperation.ManageProduct) },
            Principal(userId),
            new ResourceReference(productId));
        await new ResourceAccessHandler(uow.Object).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GardenStaff_CanUpdateOnlyAssignedDelivery(bool isAssigned)
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var uow = new Mock<IUnitOfWork>();
        var orders = new Mock<IOrderRepository>();
        var stores = new Mock<IStoreRepository>();
        uow.SetupGet(x => x.Orders).Returns(orders.Object);
        uow.SetupGet(x => x.Stores).Returns(stores.Object);
        orders.Setup(x => x.GetDeliveryWithOrderAsync(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Delivery
            {
                Id = deliveryId,
                GardenStoreId = storeId,
                AssignedStaffId = isAssigned ? userId : Guid.NewGuid(),
            });
        stores.Setup(x => x.IsOwnerAsync(storeId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        stores.Setup(x => x.IsAcceptedStaffAsync(storeId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new AuthorizationHandlerContext(
            new[] { new ResourceAccessRequirement(ResourceOperation.UpdateDelivery) },
            Principal(userId),
            new ResourceReference(deliveryId));
        await new ResourceAccessHandler(uow.Object).HandleAsync(context);

        Assert.Equal(isAssigned, context.HasSucceeded);
    }

    private static ClaimsPrincipal Principal(Guid userId)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, Roles.Customer),
        }, "test"));
}
