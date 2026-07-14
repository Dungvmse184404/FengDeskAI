using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// CRUD workspace profile của user đang đăng nhập.
/// User chỉ thao tác được trên profile của chính mình.
/// </summary>
[Route("api/workspace")]
[Authorize]
public class WorkspaceProfilesController : ApiControllerBase
{
    private readonly IWorkspaceProfileService _service;
    private readonly IWorkspaceIntakeService _intakeService;
    private readonly IWorkspaceElementInputClassifierService _classifier;

    public WorkspaceProfilesController(
        IWorkspaceProfileService service,
        IWorkspaceIntakeService intakeService,
        IWorkspaceElementInputClassifierService classifier)
    {
        _service = service;
        _intakeService = intakeService;
        _classifier = classifier;
    }

    /// <summary>
    /// AI intake (ASYNC): mô tả không gian bằng lời (+ ảnh tùy chọn) → đẩy job nền, trả operationId NGAY
    /// (không chờ LLM ~80s → tránh FE timeout). Client nghe realtime qua SignalR group "ai-op-{operationId}"
    /// (event "workspaceIntakeResult" / "workspaceIntakeFailed") hoặc poll <see cref="GetIntakeStatus"/> để
    /// lấy kết quả. Stateless — KHÔNG lưu DB; user review/sửa rồi submit qua <see cref="Create"/>.
    /// </summary>
    [HttpPost("parse-description")]
    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [EnableRateLimiting("workspace-intake")]
    public async Task<IActionResult> ParseDescription(
        [FromBody] ParseWorkspaceDescriptionRequest request, CancellationToken ct)
        => ToActionResult(await _intakeService.StartParseAsync(CurrentUserId, request, ct));

    /// <summary>Trạng thái/kết quả 1 job intake (pending/done/failed) — fallback khi client F5 hoặc lỡ event realtime.</summary>
    [HttpGet("parse-description/{operationId}")]
    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    public IActionResult GetIntakeStatus(string operationId)
        => ToActionResult(_intakeService.GetJobStatus(operationId));

    /// <summary>Tải ảnh không gian lên storage (multipart, field "file") → trả link để đính kèm parse-description.</summary>
    [HttpPost("images")]
    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return ToActionResult(ServiceResult<string>.Failure(ApiStatusCodes.BadRequest, "Vui lòng chọn tệp ảnh."));

        await using var stream = file.OpenReadStream();
        return ToActionResult(await _intakeService.UploadImageAsync(CurrentUserId, stream, file.FileName, file.ContentType, ct));
    }

    /// <summary>Danh sách workspace profile của user hiện tại.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, ct));

    /// <summary>Từ vựng màu/vật liệu/hình khối hợp lệ — cho FE dựng tag picker "hiện trạng phòng hiện tại".</summary>
    [HttpGet("element-inputs")]
    public async Task<IActionResult> GetElementInputVocabulary(CancellationToken ct)
        => ToActionResult(await _service.GetElementInputVocabularyAsync(ct));

    /// <summary>User gõ tên 1 tag mới (chưa có sẵn) → AI phân loại thành hành + weight, lưu luôn vào vocabulary.</summary>
    [HttpPost("element-inputs/classify")]
    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [EnableRateLimiting("workspace-intake")]
    public async Task<IActionResult> ClassifyElementInput([FromBody] ClassifyElementInputRequest request, CancellationToken ct)
        => ToActionResult(await _classifier.ClassifyAsync(request, ct));

    /// <summary>Profile mặc định (dùng làm input AI khi user không chỉ định).</summary>
    [HttpGet("default")]
    public async Task<IActionResult> GetDefault(CancellationToken ct)
        => ToActionResult(await _service.GetDefaultAsync(CurrentUserId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, CurrentUserId, ct));

    /// <summary>Phân tích ngũ hành phòng (ideal/adjustedIdeal/current/gap) — FE hiển thị phòng thiếu/thừa hành gì.</summary>
    [HttpGet("{id:guid}/element-analysis")]
    public async Task<IActionResult> GetElementAnalysis(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetElementAnalysisAsync(id, CurrentUserId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceProfileRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceProfileRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAsync(id, CurrentUserId, request, ct));

    /// <summary>Đặt profile làm default. Tự động bỏ default của các profile khác cùng user.</summary>
    [HttpPatch("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
        => ToActionResult(await _service.SetDefaultAsync(id, CurrentUserId, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteAsync(id, CurrentUserId, ct));
}
