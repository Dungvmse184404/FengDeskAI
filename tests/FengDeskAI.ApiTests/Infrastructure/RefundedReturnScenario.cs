using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Features.Returns.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>Một yêu cầu trả hàng đã hoàn tiền xong, kèm khoản công nợ nhà cung cấp sinh ra theo nó.</summary>
public sealed record RefundedReturn(Guid TicketId, Guid RefundId, Guid StoreId, Guid LiabilityId, decimal Amount);

/// <summary>
/// Đưa một đơn đã giao đi hết vòng RMA tới khi lệnh hoàn tiền ở trạng thái Completed — đó là điều
/// kiện DUY NHẤT sinh ra <c>VendorLiability</c> (xem <c>RefundService.CompleteTicketAndLiabilityAsync</c>).
///
/// Vì sao bước cuối KHÔNG đi qua HTTP: cả ba đường đưa lệnh hoàn tiền về Completed đều bị chặn trong
/// môi trường test —
/// <list type="number">
/// <item><c>ReturnSlaWorker</c> (thứ điều phối lệnh Pending) đã bị gỡ cùng mọi IHostedService</item>
/// <item><c>/api/dev/refunds/{id}/success</c> chỉ mở khi <c>IsDevelopment()</c>, mà test chạy ở
///   environment "Testing" → 404</item>
/// <item>webhook PayOS cần <c>VerifyWebhook</c> trả hợp lệ, mà <c>FakePaymentGateway</c> luôn trả
///   không hợp lệ → 400</item>
/// </list>
/// Nên bước cuối gọi thẳng <c>IRefundService.SimulateResultAsync</c> qua scope DI thật. Toàn bộ các
/// bước TRƯỚC đó vẫn đi qua HTTP thật, nên luồng nghiệp vụ vẫn được kiểm.
///
/// Lý do trả hàng cố ý dùng <c>PlantHealth</c>: cây chết thì không thu hồi hàng, ticket đi thẳng
/// <c>Reviewing</c> mà không phải qua <c>ship-back</c> / <c>confirm-received</c>.
/// </summary>
public static class RefundedReturnScenario
{
    private const string EvidenceImageUrl = "https://fake-storage.test/bucket/evidence/test.jpg";

    public static async Task<RefundedReturn> CreateAsync(ApiTestFixture fixture)
    {
        var order = await DeliveredOrderScenario.CreateAsync(fixture);

        // 1. Khách mở yêu cầu hoàn tiền.
        var create = await fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/returns", new
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
        var ticketId = (await ApiEnvelope.DataAsync(create)).GetProperty("id").GetGuid();

        // 2-3. Staff duyệt rồi chốt hoàn tiền → sinh lệnh hoàn tiền ở trạng thái Pending.
        var staff = fixture.ClientFor(TestRole.Staff);
        var accept = await staff.PostAsync($"/api/returns/{ticketId}/accept", null);
        Assert.True(accept.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(accept, "staff duyệt yêu cầu"));

        var approve = await staff.PostAsJsonAsync($"/api/returns/{ticketId}/approve-refund",
            new { restock = false, note = "Duyệt hoàn tiền trong test." });
        Assert.True(approve.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(approve, "duyệt hoàn tiền"));

        // 4. Đọc lại ticket để lấy id lệnh hoàn tiền.
        var detail = await staff.GetAsync($"/api/returns/{ticketId}");
        var refund = (await ApiEnvelope.DataAsync(detail)).GetProperty("refund");
        var refundId = refund.GetProperty("id").GetGuid();
        var amount = refund.GetProperty("amount").GetDecimal();

        // 5. Đưa lệnh hoàn tiền về Completed qua scope DI (xem ghi chú ở đầu lớp).
        await CompleteRefundAsync(fixture, refundId);

        // 6. Khoản công nợ phải có mặt ngay sau đó.
        var liabilities = await fixture.ClientFor(TestRole.Manager)
            .GetAsync($"/api/vendor-liabilities/gardens/{order.StoreId}?page=1&pageSize=100");
        Assert.True(liabilities.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(liabilities, "đọc công nợ"));

        var liability = (await ApiEnvelope.DataAsync(liabilities)).GetProperty("items").EnumerateArray()
            .Single(l => l.GetProperty("returnRequestId").GetGuid() == ticketId);

        return new RefundedReturn(ticketId, refundId, order.StoreId, liability.GetProperty("id").GetGuid(), amount);
    }

    private static async Task CompleteRefundAsync(ApiTestFixture fixture, Guid refundId)
    {
        await fixture.WithScopeAsync(async sp =>
        {
            var refunds = sp.GetRequiredService<IRefundService>();
            var admin = new RmaActor(fixture.UserId(TestRole.Admin),
                IsStaff: false, IsManager: false, IsAdmin: true, IsGardenOwner: false);

            var result = await refunds.SimulateResultAsync(refundId, success: true, admin);
            Assert.True(result.IsSuccess, $"Không đưa được lệnh hoàn tiền về Completed: {result.StatusCode} {result.Message}");
        });
    }
}
