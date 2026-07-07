using System.Diagnostics.CodeAnalysis;
using System.Text;
using FengDeskAI.Application.Features.CustomerCare;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Infrastructure.ExternalServices.Ai;
using FengDeskAI.Infrastructure.ExternalServices.Payment;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Infrastructure.Authentication;
using FengDeskAI.Infrastructure.Common;
using FengDeskAI.Infrastructure.ExternalServices.Mail;
using FengDeskAI.Infrastructure.ExternalServices.Model3D;
using FengDeskAI.Infrastructure.ExternalServices.Shipping;
using FengDeskAI.Infrastructure.ExternalServices.Storage;
using FengDeskAI.Infrastructure.Persistence;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using FengDeskAI.Infrastructure.Persistence.Repositories;
using FengDeskAI.Infrastructure.Persistence.Seeding;
using FengDeskAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FengDeskAI.Infrastructure;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        services.AddSettings<JwtSettings>(configuration);
        var jwtSettings = configuration.GetSettings<JwtSettings>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.FromMinutes(5),
            };

            // Log chi tiết khi token fail — giúp debug 401
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    // SignalR qua WebSocket không set được header Authorization → token nằm ở query "access_token".
                    var accessToken = ctx.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrEmpty(accessToken) &&
                        ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        ctx.Token = accessToken;
                        return Task.CompletedTask;
                    }

                    var raw = ctx.Request.Headers.Authorization.ToString();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        // Nếu client gửi "Bearer Bearer xxx" hoặc "bearer xxx" → normalize
                        var trimmed = raw.Trim();
                        if (trimmed.StartsWith("Bearer Bearer ", StringComparison.OrdinalIgnoreCase))
                            ctx.Token = trimmed.Substring("Bearer Bearer ".Length).Trim();
                        else if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            ctx.Token = trimmed.Substring("Bearer ".Length).Trim();
                        else
                            ctx.Token = trimmed; // raw token without prefix — accept it
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtBearer");
                    logger.LogWarning(
                        "JWT authentication failed: {ExceptionType} — {Message}",
                        ctx.Exception.GetType().Name, ctx.Exception.Message);
                    return Task.CompletedTask;
                },
                OnChallenge = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtBearer");
                    logger.LogInformation(
                        "JWT challenge issued. Error: {Error} | Description: {Description}",
                        ctx.Error ?? "(none)", ctx.ErrorDescription ?? "(none)");
                    return Task.CompletedTask;
                },
                OnTokenValidated = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtBearer");
                    logger.LogDebug(
                        "JWT validated OK for {Name}", ctx.Principal?.Identity?.Name ?? "(unknown)");
                    return Task.CompletedTask;
                },
            };
        });

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

            if (isDevelopment)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Repo generic mở (dùng cho bảng tra cứu Style/Vibe — không cần repo riêng).
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IWorkspaceProfileRepository, WorkspaceProfileRepository>();
        services.AddScoped<IWorkspaceTypeRepository, WorkspaceTypeRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IReturnRepository, ReturnRepository>();
        services.AddScoped<IShippingRepository, ShippingRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IChatboxRepository, ChatboxRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IScoringConfigRepository, ScoringConfigRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddDistributedMemoryCache();

        services.AddSettings<MailSettings>(configuration);
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();

        services.AddSettings<OtpOptions>(configuration);
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IRegistrationTokenService, RegistrationTokenService>();

        services.AddSettings<ShippingWebhookSettings>(configuration);

        // Payment (PayOS)
        services.AddSettings<PayOsSettings>(configuration);
        services.AddHttpClient<IPaymentGateway, PayOsPaymentGateway>();

        // Shipping provider: chọn theo cờ Shipping:Provider = "Ghn" | "Ahamove" | "Mock"
        // (default Mock — chạy local không cần credential).
        services.AddSettings<AhamoveSettings>(configuration);
        services.AddSettings<GhnSettings>(configuration);
        var shippingProvider = configuration["Shipping:Provider"];
        if (string.Equals(shippingProvider, "Ghn", StringComparison.OrdinalIgnoreCase))
        {
            // GHN: token là API key dài hạn → gắn header Token mặc định; ShopId gắn theo từng request.
            services.AddHttpClient<IShippingProvider, GhnShippingProvider>((sp, c) =>
            {
                var cfg = sp.GetRequiredService<IOptions<GhnSettings>>().Value;
                c.BaseAddress = new Uri(cfg.BaseUrl);
                c.DefaultRequestHeaders.Add("Token", cfg.Token);
            });
        }
        else if (string.Equals(shippingProvider, "Ahamove", StringComparison.OrdinalIgnoreCase))
        {
            // Token cache phải singleton (giữ token giữa các request) → dùng IHttpClientFactory
            // qua named client thay vì typed client (vốn transient).
            services.AddHttpClient(AhamoveTokenProvider.HttpClientName, (sp, c) =>
            {
                var cfg = sp.GetRequiredService<IOptions<AhamoveSettings>>().Value;
                c.BaseAddress = new Uri(cfg.BaseUrl);
            });
            services.AddSingleton<IAhamoveTokenProvider, AhamoveTokenProvider>();
            services.AddHttpClient<IShippingProvider, AhamoveShippingProvider>((sp, c) =>
            {
                var cfg = sp.GetRequiredService<IOptions<AhamoveSettings>>().Value;
                c.BaseAddress = new Uri(cfg.BaseUrl);
            });
        }
        else
        {
            services.AddScoped<IShippingProvider, MockShopeeShippingProvider>();
        }

        // Đồng bộ dữ liệu hành chính (open-api.vn) + mã GHN — chạy qua lệnh `sync-geo`.
        services.AddHttpClient(Persistence.Seeding.GeoSyncService.OpenApiClient,
            c => c.BaseAddress = new Uri("https://provinces.open-api.vn"));
        services.AddHttpClient(Persistence.Seeding.GeoSyncService.GhnMasterDataClient, (sp, c) =>
        {
            var cfg = sp.GetRequiredService<IOptions<GhnSettings>>().Value;
            c.BaseAddress = new Uri(cfg.BaseUrl);
            if (!string.IsNullOrEmpty(cfg.Token)) c.DefaultRequestHeaders.Add("Token", cfg.Token);
        });
        services.AddScoped<Application.Features.Geography.Services.IGeoSyncService, Persistence.Seeding.GeoSyncService>();

        // AI recommendation — mock theo CONTRACT.md (mặc định) hoặc HTTP client gọi Python.
        // Toggle bằng AiRecommendationSettings:UseMock.
        services.AddSettings<AiRecommendationSettings>(configuration);
        var aiSettings = configuration.GetSettings<AiRecommendationSettings>();
        if (aiSettings.UseMock)
            services.AddScoped<IAiRecommendationClient, MockAiRecommendationClient>();
        else
            services.AddHttpClient<IAiRecommendationClient, HttpAiRecommendationClient>();

        // AI chat hội thoại — gọi LLM kiểu Ollama (/api/chat). Lịch sử lưu trong chatboxes/chat_messages (DB).
        services.AddSettings<AiChatOptions>(configuration);
        services.AddHttpClient<IAiChatClient, OllamaChatClient>();

        // Object storage (Supabase) cho ảnh sản phẩm/người dùng + encoder ảnh → base64 để feed AI.
        services.AddSettings<SupabaseStorageOptions>(configuration);
        services.AddHttpClient<IFileStorage, SupabaseFileStorage>();
        services.AddHttpClient<IImageEncoder, ImageEncoder>();

        // Sinh model 3D từ ảnh (Meshy AI). Mock mặc định để khỏi tốn credit; toggle MeshySettings:UseMock.
        services.AddSettings<MeshySettings>(configuration);
        var meshySettings = configuration.GetSettings<MeshySettings>();
        if (meshySettings.UseMock)
            services.AddHttpClient<IModel3DGenerator, MockModel3DGenerator>();
        else
            services.AddHttpClient<IModel3DGenerator, MeshyModel3DGenerator>();

        services.AddScoped<IDataSeeder, StyleVibeSeeder>();
        services.AddScoped<IDataSeeder, ScoringParamSeeder>();
        services.AddScoped<IDataSeeder, ElementInputMapSeeder>();
        services.AddScoped<IDataSeeder, WorkPurposeModifierSeeder>();
        services.AddScoped<IDataSeeder, WorkspaceTypeSeeder>();
        services.AddScoped<IDataSeeder, WorkspaceTypeElementSeeder>();
        services.AddScoped<IDataSeeder, FengShuiRuleSeeder>();
        services.AddScoped<IDataSeeder, GeographySeeder>();
        services.AddScoped<IDataSeeder, CatalogDemoSeeder>();
        services.AddScoped<IDataSeeder, ProductFengShuiDemoSeeder>();

        return services;
    }
}
