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

    // System
    SystemAlert,
}
