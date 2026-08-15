using Microsoft.Extensions.Logging;
using RecipeHub.Application.Abstractions;

namespace RecipeHub.Application.Services;

public sealed class OutboxDispatchService(
    IOutboxStore store,
    IEventPublisher publisher,
    ILogger<OutboxDispatchService> logger) : IOutboxDispatcher
{
    public const int DefaultMaxAttempts = 5;

    public async Task<int> DispatchPendingAsync(
        int batchSize = 50,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken cancellationToken = default)
    {
        var pending = await store.ClaimPendingAsync(batchSize, cancellationToken);
        if (pending.Count == 0)
            return 0;

        var published = 0;
        var now = DateTimeOffset.UtcNow;
        var attempts = Math.Max(1, maxAttempts);

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(message.EventType, message.Payload, cancellationToken);
                message.MarkPublished(now);
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to publish outbox message {OutboxId} ({EventType})",
                    message.Id, message.EventType);
                message.MarkAttemptFailed(ex.Message, attempts, now);
            }
        }

        await store.SaveAsync(cancellationToken);
        return published;
    }
}
