using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeHub.Application.Abstractions;

namespace RecipeHub.Infrastructure.Messaging;

public sealed class PublishingOptions
{
    public const string SectionName = "Publishing";

    /// <summary>
    /// Publish mode: console | kafka | sns | both.
    /// Prefer PLAN env <c>PUBLISH_MODE</c>; falls back to <c>Publishing:Mode</c>.
    /// <c>both</c> means Kafka + SNS (not console).
    /// </summary>
    public string Mode { get; set; } = "console";

    public string Source { get; set; } = "urn:lifeatlas:recipe-hub";

    /// <summary>How often the outbox dispatcher polls for pending rows (default 2s).</summary>
    public int DispatcherIntervalSeconds { get; set; } = 2;

    public int DispatcherBatchSize { get; set; } = 50;

    /// <summary>Max publish attempts before a row is marked Failed (DB-side DLQ).</summary>
    public int MaxPublishAttempts { get; set; } = 5;

    public KafkaOptions Kafka { get; set; } = new();
    public SnsOptions Sns { get; set; } = new();
}

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "lifeatlas.recipes";
    public string ClientId { get; set; } = "recipe-hub";
}

public sealed class SnsOptions
{
    /// <summary>SNS topic ARN (required when mode is sns or both).</summary>
    public string TopicArn { get; set; } = "";

    public string Region { get; set; } = "eu-west-1";

    /// <summary>Optional LocalStack / custom endpoint (e.g. http://localhost:4566).</summary>
    public string? ServiceUrl { get; set; }

    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}

public interface IKafkaEventPublisher : IEventPublisher;

public interface ISnsEventPublisher : IEventPublisher;

/// <summary>Local publisher: writes CloudEvent JSON to the console (and logs).</summary>
public sealed class ConsoleEventPublisher(ILogger<ConsoleEventPublisher> logger) : IEventPublisher
{
    private readonly object _gate = new();
    private readonly StringBuilder _captured = new();

    public string CapturedOutput
    {
        get
        {
            lock (_gate)
                return _captured.ToString();
        }
    }

    public void ClearCapturedOutput()
    {
        lock (_gate)
            _captured.Clear();
    }

    public Task PublishAsync(string eventType, string cloudEventJson, CancellationToken cancellationToken = default)
    {
        var line = $"[RecipeHub outbox] {eventType} {cloudEventJson}";
        lock (_gate)
            _captured.AppendLine(line);
        Console.WriteLine(line);
        logger.LogInformation("Published {EventType}: {CloudEvent}", eventType, cloudEventJson);
        return Task.CompletedTask;
    }
}

/// <summary>Routes to console, Kafka, SNS, or Kafka+SNS based on <see cref="PublishingOptions.Mode"/>.</summary>
public sealed class ConfiguredEventPublisher(
    IOptions<PublishingOptions> options,
    ConsoleEventPublisher console,
    IKafkaEventPublisher kafka,
    ISnsEventPublisher sns) : IEventPublisher
{
    public async Task PublishAsync(string eventType, string cloudEventJson, CancellationToken cancellationToken = default)
    {
        var mode = NormalizeMode(options.Value.Mode);
        switch (mode)
        {
            case "console":
                await console.PublishAsync(eventType, cloudEventJson, cancellationToken);
                return;
            case "kafka":
                await kafka.PublishAsync(eventType, cloudEventJson, cancellationToken);
                return;
            case "sns":
                await sns.PublishAsync(eventType, cloudEventJson, cancellationToken);
                return;
            case "both":
                // Kafka first, then SNS — either failure leaves the outbox pending for retry.
                await kafka.PublishAsync(eventType, cloudEventJson, cancellationToken);
                await sns.PublishAsync(eventType, cloudEventJson, cancellationToken);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unknown PUBLISH_MODE '{options.Value.Mode}'. Expected console|kafka|sns|both.");
        }
    }

    public static string NormalizeMode(string? mode) =>
        (mode ?? "console").Trim().ToLowerInvariant();
}

/// <summary>Prefer data.recipeId for partition key; fall back to CloudEvent id.</summary>
public static class CloudEventKey
{
    public static string Extract(string cloudEventJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(cloudEventJson);
            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("recipeId", out var recipeId)
                && recipeId.ValueKind == JsonValueKind.String)
            {
                return recipeId.GetString()!;
            }

            if (doc.RootElement.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String)
            {
                return id.GetString()!;
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return Guid.NewGuid().ToString();
    }
}
