namespace FengDeskAI.Domain.Enums.Notification;

public enum NotificationType
{
    // Order
    OrderPlaced,
    OrderPaid,
    OrderCancelled,
    OrderCompleted,

    // Delivery
    DeliveryConfirmed,
    DeliveryPreparing,
    DeliveryShipped,
    DeliveryDelivered,
    DeliveryReturned,
    DeliveryCancelled,

    // Return / Refund / Exchange
    ReturnRequested,
    ReturnApproved,
    ReturnRejected,
    ReturnReceived,
    ReturnCancelled,
    RefundCompleted,
    ExchangeShipped,

    // Staff invitation
    StaffInvited,
    StaffInvitationAccepted,
    StaffInvitationRejected,

    // System
    SystemAlert,
}
