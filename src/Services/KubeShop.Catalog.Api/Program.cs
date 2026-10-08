using KubeShop.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapGet("/info", () => new
{
    Service = "Catalog",
    Host = Environment.MachineName,
    Environment = app.Environment.EnvironmentName
});

Product[] products =
[
    new(1, "Mechanical keyboard", 89.90m),
    new(2, "Wireless mouse", 29.90m),
    new(3, "27-inch monitor", 249.00m)
];

app.MapGet("/products", () => products);

app.MapGet("/products/{id:int}", (int id, ILogger<Program> logger) =>
{
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product is null)
    {
        logger.LogWarning("Product {ProductId} not found", id);
        return Results.NotFound();
    }

    logger.LogInformation("Product {ProductId} requested", id);
    return Results.Ok(product);
});

app.Run();
