using AutoMapper;
using FengDeskAI.Application.Features.Returns.DTOs;
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

    [Fact]
    public async Task ManagerConfirmRefundAsync_RejectsNonImageWithoutUploading()
    {
        var storage = new Mock<IFileStorage>();
        var service = CreateService(Mock.Of<IUnitOfWork>(), Mock.Of<IPaymentGateway>(), storage.Object);
        var actor = new RmaActor(Guid.NewGuid(), false, true, false, false);
        var request = new ManagerConfirmRefundRequest
        {
            ManualReason = "Đã chuyển khoản thủ công",
            EvidenceFile = new RefundEvidenceFile(
                new MemoryStream([1, 2, 3]), "evidence.pdf", "application/pdf"),
        };

        var result = await service.ManagerConfirmRefundAsync(Guid.NewGuid(), actor, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(422, result.StatusCode);
        storage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ManagerConfirmRefundAsync_UploadsEvidenceAndStoresReturnedUrl()
    {
        var refund = ManagerReviewRefund();
        var returns = new Mock<IReturnRepository>();
        returns.Setup(r => r.GetRefundByIdAsync(refund.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);
        returns.Setup(r => r.GetWithGraphAsync(refund.ReturnRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReturnRequest?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Returns).Returns(returns.Object);
        unitOfWork.Setup(u => u.ExecuteInTransactionAsync<object?>(
                It.IsAny<Func<CancellationToken, Task<object?>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<object?>> action, CancellationToken ct) => action(ct));

        const string evidenceUrl = "https://storage.example/refund-evidence.png";
        var storage = new Mock<IFileStorage>();
        storage.Setup(s => s.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, Stream _, string _, CancellationToken _) =>
                new StoredFile(path, evidenceUrl));

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<RefundResponse>(refund)).Returns(new RefundResponse());
        var service = CreateService(unitOfWork.Object, Mock.Of<IPaymentGateway>(), storage.Object, mapper.Object);
        var actor = new RmaActor(Guid.NewGuid(), false, true, false, false);
        var request = new ManagerConfirmRefundRequest
        {
            ManualReason = "Đã chuyển khoản thủ công",
            EvidenceFile = new RefundEvidenceFile(
                new MemoryStream([1, 2, 3]), "evidence.png", "image/png"),
        };

        var result = await service.ManagerConfirmRefundAsync(refund.Id, actor, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundStatus.Completed, refund.Status);
        Assert.Equal(evidenceUrl, refund.EvidenceUrl);
        Assert.Equal(actor.UserId, refund.PerformedBy);
        storage.Verify(s => s.UploadAsync(
            It.Is<string>(path => path.StartsWith($"Refund_evidence/{refund.Id}/") && path.EndsWith(".png")),
            request.EvidenceFile.Content, "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RefundService CreateService(
        IUnitOfWork unitOfWork,
        IPaymentGateway gateway,
        IFileStorage? storage = null,
        IMapper? mapper = null)
        => new(unitOfWork, gateway, Mock.Of<IVendorLiabilityService>(), mapper ?? Mock.Of<IMapper>(),
            storage ?? Mock.Of<IFileStorage>(), NullLogger<RefundService>.Instance);

    private static Refund ManagerReviewRefund()
    {
        var refund = new Refund
        {
            ReturnRequestId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Amount = 100_000m,
            IdempotencyKey = $"refund-{Guid.NewGuid():N}",
        };
        refund.MarkProcessing("provider-ref", DateTime.UtcNow);
        refund.MarkFailed();
        refund.EscalateToManagerReview();
        return refund;
    }

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
