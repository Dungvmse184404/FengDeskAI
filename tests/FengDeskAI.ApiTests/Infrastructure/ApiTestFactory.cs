using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Dựng API in-process cho test: ép config an toàn, gỡ background worker, thay mọi dịch vụ ngoài
/// bằng fake. Xem docs/adr/api-integration-testing.md.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Lấy từ biến môi trường hoặc file cục bộ (xem <see cref="TestConnectionString"/>) — KHÔNG có
    /// giá trị mặc định trong code. Không tìm thấy thì <see cref="TestDatabaseGuard"/> ném lỗi kèm
    /// hướng dẫn, thay vì âm thầm chạy vào DB nào đó.
    /// </summary>
    public string? ConnectionString { get; }

    /// <summary>Secret webhook nhà vận chuyển của phiên test — test nào cần ký webhook thì đọc từ đây.</summary>
    public string ShippingWebhookSecret { get; } = TestSecrets.NewSecret(16);

    /// <summary>Email đã gửi trong quá trình test — dùng để lấy OTP.</summary>
    public FakeEmailSender Emails { get; } = new();

    public ApiTestFactory()
    {
        ConnectionString = TestConnectionString.Resolve();

        // Chặn TRƯỚC khi bất cứ thứ gì chạm tới DB, kể cả migrate.
        TestDatabaseGuard.EnsureSafe(ConnectionString);

        ApplyTestConfiguration();
    }

    /// <summary>
    /// Ép config bằng BIẾN MÔI TRƯỜNG, không dùng <c>ConfigureAppConfiguration</c>.
    ///
    /// Lý do (bẫy của minimal hosting, mất công tìm nên ghi lại): callback
    /// <c>ConfigureAppConfiguration</c> của WebApplicationFactory chỉ chạy khi host được build, tức
    /// SAU khi thân <c>Program.cs</c> đã chạy. Mà <c>Program.cs</c> gọi
    /// <c>AddInfrastructure(builder.Configuration, …)</c>, trong đó <c>JwtSettings</c> được đọc NGAY
    /// để dựng <c>IssuerSigningKey</c>. Hệ quả: app verify token bằng secret trong appsettings.json,
    /// nhưng <c>IOptions&lt;JwtSettings&gt;</c> (resolve lúc chạy) lại ký bằng secret của test →
    /// mọi request có token đều 401, rất khó đoán ra.
    ///
    /// <c>WebApplication.CreateBuilder</c> nạp biến môi trường ngay lúc khởi tạo, nên đặt ở đây thì
    /// cả hai phía đều thấy cùng một giá trị.
    /// </summary>
    private void ApplyTestConfiguration()
    {
        var settings = new Dictionary<string, string>
        {
            // Bắt buộc đặt lại: connection string có thể đến từ file cục bộ chứ không phải biến
            // môi trường, mà host chỉ đọc được qua biến môi trường (xem ghi chú ở dưới).
            [TestConnectionString.EnvironmentVariable] = ConnectionString!,

            // Sinh ngẫu nhiên mỗi lần chạy — app tự ký và tự verify trong cùng tiến trình nên không
            // cần giá trị cố định, và không có secret nào nằm trong mã nguồn.
            ["JwtSettings__SecretKey"] = TestSecrets.NewSecret(),
            ["JwtSettings__Issuer"] = "FengDesk_Test",
            ["JwtSettings__Audience"] = "FengDesk_Test",
            ["JwtSettings__AccessTokenMinutes"] = "60",
            ["JwtSettings__RefreshTokenDays"] = "7",

            // Worker nạp 63 tỉnh từ open-api.vn lúc khởi động — test không được phụ thuộc mạng ngoài.
            ["Seeding__AutoGeoSync"] = "false",

            // Hai dịch vụ đã có sẵn công tắc mock trong code production.
            ["Shipping__Provider"] = "Mock",
            ["AiRecommendationSettings__UseMock"] = "true",

            ["CarrierShopSync__IsActive"] = "false",
            ["OrderExpiration__IsActive"] = "false",
            ["AiOrderDraft__IsActive"] = "false",
            ["Speech__Enabled"] = "false",

            // Rút cooldown xuống mức nhỏ nhất để test gửi OTP liên tiếp không phải chờ 60 giây.
            // KHÔNG đặt 0: OtpService truyền thẳng giá trị này vào AbsoluteExpirationRelativeToNow,
            // mà cache yêu cầu giá trị dương → 0 làm endpoint gửi OTP ném 500 (xem ghi chú lỗi
            // đã báo cho nhóm — cùng rủi ro nếu ai đó đặt 0 trong appsettings thật).
            ["Otp__ResendCooldownSeconds"] = "1",

            ["ShippingWebhook__Secret"] = ShippingWebhookSecret,

            // FakeGoogleTokenValidator không đọc giá trị này, nhưng để trống thì validator thật
            // (nếu ai đó gỡ fake) sẽ ném lúc khởi động.
            ["GoogleAuth__ClientId"] = TestSecrets.NewSecret(8),
        };

        foreach (var (key, value) in settings)
            Environment.SetEnvironmentVariable(key, value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" chứ không phải "Development": tránh nạp appsettings.Development.json của máy dev
        // (gitignore, mỗi máy một khác) — test phải chạy giống nhau ở mọi nơi.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Worker nền gọi mạng ngoài và sửa dữ liệu GIỮA LÚC test chạy → gỡ sạch.
            services.RemoveAll<IHostedService>();

            ReplaceExternalServices(services);

            // Log EF Core mỗi câu SQL làm output test không đọc nổi.
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        });
    }

    /// <summary>
    /// Thay 7 dịch vụ ngoài chưa có công tắc mock. Tất cả đều nằm sau interface nên không phải
    /// sửa code production. Thêm tích hợp ngoài mới thì bổ sung vào đây.
    /// </summary>
    private void ReplaceExternalServices(IServiceCollection services)
    {
        services.RemoveAll<IEmailSender>();
        services.AddSingleton<IEmailSender>(Emails);

        services.RemoveAll<IPaymentGateway>();
        services.AddScoped<IPaymentGateway, FakePaymentGateway>();

        services.RemoveAll<IFileStorage>();
        services.AddScoped<IFileStorage, FakeFileStorage>();

        services.RemoveAll<IAiChatClient>();
        services.AddScoped<IAiChatClient, FakeAiChatClient>();

        services.RemoveAll<IModel3DGenerator>();
        services.AddScoped<IModel3DGenerator, FakeModel3DGenerator>();

        services.RemoveAll<IGoogleTokenValidator>();
        services.AddScoped<IGoogleTokenValidator, FakeGoogleTokenValidator>();

        services.RemoveAll<ISpeechToTextService>();
        services.AddScoped<ISpeechToTextService, FakeSpeechToTextService>();
    }
}
