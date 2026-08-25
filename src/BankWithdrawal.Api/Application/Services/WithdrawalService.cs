using BankWithdrawal.Api.Application.Models;
using BankWithdrawal.Api.Infrastructure;

namespace BankWithdrawal.Api.Application.Services
{
    public class WithdrawalService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ILogger<WithdrawalService> _logger;

        public WithdrawalService(
            IAccountRepository accountRepository,
            ILogger<WithdrawalService> logger)
        {
            _accountRepository = accountRepository;
            _logger = logger;
        }

        public async Task<WithdrawalResult> WithdrawAsync(
            long accountId,
            decimal amount,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Processing withdrawal for AccountId {AccountId}.",
                accountId);

            var result =
                await _accountRepository.WithdrawAsync(
                    accountId,
                    amount,
                    cancellationToken);

            _logger.LogInformation(
                "Withdrawal completed with outcome {Outcome} for AccountId {AccountId}.",
                result.Outcome,
                accountId);

            return result;
        }
    }
}