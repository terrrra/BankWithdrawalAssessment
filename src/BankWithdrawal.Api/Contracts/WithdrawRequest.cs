using System.ComponentModel.DataAnnotations;

namespace BankWithdrawal.Api.Contracts
{
    public class WithdrawRequest
    {
        [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
        public decimal Amount { get; set; }
    }
}
