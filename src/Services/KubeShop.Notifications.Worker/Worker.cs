using System.Net.Http.Json;
using KubeShop.Contracts;

namespace KubeShop.Notifications.Worker;

/// <summary>
/// Polls Orders.Api and "sends" a notification (a log line) for every order it hasn't seen yet.
/// </summary>
public class Worker(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<Worker> logger) : BackgroundService
{
    private readonly HashSet<Guid> _notified = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(configuration.GetValue("Notifications:PollSeconds", 5));
        var client = httpClientFactory.CreateClient("orders");
        logger.LogInformation("Polling {OrdersUrl} every {Interval}", client.BaseAddress, interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                var orders = await client.GetFromJsonAsync<OrderPlaced[]>("/orders", stoppingToken) ?? [];
                logger.LogDebug("Fetched {Count} orders", orders.Length);

                foreach (var order in orders)
                {
                    if (_notified.Add(order.OrderId))
                    {
                        logger.LogInformation(
                            "Notification sent for order {OrderId}: {Quantity} item(s), total {TotalPrice}",
                            order.OrderId, order.Quantity, order.TotalPrice);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Orders API is unavailable: {Message}", ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
