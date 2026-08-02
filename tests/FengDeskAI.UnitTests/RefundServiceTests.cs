using AutoMapper;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FengDeskAI.UnitTests;

public class RefundServiceTests
{
    [Fact]
    public async Task CreateRefundAsync_CreatesPending_WithoutCallingGatewayInsideTicketTransaction()
    {
        var returns = new Mock<IReturnRepository>();
        returns.Setup(r => r.GetRefundByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Returns).Returns(returns.Object);
        unitOfWork.SetupGet(u => u.Transactions).Returns(Mock.Of<ITransactionRepository>());

        var gateway = new Mock<IPaymentGateway>();
        var service = CreateService(unitOfWork.Object, gateway.Object);
        var ticket = Ticket();

        var refund = await service.CreateRefundAsync(
            ticket, 100_000m, RefundMethod.BankTransfer, "test", CancellationToken.None);

        Assert.Equal(RefundStatus.Pending, refund.Status);
        Assert.Same(refund, ticket.Refund);
        gateway.Verify(g => g.RefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailStaleProcessingRefundsAsync_MovesTimedOutRefundToFailed()
    {
        var refund = new Refund
        {
            ReturnRequestId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Amount = 100_000m,
            IdempotencyKey = "refund-timeout-test",
        };
        refund.MarkProcessing("provider-ref", DateTime.UtcNow.AddHours(-1));

        var returns = new Mock<IReturnRepository>();
        returns.Setup(r => r.GetStaleProcessingRefundsAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Refund> { refund });

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Returns).Returns(returns.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = CreateService(unitOfWork.Object, Mock.Of<IPaymentGateway>());
        var count = await service.FailStaleProcessingRefundsAsync();

        Assert.Equal(1, count);
        Assert.Equal(RefundStatus.Failed, refund.Status);
    }

    private static RefundService CreateService(IUnitOfWork unitOfWork, IPaymentGateway gateway)
        => new(unitOfWork, gateway, Mock.Of<IVendorLiabilityService>(), Mock.Of<IMapper>(),
            NullLogger<RefundService>.Instance);

    private static ReturnRequest Ticket()
    {
        var ticket = new ReturnRequest
        {
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Reason = ReturnReason.WrongItem,
        };
        ticket.Items.Add(new ReturnItem { Quantity = 1, UnitPrice = 100_000m });
        return ticket;
    }
}
