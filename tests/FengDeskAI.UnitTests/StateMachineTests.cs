using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.StateMachines;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>Bao phủ transition hợp lệ VÀ không hợp lệ của cả hai/ba máy trạng thái.</summary>
public class StateMachineTests
{
    // ---------------- Ticket ----------------

    public static IEnumerable<object[]> ValidTicket() => new List<object[]>
    {
        new object[] { ReturnRequestStatus.Requested, ReturnRequestStatus.NeedMoreEvidence, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Requested, ReturnRequestStatus.UnderReview, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Requested, ReturnRequestStatus.Cancelled, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.NeedMoreEvidence, ReturnRequestStatus.Requested, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.NeedMoreEvidence, ReturnRequestStatus.Rejected, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.UnderReview, ReturnRequestStatus.Reviewing, ReturnReason.PlantHealth },
        new object[] { ReturnRequestStatus.UnderReview, ReturnRequestStatus.ReturnInTransit, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.ReturnInTransit, ReturnRequestStatus.ItemReceived, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.ItemReceived, ReturnRequestStatus.Reviewing, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Reviewing, ReturnRequestStatus.Refunding, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Reviewing, ReturnRequestStatus.Exchanging, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Reviewing, ReturnRequestStatus.Rejected, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Exchanging, ReturnRequestStatus.Completed, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Exchanging, ReturnRequestStatus.Refunding, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Refunding, ReturnRequestStatus.Completed, ReturnReason.WrongItem },
    };

    [Theory]
    [MemberData(nameof(ValidTicket))]
    public void Ticket_ValidTransitions_Allowed(ReturnRequestStatus from, ReturnRequestStatus to, ReturnReason reason)
        => Assert.True(ReturnStateMachine.CanTransition(from, to, reason));

    public static IEnumerable<object[]> InvalidTicket() => new List<object[]>
    {
        new object[] { ReturnRequestStatus.Requested, ReturnRequestStatus.Refunding, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.UnderReview, ReturnRequestStatus.ReturnInTransit, ReturnReason.PlantHealth }, // #3
        new object[] { ReturnRequestStatus.UnderReview, ReturnRequestStatus.Reviewing, ReturnReason.WrongItem },        // hàng vật lý phải thu hồi
        new object[] { ReturnRequestStatus.Completed, ReturnRequestStatus.Refunding, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Cancelled, ReturnRequestStatus.Requested, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Rejected, ReturnRequestStatus.Reviewing, ReturnReason.WrongItem },
        new object[] { ReturnRequestStatus.Reviewing, ReturnRequestStatus.Completed, ReturnReason.WrongItem },
    };

    [Theory]
    [MemberData(nameof(InvalidTicket))]
    public void Ticket_InvalidTransitions_Rejected(ReturnRequestStatus from, ReturnRequestStatus to, ReturnReason reason)
        => Assert.False(ReturnStateMachine.CanTransition(from, to, reason));

    // ---------------- Refund ----------------

    [Theory]
    [InlineData(RefundStatus.Pending, RefundStatus.Processing, true)]
    [InlineData(RefundStatus.Pending, RefundStatus.Cancelled, true)]
    [InlineData(RefundStatus.Processing, RefundStatus.Completed, true)]
    [InlineData(RefundStatus.Processing, RefundStatus.Failed, true)]
    [InlineData(RefundStatus.Failed, RefundStatus.Processing, true)]
    [InlineData(RefundStatus.Failed, RefundStatus.ManagerReview, true)]
    [InlineData(RefundStatus.ManagerReview, RefundStatus.Processing, true)]
    [InlineData(RefundStatus.ManagerReview, RefundStatus.Completed, true)]
    [InlineData(RefundStatus.Pending, RefundStatus.Completed, false)]
    [InlineData(RefundStatus.Completed, RefundStatus.Processing, false)]
    [InlineData(RefundStatus.Cancelled, RefundStatus.Processing, false)]
    [InlineData(RefundStatus.Failed, RefundStatus.Completed, false)]
    public void Refund_Transitions(RefundStatus from, RefundStatus to, bool expected)
        => Assert.Equal(expected, RefundStateMachine.CanTransition(from, to));

    // ---------------- Liability ----------------

    [Theory]
    [InlineData(VendorLiabilityStatus.Pending, VendorLiabilityStatus.Disputed, true)]
    [InlineData(VendorLiabilityStatus.Pending, VendorLiabilityStatus.Settled, true)]
    [InlineData(VendorLiabilityStatus.Disputed, VendorLiabilityStatus.Settled, true)]
    [InlineData(VendorLiabilityStatus.Disputed, VendorLiabilityStatus.Waived, true)]
    [InlineData(VendorLiabilityStatus.Pending, VendorLiabilityStatus.Waived, false)]
    [InlineData(VendorLiabilityStatus.Settled, VendorLiabilityStatus.Disputed, false)]
    [InlineData(VendorLiabilityStatus.Waived, VendorLiabilityStatus.Settled, false)]
    public void Liability_Transitions(VendorLiabilityStatus from, VendorLiabilityStatus to, bool expected)
        => Assert.Equal(expected, LiabilityStateMachine.CanTransition(from, to));
}
