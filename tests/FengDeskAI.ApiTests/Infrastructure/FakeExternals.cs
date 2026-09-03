using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Security;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Thay SMTP thật. Ngoài việc chặn gửi mail, nó GIỮ LẠI email để test đọc được OTP — nhờ vậy các
/// luồng đăng ký / quên mật khẩu / đổi email chạy trọn vẹn được trong test tự động.
/// Singleton: OTP gửi ở request này phải đọc được ở request sau.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private static readonly Regex OtpPattern = new(@"\b(\d{6})\b", RegexOptions.Compiled);

    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>OTP trong email GẦN NHẤT gửi tới địa chỉ này. Null nếu chưa có mail nào.</summary>
    public string? LatestOtpFor(string email)
    {
        var message = _sent
            .Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

        if (message is null) return null;

        var match = OtpPattern.Match(message.HtmlBody);
        return match.Success ? match.Groups[1].Value : null;
    }

    public void Clear() => _sent.Clear();
}

/// <summary>Thay PayOS. Trả link giả lập, không gọi mạng.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public string Provider => "FakeGateway";

    public Task<PaymentLinkResult> CreatePaymentLinkAsync(PaymentLinkRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentLinkResult(
            CheckoutUrl: $"https://fake-gateway.test/checkout/{request.OrderCode}",
            PaymentLinkId: $"fake-link-{request.OrderCode}",
            OrderCode: request.OrderCode,
            QrCode: "fake-qr"));

    public Task CancelPaymentLinkAsync(long orderCode, string? reason, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct = default)
        => Task.FromResult(new RefundResult(true, $"fake-refund-{request.IdempotencyKey}", "00", "Fake refund succeeded"));

    public PaymentWebhookResult VerifyWebhook(string rawJsonBody)
        => new(false, 0, 0, null, "99", "Fake gateway không xác thực webhook trong test");
}

/// <summary>Thay Supabase Storage. Không ghi đĩa, chỉ trả URL giả lập ổn định.</summary>
public sealed class FakeFileStorage : IFileStorage
{
    private const string BaseUrl = "https://fake-storage.test/bucket";

    public Task<StoredFile> UploadAsync(string objectPath, Stream content, string contentType, CancellationToken ct = default)
        => Task.FromResult(new StoredFile(objectPath, GetPublicUrl(objectPath)));

    public Task DeleteByUrlAsync(string publicUrl, CancellationToken ct = default) => Task.CompletedTask;

    public string GetPublicUrl(string objectPath) => $"{BaseUrl}/{objectPath.TrimStart('/')}";
}

/// <summary>
/// Thay LLM relay. Trả đáp án cố định, KHÔNG bao giờ gọi tool — vòng lặp tool-calling của
/// <c>AiChatService</c> vì thế kết thúc ngay, test không treo chờ model.
/// </summary>
public sealed class FakeAiChatClient : IAiChatClient
{
    public const string CannedReply = "Đây là phản hồi giả lập dùng cho test.";

    public Task<AiChatCompletion> CompleteAsync(
        string model,
        IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools = null,
        AiCompletionOptions? options = null,
        IProgress<AiStreamChunk>? onDelta = null,
        CancellationToken ct = default)
    {
        // Chế độ JSON (workspace intake) cần parse được, trả object rỗng thay vì câu chữ.
        var content = options?.JsonMode == true ? "{}" : CannedReply;
        onDelta?.Report(new AiStreamChunk(AiStreamKind.Content, content));
        return Task.FromResult(new AiChatCompletion(content, model, ToolCalls: null));
    }
}

/// <summary>Thay Meshy. Job "xong ngay" để luồng model 3D chạy hết mà không cần poll thật.</summary>
public sealed class FakeModel3DGenerator : IModel3DGenerator
{
    public int InsufficientCreditsBackoffMinutes => 1;

    public Task<string> StartImageTo3DAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default)
        => Task.FromResult($"fake-task-{Guid.NewGuid():N}");

    public Task<Model3DTaskResult> GetTaskAsync(string taskId, CancellationToken ct = default)
        => Task.FromResult(new Model3DTaskResult(
            Model3DGenerationState.Succeeded, 100, "https://fake-meshy.test/model.glb", "https://fake-meshy.test/thumb.png", null));

    public Task<Stream> DownloadAsync(string url, CancellationToken ct = default)
        => Task.FromResult<Stream>(new MemoryStream("glb-gia-lap"u8.ToArray()));
}

/// <summary>Thay Google. Token dạng "fake-google:{email}" là hợp lệ, còn lại đều null (401).</summary>
public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public const string TokenPrefix = "fake-google:";

    public Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || !idToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return Task.FromResult<GoogleUserInfo?>(null);

        var email = idToken[TokenPrefix.Length..].Trim().ToLowerInvariant();
        if (email.Length == 0) return Task.FromResult<GoogleUserInfo?>(null);

        return Task.FromResult<GoogleUserInfo?>(
            new GoogleUserInfo($"fake-google-id-{email}", email, EmailVerified: true, FullName: "Fake Google User", PictureUrl: null));
    }
}

/// <summary>Thay STT. Trả câu cố định, không gọi model.</summary>
public sealed class FakeSpeechToTextService : ISpeechToTextService
{
    public Task<string> TranscribeAsync(Stream audio, string fileName, string? language = null, CancellationToken ct = default)
        => Task.FromResult("Văn bản giả lập từ giọng nói.");
}
