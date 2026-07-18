using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Ticket RMA v2 (trả hàng / hoàn tiền / đổi trả).
/// - Customer: tạo ticket + bằng chứng, bổ sung bằng chứng, hủy, theo dõi.
/// - Vendor (garden owner): góp ý trong SLA (acknowledge/dispute — non-blocking) + xác nhận đã nhận hàng.
/// - Staff (nền tảng): tiếp nhận + RA QUYẾT ĐỊNH (duyệt hoàn/đổi, từ chối, yêu cầu bổ sung).
/// Transition không hợp lệ → HTTP 409. Refund do Manager (xem RefundsController); công nợ ở VendorLiabilitiesController.
/// </summary>
[Route("api/returns")]
[Authorize]
public class ReturnsController : ApiControllerBase
{
    private readonly IReturnService _service;

    public ReturnsController(IReturnService service) => _service = service;

    // ================= Customer =================

    /// <summary>
    /// Tạo ticket RMA (JSON thuần). Ảnh bằng chứng: FE upload trước qua <c>POST /api/uploads</c>
    /// để lấy URL, rồi truyền vào <c>imageUrls</c> — BẮT BUỘC có ít nhất 1 (guard rule).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, page, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, RmaActor, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ToActionResult(await _service.CancelAsync(id, CurrentUserId, ct));

    /// <summary>Khách bổ sung bằng chứng (multipart, field "files") → ticket quay lại chờ duyệt.</summary>
    [HttpPost("{id:guid}/resubmit-evidence")]
    public async Task<IActionResult> ResubmitEvidence(Guid id, [FromForm] List<IFormFile>? files, CancellationToken ct)
        => ToActionResult(await _service.ResubmitEvidenceAsync(id, CurrentUserId, await ReadFilesAsync(files, ct), ct));

    /// <summary>Tải THÊM ảnh bằng chứng (multipart, field "files"). Chỉ chủ ticket.</summary>
    [HttpPost("{id:guid}/images")]
    public async Task<IActionResult> UploadImages(Guid id, [FromForm] List<IFormFile> files, CancellationToken ct)
        => ToActionResult(await _service.UploadImagesAsync(id, CurrentUserId, await ReadFilesAsync(files, ct), ct));

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken ct)
        => ToActionResult(await _service.DeleteImageAsync(id, imageId, CurrentUserId, ct));

    // ================= Vendor (non-blocking) =================

    /// <summary>Ticket của một store (màn vendor). Owner/staff store đó hoặc Staff nền tảng.</summary>
    [HttpGet("stores/{storeId:guid}")]
    public async Task<IActionResult> GetForStore(Guid storeId, [FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetForStoreAsync(storeId, RmaActor, page, ct));

    /// <summary>Vendor ghi nhận/đồng ý ticket (không chặn quyết định của Staff).</summary>
    [HttpPost("{id:guid}/vendor-acknowledge")]
    [Authorize(Policy = AuthorizationPolicies.GardenOwnerOrAbove)]
    public async Task<IActionResult> VendorAcknowledge(Guid id, CancellationToken ct)
        => ToActionResult(await _service.VendorAcknowledgeAsync(id, RmaActor, ct));

    /// <summary>Vendor phản đối ticket (non-blocking) — Staff vẫn toàn quyền quyết định.</summary>
    [HttpPost("{id:guid}/vendor-dispute")]
    [Authorize(Policy = AuthorizationPolicies.GardenOwnerOrAbove)]
    public async Task<IActionResult> VendorDispute(Guid id, [FromBody] VendorDisputeRequest request, CancellationToken ct)
        => ToActionResult(await _service.VendorDisputeAsync(id, RmaActor, request, ct));

    /// <summary>Vendor xác nhận đã nhận hàng trả (chỉ xác nhận, KHÔNG quyết định kết quả).</summary>
    [HttpPost("{id:guid}/confirm-received")]
    [Authorize(Policy = AuthorizationPolicies.GardenOwnerOrAbove)]
    public async Task<IActionResult> ConfirmReceived(Guid id, CancellationToken ct)
        => ToActionResult(await _service.ConfirmItemReceivedAsync(id, RmaActor, ct));

    // ================= Staff (decision) =================

    /// <summary>Hàng đợi ticket cần Staff xử lý.</summary>
    [HttpGet("pending")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> GetPending([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetPendingForStaffAsync(page, ct));

    /// <summary>Tất cả ticket (giám sát).</summary>
    [HttpGet("all")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> GetAll([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetAllAsync(page, ct));

    /// <summary>Staff tiếp nhận ticket → thông báo vendor (SLA) + rẽ nhánh theo lý do.</summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
        => ToActionResult(await _service.AcceptAsync(id, RmaActor, ct));

    [HttpPost("{id:guid}/request-more-evidence")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> RequestMoreEvidence(Guid id, [FromBody] RequestMoreEvidenceRequest request, CancellationToken ct)
        => ToActionResult(await _service.RequestMoreEvidenceAsync(id, RmaActor, request, ct));

    [HttpPost("{id:guid}/approve-refund")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> ApproveRefund(Guid id, [FromBody] ApproveRefundRequest request, CancellationToken ct)
        => ToActionResult(await _service.ApproveRefundAsync(id, RmaActor, request, ct));

    [HttpPost("{id:guid}/approve-exchange")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> ApproveExchange(Guid id, [FromBody] ApproveExchangeRequest request, CancellationToken ct)
        => ToActionResult(await _service.ApproveExchangeAsync(id, RmaActor, request, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReturnRequest request, CancellationToken ct)
        => ToActionResult(await _service.RejectAsync(id, RmaActor, request, ct));

    // ================= Helpers =================

    /// <summary>Đọc IFormFile vào MemoryStream (seekable) để truyền xuống service upload.</summary>
    private static async Task<List<ReturnImageFile>> ReadFilesAsync(List<IFormFile>? files, CancellationToken ct)
    {
        var uploads = new List<ReturnImageFile>();
        if (files is null) return uploads;
        foreach (var f in files)
        {
            if (f.Length == 0) continue;
            var ms = new MemoryStream();
            await f.CopyToAsync(ms, ct);
            ms.Position = 0;
            uploads.Add(new ReturnImageFile(ms, f.FileName, f.ContentType));
        }
        return uploads;
    }
}
