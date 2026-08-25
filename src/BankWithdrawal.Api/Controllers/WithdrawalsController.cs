using BankWithdrawal.Api.Application.Services;
using BankWithdrawal.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BankWithdrawal.Api.Controllers
{
    
    [ApiController]
    [Route("api/accounts/{accountId:long}/withdrawals")]
    public class WithdrawalsController : ControllerBase
    {
        private readonly WithdrawalService _withdrawalService;

        public WithdrawalsController(WithdrawalService withdrawalService)
        {
            _withdrawalService = withdrawalService;
        }

        [HttpPost]
        public async Task<IActionResult> Withdraw(
            long accountId,
            [FromBody] WithdrawRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _withdrawalService.WithdrawAsync(accountId,request.Amount,cancellationToken);

            if (!result.Successful)
            {
                return BadRequest(new
                {
                    message = "Withdrawal could not be completed."
                });
            }

            return Ok(new
            {
                withdrawalId = result.WithdrawalId,
                accountId,
                amount = request.Amount,
                status = "Successful"
            });
        }
    }

}
