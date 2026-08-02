using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Hàng chờ chung cho staff sàn xử lý cả yêu cầu tạo mới (Initial) và tạo lại (Regenerate).
/// Mỗi request gắn với một ảnh đích; staff có thể chọn thêm ảnh cùng kiểu dáng trước khi gửi Meshy.
/// </summary>
[Route("api/model3d-requests")]
[Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
public class Model3DRequestsController : ApiControllerBase
{
    private readonly IModel3DRequestService _service;

    public Model3DRequestsController(IModel3DRequestService service) => _service = service;

    /// <summary>
    /// Hàng chờ thống nhất. Có thể lọc theo status/reason; response kèm tổng số theo từng trạng thái.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQueue(
        [FromQuery] Model3DRequestStatus? status, [FromQuery] Model3DFailureReason? reason,
        [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
        => ToActionResult(await _service.GetQueueAsync(status, reason, skip, take, ct));

    /// <summary>Chọn ảnh (tick có sẵn + upload mới, 1–4 ảnh) rồi gửi task Meshy lần đầu.</summary>
    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> Generate(Guid id, [FromForm] Model3DRequestFormModel form, CancellationToken ct)
    {
        var (request, streams) = form.ToRequest();
        try
        {
            return ToActionResult(await _service.GenerateAsync(id, CurrentUserId, request, ct));
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }
    }

    /// <summary>Chưa ưng ý kết quả trước — chọn lại ảnh, gửi lại Meshy. Không giới hạn số lần.</summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, [FromForm] Model3DRequestFormModel form, CancellationToken ct)
    {
        var (request, streams) = form.ToRequest();
        try
        {
            return ToActionResult(await _service.RetryAsync(id, CurrentUserId, request, ct));
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }
    }

    /// <summary>Xem trước kết quả Meshy hiện tại (live poll, URL tạm — không lưu) để quyết định accept/retry.</summary>
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
        => ToActionResult(await _service.PreviewAsync(id, ct));

    /// <summary>Ưng ý — tải GLB từ Meshy, re-host storage vĩnh viễn, ghi đè model hiện tại của sản phẩm.</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
        => ToActionResult(await _service.AcceptAsync(id, CurrentUserId, ct));

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectModel3DRequestRequest request, CancellationToken ct)
        => ToActionResult(await _service.RejectAsync(id, CurrentUserId, request.Reason, ct));
}

/// <summary>
/// Binding model multipart/form-data cho POST .../model-3d/requests|generate|retry — tick ảnh có
/// sẵn (<see cref="SourceImageIds"/>) + upload ảnh mới (<see cref="NewImages"/>). IFormFile là kiểu
/// ASP.NET Core nên convert sang <c>NewModel3DImage</c> (Application layer, chỉ biết Stream) ở đây.
/// </summary>
public class Model3DRequestFormModel
{
    public Guid? ProductImageId { get; set; }
    public List<Guid>? SourceImageIds { get; set; }
    public List<IFormFile>? NewImages { get; set; }

    /// <summary>Trả về DTO cho service + danh sách stream cần dispose sau khi service dùng xong.</summary>
    public (RequestModel3DRequest Request, List<Stream> Streams) ToRequest()
    {
        var streams = new List<Stream>();
        List<NewModel3DImage>? newImages = null;

        if (NewImages is { Count: > 0 })
        {
            newImages = new List<NewModel3DImage>();
            foreach (var file in NewImages)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                newImages.Add(new NewModel3DImage(stream, file.FileName, file.ContentType));
            }
        }

        return (new RequestModel3DRequest
        {
            ProductImageId = ProductImageId,
            SourceImageIds = SourceImageIds,
            NewImages = newImages,
        }, streams);
    }
}
