using BankWithdrawal.Api.Infrastructure.Messaging;
using BankWithdrawal.Api.Infrastructure.Outbox;

namespace BankWithdrawal.Api.BackgroundServices
{
    public class OutboxDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxDispatcher> _logger;

        public OutboxDispatcher(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox dispatcher started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope =_scopeFactory.CreateScope();

                    var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

                    var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                    var messages = await outboxRepository.GetPendingAsync(stoppingToken);

                    foreach (var message in messages)
                    {
                        try
                        {
                            await eventPublisher.PublishAsync(message.EventType, message.Payload, stoppingToken);

                            await outboxRepository.MarkAsPublishedAsync(message.Id,stoppingToken);

                            _logger.LogInformation("Outbox message {MessageId} published successfully.",message.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,"Failed to publish outbox message {MessageId}. It will be retried.",message.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,"Outbox dispatcher encountered an error.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken);
            }
        }
    }
}