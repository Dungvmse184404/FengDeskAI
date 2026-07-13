using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>
/// Mô tả không gian bằng lời (10..2000 ký tự, hoặc rỗng nếu có ít nhất 1 ảnh) — AI phân tích thành
/// draft, KHÔNG lưu DB. <see cref="ImageUrls"/> (tối đa 3) là link đã upload qua endpoint images.
/// </summary>
public sealed class ParseWorkspaceDescriptionRequest
{
    public string Description { get; set; } = null!;
    public List<string>? ImageUrls { get; set; }

    /// <summary>
    /// Công tắc "suy nghĩ kỹ" do user chọn: true = bật thinking (kỹ hơn nhưng CHẬM hơn nhiều), false = tắt
    /// (nhanh). null = theo cấu hình mặc định Ai:Intake.
    /// </summary>
    public bool? Think { get; set; }
}

/// <summary>
/// Draft AI intake — mọi field nullable (null = AI không suy ra được, KHÔNG được đoán bừa).
/// Chỉ để prefill form; lưu thật vẫn đi qua Create/Update sẵn có (validate 1 nơi duy nhất).
/// </summary>
public sealed class WorkspaceProfileDraftResponse
{
    public string? Name { get; set; }
    public LocationType? LocationType { get; set; }
    public Guid? WorkspaceTypeId { get; set; }
    public string? StyleCode { get; set; }
    public LightingType? Lighting { get; set; }

    /// <summary>true = có bàn làm việc, false = rõ ràng không có (vd bếp/phòng khách), null = không đủ căn cứ.</summary>
    public bool? HasDesk { get; set; }
    public DeskType? DeskType { get; set; }
    public CompassDirection? DeskOrientation { get; set; }
    public CompassDirection? RoomFacingDirection { get; set; }
    public WorkPurpose? WorkPurpose { get; set; }
    public int? DeskArea { get; set; }

    /// <summary>Input codes hợp lệ cho workspace_profile_inputs (màu/vật liệu nhận ra được).</summary>
    public List<WorkspaceProfileInputDto> Inputs { get; set; } = new();

    /// <summary>0..1 — mức tự tin tổng thể của lượt parse (FE hiện badge).</summary>
    public decimal Confidence { get; set; }

    /// <summary>Chi tiết user nhắc đến nhưng hệ thống không map được — FE hiện để user tự xử lý.</summary>
    public List<string> Unrecognized { get; set; } = new();
}

/// <summary>Nhận job intake async → trả operationId ngay để FE join realtime + poll fallback (KHÔNG chờ LLM).</summary>
public sealed record WorkspaceIntakeStartResponse(string OperationId);

/// <summary>Trạng thái 1 job intake (cache TTL ngắn) — cho FE kết nối lại/F5 khi lỡ mất event realtime.</summary>
public sealed class WorkspaceIntakeJobStatusResponse
{
    /// <summary>"pending" | "done" | "failed".</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Có khi Status = "done".</summary>
    public WorkspaceProfileDraftResponse? Draft { get; set; }

    /// <summary>Có khi Status = "failed".</summary>
    public string? Message { get; set; }

    public static WorkspaceIntakeJobStatusResponse Pending() => new() { Status = "pending" };
    public static WorkspaceIntakeJobStatusResponse Done(WorkspaceProfileDraftResponse draft) =>
        new() { Status = "done", Draft = draft };
    public static WorkspaceIntakeJobStatusResponse Failed(string message) =>
        new() { Status = "failed", Message = message };
}
