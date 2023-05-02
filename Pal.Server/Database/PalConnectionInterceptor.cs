using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pal.Server.Database
{
    internal sealed class PalConnectionInterceptor : DbConnectionInterceptor
    {
        private const string SetBusyTimeout = "PRAGMA busy_timeout = 5000;";

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var command = connection.CreateCommand();
            command.CommandText = SetBusyTimeout;
            command.ExecuteNonQuery();
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = SetBusyTimeout;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
