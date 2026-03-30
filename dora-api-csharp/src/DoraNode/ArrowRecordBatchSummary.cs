using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraNode;

/// <summary>
/// Summarizes the shape of an Arrow <see cref="RecordBatch"/> for logging and test assertions.
/// </summary>
public sealed class ArrowRecordBatchSummary
{
    private ArrowRecordBatchSummary(
        long rowCount,
        int columnCount,
        IReadOnlyList<string> fieldNames,
        IReadOnlyList<ArrowTypeId> typeIds)
    {
        RowCount = rowCount;
        ColumnCount = columnCount;
        FieldNames = fieldNames;
        TypeIds = typeIds;
    }

    public long RowCount { get; }

    public int ColumnCount { get; }

    public IReadOnlyList<string> FieldNames { get; }

    public IReadOnlyList<ArrowTypeId> TypeIds { get; }

    public string FieldsCsv => string.Join(",", FieldNames);

    public string TypesCsv => string.Join(",", TypeIds);

    public static ArrowRecordBatchSummary Create(RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);

        var fields = recordBatch.Schema.FieldsList;
        var fieldNames = fields.Select(static field => field.Name).ToArray();
        var typeIds = fields.Select(static field => field.DataType.TypeId).ToArray();
        return new ArrowRecordBatchSummary(recordBatch.Length, recordBatch.ColumnCount, fieldNames, typeIds);
    }

    public string ToSummaryString(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return $"{prefix} fields={FieldsCsv} cols={ColumnCount} rows={RowCount} types={TypesCsv}";
    }
}
