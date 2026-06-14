using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraOperator;

/// <summary>
/// Summarizes the shape of an Arrow record batch for logging and assertions.
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

    /// <summary>
    /// Gets the row count of the summarized record batch.
    /// </summary>
    public long RowCount { get; }

    /// <summary>
    /// Gets the column count of the summarized record batch.
    /// </summary>
    public int ColumnCount { get; }

    /// <summary>
    /// Gets the field names in schema order.
    /// </summary>
    public IReadOnlyList<string> FieldNames { get; }

    /// <summary>
    /// Gets the Arrow type IDs in schema order.
    /// </summary>
    public IReadOnlyList<ArrowTypeId> TypeIds { get; }

    /// <summary>
    /// Gets the field names joined as a comma-separated string.
    /// </summary>
    public string FieldsCsv => string.Join(",", FieldNames);

    /// <summary>
    /// Gets the type IDs joined as a comma-separated string.
    /// </summary>
    public string TypesCsv => string.Join(",", TypeIds);

    /// <summary>
    /// Creates a summary from a record batch.
    /// </summary>
    public static ArrowRecordBatchSummary Create(RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);

        var fields = recordBatch.Schema.FieldsList;
        var fieldNames = fields.Select(static field => field.Name).ToArray();
        var typeIds = fields.Select(static field => field.DataType.TypeId).ToArray();
        return new ArrowRecordBatchSummary(recordBatch.Length, recordBatch.ColumnCount, fieldNames, typeIds);
    }

    /// <summary>
    /// Formats the summary as a single-line string with a custom prefix token.
    /// </summary>
    public string ToSummaryString(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return $"{prefix} fields={FieldsCsv} cols={ColumnCount} rows={RowCount} types={TypesCsv}";
    }
}
