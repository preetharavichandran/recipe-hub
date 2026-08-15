using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeHub.Application.Abstractions;
using RecipeHub.Application.Services;
using RecipeHub.Contracts.Events;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Enums;

namespace RecipeHub.Application.Tests;

public class OutboxDispatchServiceTests
{
    [Fact]
    public async Task DispatchPending_publishes_and_marks_published()
    {
        var message = PendingMessage(RecipeEventTypes.Created);
        var store = new FakeOutboxStore([message]);
        var publisher = new FakePublisher();
        var sut = new OutboxDispatchService(store, publisher, NullLogger<OutboxDispatchService>.Instance);

        var count = await sut.DispatchPendingAsync();

        count.Should().Be(1);
        publisher.Published.Should().ContainSingle()
            .Which.EventType.Should().Be(RecipeEventTypes.Created);
        message.Status.Should().Be(OutboxStatus.Published);
        message.PublishedAt.Should().NotBeNull();
        store.Saved.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchPending_on_failure_increments_attempts_and_stays_pending()
    {
        var message = PendingMessage(RecipeEventTypes.Updated);
        var store = new FakeOutboxStore([message]);
        var publisher = new FakePublisher { ThrowOnPublish = true };
        var sut = new OutboxDispatchService(store, publisher, NullLogger<OutboxDispatchService>.Instance);

        var count = await sut.DispatchPendingAsync();

        count.Should().Be(0);
        message.Status.Should().Be(OutboxStatus.Pending);
        message.AttemptCount.Should().Be(1);
        message.LastError.Should().NotBeNullOrWhiteSpace();
        store.Saved.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchPending_marks_failed_after_max_attempts()
    {
        var message = PendingMessage(RecipeEventTypes.Deleted);
        message.AttemptCount = OutboxDispatchService.DefaultMaxAttempts - 1;
        var store = new FakeOutboxStore([message]);
        var publisher = new FakePublisher { ThrowOnPublish = true };
        var sut = new OutboxDispatchService(store, publisher, NullLogger<OutboxDispatchService>.Instance);

        await sut.DispatchPendingAsync();

        message.Status.Should().Be(OutboxStatus.Failed);
        message.AttemptCount.Should().Be(OutboxDispatchService.DefaultMaxAttempts);
    }

    [Fact]
    public async Task DispatchPending_returns_zero_when_empty()
    {
        var store = new FakeOutboxStore([]);
        var publisher = new FakePublisher();
        var sut = new OutboxDispatchService(store, publisher, NullLogger<OutboxDispatchService>.Instance);

        var count = await sut.DispatchPendingAsync();

        count.Should().Be(0);
        publisher.Published.Should().BeEmpty();
        store.Saved.Should().BeFalse();
    }

    private static OutboxMessage PendingMessage(string eventType) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            AggregateId = Guid.NewGuid(),
            Payload = """{"specversion":"1.0","type":"x"}""",
            OccurredAt = DateTimeOffset.UtcNow,
            Status = OutboxStatus.Pending
        };

    private sealed class FakeOutboxStore(IReadOnlyList<OutboxMessage> pending) : IOutboxStore
    {
        public bool Saved { get; private set; }

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>(pending.Take(batchSize).ToList());

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePublisher : IEventPublisher
    {
        public bool ThrowOnPublish { get; set; }
        public List<(string EventType, string Json)> Published { get; } = [];

        public Task PublishAsync(string eventType, string cloudEventJson, CancellationToken cancellationToken)
        {
            if (ThrowOnPublish)
                throw new InvalidOperationException("broker unavailable");
            Published.Add((eventType, cloudEventJson));
            return Task.CompletedTask;
        }
    }
}
