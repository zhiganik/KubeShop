namespace KubeShop.Contracts;

/// <summary>
/// Published by Orders.Api to RabbitMQ when an order is created; consumed by Notifications.Worker.
/// </summary>
public sealed record OrderPlaced(Guid OrderId, Guid ProductId, int Quantity, decimal TotalPrice, DateTimeOffset PlacedAt);
