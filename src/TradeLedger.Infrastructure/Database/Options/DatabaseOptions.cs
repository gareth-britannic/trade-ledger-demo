using Npgsql;

namespace TradeLedger.Infrastructure.Database.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string? ConnectionString { get; init; }

    public string? Host { get; init; }

    public int Port { get; init; } = 5432;

    public string Name { get; init; } = "trade_ledger";

    public string? Username { get; init; }

    public string? Password { get; init; }

    internal bool IsValid() => !string.IsNullOrWhiteSpace(ConnectionString) ||
                               (!string.IsNullOrWhiteSpace(Host) &&
                                Port is > 0 and <= 65535 &&
                                !string.IsNullOrWhiteSpace(Name) &&
                                !string.IsNullOrWhiteSpace(Username) &&
                                !string.IsNullOrWhiteSpace(Password));

    internal string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return ConnectionString;
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Name,
            Username = Username,
            Password = Password
        }.ConnectionString;
    }
}
