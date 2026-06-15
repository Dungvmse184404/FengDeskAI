using FengDeskAI.Domain.Enums.Notification;

namespace FengDeskAI.Application.Features.Announcement.DTOs;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public Guid? ReferenceId { get; set; }
    public ReferenceType? ReferenceType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateNotificationRequest
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public Guid? ReferenceId { get; set; }
    public ReferenceType? ReferenceType { get; set; }
}
