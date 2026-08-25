namespace BankWithdrawal.Api.Infrastructure.Messaging
{
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;

    namespace BankWithdrawal.Api.Infrastructure.Messaging
    {
        public class SnsEventPublisher : IEventPublisher
        {
            private readonly IAmazonSimpleNotificationService _snsClient;
            private readonly IConfiguration _configuration;
            private readonly ILogger<SnsEventPublisher> _logger;

            public SnsEventPublisher(
                IAmazonSimpleNotificationService snsClient,
                IConfiguration configuration,
                ILogger<SnsEventPublisher> logger)
            {
                _snsClient = snsClient;
                _configuration = configuration;
                _logger = logger;
            }

            public async Task PublishAsync(
                string eventType,
                string payload,
                CancellationToken cancellationToken)
            {
                var topicArn =
                    _configuration["AWS:SnsTopicArn"]
                    ?? throw new InvalidOperationException(
                        "AWS:SnsTopicArn is not configured.");

                var request = new PublishRequest
                {
                    TopicArn = topicArn,
                    Message = payload,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        ["EventType"] = new MessageAttributeValue
                        {
                            DataType = "String",
                            StringValue = eventType
                        }
                    }
                };

                var response =
                    await _snsClient.PublishAsync(
                        request,
                        cancellationToken);

                _logger.LogInformation(
                    "Published event {EventType} to SNS. MessageId {MessageId}.",
                    eventType,
                    response.MessageId);
            }
        }
    }
}
