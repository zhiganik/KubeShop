using System.Collections.Concurrent;
using System.Net;
using KubeShop.Contracts;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["Orders:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Orders:ApiKey is not configured. Provide it via the Orders__ApiKey environment variable.");
}

var catalogUrl = builder.Configuration["Catalog:BaseUrl"]
    ?? throw new InvalidOperationException("Catalog:BaseUrl is not configured.");
var maxQuantity = builder.Configuration.GetValue("Orders:MaxQuantity", 10);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient("catalog", client => client.BaseAddress = new Uri(catalogUrl));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Logger.LogInformation("Orders API starting. Catalog: {CatalogUrl}, max quantity: {MaxQuantity}", catalogUrl, maxQuantity);

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapGet("/info", () => new
{
    Service = "Orders",
    Host = Environment.MachineName,
    Environment = app.Environment.EnvironmentName
});

var orders = new ConcurrentQueue<OrderPlaced>();

app.MapGet("/orders", () => orders.ToArray());

app.MapPost("/orders", async (
    PlaceOrderRequest request,
    [FromHeader(Name = "X-Api-Key")] string? requestApiKey,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
{
    if (requestApiKey != apiKey)
    {
        logger.LogWarning("Rejected order: missing or invalid API key");
        return Results.Unauthorized();
    }

    if (request.Quantity < 1 || request.Quantity > maxQuantity)
    {
        return Results.BadRequest($"Quantity must be between 1 and {maxQuantity}.");
    }

    Product? product;
    try
    {
        product = await httpClientFactory.CreateClient("catalog").GetFromJsonAsync<Product>($"/products/{request.ProductId}");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        product = null;
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Catalog is unreachable at {CatalogUrl}", catalogUrl);
        return Results.Problem("Catalog is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (product is null)
    {
        return Results.NotFound($"Product {request.ProductId} does not exist.");
    }

    var order = new OrderPlaced(Guid.NewGuid(), product.Id, request.Quantity, product.Price * request.Quantity, DateTimeOffset.UtcNow);
    orders.Enqueue(order);

    logger.LogInformation(
        "Order {OrderId} placed: {Quantity} x {ProductName} = {TotalPrice}",
        order.OrderId, order.Quantity, product.Name, order.TotalPrice);

    return Results.Created($"/orders/{order.OrderId}", order);
});

app.MapGet("/debug/boom", () =>
{
    throw new InvalidOperationException("Boom! Deliberate failure for debugging practice.");
});

app.Run();

internal sealed record PlaceOrderRequest(int ProductId, int Quantity);
