using System.Security.Claims;
using System.Threading.RateLimiting;
using FengDeskAI.Application;
using FengDeskAI.Application.Features.Geography.Services;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Infrastructure;
using FengDeskAI.Infrastructure.Common;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using FengDeskAI.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common.Filters;
using FengDeskAI.WebAPI.Hubs;
using FengDeskAI.WebAPI.Services;
using FengDeskAI.WebAPI.Workers;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Railway (và nhiều PaaS) inject biến PORT động → bind Kestrel vào cổng đó nếu có.
// Local/Docker không set PORT → giữ nguyên ASPNETCORE_URLS (cổng 8080).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers(options =>
{
    options.Filters.Add<UnauthorizedExceptionFilter>();
})
.AddJsonOptions(options =>
{
    // Enum nhận/trả dưới dạng tên (vd "Moc", "Office") — vẫn chấp nhận số khi deserialize.
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FengDeskAI API",
        Version = "v1",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Mấy ông cháu paste token thôi nhá"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        }
    });
});

// CORS đọc từ section "Cors:AllowedOrigins" trong appsettings.
// Rỗng → mở cho mọi origin (tiện dev). Có origin → khoá đúng danh sách + cho credentials
// (cần khi FE gửi cookie/SignalR auth; AllowAnyOrigin KHÔNG đi kèm AllowCredentials được).
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        if (corsOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    });
});

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<FengDeskAI.Application.Interfaces.External.IChatRealtimeNotifier, FengDeskAI.WebAPI.Hubs.ChatRealtimeNotifier>();
builder.Services.AddSingleton<FengDeskAI.Application.Interfaces.External.IAiActivityNotifier, FengDeskAI.WebAPI.Hubs.AiActivityNotifier>();

// Bot AI nền (Phase 3): hàng đợi singleton + worker xử lý.
builder.Services.AddSingleton<FengDeskAI.WebAPI.Workers.AiBotQueue>();
builder.Services.AddSingleton<FengDeskAI.Application.Interfaces.External.IAiBotQueue>(
    sp => sp.GetRequiredService<FengDeskAI.WebAPI.Workers.AiBotQueue>());
builder.Services.AddHostedService<FengDeskAI.WebAPI.Workers.AiBotWorker>();

// AI intake workspace nền: LLM chậm (kèm ảnh ~80s) → chạy async, push kết quả realtime + cache fallback.
builder.Services.AddSingleton<FengDeskAI.WebAPI.Workers.WorkspaceIntakeQueue>();
builder.Services.AddSingleton<FengDeskAI.Application.Interfaces.External.IWorkspaceIntakeQueue>(
    sp => sp.GetRequiredService<FengDeskAI.WebAPI.Workers.WorkspaceIntakeQueue>());
builder.Services.AddSingleton<FengDeskAI.Application.Interfaces.External.IWorkspaceIntakeNotifier, FengDeskAI.WebAPI.Hubs.WorkspaceIntakeNotifier>();
builder.Services.AddHostedService<FengDeskAI.WebAPI.Workers.WorkspaceIntakeWorker>();

// camelCase + enum-as-string cho payload SignalR để KHỚP với FE (mặc định SignalR giữ nguyên tên
// PascalCase → FE đọc m.chatboxId/m.content ra undefined, tin realtime bị rớt).
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddApplication();

// Worker chuyển đơn online quá hạn thanh toán sang Expired
builder.Services.AddSettings<OrderExpirationOptions>(builder.Configuration);
builder.Services.AddHostedService<OrderExpirationWorker>();

// Worker poll job sinh model 3D (Meshy) đang xử lý → hoàn tất / đánh dấu lỗi.
builder.Services.AddHostedService<Model3DPollingWorker>();

builder.Services.AddAuthorization(options =>
{
    // Thứ tự quyền: Customer < Staff < Manager < Admin. "...OrAbove" gồm role đó + mọi role cao hơn.
    // (Staff là tuyến hỗ trợ trực tiếp cho Customer/garden owner; Manager cao hơn Staff.)
    options.AddPolicy(AuthorizationPolicies.AdminOnly,
        p => p.RequireRole(Roles.Admin));
    options.AddPolicy(AuthorizationPolicies.StaffOrAbove,
        p => p.RequireRole(Roles.Staff, Roles.Manager, Roles.Admin));
    options.AddPolicy(AuthorizationPolicies.ManagerOrAbove,
        p => p.RequireRole(Roles.Manager, Roles.Admin));
    options.AddPolicy(AuthorizationPolicies.CustomerOnly,
        p => p.RequireRole(Roles.Customer));
    // Người bán tự vận hành shop (marketplace). Admin luôn được phép.
    options.AddPolicy(AuthorizationPolicies.GardenOwnerOrAbove,
        p => p.RequireRole(Roles.GardenOwner, Roles.Admin));
});

// Chống spam gọi LLM ở endpoint workspace AI intake: 10 req/phút, partition theo userId
// (JWT NameIdentifier — endpoint đã [Authorize] nên luôn có claim khi middleware chạy tới đây).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("workspace-intake", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        }));
});

var app = builder.Build();

// Chế độ "chỉ seeding": `dotnet run --project src/FengDeskAI.WebAPI -- seed`
// → apply migrations + chạy seeder rồi thoát, KHÔNG bật web server.
if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    await app.Services.RunSeedersAsync();
    return;
}

// Đồng bộ dữ liệu hành chính VN (open-api.vn) + mã GHN: `dotnet run --project src/FengDeskAI.WebAPI -- sync-geo`
// → migrate + Bước A (nạp tỉnh/quận/phường) + Bước B (điền mã GHN) rồi thoát. Xem Documents/GHN_INTEGRATION.md §10.
if (args.Contains("sync-geo", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    var geo = sp.GetRequiredService<IGeoSyncService>();
    await geo.ImportGovernmentDataAsync();
    await geo.SyncGhnCodesAsync();
    return;
}

app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    // Giữ token sau khi reload Swagger UI
    opt.EnablePersistAuthorization();
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
