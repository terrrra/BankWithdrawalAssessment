using BankWithdrawal.Api.Application;
using BankWithdrawal.Api.Application.Models;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace BankWithdrawal.Api.Infrastructure
{
    public class AccountRepository : IAccountRepository
    {
        private readonly string _connectionString;

        public AccountRepository(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is not configured.");
        }

        public async Task<WithdrawalResult> WithdrawAsync(long accountId, decimal amount, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                #region Update the Balance in accounts table
                const string updateAccountSql = """
                UPDATE Accounts
                SET Balance = Balance - @Amount
                WHERE Id = @AccountId
                AND Balance >= @Amount;
                """;

                await using var updateCommand =
                    new SqlCommand(updateAccountSql, connection, (SqlTransaction)transaction);

                updateCommand.Parameters.AddWithValue("@Amount", amount);
                updateCommand.Parameters.AddWithValue("@AccountId", accountId);

                var rowsAffected =
                    await updateCommand.ExecuteNonQueryAsync(cancellationToken);

                if (rowsAffected != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    return new WithdrawalResult
                    {
                        Successful = false
                    };
                }
                #endregion

                #region Audit the withdrawal in withdrawals table
                var withdrawalId = Guid.NewGuid();
                var eventId = Guid.NewGuid();
                var createdAtUtc = DateTime.UtcNow;

                const string insertWithdrawalSql = """
                INSERT INTO Withdrawals
                (
                    Id,
                    AccountId,
                    Amount,
                    Status,
                    CreatedAtUtc
                )
                VALUES
                (
                    @Id,
                    @AccountId,
                    @Amount,
                    @Status,
                    @CreatedAtUtc
                );
                """;

                await using var withdrawalCommand =
                    new SqlCommand(insertWithdrawalSql,connection,(SqlTransaction)transaction);

                withdrawalCommand.Parameters.AddWithValue("@Id", withdrawalId);
                withdrawalCommand.Parameters.AddWithValue("@AccountId", accountId);
                withdrawalCommand.Parameters.AddWithValue("@Amount", amount);
                withdrawalCommand.Parameters.AddWithValue("@Status", "Successful");
                withdrawalCommand.Parameters.AddWithValue("@CreatedAtUtc",createdAtUtc);

                await withdrawalCommand.ExecuteNonQueryAsync(cancellationToken);
                #endregion

                #region Outbox Pattern, we will insert the event into the OutboxMessages table
                var withdrawalEvent = new
                {
                    EventId = eventId,
                    WithdrawalId = withdrawalId,
                    AccountId = accountId,
                    Amount = amount,
                    Status = "Successful",
                    OccurredAtUtc = createdAtUtc
                };

                var payload = JsonSerializer.Serialize(withdrawalEvent);

                const string insertOutboxSql = """
                INSERT INTO OutboxMessages
                (
                    Id,
                    EventType,
                    Payload,
                    CreatedAtUtc,
                    PublishedAtUtc
                )
                VALUES
                (
                    @Id,
                    @EventType,
                    @Payload,
                    @CreatedAtUtc,
                    NULL
                );
                """;

                await using var outboxCommand =
                    new SqlCommand(insertOutboxSql,connection,(SqlTransaction)transaction);

                outboxCommand.Parameters.AddWithValue("@Id", eventId);
                outboxCommand.Parameters.AddWithValue("@EventType","WithdrawalCompleted");

                outboxCommand.Parameters.AddWithValue("@Payload", payload);
                outboxCommand.Parameters.AddWithValue("@CreatedAtUtc",createdAtUtc);

                await outboxCommand.ExecuteNonQueryAsync(cancellationToken);
                #endregion


                await transaction.CommitAsync(cancellationToken);

                return new WithdrawalResult
                {
                    Successful = true,
                    WithdrawalId = withdrawalId
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
