using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Amazoff.Api.Data;

public static class DatabaseSchemaBootstrapper
{
    public static async Task EnsureIdentityColumnsAsync(
        AmazoffDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == ConnectionState.Closed;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var databaseName = connection.Database;

            await EnsureAutoIncrementAsync(connection, databaseName, "users", "id", cancellationToken);
            await EnsureAutoIncrementAsync(connection, databaseName, "roles", "id", cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureAutoIncrementAsync(
        DbConnection connection,
        string databaseName,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var inspectCommand = connection.CreateCommand();
        inspectCommand.CommandText =
            """
            SELECT EXTRA
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME = @table
              AND COLUMN_NAME = @column
            LIMIT 1;
            """;

        AddParameter(inspectCommand, "@schema", databaseName);
        AddParameter(inspectCommand, "@table", tableName);
        AddParameter(inspectCommand, "@column", columnName);

        var currentColumnExtra = await inspectCommand.ExecuteScalarAsync(cancellationToken);

        if (currentColumnExtra is null or DBNull)
        {
            return;
        }

        var extra = Convert.ToString(currentColumnExtra) ?? string.Empty;

        if (extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText =
            $"""
             ALTER TABLE `{tableName}`
             MODIFY COLUMN `{columnName}` INT(11) NOT NULL AUTO_INCREMENT;
             """;

        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
