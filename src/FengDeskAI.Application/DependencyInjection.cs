using FengDeskAI.Application.Common.Sanitization;
using FengDeskAI.Application.Features.Catalog.Mappings;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Features.Chat.Mappings;
using FengDeskAI.Application.Features.Chat.Services;
using FengDeskAI.Application.Features.Geography.Mappings;
using FengDeskAI.Application.Features.Geography.Services;
using FengDeskAI.Application.Features.Announcement.Mappings;
using FengDeskAI.Application.Features.Announcement.Services;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Application.Features.Returns.Mappings;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.Application.Features.Sales.Mappings;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Shipping.Mappings;
using FengDeskAI.Application.Features.Shipping.Services;
using FengDeskAI.Application.Features.Storage.Services;
using FengDeskAI.Application.Features.Vendor.Mappings;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Application.Features.Identity.Mappings;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.Application.Features.Workspace.Mappings;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.Application.Features.CustomerCare;
using FengDeskAI.Application.Features.CustomerCare.Mappings;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.Application.Features.CustomerCare.Tools;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using FengDeskAI.Application.Features.CustomerCare.Engine;

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
            cfg.AddProfile<ReturnMappingProfile>();
            cfg.AddProfile<ShippingMappingProfile>();
            cfg.AddProfile<ReviewMappingProfile>();
            cfg.AddProfile<NotificationMappingProfile>();
            cfg.AddProfile<ChatMappingProfile>();
        });

        // Bộ lọc text AI dùng chung (đáp án cuối + mọi kênh stream). Stateless → Singleton.
        services.AddSingleton<ISensitiveTermSource, AiToolNameTermSource>();
        services.AddSingleton<IAiTextSanitizer, AiTextSanitizer>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistrationFlowService, RegistrationFlowService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IWorkspaceProfileService, WorkspaceProfileService>();
        services.AddScoped<IWorkspaceTypeService, WorkspaceTypeService>();
        services.AddScoped<IWorkspaceIntakeService, WorkspaceIntakeService>();
        services.AddScoped<IWorkspaceElementInputClassifierService, WorkspaceElementInputClassifierService>();

        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IUserAddressService, UserAddressService>();

        services.AddScoped<IStoreService, StoreService>();

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();

        // Product & 3D model
        services.AddScoped<ISkuGenerator, SkuGenerator>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductVectorService, ProductVectorService>();
        services.AddScoped<IProductModel3DService, ProductModel3DService>();
        services.AddScoped<IModel3DRequestService, Model3DRequestService>();
        services.AddScoped<ITaxonomyService, TaxonomyService>();

        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderCancellationService, OrderCancellationService>();
        services.AddScoped<IOrderExpirationService, OrderExpirationService>();

        // return/refund/liability
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IVendorLiabilityService, VendorLiabilityService>();

        // Shipping & Delivery
        services.AddScoped<IShippingService, ShippingService>();
        services.AddSingleton<IShippingFeeCalculator, ShippingFeeCalculator>();
        services.AddScoped<IDeliveryFeeEstimator, DeliveryFeeEstimator>();
        services.AddScoped<IStoreShopProvisioner, StoreShopProvisioner>();

        services.AddScoped<IPaymentService, PaymentService>();

        services.AddScoped<IUploadService, UploadService>();

        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IChatService, ChatService>();

        services.AddScoped<IReviewService, ReviewService>();

        services.AddSingleton<IRecommendationScorer, RecommendationScorer>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IScoringConfigAdminService, ScoringConfigAdminService>();
        services.AddScoped<IOccupationService, OccupationService>();

        services.AddScoped<IAiTool, SearchProductsTool>();
        services.AddScoped<IAiTool, GetProductTool>();
        services.AddScoped<IAiTool, RecommendProductsTool>();

        services.AddScoped<IAiTool, RecommendPersonalItemsTool>();
        services.AddScoped<IAiTool, ListMyWorkspacesTool>();
        services.AddScoped<IAiTool, GetMyProfileTool>();
        services.AddScoped<IAiTool, ListMyOrdersTool>();
        services.AddScoped<IAiTool, GetPaymentStatusTool>();
        services.AddScoped<IAiTool, GetChatPartnerInfoTool>();
        services.AddScoped<IAiTool, ListMyAddressesTool>();
        services.AddScoped<IAiTool, GetShopInfoTool>();

        services.AddScoped<IAiTool, ComputeDestinyChartTool>();
        // Tool có tác dụng phụ (tạo đơn) — chỉ enable ở phòng riêng, xem AiChatService.PrivateRoomOnlyTools.
        services.AddScoped<IAiTool, PrepareOrderTool>();
        services.AddScoped<IAiTool, ConfirmOrderTool>();

        services.AddScoped<IAiChatService, AiChatService>();

        return services;
    }
}
