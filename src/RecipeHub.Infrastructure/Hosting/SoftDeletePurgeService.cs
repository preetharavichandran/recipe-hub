using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeHub.Infrastructure.DependencyInjection;
using RecipeHub.Infrastructure.Persistence;

namespace RecipeHub.Infrastructure.Hosting;

/// <summary>Hard-deletes soft-deleted recipes past the retention window. Postgres has no native row TTL.</summary>
public sealed class SoftDeletePurgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<SoftDeletePurgeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.PurgeIntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Soft-delete purge failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PurgeOnceAsync(CancellationToken ct)
    {
        var days = Math.Max(1, options.Value.SoftDeleteRetentionDays);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeHubDbContext>();

        var expired = await db.Recipes
            .Where(r => r.DeletedAt != null && r.DeletedAt < cutoff)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return;

        db.Recipes.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Purged {Count} soft-deleted recipes older than {Days} days", expired.Count, days);
    }
}
