using FengDeskAI.Application.Features.Catalog.Mappings;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Features.Chat.Mappings;
using FengDeskAI.Application.Features.Chat.Services;
using FengDeskAI.Application.Features.Geography.Mappings;
using FengDeskAI.Application.Features.Geography.Services;
using FengDeskAI.Application.Features.Announcement.Mappings;
using FengDeskAI.Application.Features.Announcement.Services;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Application.Features.Sales.Mappings;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Shipping.Mappings;
using FengDeskAI.Application.Features.Shipping.Services;
using FengDeskAI.Application.Features.Vendor.Mappings;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Application.Features.Identity.Mappings;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.Application.Features.Workspace.Mappings;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.Application.Features.CustomerCare.Mappings;
using FengDeskAI.Application.Features.CustomerCare.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace FengDeskAI.Application;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<IdentityProfile>();
            cfg.AddProfile<WorkspaceProfileMappingProfile>();
            cfg.AddProfile<GeographyMappingProfile>();
            cfg.AddProfile<VendorMappingProfile>();
            cfg.AddProfile<CatalogMappingProfile>();
            cfg.AddProfile<SalesMappingProfile>();
            cfg.AddProfile<ShippingMappingProfile>();
            cfg.AddProfile<ReviewMappingProfile>();
            cfg.AddProfile<NotificationMappingProfile>();
            cfg.AddProfile<ChatMappingProfile>();
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistrationFlowService, RegistrationFlowService>();
        services.AddScoped<IWorkspaceProfileService, WorkspaceProfileService>();

        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IUserAddressService, UserAddressService>();

        services.AddScoped<IStoreService, StoreService>();

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IProductService, ProductService>();

        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderCancellationService, OrderCancellationService>();
        services.AddScoped<IOrderExpirationService, OrderExpirationService>();

        services.AddScoped<IShippingService, ShippingService>();

        services.AddScoped<IPaymentService, PaymentService>();

        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IChatService, ChatService>();

        services.AddScoped<IReviewService, ReviewService>();

        return services;
    }
}
