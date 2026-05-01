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

            await EnsureCategoriesTableAsync(connection, cancellationToken);
            await EnsureModelsTableAsync(connection, cancellationToken);
            await EnsureColumnExistsAsync(connection, databaseName, "modelos", "image_path", "ALTER TABLE `modelos` ADD COLUMN `image_path` VARCHAR(500) NULL AFTER `id_categoria`;", cancellationToken);
            await EnsureAutoIncrementAsync(connection, databaseName, "users", "id", cancellationToken);
            await EnsureAutoIncrementAsync(connection, databaseName, "roles", "id", cancellationToken);
            await EnsureAutoIncrementAsync(connection, databaseName, "categorias", "id", cancellationToken);
            await EnsureAutoIncrementAsync(connection, databaseName, "modelos", "id", cancellationToken);
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

    private static async Task EnsureColumnExistsAsync(
        DbConnection connection,
        string databaseName,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        await using var inspectCommand = connection.CreateCommand();
        inspectCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME = @table
              AND COLUMN_NAME = @column;
            """;

        AddParameter(inspectCommand, "@schema", databaseName);
        AddParameter(inspectCommand, "@table", tableName);
        AddParameter(inspectCommand, "@column", columnName);

        var existingColumns = await inspectCommand.ExecuteScalarAsync(cancellationToken);
        var exists = Convert.ToInt32(existingColumns) > 0;

        if (exists)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = alterSql;
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureCategoriesTableAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS `categorias` (
                `id` INT(11) NOT NULL AUTO_INCREMENT,
                `nome` VARCHAR(255) NOT NULL,
                `is_active` BIT(1) NOT NULL DEFAULT b'1',
                `date_created` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `date_changed` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`)
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureModelsTableAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS `modelos` (
                `id` INT(11) NOT NULL AUTO_INCREMENT,
                `nome` VARCHAR(255) NOT NULL,
                `id_categoria` INT(11) NOT NULL,
                `image_path` VARCHAR(500) NULL,
                `is_active` BIT(1) NOT NULL DEFAULT b'1',
                `date_created` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `date_changed` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                KEY `ix_modelos_id_categoria` (`id_categoria`),
                CONSTRAINT `fk_modelos_categorias`
                    FOREIGN KEY (`id_categoria`) REFERENCES `categorias` (`id`)
                    ON DELETE CASCADE
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
