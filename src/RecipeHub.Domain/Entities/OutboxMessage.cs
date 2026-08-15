using RecipeHub.Domain.Enums;

namespace RecipeHub.Domain.Entities;

/// <summary>Transactional outbox row holding a serialized CloudEvent pending publish.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public required string EventType { get; set; }
    public Guid AggregateId { get; set; }
    /// <summary>Full CloudEvents JSON body.</summary>
    public required string Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public DateTimeOffset? PublishedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        Status = OutboxStatus.Published;
        PublishedAt = publishedAt;
        LastError = null;
    }

    public void MarkAttemptFailed(string error, int maxAttempts, DateTimeOffset attemptedAt)
    {
        AttemptCount++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        if (AttemptCount >= maxAttempts)
        {
            Status = OutboxStatus.Failed;
            PublishedAt = null;
        }
        else
        {
            Status = OutboxStatus.Pending;
        }

        _ = attemptedAt;
    }
}
