using BankWithdrawal.Api.Application.Models;

namespace BankWithdrawal.Api.Infrastructure.Outbox
{
    public interface IOutboxRepository
    {
        Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
            CancellationToken cancellationToken);

        Task MarkAsPublishedAsync(
            Guid messageId,
            CancellationToken cancellationToken);
    }
}
