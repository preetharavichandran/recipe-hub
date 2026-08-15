using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeHub.Application.Abstractions;
using RecipeHub.Contracts.Events;
using RecipeHub.Infrastructure.Messaging;

namespace RecipeHub.Application.Tests;

public class ConfiguredEventPublisherTests
{
    [Theory]
    [InlineData("console", true, false, false)]
    [InlineData("kafka", false, true, false)]
    [InlineData("sns", false, false, true)]
    [InlineData("both", false, true, true)]
    [InlineData("CONSOLE", true, false, false)]
    public async Task Publish_routes_by_mode(string mode, bool toConsole, bool toKafka, bool toSns)
    {
        var console = new ConsoleEventPublisher(NullLogger<ConsoleEventPublisher>.Instance);
        var kafka = new RecordingPublisher();
        var sns = new RecordingPublisher();
        var sut = new ConfiguredEventPublisher(
            Options.Create(new PublishingOptions { Mode = mode }),
            console,
            kafka,
            sns);

        await sut.PublishAsync(RecipeEventTypes.Created, """{"specversion":"1.0","id":"1"}""");

        (console.CapturedOutput.Length > 0).Should().Be(toConsole);
        kafka.Calls.Should().HaveCount(toKafka ? 1 : 0);
        sns.Calls.Should().HaveCount(toSns ? 1 : 0);
    }

    [Fact]
    public async Task Both_publishes_kafka_then_sns_and_fails_if_kafka_fails()
    {
        var console = new ConsoleEventPublisher(NullLogger<ConsoleEventPublisher>.Instance);
        var kafka = new RecordingPublisher { Throw = true };
        var sns = new RecordingPublisher();
        var sut = new ConfiguredEventPublisher(
            Options.Create(new PublishingOptions { Mode = "both" }),
            console,
            kafka,
            sns);

        var act = () => sut.PublishAsync(RecipeEventTypes.Updated, "{}");
        await act.Should().ThrowAsync<InvalidOperationException>();
        sns.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_mode_throws()
    {
        var sut = new ConfiguredEventPublisher(
            Options.Create(new PublishingOptions { Mode = "rabbit" }),
            new ConsoleEventPublisher(NullLogger<ConsoleEventPublisher>.Instance),
            new RecordingPublisher(),
            new RecordingPublisher());

        var act = () => sut.PublishAsync(RecipeEventTypes.Deleted, "{}");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*console|kafka|sns|both*");
    }

    [Fact]
    public void CloudEventKey_prefers_recipeId()
    {
        var json = """
            {"specversion":"1.0","id":"evt-1","data":{"recipeId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}}
            """;
        CloudEventKey.Extract(json).Should().Be("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    }

    [Fact]
    public void CloudEventKey_falls_back_to_event_id()
    {
        var json = """{"specversion":"1.0","id":"evt-42","data":{"author":"x"}}""";
        CloudEventKey.Extract(json).Should().Be("evt-42");
    }

    [Fact]
    public async Task Sns_requires_topic_arn()
    {
        var sut = new SnsEventPublisher(
            Options.Create(new PublishingOptions { Sns = new SnsOptions { TopicArn = "" } }),
            NullLogger<SnsEventPublisher>.Instance);

        var act = () => sut.PublishAsync(RecipeEventTypes.Created, "{}");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TopicArn*");
    }

    private sealed class RecordingPublisher : IKafkaEventPublisher, ISnsEventPublisher
    {
        public bool Throw { get; set; }
        public List<(string Type, string Json)> Calls { get; } = [];

        public Task PublishAsync(string eventType, string cloudEventJson, CancellationToken cancellationToken)
        {
            if (Throw)
                throw new InvalidOperationException("broker down");
            Calls.Add((eventType, cloudEventJson));
            return Task.CompletedTask;
        }
    }
}
