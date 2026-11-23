using Microsoft.Data.SqlClient;

namespace MaintainIQ.Data.Connection;

public interface IDbConnectionFactory
{
    Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
