using BankWithdrawal.Api.Application.Models;
using BankWithdrawal.Api.Infrastructure;

namespace BankWithdrawal.Api.Application.Services
{
    public class WithdrawalService
    {
        private readonly IAccountRepository _accountRepository;

        public WithdrawalService(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public async Task<WithdrawalResult> WithdrawAsync(long accountId,decimal amount,CancellationToken cancellationToken)
        {
            return await _accountRepository.WithdrawAsync(accountId, amount,cancellationToken);
        }
    }
}
