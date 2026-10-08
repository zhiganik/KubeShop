namespace KubeShop.Contracts;

/// <summary>
/// An order created by Orders.Api. For now Notifications.Worker simply polls GET /orders for it;
/// later it will be published to RabbitMQ instead.
/// </summary>
public sealed record OrderPlaced(Guid OrderId, int ProductId, int Quantity, decimal TotalPrice, DateTimeOffset PlacedAt);
