namespace BankWithdrawal.Api.Application.Models
{
    public class WithdrawalResult
    {
        public bool Successful { get; set; }
        public Guid? WithdrawalId { get; set; }
    }
}
