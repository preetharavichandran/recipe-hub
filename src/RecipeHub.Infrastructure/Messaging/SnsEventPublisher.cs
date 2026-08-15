using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RecipeHub.Infrastructure.Messaging;

/// <summary>Publishes CloudEvents JSON to an SNS topic.</summary>
public sealed class SnsEventPublisher : ISnsEventPublisher, IDisposable
{
    private readonly IOptions<PublishingOptions> _options;
    private readonly ILogger<SnsEventPublisher> _logger;
    private readonly IAmazonSimpleNotificationService? _injectedClient;
    private readonly object _gate = new();
    private IAmazonSimpleNotificationService? _ownedClient;

    public SnsEventPublisher(IOptions<PublishingOptions> options, ILogger<SnsEventPublisher> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>Test seam — inject an SNS client.</summary>
    public SnsEventPublisher(
        IOptions<PublishingOptions> options,
        ILogger<SnsEventPublisher> logger,
        IAmazonSimpleNotificationService client)
    {
        _options = options;
        _logger = logger;
        _injectedClient = client;
    }

    public async Task PublishAsync(string eventType, string cloudEventJson, CancellationToken cancellationToken = default)
    {
        var sns = _options.Value.Sns;
        if (string.IsNullOrWhiteSpace(sns.TopicArn))
            throw new InvalidOperationException(
                "Publishing:Sns:TopicArn is required when PUBLISH_MODE is sns or both.");

        var client = _injectedClient ?? GetOrCreateClient(sns);
        var request = new PublishRequest
        {
            TopicArn = sns.TopicArn,
            Subject = eventType.Length <= 100 ? eventType : eventType[..100],
            Message = cloudEventJson,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["eventType"] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = eventType
                }
            }
        };

        var response = await client.PublishAsync(request, cancellationToken);
        _logger.LogInformation(
            "Published {EventType} to SNS topic {TopicArn} messageId {MessageId}",
            eventType, sns.TopicArn, response.MessageId);
    }

    private IAmazonSimpleNotificationService GetOrCreateClient(SnsOptions sns)
    {
        if (_ownedClient is not null)
            return _ownedClient;

        lock (_gate)
        {
            return _ownedClient ??= CreateClient(sns);
        }
    }

    internal static IAmazonSimpleNotificationService CreateClient(SnsOptions sns)
    {
        var config = new AmazonSimpleNotificationServiceConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(sns.Region)
        };

        if (!string.IsNullOrWhiteSpace(sns.ServiceUrl))
        {
            config.ServiceURL = sns.ServiceUrl;
            config.AuthenticationRegion = sns.Region;
        }

        if (!string.IsNullOrWhiteSpace(sns.AccessKey) && !string.IsNullOrWhiteSpace(sns.SecretKey))
        {
            var credentials = new BasicAWSCredentials(sns.AccessKey, sns.SecretKey);
            return new AmazonSimpleNotificationServiceClient(credentials, config);
        }

        return new AmazonSimpleNotificationServiceClient(config);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _ownedClient?.Dispose();
            _ownedClient = null;
        }
    }
}
