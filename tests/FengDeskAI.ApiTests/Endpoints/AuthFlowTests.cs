using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 1 — các luồng xác thực NHIỀU BƯỚC chạy trọn vẹn: đăng ký 3 bước, quên mật khẩu 3 bước,
/// đổi email 4 bước, đăng nhập Google.
///
/// Chạy được nhờ <see cref="FakeEmailSender"/> giữ lại email đã gửi để lấy OTP — thứ không thể
/// tự động hóa nếu dùng SMTP thật. Đây là phần bù cho <see cref="FunctionalCaseTests"/>, vốn chỉ
/// xử lý ca một-request.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AuthFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== UC-01: đăng ký 3 bước =====================

    [Fact(DisplayName = "AUTH-REG-01 [Normal] Complete three-step registration with a new email")]
    public async Task Register_ThreeStepFlow_CreatesUsableAccount()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();

        var initiate = await client.PostAsJsonAsync("/api/Auth/register/initiate", new { email });
        Assert.Equal(HttpStatusCode.OK, initiate.StatusCode);

        var otp = _fixture.Factory.Emails.LatestOtpFor(email);
        Assert.False(string.IsNullOrEmpty(otp), $"Không lấy được OTP đăng ký gửi tới {email}.");
        _output.WriteLine($"OTP đăng ký nhận được: {otp}");

        var verify = await client.PostAsJsonAsync("/api/Auth/register/verify", new { email, otp });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var registrationToken = await ReadDataStringAsync(verify, "registrationToken");

        var finalize = await client.PostAsJsonAsync("/api/Auth/register/finalize", new
        {
            registrationToken,
            password = _fixture.Password,
            fullName = "Người dùng kiểm thử",
            gender = "Male",
        });

        Assert.Equal(HttpStatusCode.Created, finalize.StatusCode);
        var accessToken = await ReadDataStringAsync(finalize, "accessToken");
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // Tài khoản vừa tạo phải đăng nhập được ngay bằng chính mật khẩu đó.
        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = _fixture.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact(DisplayName = "AUTH-REG-02 [Abnormal] Register with an email that already has an account")]
    public async Task Register_ExistingEmail_BlockedAtInitiate()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous)
            .PostAsJsonAsync("/api/Auth/register/initiate", new { email = ApiTestFixture.EmailFor(TestRole.Customer) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "AUTH-REG-03 [Abnormal] registrationToken can only be used once")]
    public async Task RegistrationToken_IsSingleUse()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();

        await client.PostAsJsonAsync("/api/Auth/register/initiate", new { email });
        var otp = _fixture.Factory.Emails.LatestOtpFor(email);
        var verify = await client.PostAsJsonAsync("/api/Auth/register/verify", new { email, otp });
        var token = await ReadDataStringAsync(verify, "registrationToken");

        object Payload() => new { registrationToken = token, password = _fixture.Password, fullName = "Lần đầu" };

        var first = await client.PostAsJsonAsync("/api/Auth/register/finalize", Payload());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/Auth/register/finalize", Payload());
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact(DisplayName = "AUTH-REG-04 [Abnormal] Too many wrong OTP attempts locks the code")]
    public async Task RegisterOtp_TooManyWrongAttempts_IsBlocked()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();
        await client.PostAsJsonAsync("/api/Auth/register/initiate", new { email });

        // MaxVerifyAttempts mặc định là 5 → lần thứ 6 phải bị chặn vì quá số lần.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
            last = await client.PostAsJsonAsync("/api/Auth/register/verify", new { email, otp = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, last!.StatusCode);
        var message = await ReadMessageAsync(last);
        Assert.Contains("quá nhiều lần", message, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== Quên mật khẩu 3 bước =====================

    [Fact(DisplayName = "AUTH-FP-09 [Normal] Full forgot-password flow revokes every existing session")]
    public async Task ForgotPassword_FullFlow_RevokesExistingSessions()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);

        // Dùng tài khoản riêng để không phá mật khẩu của các user mẫu dùng chung.
        var email = ApiTestFixture.NewEmail();
        await RegisterAsync(client, email, _fixture.Password);

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = _fixture.Password });
        var oldRefreshToken = await ReadDataStringAsync(login, "refreshToken");

        var initiate = await client.PostAsJsonAsync("/api/Auth/forgot-password/initiate", new { email });
        Assert.Equal(HttpStatusCode.OK, initiate.StatusCode);

        var otp = _fixture.Factory.Emails.LatestOtpFor(email);
        Assert.False(string.IsNullOrEmpty(otp), $"Không lấy được OTP đặt lại mật khẩu gửi tới {email}.");

        var verify = await client.PostAsJsonAsync("/api/Auth/forgot-password/verify", new { email, otp });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var resetToken = await ReadDataStringAsync(verify, "resetPasswordToken");

        var newPassword = TestSecrets.NewPassword();
        var reset = await client.PostAsJsonAsync("/api/Auth/forgot-password/reset",
            new { resetPasswordToken = resetToken, newPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // Mật khẩu cũ hết hiệu lực, mật khẩu mới dùng được.
        var loginOld = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = _fixture.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, loginOld.StatusCode);

        var loginNew = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, loginNew.StatusCode);

        // Refresh token cấp trước khi đổi mật khẩu phải bị thu hồi.
        var refresh = await client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = oldRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact(DisplayName = "AUTH-FP-10 [Abnormal] resetPasswordToken can only be used once")]
    public async Task ResetPasswordToken_IsSingleUse()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();
        await RegisterAsync(client, email, _fixture.Password);

        await client.PostAsJsonAsync("/api/Auth/forgot-password/initiate", new { email });
        var otp = _fixture.Factory.Emails.LatestOtpFor(email);
        var verify = await client.PostAsJsonAsync("/api/Auth/forgot-password/verify", new { email, otp });
        var resetToken = await ReadDataStringAsync(verify, "resetPasswordToken");

        var first = await client.PostAsJsonAsync("/api/Auth/forgot-password/reset",
            new { resetPasswordToken = resetToken, newPassword = TestSecrets.NewPassword() });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/Auth/forgot-password/reset",
            new { resetPasswordToken = resetToken, newPassword = TestSecrets.NewPassword() });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact(DisplayName = "AUTH-FP-11 [Abnormal] New password identical to the current one is rejected")]
    public async Task ResetPassword_SameAsCurrentPassword_IsRejected()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();
        await RegisterAsync(client, email, _fixture.Password);

        await client.PostAsJsonAsync("/api/Auth/forgot-password/initiate", new { email });
        var otp = _fixture.Factory.Emails.LatestOtpFor(email);
        var verify = await client.PostAsJsonAsync("/api/Auth/forgot-password/verify", new { email, otp });
        var resetToken = await ReadDataStringAsync(verify, "resetPasswordToken");

        var reset = await client.PostAsJsonAsync("/api/Auth/forgot-password/reset",
            new { resetPasswordToken = resetToken, newPassword = _fixture.Password });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Contains("trùng", await ReadMessageAsync(reset), StringComparison.OrdinalIgnoreCase);
    }

    // ===================== Đổi email 4 bước =====================

    [Fact(DisplayName = "AUTH-EMAIL-01 [Normal] Complete four-step email change")]
    public async Task ChangeEmail_FourStepFlow_SwitchesLoginEmail()
    {
        var anonymous = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();
        await RegisterAsync(anonymous, email, _fixture.Password);

        var client = await LoggedInClientAsync(email, _fixture.Password);

        var b1 = await client.PostAsync("/api/Auth/me/email/initiate", null);
        Assert.Equal(HttpStatusCode.OK, b1.StatusCode);

        var otpCurrent = _fixture.Factory.Emails.LatestOtpFor(email);
        var b2 = await client.PostAsJsonAsync("/api/Auth/me/email/verify-current", new { otp = otpCurrent });
        Assert.Equal(HttpStatusCode.OK, b2.StatusCode);
        var changeToken = await ReadDataStringAsync(b2, "changeEmailToken");

        var newEmail = ApiTestFixture.NewEmail();
        var b3 = await client.PostAsJsonAsync("/api/Auth/me/email/request-new",
            new { changeEmailToken = changeToken, newEmail });
        Assert.Equal(HttpStatusCode.OK, b3.StatusCode);

        var otpNew = _fixture.Factory.Emails.LatestOtpFor(newEmail);
        Assert.False(string.IsNullOrEmpty(otpNew), $"Không lấy được OTP gửi tới email mới {newEmail}.");

        var b4 = await client.PostAsJsonAsync("/api/Auth/me/email/confirm",
            new { changeEmailToken = changeToken, otp = otpNew });
        Assert.Equal(HttpStatusCode.OK, b4.StatusCode);

        // Đổi email xong phải đăng nhập được bằng email MỚI, và email cũ hết dùng được.
        var loginNew = await anonymous.PostAsJsonAsync("/api/Auth/login", new { email = newEmail, password = _fixture.Password });
        Assert.Equal(HttpStatusCode.OK, loginNew.StatusCode);

        var loginOld = await anonymous.PostAsJsonAsync("/api/Auth/login", new { email, password = _fixture.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, loginOld.StatusCode);
    }

    [Fact(DisplayName = "AUTH-EMAIL-02 [Abnormal] Skipping current-email verification is blocked")]
    public async Task ChangeEmail_SkippingCurrentEmailVerification_IsBlocked()
    {
        var client = _fixture.ClientFor(TestRole.Customer);

        var response = await client.PostAsJsonAsync("/api/Auth/me/email/request-new",
            new { changeEmailToken = "token-bia-dat", newEmail = ApiTestFixture.NewEmail() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ===================== Đăng nhập Google =====================

    [Fact(DisplayName = "AUTH-GOOGLE-01 [Normal] First Google sign-in creates a new account")]
    public async Task GoogleLogin_FirstTime_CreatesAccount()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();

        var response = await client.PostAsJsonAsync("/api/Auth/google",
            new { idToken = FakeGoogleTokenValidator.TokenPrefix + email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadDataStringAsync(response, "accessToken")));
    }

    [Fact(DisplayName = "AUTH-GOOGLE-02 [Abnormal] Google-only account cannot sign in with a password")]
    public async Task GoogleOnlyAccount_CannotLoginWithPassword()
    {
        var client = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();

        await client.PostAsJsonAsync("/api/Auth/google", new { idToken = FakeGoogleTokenValidator.TokenPrefix + email });

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email, password = _fixture.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact(DisplayName = "AUTH-GOOGLE-03 [Abnormal] Invalid Google ID token is rejected")]
    public async Task GoogleLogin_InvalidIdToken_IsRejected()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous)
            .PostAsJsonAsync("/api/Auth/google", new { idToken = "token-bia-dat" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ===================== Tiện ích =====================

    /// <summary>Chạy trọn 3 bước đăng ký để có một tài khoản dùng riêng cho ca test.</summary>
    private async Task RegisterAsync(HttpClient client, string email, string password)
    {
        var initiate = await client.PostAsJsonAsync("/api/Auth/register/initiate", new { email });
        Assert.Equal(HttpStatusCode.OK, initiate.StatusCode);

        var otp = _fixture.Factory.Emails.LatestOtpFor(email);
        var verify = await client.PostAsJsonAsync("/api/Auth/register/verify", new { email, otp });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var finalize = await client.PostAsJsonAsync("/api/Auth/register/finalize", new
        {
            registrationToken = await ReadDataStringAsync(verify, "registrationToken"),
            password,
            fullName = "Tài khoản kiểm thử",
        });
        Assert.Equal(HttpStatusCode.Created, finalize.StatusCode);
    }

    private async Task<HttpClient> LoggedInClientAsync(string email, string password)
    {
        var login = await _fixture.ClientFor(TestRole.Anonymous)
            .PostAsJsonAsync("/api/Auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await ReadDataStringAsync(login, "accessToken"));
        return client;
    }

    private static async Task<string> ReadDataStringAsync(HttpResponseMessage response, string property)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty(property).GetString()
               ?? throw new InvalidOperationException($"Response không có data.{property}: {body}");
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
    }
}
