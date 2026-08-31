using Microsoft.Data.Sqlite;
using PayMaestro.FakeProvider.Contracts;

namespace PayMaestro.FakeProvider.Ledger;

/// <summary>
/// The acquirer's own record of what it already did for an idempotency key. It survives a
/// restart of the fake provider, so a key settled before a crash is still recognised after it.
/// The first outcome written for a key wins: a second charge on the same key returns that
/// outcome instead of moving money again.
/// </summary>
public sealed class ChargeLedger
{
    private readonly string _connectionString;

    public ChargeLedger(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Ledger") ?? "Data Source=fake-provider.db";
        Initialize();
    }

    /// <summary>Writes the outcome for a key, or returns the outcome already written for it.</summary>
    public ChargeResponse Settle(string idempotencyKey, string gatewayName, ChargeResponse outcome)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT OR IGNORE INTO Charge (IdempotencyKey, GatewayName, ResultType, ResponseCode, Message, CreatedAt)
            VALUES ($key, $gateway, $resultType, $responseCode, $message, $createdAt);
            """;
        insert.Parameters.AddWithValue("$key", idempotencyKey);
        insert.Parameters.AddWithValue("$gateway", gatewayName);
        insert.Parameters.AddWithValue("$resultType", outcome.ResultType);
        insert.Parameters.AddWithValue("$responseCode", outcome.ResponseCode);
        insert.Parameters.AddWithValue("$message", outcome.Message);
        insert.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();

        return Find(idempotencyKey) ?? outcome;
    }

    public ChargeResponse? Find(string idempotencyKey)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand select = connection.CreateCommand();
        select.CommandText =
            "SELECT ResultType, ResponseCode, Message FROM Charge WHERE IdempotencyKey = $key;";
        select.Parameters.AddWithValue("$key", idempotencyKey);

        using SqliteDataReader reader = select.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ChargeResponse
        {
            ResultType = reader.GetString(0),
            ResponseCode = reader.GetString(1),
            Message = reader.GetString(2)
        };
    }

    private void Initialize()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Charge (
                IdempotencyKey TEXT NOT NULL PRIMARY KEY,
                GatewayName    TEXT NOT NULL,
                ResultType     TEXT NOT NULL,
                ResponseCode   TEXT NOT NULL,
                Message        TEXT NOT NULL,
                CreatedAt      TEXT NOT NULL
            );
            """;
        create.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }
}
