using System.Text.Json;

namespace FinancesDashboard.Web.Models;

public sealed record RawTableListItemModel(string Name, int RowCount);

public sealed record RawTableColumnModel(string Name, string ClrType, bool IsPrimaryKey, bool IsNullable);

public sealed record RawTableDataModel(string Name, IReadOnlyList<RawTableColumnModel> Columns, IReadOnlyList<Dictionary<string, JsonElement>> Rows);

public sealed record RawTableMutationModel(IReadOnlyDictionary<string, JsonElement> Key, IReadOnlyDictionary<string, JsonElement> Values);
