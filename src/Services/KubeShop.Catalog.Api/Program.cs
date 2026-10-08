var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Kubernetes probes: liveness = "process is alive", readiness = "ready to receive traffic".
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// Shows which pod answered — handy for watching load balancing across replicas.
app.MapGet("/info", () => new
{
    Service = "Catalog",
    Host = Environment.MachineName,
    Environment = app.Environment.EnvironmentName
});

app.Run();
