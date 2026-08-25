namespace BankWithdrawal.Api.Application.Models
{
    public enum WithdrawalOutcome
    {
        Successful,
        InsufficientFunds,
        AccountNotFound
    }

    public class WithdrawalResult
    {
        public WithdrawalOutcome Outcome { get; set; }
        public Guid? WithdrawalId { get; set; }
    }
}
