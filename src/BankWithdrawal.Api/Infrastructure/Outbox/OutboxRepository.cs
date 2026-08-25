using BankWithdrawal.Api.Application.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BankWithdrawal.Api.Infrastructure.Outbox
{
    public class OutboxRepository : IOutboxRepository
    {
        private readonly string _connectionString;

        public OutboxRepository(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is not configured.");
        }

        #region Get Pending Messages

        public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT TOP (20)
                    Id,
                    EventType,
                    Payload,
                    CreatedAtUtc,
                    PublishedAtUtc
                FROM OutboxMessages
                WHERE PublishedAtUtc IS NULL
                ORDER BY CreatedAtUtc;
                """;

            var messages = new List<OutboxMessage>();

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command =
                new SqlCommand(sql, connection);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                messages.Add(new OutboxMessage
                {
                    Id = reader.GetGuid(
                        reader.GetOrdinal("Id")),

                    EventType = reader.GetString(
                        reader.GetOrdinal("EventType")),

                    Payload = reader.GetString(
                        reader.GetOrdinal("Payload")),

                    CreatedAtUtc = reader.GetDateTime(
                        reader.GetOrdinal("CreatedAtUtc")),

                    PublishedAtUtc =
                        reader.IsDBNull(reader.GetOrdinal("PublishedAtUtc"))? null
                        : reader.GetDateTime(reader.GetOrdinal("PublishedAtUtc"))
                });
            }

            return messages;
        }

        #endregion


        #region Mark Message As Published

        public async Task MarkAsPublishedAsync(Guid messageId, CancellationToken cancellationToken)
        {
            const string sql = """
                UPDATE OutboxMessages
                SET PublishedAtUtc = @PublishedAtUtc
                WHERE Id = @Id
                  AND PublishedAtUtc IS NULL;
                """;

            await using var connection = new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);

            command.Parameters
                .Add("@Id", SqlDbType.UniqueIdentifier)
                .Value = messageId;

            command.Parameters
                .Add("@PublishedAtUtc", SqlDbType.DateTime2)
                .Value = DateTime.UtcNow;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        #endregion
    }
}