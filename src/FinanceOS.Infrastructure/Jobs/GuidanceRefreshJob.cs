using FinanceOS.Infrastructure.Data;
using FinanceOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinanceOS.Infrastructure.Jobs;

public sealed class GuidanceRefreshJob(IServiceProvider services, ILogger<GuidanceRefreshJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                if (await db.Owners.AnyAsync(stoppingToken))
                {
                    var finance = scope.ServiceProvider.GetRequiredService<FinanceOsService>();
                    await finance.ActionsAsync(stoppingToken);
                    await finance.AlertsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Guidance refresh skipped.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
