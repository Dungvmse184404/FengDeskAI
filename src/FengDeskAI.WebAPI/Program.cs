using FengDeskAI.Application;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Infrastructure;
using FengDeskAI.Infrastructure.Common;
using FengDeskAI.Infrastructure.Persistence.Seeding;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common.Filters;
using FengDeskAI.WebAPI.Hubs;
using FengDeskAI.WebAPI.Services;
using FengDeskAI.WebAPI.Workers;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<FengDeskAI.Application.Interfaces.External.IChatRealtimeNotifier, FengDeskAI.WebAPI.Hubs.ChatRealtimeNotifier>();

// Bot AI nền (Phase 3): hàng đợi singleton + worker xử lý.
builder.Services.AddSingleton<FengDeskAI.WebAPI.Workers.AiBotQueue>();
builder.Services.AddSingleton<FengDeskAI.Application.Interfaces.External.IAiBotQueue>(
    sp => sp.GetRequiredService<FengDeskAI.WebAPI.Workers.AiBotQueue>());
builder.Services.AddHostedService<FengDeskAI.WebAPI.Workers.AiBotWorker>();

builder.Services.AddSignalR();

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddApplication();

// Worker chuyển đơn online quá hạn thanh toán sang Expired
builder.Services.AddSettings<OrderExpirationOptions>(builder.Configuration);
builder.Services.AddHostedService<OrderExpirationWorker>();

builder.Services.AddAuthorization(options =>
{
    // Thứ tự quyền: Customer < Manager < Staff < Admin. "...OrAbove" gồm role đó + mọi role cao hơn.
    options.AddPolicy(AuthorizationPolicies.AdminOnly,
        p => p.RequireRole(Roles.Admin));
    options.AddPolicy(AuthorizationPolicies.StaffOrAbove,
        p => p.RequireRole(Roles.Staff, Roles.Admin));
    options.AddPolicy(AuthorizationPolicies.ManagerOrAbove,
        p => p.RequireRole(Roles.Manager, Roles.Staff, Roles.Admin));
    options.AddPolicy(AuthorizationPolicies.CustomerOnly,
        p => p.RequireRole(Roles.Customer));
});

var app = builder.Build();

// Chế độ "chỉ seeding": `dotnet run --project src/FengDeskAI.WebAPI -- seed`
// → apply migrations + chạy seeder rồi thoát, KHÔNG bật web server.
if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    await app.Services.RunSeedersAsync();
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

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
