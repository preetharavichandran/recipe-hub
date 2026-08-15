using FluentAssertions;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Domain.Tests;

public class OutboxMessageTests
{
    [Fact]
    public void MarkPublished_sets_status_and_clears_error()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "lifeatlas.recipe.created",
            AggregateId = Guid.NewGuid(),
            Payload = "{}",
            OccurredAt = DateTimeOffset.UtcNow,
            Status = OutboxStatus.Pending,
            AttemptCount = 2,
            LastError = "previous"
        };

        var when = DateTimeOffset.Parse("2026-08-14T16:00:00Z");
        message.MarkPublished(when);

        message.Status.Should().Be(OutboxStatus.Published);
        message.PublishedAt.Should().Be(when);
        message.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkAttemptFailed_keeps_pending_until_max_attempts()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "lifeatlas.recipe.updated",
            AggregateId = Guid.NewGuid(),
            Payload = "{}",
            OccurredAt = DateTimeOffset.UtcNow
        };

        message.MarkAttemptFailed("boom", maxAttempts: 3, DateTimeOffset.UtcNow);
        message.Status.Should().Be(OutboxStatus.Pending);
        message.AttemptCount.Should().Be(1);

        message.MarkAttemptFailed("boom", maxAttempts: 3, DateTimeOffset.UtcNow);
        message.MarkAttemptFailed("boom", maxAttempts: 3, DateTimeOffset.UtcNow);

        message.Status.Should().Be(OutboxStatus.Failed);
        message.AttemptCount.Should().Be(3);
        message.LastError.Should().Be("boom");
    }
}
