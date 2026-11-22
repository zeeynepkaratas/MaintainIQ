using Microsoft.Data.SqlClient;

namespace MaintainIQ.Data.Connection;

public sealed class SqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    private readonly string _connectionString = string.IsNullOrWhiteSpace(connectionString)
        ? throw new ArgumentException("Bağlantı dizesi zorunludur.", nameof(connectionString))
        : connectionString;

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
