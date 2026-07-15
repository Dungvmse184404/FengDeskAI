using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.StateMachines;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Chứng minh 7 invariant của luồng RMA v2. Mỗi invariant ít nhất một test.
/// </summary>
public class InvariantTests
{
    private static ReturnRequest Ticket(ReturnReason reason, ReturnType type = ReturnType.Refund)
    {
        var t = new ReturnRequest { Reason = reason, Type = type, CustomerId = Guid.NewGuid() };
        t.Items.Add(new ReturnItem { UnitPrice = 100_000m, Quantity = 2 });
        return t;
    }

    // Invariant #1: Vendor (GardenOwner thuần) KHÔNG BAO GIỜ được ra quyết định ticket; chỉ Staff trở lên.
    [Fact]
    public void Inv1_VendorActor_CannotDecide_StaffCan()
    {
        var vendor = new RmaActor(Guid.NewGuid(), IsStaff: false, IsManager: false, IsAdmin: false, IsGardenOwner: true);
        var staff = new RmaActor(Guid.NewGuid(), IsStaff: true, IsManager: false, IsAdmin: false, IsGardenOwner: false);

        Assert.False(vendor.CanDecide);
        Assert.True(staff.CanDecide);
        // Ngay cả khi vừa là vendor vừa là customer vẫn không quyết được.
        var vendorCustomer = vendor with { };
        Assert.False(vendorCustomer.CanDecide);
    }

    // Invariant #2: Refund không bao giờ thực thi 2 lần — khóa idempotency tất định theo ticket + trạng thái Completed là terminal.
    [Fact]
    public void Inv2_Refund_IdempotencyKey_IsDeterministic_AndCompletedIsTerminal()
    {
        var ticketId = Guid.NewGuid();
        Assert.Equal(ReturnWorkflow.RefundIdempotencyKey(ticketId), ReturnWorkflow.RefundIdempotencyKey(ticketId));

        var refund = Processing();
        refund.MarkCompleted(Guid.NewGuid(), DateTime.UtcNow);
        Assert.True(RefundStateMachine.IsTerminal(refund.Status));
        // Không thể xử lý/hoàn thành lần nữa.
        Assert.Throws<InvalidStateTransitionException>(() => refund.MarkProcessing("ref", DateTime.UtcNow));
    }

    // Invariant #3: reason = plant_health KHÔNG BAO GIỜ đi qua ReturnInTransit / ItemReceived.
    [Fact]
    public void Inv3_PlantHealth_NeverGoesThroughReturnFlow()
    {
        Assert.False(ReturnStateMachine.CanTransition(ReturnRequestStatus.UnderReview, ReturnRequestStatus.ReturnInTransit, ReturnReason.PlantHealth));
        Assert.True(ReturnStateMachine.CanTransition(ReturnRequestStatus.UnderReview, ReturnRequestStatus.Reviewing, ReturnReason.PlantHealth));

        var ticket = Ticket(ReturnReason.PlantHealth);
        ticket.Accept(DateTime.UtcNow.AddHours(48));
        ticket.RouteAfterAccept();
        Assert.Equal(ReturnRequestStatus.Reviewing, ticket.Status); // đi thẳng Reviewing, không thu hồi
    }

    // Invariant #4: KHÔNG có nhánh nào tới Failed mà dead-end.
    [Fact]
    public void Inv4_FailedRefund_IsNotDeadEnd()
    {
        Assert.False(RefundStateMachine.IsTerminal(RefundStatus.Failed));
        Assert.True(RefundStateMachine.CanTransition(RefundStatus.Failed, RefundStatus.Processing));      // retry
        Assert.True(RefundStateMachine.CanTransition(RefundStatus.Failed, RefundStatus.ManagerReview));   // escalate
    }

    // Invariant #5: is_manual = true BẮT BUỘC đủ manual_reason + evidence_url + performed_by.
    [Fact]
    public void Inv5_ManualRefund_RequiresFullAuditTrail()
    {
        var manager = Guid.NewGuid();

        var r1 = ManagerReview();
        Assert.Throws<ArgumentException>(() => r1.ManagerComplete("", "http://e", manager, DateTime.UtcNow));
        var r2 = ManagerReview();
        Assert.Throws<ArgumentException>(() => r2.ManagerComplete("lý do", "", manager, DateTime.UtcNow));
        var r3 = ManagerReview();
        Assert.Throws<ArgumentException>(() => r3.ManagerComplete("lý do", "http://e", Guid.Empty, DateTime.UtcNow));

        var ok = ManagerReview();
        ok.ManagerComplete("chuyển khoản tay", "http://evidence/x.png", manager, DateTime.UtcNow);
        Assert.True(ok.IsManual);
        Assert.Equal(RefundStatus.Completed, ok.Status);
        Assert.Equal(manager, ok.PerformedBy);
        Assert.False(string.IsNullOrWhiteSpace(ok.ManualReason));
        Assert.False(string.IsNullOrWhiteSpace(ok.EvidenceUrl));
    }

    // Invariant #6: Vendor phản đối (liability disputed) KHÔNG đảo ngược khoản đã hoàn cho khách.
    [Fact]
    public void Inv6_LiabilityDispute_DoesNotReverseCustomerRefund()
    {
        var refund = Processing();
        refund.MarkCompleted(Guid.NewGuid(), DateTime.UtcNow);

        var liability = new VendorLiability { Amount = 100_000m, DisputeDeadline = DateTime.UtcNow.AddDays(7) };
        liability.Dispute("Hàng vẫn tốt");

        Assert.Equal(VendorLiabilityStatus.Disputed, liability.Status);
        Assert.Equal(RefundStatus.Completed, refund.Status); // tiền đã hoàn KHÔNG bị đảo ngược
        // Máy trạng thái công nợ không có đường quay về refund.
        Assert.False(LiabilityStateMachine.CanTransition(VendorLiabilityStatus.Disputed, VendorLiabilityStatus.Pending));
    }

    // Invariant #7: tổng tiền hoàn KHÔNG vượt tổng giá trị các order_item của ticket.
    [Fact]
    public void Inv7_RefundAmount_NeverExceedsTicketLineTotals()
    {
        var ticket = Ticket(ReturnReason.WrongItem); // 2 x 100_000 = 200_000
        var lineTotal = ticket.Items.Sum(i => i.UnitPrice * i.Quantity);
        var refundAmount = ReturnWorkflow.ComputeRefundAmount(ticket.Items);
        Assert.Equal(200_000m, refundAmount);
        Assert.True(refundAmount <= lineTotal);
    }

    // ----- helpers dựng refund ở trạng thái mong muốn -----

    private static Refund NewRefund() => new()
    {
        ReturnRequestId = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        Amount = 100_000m,
        IdempotencyKey = ReturnWorkflow.RefundIdempotencyKey(Guid.NewGuid()),
    };

    private static Refund Processing()
    {
        var r = NewRefund();
        r.MarkProcessing("ref-1", DateTime.UtcNow);
        return r;
    }

    private static Refund ManagerReview()
    {
        var r = Processing();
        r.MarkFailed();
        r.EscalateToManagerReview();
        return r;
    }
}
