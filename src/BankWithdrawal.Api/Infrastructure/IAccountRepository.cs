using BankWithdrawal.Api.Application.Models;

namespace BankWithdrawal.Api.Infrastructure
{
    public interface IAccountRepository
    {
        Task<WithdrawalResult> WithdrawAsync(long accountId,decimal amount,CancellationToken cancellationToken);
    }
}
