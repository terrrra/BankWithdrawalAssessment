
namespace BankWithdrawal.Api.BackgroundServices
{
    public class OutboxDispatcher : BackgroundService
    {
        // ...
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
