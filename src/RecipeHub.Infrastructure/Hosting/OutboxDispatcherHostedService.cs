using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeHub.Application.Abstractions;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;
using RecipeHub.Infrastructure.Messaging;
using RecipeHub.Infrastructure.Persistence;

namespace RecipeHub.Infrastructure.Hosting;

public sealed class EfOutboxStore(RecipeHubDbContext db) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        return await db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}

public sealed class OutboxDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PublishingOptions> options,
    ILogger<OutboxDispatcherHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.DispatcherIntervalSeconds));
        var batchSize = Math.Max(1, options.Value.DispatcherBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var published = await dispatcher.DispatchPendingAsync(
                    batchSize,
                    Math.Max(1, options.Value.MaxPublishAttempts),
                    stoppingToken);
                if (published > 0)
                    logger.LogInformation("Outbox dispatcher published {Count} message(s)", published);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatcher failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
