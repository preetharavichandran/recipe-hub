using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RecipeHub.Infrastructure.Messaging;

/// <summary>Publishes CloudEvents JSON to a Kafka topic (key = recipeId when present).</summary>
public sealed class KafkaEventPublisher(
    IOptions<PublishingOptions> options,
    ILogger<KafkaEventPublisher> logger) : IKafkaEventPublisher, IDisposable
{
    private readonly object _gate = new();
    private IProducer<string, string>? _producer;

    public async Task PublishAsync(string eventType, string cloudEventJson, CancellationToken cancellationToken = default)
    {
        var kafka = options.Value.Kafka;
        var producer = GetOrCreateProducer(kafka);
        var key = CloudEventKey.Extract(cloudEventJson);
        var message = new Message<string, string>
        {
            Key = key,
            Value = cloudEventJson,
            Headers =
            [
                new Header("ce_type", System.Text.Encoding.UTF8.GetBytes(eventType))
            ]
        };

        var result = await producer.ProduceAsync(kafka.Topic, message, cancellationToken);
        logger.LogInformation(
            "Published {EventType} to Kafka topic {Topic} partition {Partition} offset {Offset}",
            eventType, kafka.Topic, result.Partition.Value, result.Offset.Value);
    }

    private IProducer<string, string> GetOrCreateProducer(KafkaOptions kafka)
    {
        if (_producer is not null)
            return _producer;

        lock (_gate)
        {
            if (_producer is not null)
                return _producer;

            var config = new ProducerConfig
            {
                BootstrapServers = kafka.BootstrapServers,
                ClientId = kafka.ClientId,
                Acks = Acks.All,
                EnableIdempotence = true
            };
            _producer = new ProducerBuilder<string, string>(config).Build();
            return _producer;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
            _producer = null;
        }
    }
}
