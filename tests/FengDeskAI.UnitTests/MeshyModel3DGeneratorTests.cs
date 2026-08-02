using System.Net;
using System.Text;
using System.Text.Json;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Infrastructure.ExternalServices.Model3D;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FengDeskAI.UnitTests;

public class MeshyModel3DGeneratorTests
{
    [Fact]
    public async Task StartImageTo3DAsync_Success_SendsExpectedPayloadAndReturnsTaskId()
    {
        HttpRequestMessage? captured = null;
        string? requestBody = null;
        var handler = new StubHandler(async request =>
        {
            captured = request;
            requestBody = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.Accepted, "{\"result\":\"task-123\"}");
        });
        var sut = CreateSut(handler);

        var result = await sut.StartImageTo3DAsync(["https://cdn.example.com/plant.jpg"]);

        Assert.Equal("task-123", result);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("/openapi/v1/multi-image-to-3d", captured.RequestUri!.AbsolutePath);
        using var payload = JsonDocument.Parse(requestBody!);
        Assert.Equal("https://cdn.example.com/plant.jpg",
            payload.RootElement.GetProperty("image_urls")[0].GetString());
        Assert.Equal("meshy-5", payload.RootElement.GetProperty("ai_model").GetString());
    }

    [Fact]
    public async Task StartImageTo3DAsync_BadRequest_PreservesProviderMessage()
    {
        var sut = CreateSut(new StubHandler(_ => Task.FromResult(
            Json(HttpStatusCode.BadRequest, "{\"message\":\"Image URL is unreachable\"}"))));

        var error = await Assert.ThrowsAsync<Model3DProviderException>(
            () => sut.StartImageTo3DAsync(["https://cdn.example.com/plant.jpg"]));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal("Image URL is unreachable", error.ProviderMessage);
    }

    [Fact]
    public async Task StartImageTo3DAsync_PaymentRequired_ThrowsCreditException()
    {
        var sut = CreateSut(new StubHandler(_ => Task.FromResult(
            Json(HttpStatusCode.PaymentRequired, "{\"message\":\"Insufficient credits\"}"))));

        await Assert.ThrowsAsync<InsufficientCreditsException>(
            () => sut.StartImageTo3DAsync(["https://cdn.example.com/plant.jpg"]));
    }

    private static MeshyModel3DGenerator CreateSut(HttpMessageHandler handler)
    {
        var settings = Options.Create(new MeshySettings
        {
            BaseUrl = "https://api.meshy.test",
            ApiKey = "test-key",
            MultiImageTo3DPath = "/openapi/v1/multi-image-to-3d",
        });
        return new MeshyModel3DGenerator(
            new HttpClient(handler), settings, NullLogger<MeshyModel3DGenerator>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => responseFactory(request);
    }
}
