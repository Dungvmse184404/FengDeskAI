using AutoMapper;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FengDeskAI.UnitTests;

public class Model3DPreviewTests
{
    private readonly Guid _id = Guid.NewGuid();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IModel3DGenerator> _generator = new();
    private readonly Mock<IFileStorage> _storage = new(MockBehavior.Strict);

    private Model3DRequestService CreateService(Model3DGenerationState state, string? url)
    {
        _products.Setup(p => p.GetModel3DRequestAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Model3DRequest { Id = _id, MeshyTaskId = "current-task", Status = Model3DRequestStatus.InProgress });
        _uow.SetupGet(u => u.Products).Returns(_products.Object);
        _generator.Setup(g => g.GetTaskAsync("current-task", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Model3DTaskResult(state, 100, url, null, null));
        return new Model3DRequestService(_uow.Object, Mock.Of<IMapper>(), _storage.Object,
            _generator.Object, NullLogger<Model3DRequestService>.Instance);
    }

    [Fact]
    public async Task DownloadPreview_UsesCurrentTaskUrl_WithoutAcceptingOrStoringModel()
    {
        const string url = "https://assets.meshy.ai/model.glb?Expires=fresh";
        var service = CreateService(Model3DGenerationState.Succeeded, url);
        using var stream = new MemoryStream([0x67, 0x6c, 0x54, 0x46]);
        _generator.Setup(g => g.DownloadAsync(url, It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        var result = await service.DownloadPreviewAsync(_id);

        Assert.True(result.IsSuccess);
        Assert.Same(stream, result.Data);
        Assert.True(stream.CanRead);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _storage.VerifyNoOtherCalls();
        _generator.Verify(g => g.StartImageTo3DAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(Model3DGenerationState.Running, "https://assets.meshy.ai/model.glb")]
    [InlineData(Model3DGenerationState.Succeeded, null)]
    public async Task DownloadPreview_IncompleteResult_DoesNotDownload(Model3DGenerationState state, string? url)
    {
        var result = await CreateService(state, url).DownloadPreviewAsync(_id);
        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        _generator.Verify(g => g.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DownloadPreview_MissingRequest_Returns404WithoutCallingProvider()
    {
        var service = CreateService(Model3DGenerationState.Succeeded, null);
        _products.Setup(p => p.GetModel3DRequestAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Model3DRequest?)null);
        var result = await service.DownloadPreviewAsync(_id);
        Assert.Equal(404, result.StatusCode);
        _generator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DownloadPreview_DownloadFailure_Returns503()
    {
        const string url = "https://assets.meshy.ai/model.glb";
        var service = CreateService(Model3DGenerationState.Succeeded, url);
        _generator.Setup(g => g.DownloadAsync(url, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("upstream unavailable"));
        var result = await service.DownloadPreviewAsync(_id);
        Assert.Equal(503, result.StatusCode);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewModel_ReturnsGlbStream_AndRequiresStaffPolicy()
    {
        using var stream = new MemoryStream([0x67, 0x6c, 0x54, 0x46]);
        var service = new Mock<IModel3DRequestService>();
        service.Setup(s => s.DownloadPreviewAsync(_id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Stream>.Success(stream));
        var controller = new Model3DRequestsController(service.Object);

        var result = Assert.IsType<FileStreamResult>(await controller.PreviewModel(_id, default));

        Assert.Equal("model/gltf-binary", result.ContentType);
        Assert.Same(stream, result.FileStream);
        var policy = Assert.Single(typeof(Model3DRequestsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.StaffOrAbove, policy.Policy);
    }
}
