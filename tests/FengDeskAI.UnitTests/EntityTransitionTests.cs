using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Sales;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>Kiểm tra các phương thức transition đóng gói của entity ticket (happy path + ném khi sai).</summary>
public class EntityTransitionTests
{
    private static ReturnRequest Ticket(ReturnReason reason) =>
        new() { Reason = reason, CustomerId = Guid.NewGuid() };

    [Fact]
    public void PhysicalGoods_FullHappyPath_ReachesCompleted()
    {
        var t = Ticket(ReturnReason.WrongItem);
        var staff = Guid.NewGuid();

        t.Accept(DateTime.UtcNow.AddHours(48));
        Assert.Equal(ReturnRequestStatus.UnderReview, t.Status);
        t.RouteAfterAccept();
        Assert.Equal(ReturnRequestStatus.ReturnInTransit, t.Status);
        t.ConfirmItemReceived(DateTime.UtcNow);
        Assert.Equal(ReturnRequestStatus.ItemReceived, t.Status);
        t.MoveToReviewing();
        t.ApproveRefund(staff, DateTime.UtcNow);
        Assert.Equal(ReturnRequestStatus.Refunding, t.Status);
        Assert.Equal(staff, t.DecidedBy);
        t.CompleteRefund();
        Assert.Equal(ReturnRequestStatus.Completed, t.Status);
    }

    [Fact]
    public void PlantHealth_SkipsRecall_ReachesCompleted()
    {
        var t = Ticket(ReturnReason.PlantHealth);
        t.Accept(DateTime.UtcNow.AddHours(48));
        t.RouteAfterAccept();
        Assert.Equal(ReturnRequestStatus.Reviewing, t.Status);
        // Không bao giờ chạm ReturnInTransit → ConfirmItemReceived phải ném.
        Assert.Throws<InvalidStateTransitionException>(() => t.ConfirmItemReceived(DateTime.UtcNow));

        t.ApproveRefund(Guid.NewGuid(), DateTime.UtcNow);
        t.CompleteRefund();
        Assert.Equal(ReturnRequestStatus.Completed, t.Status);
    }

    [Fact]
    public void Exchange_OutOfStock_CanFallbackToRefund()
    {
        var t = Ticket(ReturnReason.NotAsDescribed);
        t.Accept(DateTime.UtcNow.AddHours(48));
        t.RouteAfterAccept();
        t.ConfirmItemReceived(DateTime.UtcNow);
        t.MoveToReviewing();
        t.ApproveExchange(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(ReturnRequestStatus.Exchanging, t.Status);
        t.FallbackToRefund();
        Assert.Equal(ReturnRequestStatus.Refunding, t.Status);
    }

    [Fact]
    public void Cancel_OnlyFromRequested()
    {
        var t = Ticket(ReturnReason.WrongItem);
        t.Accept(DateTime.UtcNow.AddHours(48)); // → UnderReview
        Assert.Throws<InvalidStateTransitionException>(() => t.Cancel());

        var fresh = Ticket(ReturnReason.WrongItem);
        fresh.Cancel();
        Assert.Equal(ReturnRequestStatus.Cancelled, fresh.Status);
    }

    [Fact]
    public void ConfirmItemReceived_FromRequested_Throws()
    {
        var t = Ticket(ReturnReason.WrongItem);
        Assert.Throws<InvalidStateTransitionException>(() => t.ConfirmItemReceived(DateTime.UtcNow));
    }
}
