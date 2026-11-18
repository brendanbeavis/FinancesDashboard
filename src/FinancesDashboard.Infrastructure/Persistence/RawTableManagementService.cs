using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinancesDashboard.Infrastructure.Persistence;

public sealed record RawTableListItem(string Name, int RowCount);
public sealed record RawTableColumn(string Name, string ClrType, bool IsPrimaryKey, bool IsNullable);
public sealed record RawTableData(string Name, IReadOnlyList<RawTableColumn> Columns, IReadOnlyList<Dictionary<string, object?>> Rows);
public sealed record RawTableMutationRequest(IReadOnlyDictionary<string, JsonElement> Key, IReadOnlyDictionary<string, JsonElement> Values);

public sealed class RawTableManagementService(FinancesDashboardDbContext dbContext)
{
    private IReadOnlyDictionary<string, ITableMetadata> Tables => GetAllowedTables();

    public async Task<IReadOnlyList<RawTableListItem>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<RawTableListItem>();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (var table in Tables.Values.OrderBy(x => x.Name))
            {
                await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM {Quote(table.Name)}";
                var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                result.Add(new RawTableListItem(table.Name, count));
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
        return result;
    }

    public async Task<RawTableData> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var table = GetTable(tableName);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            var columns = table.Columns.Where(x => !x.IsShadow).ToList();
            var columnNames = string.Join(", ", columns.Select(x => Quote(x.ColumnName)));
            command.CommandText = $"SELECT {columnNames} FROM {Quote(table.Name)} LIMIT 500";

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < columns.Count; i++)
                    row[columns[i].ColumnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            var schema = columns.Select(x => new RawTableColumn(
                x.ColumnName,
                x.Property.ClrType.Name,
                table.PrimaryKeyColumns.Contains(x.ColumnName, StringComparer.OrdinalIgnoreCase),
                x.Property.IsNullable)).ToList();

            return new RawTableData(table.Name, schema, rows);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task InsertAsync(string tableName, IReadOnlyDictionary<string, JsonElement> values, CancellationToken cancellationToken = default)
    {
        var table = GetTable(tableName);
        var columns = table.Columns.Where(x => values.Keys.Contains(x.ColumnName, StringComparer.OrdinalIgnoreCase)).ToList();
        if (columns.Count == 0)
            throw new ArgumentException("At least one column value is required.", nameof(values));

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"INSERT INTO {Quote(table.Name)} ({string.Join(", ", columns.Select(x => Quote(x.ColumnName)))}) VALUES ({string.Join(", ", columns.Select((_, i) => $"@p{i}"))})";
            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var value = values.First(x => string.Equals(x.Key, column.ColumnName, StringComparison.OrdinalIgnoreCase)).Value;
                command.Parameters.Add(CreateParameter($"@p{i}", value, column.Property));
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> UpdateAsync(string tableName, RawTableMutationRequest request, CancellationToken cancellationToken = default)
    {
        var table = GetTable(tableName);
        var setColumns = table.Columns.Where(x => request.Values.Keys.Contains(x.ColumnName, StringComparer.OrdinalIgnoreCase) && !table.PrimaryKeyColumns.Contains(x.ColumnName, StringComparer.OrdinalIgnoreCase)).ToList();
        if (setColumns.Count == 0)
            return false;

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            var parameterIndex = 0;
            var setSql = new List<string>();
            foreach (var column in setColumns)
            {
                var pair = request.Values.First(x => string.Equals(x.Key, column.ColumnName, StringComparison.OrdinalIgnoreCase));
                var parameterName = $"@p{parameterIndex++}";
                setSql.Add($"{Quote(column.ColumnName)} = {parameterName}");
                command.Parameters.Add(CreateParameter(parameterName, pair.Value, column.Property));
            }

            var keySql = new List<string>();
            foreach (var keyColumnName in table.PrimaryKeyColumns)
            {
                var pair = request.Key.First(x => string.Equals(x.Key, keyColumnName, StringComparison.OrdinalIgnoreCase));
                var column = table.Columns.First(x => string.Equals(x.ColumnName, keyColumnName, StringComparison.OrdinalIgnoreCase));
                var parameterName = $"@k{parameterIndex++}";
                keySql.Add($"{Quote(keyColumnName)} = {parameterName}");
                command.Parameters.Add(CreateParameter(parameterName, pair.Value, column.Property));
            }

            command.CommandText = $"UPDATE {Quote(table.Name)} SET {string.Join(", ", setSql)} WHERE {string.Join(" AND ", keySql)}";
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public async Task<bool> DeleteAsync(string tableName, IReadOnlyDictionary<string, JsonElement> key, CancellationToken cancellationToken = default)
    {
        var table = GetTable(tableName);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            var keySql = new List<string>();
            var parameterIndex = 0;
            foreach (var keyColumnName in table.PrimaryKeyColumns)
            {
                var pair = key.First(x => string.Equals(x.Key, keyColumnName, StringComparison.OrdinalIgnoreCase));
                var column = table.Columns.First(x => string.Equals(x.ColumnName, keyColumnName, StringComparison.OrdinalIgnoreCase));
                var parameterName = $"@k{parameterIndex++}";
                keySql.Add($"{Quote(keyColumnName)} = {parameterName}");
                command.Parameters.Add(CreateParameter(parameterName, pair.Value, column.Property));
            }
            command.CommandText = $"DELETE FROM {Quote(table.Name)} WHERE {string.Join(" AND ", keySql)}";
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private ITableMetadata GetTable(string tableName)
    {
        if (!Tables.TryGetValue(tableName, out var table))
            throw new KeyNotFoundException($"Table '{tableName}' was not found.");
        return table;
    }

    private IReadOnlyDictionary<string, ITableMetadata> GetAllowedTables()
    {
        var result = new Dictionary<string, ITableMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var storeObject = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(storeObject)) continue;
            var columns = entityType.GetProperties()
                .Select(property => new ColumnMetadata(
                    property.GetColumnName(StoreObjectIdentifier.Table(storeObject, entityType.GetSchema())) ?? property.Name,
                    property,
                    false))
                .ToList();
            var primaryKey = entityType.FindPrimaryKey()?.Properties
                .Select(x => x.GetColumnName(StoreObjectIdentifier.Table(storeObject, entityType.GetSchema())) ?? x.Name)
                .ToList() ?? [];
            result[storeObject] = new TableMetadata(storeObject, columns, primaryKey);
        }
        return result;
    }

    private static SqliteParameter CreateParameter(string name, JsonElement value, IProperty property)
    {
        object? parameterValue;
        if (value.ValueKind == JsonValueKind.Null)
        {
            parameterValue = null;
        }
        else
        {
            var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (clrType == typeof(bool)) parameterValue = value.GetBoolean() ? 1 : 0;
            else if (clrType == typeof(Guid)) parameterValue = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            else if (clrType == typeof(DateOnly)) parameterValue = value.GetString();
            else if (clrType == typeof(DateTime)) parameterValue = value.GetString();
            else if (clrType == typeof(decimal)) parameterValue = value.GetDecimal();
            else if (clrType == typeof(double)) parameterValue = value.GetDouble();
            else if (clrType == typeof(float)) parameterValue = value.GetSingle();
            else if (clrType.IsEnum) parameterValue = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetInt32();
            else if (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte))
                parameterValue = value.ValueKind == JsonValueKind.String ? Convert.ChangeType(value.GetString(), clrType) : value.GetInt64();
            else parameterValue = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }
        return new SqliteParameter(name, parameterValue);
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private interface ITableMetadata
    {
        string Name { get; }
        IReadOnlyList<ColumnMetadata> Columns { get; }
        IReadOnlyList<string> PrimaryKeyColumns { get; }
    }

    private sealed record ColumnMetadata(string ColumnName, IProperty Property, bool IsShadow);
    private sealed record TableMetadata(string Name, IReadOnlyList<ColumnMetadata> Columns, IReadOnlyList<string> PrimaryKeyColumns) : ITableMetadata;
}
