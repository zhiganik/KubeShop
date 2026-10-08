namespace KubeShop.Contracts;

/// <summary>
/// A product served by Catalog.Api and looked up by Orders.Api to price an order.
/// </summary>
public sealed record Product(int Id, string Name, decimal Price);
