using System.Diagnostics.CodeAnalysis;
using System.Text;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Infrastructure.ExternalServices.Payment;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Infrastructure.Authentication;
using FengDeskAI.Infrastructure.Common;
using FengDeskAI.Infrastructure.ExternalServices.Mail;
using FengDeskAI.Infrastructure.ExternalServices.Shipping;
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

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IWorkspaceProfileRepository, WorkspaceProfileRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IShippingRepository, ShippingRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
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

        // Payment (PayOS) + Shipping provider (mock — thay impl thật khi có credential)
        services.AddSettings<PayOsSettings>(configuration);
        services.AddHttpClient<IPaymentGateway, PayOsPaymentGateway>();
        services.AddScoped<IShippingProvider, MockShopeeShippingProvider>();

        services.AddScoped<IDataSeeder, GeographySeeder>();
        services.AddScoped<IDataSeeder, CatalogDemoSeeder>();

        return services;
    }
}
