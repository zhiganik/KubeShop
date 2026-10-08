using KubeShop.Notifications.Worker;

var builder = Host.CreateApplicationBuilder(args);

var ordersUrl = builder.Configuration["Orders:BaseUrl"]
    ?? throw new InvalidOperationException("Orders:BaseUrl is not configured.");

builder.Services.AddHttpClient("orders", client => client.BaseAddress = new Uri(ordersUrl));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
