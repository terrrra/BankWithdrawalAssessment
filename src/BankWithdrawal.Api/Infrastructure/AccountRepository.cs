using BankWithdrawal.Api.Application.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace BankWithdrawal.Api.Infrastructure
{
    public class AccountRepository : IAccountRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AccountRepository> _logger;

        public AccountRepository(
            IConfiguration configuration,
            ILogger<AccountRepository> logger)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is not configured.");

            _logger = logger;
        }

        public async Task<WithdrawalResult> WithdrawAsync(
            long accountId,
            decimal amount,
            CancellationToken cancellationToken)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                #region Atomic Balance Update

                const string updateAccountSql = """
                    UPDATE Accounts
                    SET Balance = Balance - @Amount
                    WHERE Id = @AccountId
                      AND Balance >= @Amount;
                    """;

                await using var updateCommand =
                    new SqlCommand(
                        updateAccountSql,
                        connection,
                        transaction);

                AddAmountParameter(updateCommand, amount);

                updateCommand.Parameters
                    .Add("@AccountId", SqlDbType.BigInt)
                    .Value = accountId;

                var rowsAffected =
                    await updateCommand.ExecuteNonQueryAsync(
                        cancellationToken);

                if (rowsAffected == 0)
                {
                    var accountExists = await AccountExistsAsync(
                        accountId,
                        connection,
                        transaction,
                        cancellationToken);

                    await transaction.RollbackAsync(
                        cancellationToken);

                    if (!accountExists)
                    {
                        _logger.LogWarning(
                            "Withdrawal rejected because AccountId {AccountId} does not exist.",
                            accountId);

                        return new WithdrawalResult
                        {
                            Outcome = WithdrawalOutcome.AccountNotFound
                        };
                    }

                    _logger.LogWarning(
                        "Withdrawal rejected because AccountId {AccountId} has insufficient funds.",
                        accountId);

                    return new WithdrawalResult
                    {
                        Outcome = WithdrawalOutcome.InsufficientFunds
                    };
                }

                #endregion


                #region Create Transaction Identifiers

                var withdrawalId = Guid.NewGuid();
                var eventId = Guid.NewGuid();
                var createdAtUtc = DateTime.UtcNow;

                #endregion


                #region Audit Withdrawal

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
                    new SqlCommand(
                        insertWithdrawalSql,
                        connection,
                        transaction);

                withdrawalCommand.Parameters
                    .Add("@Id", SqlDbType.UniqueIdentifier)
                    .Value = withdrawalId;

                withdrawalCommand.Parameters
                    .Add("@AccountId", SqlDbType.BigInt)
                    .Value = accountId;

                AddAmountParameter(
                    withdrawalCommand,
                    amount);

                withdrawalCommand.Parameters
                    .Add("@Status", SqlDbType.NVarChar, 30)
                    .Value = "Successful";

                withdrawalCommand.Parameters
                    .Add("@CreatedAtUtc", SqlDbType.DateTime2)
                    .Value = createdAtUtc;

                await withdrawalCommand.ExecuteNonQueryAsync(
                    cancellationToken);

                #endregion


                #region Transactional Outbox

                var withdrawalEvent = new
                {
                    EventId = eventId,
                    WithdrawalId = withdrawalId,
                    AccountId = accountId,
                    Amount = amount,
                    Status = "Successful",
                    OccurredAtUtc = createdAtUtc
                };

                var payload =
                    JsonSerializer.Serialize(withdrawalEvent);

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
                    new SqlCommand(
                        insertOutboxSql,
                        connection,
                        transaction);

                outboxCommand.Parameters
                    .Add("@Id", SqlDbType.UniqueIdentifier)
                    .Value = eventId;

                outboxCommand.Parameters
                    .Add("@EventType", SqlDbType.NVarChar, 100)
                    .Value = "WithdrawalCompleted";

                outboxCommand.Parameters
                    .Add("@Payload", SqlDbType.NVarChar, -1)
                    .Value = payload;

                outboxCommand.Parameters
                    .Add("@CreatedAtUtc", SqlDbType.DateTime2)
                    .Value = createdAtUtc;

                await outboxCommand.ExecuteNonQueryAsync(
                    cancellationToken);

                #endregion


                #region Commit Transaction

                await transaction.CommitAsync(
                    cancellationToken);

                _logger.LogInformation(
                    "Withdrawal {WithdrawalId} completed for AccountId {AccountId}.",
                    withdrawalId,
                    accountId);

                return new WithdrawalResult
                {
                    Outcome = WithdrawalOutcome.Successful,
                    WithdrawalId = withdrawalId
                };

                #endregion
            }
            catch (Exception ex)
            {
                #region Rollback

                try
                {
                    await transaction.RollbackAsync(
                        cancellationToken);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(
                        rollbackException,
                        "Rollback failed for AccountId {AccountId}.",
                        accountId);
                }

                _logger.LogError(
                    ex,
                    "Withdrawal transaction failed for AccountId {AccountId}.",
                    accountId);

                throw;

                #endregion
            }
        }

        #region Helper Methods

        private static async Task<bool> AccountExistsAsync(
            long accountId,
            SqlConnection connection,
            SqlTransaction transaction,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT COUNT(1)
                FROM Accounts
                WHERE Id = @AccountId;
                """;

            await using var command =
                new SqlCommand(
                    sql,
                    connection,
                    transaction);

            command.Parameters
                .Add("@AccountId", SqlDbType.BigInt)
                .Value = accountId;

            var result =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            return Convert.ToInt32(result) > 0;
        }

        private static void AddAmountParameter(
            SqlCommand command,
            decimal amount)
        {
            var parameter =
                command.Parameters.Add(
                    "@Amount",
                    SqlDbType.Decimal);

            parameter.Precision = 18;
            parameter.Scale = 2;
            parameter.Value = amount;
        }

        #endregion
    }
}