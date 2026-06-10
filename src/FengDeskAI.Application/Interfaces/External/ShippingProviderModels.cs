namespace FengDeskAI.Application.Interfaces.External;

public record ShipmentRequest(
    Guid DeliveryId,
    Guid OrderId,
    decimal Subtotal,
    string? RecipientName,
    string? RecipientPhone,
    string? ShippingAddress);

public record ShipmentResult(
    string Provider,
    string ProviderOrderId,
    string TrackingCode,
    DateTime? EstimatedDeliveryDate);
