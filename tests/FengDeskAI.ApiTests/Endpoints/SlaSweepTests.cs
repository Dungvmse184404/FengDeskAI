using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Nợ của đợt 3 — SLA của luồng trả hàng: các lượt quét theo giờ mà <c>ReturnSlaWorker</c> chạy
/// định kỳ trong môi trường thật.
///
/// Vì sao KHÔNG gọi qua HTTP: SLA không có endpoint nào cả — toàn bộ nằm sau
/// <c>IReturnService</c> / <c>IRefundService</c> / <c>IVendorLiabilityService</c> và chỉ do worker
/// gọi. Mà <c>ApiTestFactory</c> gỡ sạch <c>IHostedService</c> (cố ý: worker sửa dữ liệu GIỮA LÚC
/// test chạy thì mọi ca test khác thành ngẫu nhiên). Nên ở đây ta dựng dữ liệu qua API thật, rồi gọi
/// thẳng phương thức quét qua scope DI thật — đúng thứ worker gọi, chỉ khác ở chỗ thời điểm do test
/// quyết định.
///
/// Cách "tua đồng hồ": các mốc hạn đều được tính <c>UtcNow + N</c> lúc tạo và không endpoint nào đổi
/// được, nên muốn chạm nhánh quá hạn thì phải kéo mốc về quá khứ. Chỗ nào setter còn public thì đi
/// qua EF, chỗ nào private set (<c>EvidenceDeadline</c>) thì phải UPDATE thẳng.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class SlaSweepTests
{
    private const string EvidenceImageUrl = "https://fake-storage.test/bucket/evidence/test.jpg";

    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SlaSweepTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Quá hạn bổ sung bằng chứng =====================

    [Fact(DisplayName = "SLA-01 [Normal] A ticket past its evidence deadline is auto-rejected by the sweep")]
    public async Task EvidenceSweep_PastDeadline_AutoRejectsTicket()
    {
        var ticketId = await TicketAwaitingEvidenceAsync();
        await BackdateEvidenceDeadlineAsync(ticketId, DateTime.UtcNow.AddHours(-1));

        var rejected = await RunAsync(sp => sp.GetRequiredService<IReturnService>().AutoRejectOverdueEvidenceAsync());
        _output.WriteLine($"Số ticket bị tự động từ chối: {rejected}");

        Assert.True(rejected >= 1);

        var detail = await Staff().GetAsync($"/api/returns/{ticketId}");
        var data = await ApiEnvelope.DataAsync(detail);
        Assert.Equal("Rejected", data.GetProperty("status").GetString());
        Assert.Contains("quá hạn", data.GetProperty("rejectedReason").GetString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SLA-02 [Normal] The auto-rejection is recorded in the ticket status log")]
    public async Task EvidenceSweep_PastDeadline_WritesStatusLog()
    {
        var ticketId = await TicketAwaitingEvidenceAsync();
        await BackdateEvidenceDeadlineAsync(ticketId, DateTime.UtcNow.AddHours(-1));
        await RunAsync(sp => sp.GetRequiredService<IReturnService>().AutoRejectOverdueEvidenceAsync());

        var detail = await Staff().GetAsync($"/api/returns/{ticketId}");
        var logs = (await ApiEnvelope.DataAsync(detail)).GetProperty("statusLogs");

        Assert.Contains(logs.EnumerateArray(), l =>
            l.GetProperty("fromStatus").GetString() == "NeedMoreEvidence"
            && l.GetProperty("toStatus").GetString() == "Rejected");
    }

    [Fact(DisplayName = "SLA-03 [Normal] The customer is notified when their ticket is auto-rejected")]
    public async Task EvidenceSweep_PastDeadline_NotifiesCustomer()
    {
        var ticketId = await TicketAwaitingEvidenceAsync();
        await BackdateEvidenceDeadlineAsync(ticketId, DateTime.UtcNow.AddHours(-1));
        await RunAsync(sp => sp.GetRequiredService<IReturnService>().AutoRejectOverdueEvidenceAsync());

        var notifications = await _fixture.ClientFor(TestRole.Customer)
            .GetAsync("/api/notifications?pageSize=100");
        var items = (await ApiEnvelope.DataAsync(notifications)).GetProperty("items");

        Assert.Contains(items.EnumerateArray(), n =>
            n.GetProperty("type").GetString() == "ReturnRejected"
            && n.GetProperty("referenceId").ValueKind != JsonValueKind.Null
            && n.GetProperty("referenceId").GetGuid() == ticketId);
    }

    [Fact(DisplayName = "SLA-04 [Boundary] A ticket still inside its evidence deadline is left alone")]
    public async Task EvidenceSweep_WithinDeadline_LeavesTicketAlone()
    {
        var ticketId = await TicketAwaitingEvidenceAsync();

        await RunAsync(sp => sp.GetRequiredService<IReturnService>().AutoRejectOverdueEvidenceAsync());

        var detail = await Staff().GetAsync($"/api/returns/{ticketId}");
        Assert.Equal("NeedMoreEvidence", (await ApiEnvelope.DataAsync(detail)).GetProperty("status").GetString());
    }

    [Fact(DisplayName = "SLA-05 [Normal] Resubmitting evidence before the deadline clears it")]
    public async Task ResubmitEvidence_BeforeDeadline_ReopensTicket()
    {
        var ticketId = await TicketAwaitingEvidenceAsync();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("anh-bang-chung-gia-lap"u8.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "files", "bang-chung.jpg");

        var response = await _fixture.ClientFor(TestRole.Customer)
            .PostAsync($"/api/returns/{ticketId}/resubmit-evidence", form);

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "bổ sung bằng chứng"));
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Requested", data.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("evidenceDeadline").ValueKind);
    }

    // ===================== Điều phối lệnh hoàn tiền =====================

    [Fact(DisplayName = "SLA-06 [Normal] The dispatch sweep moves a pending refund into processing")]
    public async Task RefundSweep_PendingRefund_MovesToProcessing()
    {
        var refundId = await PendingRefundAsync();

        var dispatched = await RunAsync(sp => sp.GetRequiredService<IRefundService>().ProcessPendingRefundsAsync());
        Assert.True(dispatched >= 1);

        var refund = await ReadRefundAsync(refundId);
        Assert.Equal("Processing", refund.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "SLA-07 [Normal] A refund stuck in processing past the timeout is marked failed")]
    public async Task RefundSweep_StaleProcessing_MarksFailed()
    {
        var refundId = await PendingRefundAsync();
        await RunAsync(sp => sp.GetRequiredService<IRefundService>().ProcessPendingRefundsAsync());
        await BackdateRefundProcessedAtAsync(refundId, DateTime.UtcNow.AddHours(-2));

        var failed = await RunAsync(sp => sp.GetRequiredService<IRefundService>().FailStaleProcessingRefundsAsync());
        Assert.True(failed >= 1);

        var refund = await ReadRefundAsync(refundId);
        Assert.Equal("Failed", refund.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "SLA-08 [Normal] The retry sweep puts a failed refund back into processing and counts the attempt")]
    public async Task RefundSweep_FailedRefund_IsRetried()
    {
        var refundId = await FailedRefundAsync();

        var retried = await RunAsync(sp => sp.GetRequiredService<IRefundService>().AutoProcessFailedRefundsAsync());
        Assert.True(retried >= 1);

        var refund = await ReadRefundAsync(refundId);
        Assert.Equal("Processing", refund.GetProperty("status").GetString());
        Assert.True(refund.GetProperty("retryCount").GetInt32() >= 1);
    }

    [Fact(DisplayName = "SLA-09 [Boundary] A refund that exhausted its retries escalates to manager review")]
    public async Task RefundSweep_AfterMaxRetries_EscalatesToManagerReview()
    {
        var refundId = await FailedRefundAsync();

        // Ba lần retry là trần (ReturnWorkflow.MaxRefundRetries); lần quét thứ tư phải đẩy lên
        // Manager thay vì gọi cổng thanh toán tiếp.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await RunAsync(sp => sp.GetRequiredService<IRefundService>().AutoProcessFailedRefundsAsync());
            await BackdateRefundProcessedAtAsync(refundId, DateTime.UtcNow.AddHours(-2));
            await RunAsync(sp => sp.GetRequiredService<IRefundService>().FailStaleProcessingRefundsAsync());
        }

        await RunAsync(sp => sp.GetRequiredService<IRefundService>().AutoProcessFailedRefundsAsync());

        var refund = await ReadRefundAsync(refundId);
        Assert.Equal("ManagerReview", refund.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "SLA-10 [Normal] A refund in manager review can be confirmed manually")]
    public async Task ManagerConfirm_AfterEscalation_CompletesRefund()
    {
        var refundId = await ManagerReviewRefundAsync();

        var response = await Manager().PostAsJsonAsync($"/api/refunds/{refundId}/manager-confirm", new
        {
            manualReason = "Đã chuyển khoản tay cho khách.",
            evidenceUrl = "https://fake-storage.test/bucket/evidence/uy-nhiem-chi.jpg",
        });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "manager xác nhận thủ công"));
        Assert.Equal("Completed", (await ReadRefundAsync(refundId)).GetProperty("status").GetString());
    }

    [Fact(DisplayName = "SLA-11 [Abnormal] Manual confirmation without evidence is refused")]
    public async Task ManagerConfirm_WithoutEvidence_IsRejected()
    {
        var refundId = await ManagerReviewRefundAsync();

        var response = await Manager().PostAsJsonAsync($"/api/refunds/{refundId}/manager-confirm",
            new { manualReason = "Thiếu bằng chứng.", evidenceUrl = "" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SLA-12 [Abnormal] A pending refund cannot be retried by hand")]
    public async Task ManualRetry_WhilePending_ReturnsConflict()
    {
        var refundId = await PendingRefundAsync();

        var response = await Manager().PostAsync($"/api/refunds/{refundId}/retry", null);

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "SLA-13 [Normal] The manager cancels a pending refund and the ticket is rejected")]
    public async Task ManagerCancel_WhilePending_RejectsTicket()
    {
        var (ticketId, refundId) = await PendingRefundWithTicketAsync();

        var cancel = await Manager().PostAsync($"/api/refunds/{refundId}/manager-cancel", null);
        Assert.True(cancel.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(cancel, "manager hủy hoàn tiền"));

        var detail = await Staff().GetAsync($"/api/returns/{ticketId}");
        Assert.Equal("Rejected", (await ApiEnvelope.DataAsync(detail)).GetProperty("status").GetString());
    }

    // ===================== Tự động chốt công nợ quá hạn =====================

    [Fact(DisplayName = "SLA-14 [Normal] A pending liability past its dispute window is auto-settled")]
    public async Task LiabilitySweep_PastDeadline_AutoSettles()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await VendorLiabilityMaintenance.BackdateDeadlineAsync(_fixture, scenario.LiabilityId, DateTime.UtcNow.AddDays(-1));

        var settled = await RunAsync(sp => sp.GetRequiredService<IVendorLiabilityService>().AutoSettleOverdueAsync());
        Assert.True(settled >= 1);

        Assert.Equal("Settled", await LiabilityStatusAsync(scenario));
    }

    [Fact(DisplayName = "SLA-15 [Boundary] A liability still inside its dispute window is left pending")]
    public async Task LiabilitySweep_WithinDeadline_LeavesPending()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        await RunAsync(sp => sp.GetRequiredService<IVendorLiabilityService>().AutoSettleOverdueAsync());

        Assert.Equal("Pending", await LiabilityStatusAsync(scenario));
    }

    [Fact(DisplayName = "SLA-16 [Normal] A disputed liability is never auto-settled, even past the deadline")]
    public async Task LiabilitySweep_Disputed_IsNotAutoSettled()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await _fixture.ClientFor(TestRole.GardenOwner)
            .PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
                new { reason = "Vườn đã phản đối, phải chờ quản lý phán quyết." });

        await VendorLiabilityMaintenance.BackdateDeadlineAsync(_fixture, scenario.LiabilityId, DateTime.UtcNow.AddDays(-1));
        await RunAsync(sp => sp.GetRequiredService<IVendorLiabilityService>().AutoSettleOverdueAsync());

        Assert.Equal("Disputed", await LiabilityStatusAsync(scenario));
    }

    // ===================== Helper =====================

    private HttpClient Staff() => _fixture.ClientFor(TestRole.Staff);

    private HttpClient Manager() => _fixture.ClientFor(TestRole.Manager);

    /// <summary>
    /// Chạy một lượt quét trong scope DI thật và trả về số bản ghi nó xử lý.
    ///
    /// <c>WithScopeAsync</c> chỉ có một overload trả <c>Task</c>, nên phải hứng kết quả qua biến cục
    /// bộ thay vì return thẳng từ lambda.
    /// </summary>
    private async Task<int> RunAsync(Func<IServiceProvider, Task<int>> sweep)
    {
        var processed = 0;
        await _fixture.WithScopeAsync(async sp => processed = await sweep(sp));
        return processed;
    }

    /// <summary>Ticket đang chờ khách bổ sung bằng chứng — nguồn duy nhất đặt <c>EvidenceDeadline</c>.</summary>
    private async Task<Guid> TicketAwaitingEvidenceAsync()
    {
        var ticketId = await RequestedTicketAsync();

        var response = await Staff().PostAsJsonAsync($"/api/returns/{ticketId}/request-more-evidence",
            new { note = "Ảnh chưa thấy rõ bộ rễ." });
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "yêu cầu bổ sung bằng chứng"));

        return ticketId;
    }

    private async Task<Guid> RequestedTicketAsync()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var create = await _fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/returns", new
        {
            deliveryId = order.DeliveryId,
            type = "Refund",
            reason = "PlantHealth",
            reasonDetail = "Cây đến nơi đã héo.",
            items = new[] { new { orderItemId = order.OrderItemId, quantity = 1 } },
            imageUrls = new[] { EvidenceImageUrl },
            bankAccountName = "KHACH KIEM THU",
            bankAccountNumber = "0123456789",
            bankName = "Ngân hàng kiểm thử",
        });

        Assert.True(create.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(create, "tạo yêu cầu trả hàng"));
        return (await ApiEnvelope.DataAsync(create)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> PendingRefundAsync() => (await PendingRefundWithTicketAsync()).RefundId;

    /// <summary>Một lệnh hoàn tiền vừa được duyệt — đang ở Pending, chưa ai điều phối.</summary>
    private async Task<(Guid TicketId, Guid RefundId)> PendingRefundWithTicketAsync()
    {
        var ticketId = await RequestedTicketAsync();
        var staff = Staff();

        var accept = await staff.PostAsync($"/api/returns/{ticketId}/accept", null);
        Assert.True(accept.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(accept, "staff duyệt yêu cầu"));

        var approve = await staff.PostAsJsonAsync($"/api/returns/{ticketId}/approve-refund",
            new { restock = false, note = "Duyệt hoàn tiền trong test." });
        Assert.True(approve.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(approve, "duyệt hoàn tiền"));

        var detail = await staff.GetAsync($"/api/returns/{ticketId}");
        var refundId = (await ApiEnvelope.DataAsync(detail)).GetProperty("refund").GetProperty("id").GetGuid();
        return (ticketId, refundId);
    }

    /// <summary>
    /// Lệnh hoàn tiền ở trạng thái Failed.
    ///
    /// Không ép cổng thanh toán trả lỗi được (<c>FakePaymentGateway.RefundAsync</c> luôn thành công),
    /// nên đi đường vòng: điều phối → kéo <c>ProcessedAt</c> về quá khứ → chạy lượt quét timeout.
    /// </summary>
    private async Task<Guid> FailedRefundAsync()
    {
        var refundId = await PendingRefundAsync();
        await RunAsync(sp => sp.GetRequiredService<IRefundService>().ProcessPendingRefundsAsync());
        await BackdateRefundProcessedAtAsync(refundId, DateTime.UtcNow.AddHours(-2));
        await RunAsync(sp => sp.GetRequiredService<IRefundService>().FailStaleProcessingRefundsAsync());
        return refundId;
    }

    private async Task<Guid> ManagerReviewRefundAsync()
    {
        var refundId = await FailedRefundAsync();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await RunAsync(sp => sp.GetRequiredService<IRefundService>().AutoProcessFailedRefundsAsync());
            await BackdateRefundProcessedAtAsync(refundId, DateTime.UtcNow.AddHours(-2));
            await RunAsync(sp => sp.GetRequiredService<IRefundService>().FailStaleProcessingRefundsAsync());
        }

        await RunAsync(sp => sp.GetRequiredService<IRefundService>().AutoProcessFailedRefundsAsync());

        var status = (await ReadRefundAsync(refundId)).GetProperty("status").GetString();
        Assert.True(status == "ManagerReview",
            $"Chưa đẩy được lệnh hoàn tiền lên ManagerReview, hiện đang ở {status}.");

        return refundId;
    }

    private async Task<JsonElement> ReadRefundAsync(Guid refundId)
    {
        var response = await Manager().GetAsync($"/api/refunds/{refundId}");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc lệnh hoàn tiền"));
        return await ApiEnvelope.DataAsync(response);
    }

    private async Task<string?> LiabilityStatusAsync(RefundedReturn scenario)
    {
        var response = await Manager().GetAsync($"/api/vendor-liabilities/gardens/{scenario.StoreId}?pageSize=100");
        var data = await ApiEnvelope.DataAsync(response);
        return data.GetProperty("items").EnumerateArray()
            .Single(l => l.GetProperty("id").GetGuid() == scenario.LiabilityId)
            .GetProperty("status").GetString();
    }

    /// <summary><c>EvidenceDeadline</c> private set → phải UPDATE thẳng, EF không đặt được.</summary>
    private async Task BackdateEvidenceDeadlineAsync(Guid ticketId, DateTime deadlineUtc)
    {
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE return_requests SET evidence_deadline = {0} WHERE id = {1}", deadlineUtc, ticketId);
        });
    }

    /// <summary><c>ProcessedAt</c> cũng private set — cùng lý do như trên.</summary>
    private async Task BackdateRefundProcessedAtAsync(Guid refundId, DateTime processedAtUtc)
    {
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE refunds SET processed_at = {0} WHERE id = {1}", processedAtUtc, refundId);
        });
    }
}
