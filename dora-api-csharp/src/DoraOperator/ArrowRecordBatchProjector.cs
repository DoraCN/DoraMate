using System.Text;
using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraOperator;

/// <summary>
/// Projects validated Arrow columns and struct rows into higher-level managed models.
/// </summary>
public static class ArrowRecordBatchProjector
{
    /// <summary>
    /// Projects a string column into a managed row collection.
    /// </summary>
    public static bool TryProjectStringColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<string>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetStringColumn(recordBatch, fieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected String column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectStringColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects an Int32 column into a managed row collection.
    /// </summary>
    public static bool TryProjectInt32Column(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<int>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetInt32Column(recordBatch, fieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Int32 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectInt32Column(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects an Int64 column into a managed row collection.
    /// </summary>
    public static bool TryProjectInt64Column(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<long>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetInt64Column(recordBatch, fieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Int64 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectInt64Column(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Boolean column into a managed row collection.
    /// </summary>
    public static bool TryProjectBooleanColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<bool>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetBooleanColumn(recordBatch, fieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Boolean column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectBooleanColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Float column into a managed row collection.
    /// </summary>
    public static bool TryProjectFloatColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<float>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetFloatColumn(recordBatch, fieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Float column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectFloatColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Double column into a managed row collection.
    /// </summary>
    public static bool TryProjectDoubleColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<double>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetDoubleColumn(recordBatch, fieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Double column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectDoubleColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Binary column into a managed row collection.
    /// </summary>
    public static bool TryProjectBinaryColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<byte[]>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetBinaryColumn(recordBatch, fieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Binary column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectBinaryColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Date32 column into a managed row collection.
    /// </summary>
    public static bool TryProjectDate32Column(
        RecordBatch recordBatch,
        string fieldName,
        DateUnit expectedUnit,
        out IReadOnlyList<DateOnly>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetDate32Column(recordBatch, fieldName, expectedUnit, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Date32 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectDate32Column(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Timestamp column into a managed row collection.
    /// </summary>
    public static bool TryProjectTimestampColumn(
        RecordBatch recordBatch,
        string fieldName,
        TimeUnit expectedUnit,
        string? expectedTimezone,
        out IReadOnlyList<DateTimeOffset>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetTimestampColumn(
                recordBatch,
                fieldName,
                expectedUnit,
                expectedTimezone,
                out var column,
                out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Timestamp column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectTimestampColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Decimal128 column into a managed row collection.
    /// </summary>
    public static bool TryProjectDecimal128Column(
        RecordBatch recordBatch,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        out IReadOnlyList<decimal>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetDecimal128Column(
                recordBatch,
                fieldName,
                expectedPrecision,
                expectedScale,
                out var column,
                out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Decimal128 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectDecimal128Column(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Decimal256 column into a managed row collection.
    /// </summary>
    public static bool TryProjectDecimal256Column(
        RecordBatch recordBatch,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        out IReadOnlyList<decimal>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetDecimal256Column(
                recordBatch,
                fieldName,
                expectedPrecision,
                expectedScale,
                out var column,
                out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Decimal256 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectDecimal256Column(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a List&lt;String&gt; column into a managed row collection.
    /// </summary>
    public static bool TryProjectStringListColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<IReadOnlyList<string>>? rows,
        out string? error) =>
        TryProjectStringListColumn(recordBatch, fieldName, expectedValueFieldName: null, out rows, out error);

    /// <summary>
    /// Projects a List&lt;String&gt; column into a managed row collection and validates the value-field name when supplied.
    /// </summary>
    public static bool TryProjectStringListColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedValueFieldName,
        out IReadOnlyList<IReadOnlyList<string>>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetStringListColumn(recordBatch, fieldName, expectedValueFieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected List<String> column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectStringListColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a List&lt;Int32&gt; column into a managed row collection.
    /// </summary>
    public static bool TryProjectInt32ListColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<IReadOnlyList<int>>? rows,
        out string? error) =>
        TryProjectInt32ListColumn(recordBatch, fieldName, expectedValueFieldName: null, out rows, out error);

    /// <summary>
    /// Projects a List&lt;Int32&gt; column into a managed row collection and validates the value-field name when supplied.
    /// </summary>
    public static bool TryProjectInt32ListColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedValueFieldName,
        out IReadOnlyList<IReadOnlyList<int>>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetInt32ListColumn(recordBatch, fieldName, expectedValueFieldName, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected List<Int32> column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectInt32ListColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a Map&lt;String, Int32&gt; column into a managed row collection.
    /// </summary>
    public static bool TryProjectStringInt32MapColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<IReadOnlyDictionary<string, int>>? rows,
        out string? error) =>
        TryProjectStringInt32MapColumn(recordBatch, fieldName, expectedKeyFieldName: null, expectedValueFieldName: null, out rows, out error);

    /// <summary>
    /// Projects a Map&lt;String, Int32&gt; column into a managed row collection and validates child-field names when supplied.
    /// </summary>
    public static bool TryProjectStringInt32MapColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedKeyFieldName,
        string? expectedValueFieldName,
        out IReadOnlyList<IReadOnlyDictionary<string, int>>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetStringInt32MapColumn(
                recordBatch,
                fieldName,
                expectedKeyFieldName,
                expectedValueFieldName,
                out var column,
                out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Map<String, Int32> column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectStringInt32MapColumn(column, fieldName, out rows, out error);
    }

    /// <summary>
    /// Projects a struct column into a row collection using a custom row projector.
    /// </summary>
    public static bool TryProjectStructColumn<TModel>(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        ArrowStructRowProjector<TModel> projector,
        out IReadOnlyList<TModel>? rows,
        out string? error)
    {
        rows = null;
        if (!ArrowRecordBatchAssertions.TryGetStructColumn(recordBatch, fieldName, expectedChildFieldNames, expectedChildTypeIds, out var column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Struct column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryProjectStructColumn(column, fieldName, expectedChildFieldNames, expectedChildTypeIds, projector, out rows, out error);
    }

    internal static bool TryProjectStringListColumn(
        ListArray column,
        string fieldName,
        out IReadOnlyList<IReadOnlyList<string>>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new List<IReadOnlyList<string>>(column.Length);
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            if (!ArrowRecordBatchAssertions.TryGetStringListValue(column, fieldName, rowIndex, out var values, out error))
            {
                return false;
            }

            projectedRows.Add(values?.ToArray() ?? System.Array.Empty<string>());
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectInt32ListColumn(
        ListArray column,
        string fieldName,
        out IReadOnlyList<IReadOnlyList<int>>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new List<IReadOnlyList<int>>(column.Length);
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            if (!ArrowRecordBatchAssertions.TryGetInt32ListValue(column, fieldName, rowIndex, out var values, out error))
            {
                return false;
            }

            projectedRows.Add(values?.ToArray() ?? System.Array.Empty<int>());
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectStringColumn(
        StringArray column,
        string fieldName,
        out IReadOnlyList<string>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new string[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            projectedRows[rowIndex] = column.GetString(rowIndex, Encoding.UTF8) ?? string.Empty;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectInt32Column(
        Int32Array column,
        string fieldName,
        out IReadOnlyList<int>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new int[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetValue(rowIndex);
            if (value is null)
            {
                error = $"Expected Int32 column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectInt64Column(
        Int64Array column,
        string fieldName,
        out IReadOnlyList<long>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new long[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetValue(rowIndex);
            if (value is null)
            {
                error = $"Expected Int64 column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectBooleanColumn(
        BooleanArray column,
        string fieldName,
        out IReadOnlyList<bool>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new bool[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetValue(rowIndex);
            if (value is null)
            {
                error = $"Expected Boolean column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectFloatColumn(
        FloatArray column,
        string fieldName,
        out IReadOnlyList<float>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new float[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetValue(rowIndex);
            if (value is null)
            {
                error = $"Expected Float column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectDoubleColumn(
        DoubleArray column,
        string fieldName,
        out IReadOnlyList<double>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new double[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetValue(rowIndex);
            if (value is null)
            {
                error = $"Expected Double column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectBinaryColumn(
        BinaryArray column,
        string fieldName,
        out IReadOnlyList<byte[]>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new byte[column.Length][];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            projectedRows[rowIndex] = column.GetBytes(rowIndex).ToArray();
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectDate32Column(
        Date32Array column,
        string fieldName,
        out IReadOnlyList<DateOnly>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new DateOnly[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetDateOnly(rowIndex);
            if (value is null)
            {
                error = $"Expected Date32 column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectTimestampColumn(
        TimestampArray column,
        string fieldName,
        out IReadOnlyList<DateTimeOffset>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new DateTimeOffset[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetTimestamp(rowIndex);
            if (value is null)
            {
                error = $"Expected Timestamp column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectDecimal128Column(
        Decimal128Array column,
        string fieldName,
        out IReadOnlyList<decimal>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new decimal[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetValue(rowIndex);
            if (value is null)
            {
                error = $"Expected Decimal128 column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectDecimal256Column(
        Decimal256Array column,
        string fieldName,
        out IReadOnlyList<decimal>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new decimal[column.Length];
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            var value = column.GetValue(rowIndex);
            if (value is null)
            {
                error = $"Expected Decimal256 column '{fieldName}' value at row {rowIndex} to be non-null.";
                return false;
            }

            projectedRows[rowIndex] = value.Value;
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    public static bool TryProjectRows<TModel>(
        RecordBatch recordBatch,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<ArrowTypeId> expectedTypeIds,
        ArrowRecordBatchRowProjector<TModel> projector,
        out IReadOnlyList<TModel>? rows,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        ArgumentNullException.ThrowIfNull(expectedTypeIds);
        ArgumentNullException.ThrowIfNull(projector);

        rows = null;
        ArrowRecordBatchColumnProjector columnProjector;
        try
        {
            columnProjector = new ArrowRecordBatchColumnProjector(recordBatch, expectedFieldNames, expectedTypeIds);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        var projectedRows = new List<TModel>(recordBatch.Length);
        for (var rowIndex = 0; rowIndex < recordBatch.Length; rowIndex++)
        {
            var accessor = new ArrowRecordBatchRowAccessor(columnProjector, rowIndex);
            if (!projector(accessor, out var rowModel, out error))
            {
                return false;
            }

            if (rowModel is null)
            {
                error = $"RecordBatch projector returned a null model at row {rowIndex}.";
                return false;
            }

            projectedRows.Add(rowModel);
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectStructColumn<TModel>(
        StructArray structArray,
        string fieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        ArrowStructRowProjector<TModel> projector,
        out IReadOnlyList<TModel>? rows,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(structArray);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedChildFieldNames);
        ArgumentNullException.ThrowIfNull(expectedChildTypeIds);
        ArgumentNullException.ThrowIfNull(projector);

        rows = null;
        ArrowStructColumnProjector columnProjector;
        try
        {
            columnProjector = new ArrowStructColumnProjector(structArray, fieldName, expectedChildFieldNames, expectedChildTypeIds);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        var projectedRows = new List<TModel>(structArray.Length);
        for (var rowIndex = 0; rowIndex < structArray.Length; rowIndex++)
        {
            var accessor = new ArrowStructRowAccessor(columnProjector, rowIndex);
            if (!projector(accessor, out var rowModel, out error))
            {
                return false;
            }

            if (rowModel is null)
            {
                error = $"Struct projector for '{fieldName}' returned a null model at row {rowIndex}.";
                return false;
            }

            projectedRows.Add(rowModel);
        }

        rows = projectedRows;
        error = null;
        return true;
    }

    internal static bool TryProjectStringInt32MapColumn(
        MapArray column,
        string fieldName,
        out IReadOnlyList<IReadOnlyDictionary<string, int>>? rows,
        out string? error)
    {
        rows = null;
        var projectedRows = new List<IReadOnlyDictionary<string, int>>(column.Length);
        for (var rowIndex = 0; rowIndex < column.Length; rowIndex++)
        {
            if (!ArrowRecordBatchAssertions.TryGetStringInt32MapValue(column, fieldName, rowIndex, out var values, out error))
            {
                return false;
            }

            projectedRows.Add(values is null ? new Dictionary<string, int>(StringComparer.Ordinal) : new Dictionary<string, int>(values, StringComparer.Ordinal));
        }

        rows = projectedRows;
        error = null;
        return true;
    }
}

public sealed class ArrowRecordBatchRowAccessor
{
    private readonly ArrowRecordBatchColumnProjector _projector;

    internal ArrowRecordBatchRowAccessor(ArrowRecordBatchColumnProjector projector, int rowIndex)
    {
        _projector = projector;
        RowIndex = rowIndex;
    }

    /// <summary>
    /// Gets the current row index within the projected record batch.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Tries to read a string field from the current row.
    /// </summary>
    public bool TryGetString(string fieldName, out string value, out string? error) => _projector.TryGetString(RowIndex, fieldName, out value, out error);
    /// <summary>
    /// Tries to read an Int32 field from the current row.
    /// </summary>
    public bool TryGetInt32(string fieldName, out int value, out string? error) => _projector.TryGetInt32(RowIndex, fieldName, out value, out error);
    /// <summary>
    /// Tries to read an Int64 field from the current row.
    /// </summary>
    public bool TryGetInt64(string fieldName, out long value, out string? error) => _projector.TryGetInt64(RowIndex, fieldName, out value, out error);
    /// <summary>
    /// Tries to read a Boolean field from the current row.
    /// </summary>
    public bool TryGetBoolean(string fieldName, out bool value, out string? error) => _projector.TryGetBoolean(RowIndex, fieldName, out value, out error);
    /// <summary>
    /// Tries to read a Float field from the current row.
    /// </summary>
    public bool TryGetFloat(string fieldName, out float value, out string? error) => _projector.TryGetFloat(RowIndex, fieldName, out value, out error);
    /// <summary>
    /// Tries to read a Double field from the current row.
    /// </summary>
    public bool TryGetDouble(string fieldName, out double value, out string? error) => _projector.TryGetDouble(RowIndex, fieldName, out value, out error);
    /// <summary>
    /// Tries to read a Binary field from the current row.
    /// </summary>
    public bool TryGetBinary(string fieldName, out byte[] value, out string? error) => _projector.TryGetBinary(RowIndex, fieldName, out value, out error);
    /// <summary>
    /// Tries to read a Date32 field from the current row.
    /// </summary>
    public bool TryGetDate32(string fieldName, DateUnit expectedUnit, out DateOnly value, out string? error) => _projector.TryGetDate32(RowIndex, fieldName, expectedUnit, out value, out error);
    /// <summary>
    /// Tries to read a Timestamp field from the current row.
    /// </summary>
    public bool TryGetTimestamp(string fieldName, TimeUnit expectedUnit, string? expectedTimezone, out DateTimeOffset value, out string? error) => _projector.TryGetTimestamp(RowIndex, fieldName, expectedUnit, expectedTimezone, out value, out error);
    /// <summary>
    /// Tries to read a Decimal128 field from the current row.
    /// </summary>
    public bool TryGetDecimal128(string fieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) => _projector.TryGetDecimal128(RowIndex, fieldName, expectedPrecision, expectedScale, out value, out error);
    /// <summary>
    /// Tries to read a Decimal256 field from the current row.
    /// </summary>
    public bool TryGetDecimal256(string fieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) => _projector.TryGetDecimal256(RowIndex, fieldName, expectedPrecision, expectedScale, out value, out error);
    /// <summary>
    /// Tries to read a List&lt;String&gt; field from the current row.
    /// </summary>
    public bool TryGetStringList(string fieldName, out IReadOnlyList<string> values, out string? error) => _projector.TryGetStringList(RowIndex, fieldName, out values, out error);
    /// <summary>
    /// Tries to read a List&lt;Int32&gt; field from the current row.
    /// </summary>
    public bool TryGetInt32List(string fieldName, out IReadOnlyList<int> values, out string? error) => _projector.TryGetInt32List(RowIndex, fieldName, out values, out error);
    /// <summary>
    /// Tries to read a Map&lt;String, Int32&gt; field from the current row.
    /// </summary>
    public bool TryGetStringInt32Map(string fieldName, out IReadOnlyDictionary<string, int> values, out string? error) => _projector.TryGetStringInt32Map(RowIndex, fieldName, out values, out error);
    /// <summary>
    /// Tries to project a nested struct field from the current row.
    /// </summary>
    public bool TryProjectStruct<TModel>(
        string fieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        ArrowStructRowProjector<TModel> projector,
        out TModel? model,
        out string? error) =>
        _projector.TryProjectStruct(RowIndex, fieldName, expectedChildFieldNames, expectedChildTypeIds, projector, out model, out error);
}

/// <summary>
/// Projects one record-batch row into a higher-level model.
/// </summary>
public delegate bool ArrowRecordBatchRowProjector<TModel>(
    ArrowRecordBatchRowAccessor row,
    out TModel? model,
    out string? error);

internal sealed class ArrowRecordBatchColumnProjector
{
    private readonly RecordBatch _recordBatch;
    private readonly IReadOnlyList<ArrowTypeId> _typeIds;
    private readonly Dictionary<string, int> _indices;

    public ArrowRecordBatchColumnProjector(
        RecordBatch recordBatch,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<ArrowTypeId> expectedTypeIds)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        ArgumentNullException.ThrowIfNull(expectedTypeIds);

        if (expectedFieldNames.Count != expectedTypeIds.Count)
        {
            throw new ArgumentException("Expected field-name and type-id lists to have the same length.");
        }

        if (recordBatch.ColumnCount != expectedFieldNames.Count)
        {
            throw new ArgumentException($"Expected record batch to expose {expectedFieldNames.Count} columns but got {recordBatch.ColumnCount}.");
        }

        _recordBatch = recordBatch;
        _typeIds = expectedTypeIds;
        _indices = new Dictionary<string, int>(expectedFieldNames.Count, StringComparer.Ordinal);
        for (var index = 0; index < expectedFieldNames.Count; index++)
        {
            _indices[expectedFieldNames[index]] = index;
        }
    }

    public bool TryGetString(int rowIndex, string fieldName, out string value, out string? error)
    {
        value = string.Empty;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.String, out StringArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        value = column.GetString(rowIndex, Encoding.UTF8) ?? string.Empty;
        error = null;
        return true;
    }

    public bool TryGetInt32(int rowIndex, string fieldName, out int value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Int32, out Int32Array? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected Int32 column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetInt64(int rowIndex, string fieldName, out long value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Int64, out Int64Array? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected Int64 column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetBoolean(int rowIndex, string fieldName, out bool value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Boolean, out BooleanArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected Boolean column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetFloat(int rowIndex, string fieldName, out float value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Float, out FloatArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected Float column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetDouble(int rowIndex, string fieldName, out double value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Double, out DoubleArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected Double column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetBinary(int rowIndex, string fieldName, out byte[] value, out string? error)
    {
        value = System.Array.Empty<byte>();
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Binary, out BinaryArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        value = column.GetBytes(rowIndex).ToArray();
        error = null;
        return true;
    }

    public bool TryGetDate32(int rowIndex, string fieldName, DateUnit expectedUnit, out DateOnly value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Date32, out Date32Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Data.DataType is not Date32Type dateType || dateType.Unit != expectedUnit)
        {
            var actualUnit = column.Data.DataType is Date32Type actualType ? actualType.Unit.ToString() : column.Data.DataType.Name;
            error = $"Expected column '{fieldName}' to use Date32({expectedUnit}) but got {actualUnit}.";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetDateOnly(rowIndex);
        if (actual is null)
        {
            error = $"Expected Date32 column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetTimestamp(int rowIndex, string fieldName, TimeUnit expectedUnit, string? expectedTimezone, out DateTimeOffset value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Timestamp, out TimestampArray? column, out error) || column is null)
        {
            return false;
        }

        if (column.Data.DataType is not TimestampType timestampType)
        {
            error = $"Expected column '{fieldName}' to be materialized as TimestampType but got {column.Data.DataType.Name}.";
            return false;
        }

        if (timestampType.Unit != expectedUnit || !string.Equals(timestampType.Timezone, expectedTimezone, StringComparison.Ordinal))
        {
            error = $"Expected column '{fieldName}' to use Timestamp({expectedUnit}, {(expectedTimezone ?? "<null>")}) but got Timestamp({timestampType.Unit}, {(timestampType.Timezone ?? "<null>")}).";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetTimestamp(rowIndex);
        if (actual is null)
        {
            error = $"Expected Timestamp column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetDecimal128(int rowIndex, string fieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Decimal128, out Decimal128Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error = $"Expected column '{fieldName}' to use Decimal128({expectedPrecision}, {expectedScale}) but got Decimal128({column.Precision}, {column.Scale}).";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected Decimal128 column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetDecimal256(int rowIndex, string fieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Decimal256, out Decimal256Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error = $"Expected column '{fieldName}' to use Decimal256({expectedPrecision}, {expectedScale}) but got Decimal256({column.Precision}, {column.Scale}).";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected Decimal256 column '{fieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetStringList(int rowIndex, string fieldName, out IReadOnlyList<string> values, out string? error)
    {
        values = System.Array.Empty<string>();
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.List, out ListArray? column, out error) || column is null)
        {
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryGetStringListValue(column, fieldName, rowIndex, out var rowValues, out error))
        {
            return false;
        }

        values = rowValues?.ToArray() ?? System.Array.Empty<string>();
        error = null;
        return true;
    }

    public bool TryGetInt32List(int rowIndex, string fieldName, out IReadOnlyList<int> values, out string? error)
    {
        values = System.Array.Empty<int>();
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.List, out ListArray? column, out error) || column is null)
        {
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryGetInt32ListValue(column, fieldName, rowIndex, out var rowValues, out error))
        {
            return false;
        }

        values = rowValues?.ToArray() ?? System.Array.Empty<int>();
        error = null;
        return true;
    }

    public bool TryGetStringInt32Map(int rowIndex, string fieldName, out IReadOnlyDictionary<string, int> values, out string? error)
    {
        values = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Map, out MapArray? column, out error) || column is null)
        {
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryGetStringInt32MapValue(column, fieldName, rowIndex, out var rowValues, out error))
        {
            return false;
        }

        values = rowValues is null ? new Dictionary<string, int>(StringComparer.Ordinal) : new Dictionary<string, int>(rowValues, StringComparer.Ordinal);
        error = null;
        return true;
    }

    public bool TryProjectStruct<TModel>(
        int rowIndex,
        string fieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        ArrowStructRowProjector<TModel> projector,
        out TModel? model,
        out string? error)
    {
        model = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Struct, out StructArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, fieldName, column.Length, out error))
        {
            return false;
        }

        ArrowStructColumnProjector nestedProjector;
        try
        {
            nestedProjector = new ArrowStructColumnProjector(column, fieldName, expectedChildFieldNames, expectedChildTypeIds);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        return projector(new ArrowStructRowAccessor(nestedProjector, rowIndex), out model, out error);
    }

    private bool TryGetTypedColumn<TArray>(string fieldName, ArrowTypeId expectedTypeId, out TArray? column, out string? error)
        where TArray : class
    {
        column = null;
        if (!TryResolveIndex(fieldName, expectedTypeId, out var index, out error))
        {
            return false;
        }

        var rawColumn = _recordBatch.Column(index);
        if (rawColumn is not TArray typedColumn)
        {
            error = $"Expected column '{fieldName}' to be materialized as {typeof(TArray).Name} but got {rawColumn.GetType().Name}.";
            return false;
        }

        column = typedColumn;
        error = null;
        return true;
    }

    private bool TryResolveIndex(string fieldName, ArrowTypeId expectedTypeId, out int index, out string? error)
    {
        if (!_indices.TryGetValue(fieldName, out index))
        {
            error = $"Expected record batch to contain field '{fieldName}', but it was not found.";
            return false;
        }

        var configuredType = _typeIds[index];
        if (configuredType != expectedTypeId)
        {
            error = $"Expected field '{fieldName}' to have configured type {expectedTypeId} but contract declared {configuredType}.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryEnsureRowIndex(int rowIndex, string fieldName, int length, out string? error)
    {
        if (rowIndex < 0 || rowIndex >= length)
        {
            error = $"Expected column '{fieldName}' to contain row {rowIndex}, but it has {length} rows.";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class ArrowStructRowAccessor
{
    private readonly ArrowStructColumnProjector _projector;

    internal ArrowStructRowAccessor(ArrowStructColumnProjector projector, int rowIndex)
    {
        _projector = projector;
        RowIndex = rowIndex;
    }

    /// <summary>
    /// Gets the current row index within the parent struct column.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Tries to read a string child field from the current struct row.
    /// </summary>
    public bool TryGetString(string childFieldName, out string value, out string? error) => _projector.TryGetString(RowIndex, childFieldName, out value, out error);
    /// <summary>
    /// Tries to read an Int32 child field from the current struct row.
    /// </summary>
    public bool TryGetInt32(string childFieldName, out int value, out string? error) => _projector.TryGetInt32(RowIndex, childFieldName, out value, out error);
    /// <summary>
    /// Tries to read an Int64 child field from the current struct row.
    /// </summary>
    public bool TryGetInt64(string childFieldName, out long value, out string? error) => _projector.TryGetInt64(RowIndex, childFieldName, out value, out error);
    /// <summary>
    /// Tries to read a Boolean child field from the current struct row.
    /// </summary>
    public bool TryGetBoolean(string childFieldName, out bool value, out string? error) => _projector.TryGetBoolean(RowIndex, childFieldName, out value, out error);
    /// <summary>
    /// Tries to read a Float child field from the current struct row.
    /// </summary>
    public bool TryGetFloat(string childFieldName, out float value, out string? error) => _projector.TryGetFloat(RowIndex, childFieldName, out value, out error);
    /// <summary>
    /// Tries to read a Double child field from the current struct row.
    /// </summary>
    public bool TryGetDouble(string childFieldName, out double value, out string? error) => _projector.TryGetDouble(RowIndex, childFieldName, out value, out error);
    /// <summary>
    /// Tries to read a Binary child field from the current struct row.
    /// </summary>
    public bool TryGetBinary(string childFieldName, out byte[] value, out string? error) => _projector.TryGetBinary(RowIndex, childFieldName, out value, out error);
    /// <summary>
    /// Tries to read a Date32 child field from the current struct row.
    /// </summary>
    public bool TryGetDate32(string childFieldName, DateUnit expectedUnit, out DateOnly value, out string? error) => _projector.TryGetDate32(RowIndex, childFieldName, expectedUnit, out value, out error);
    /// <summary>
    /// Tries to read a Timestamp child field from the current struct row.
    /// </summary>
    public bool TryGetTimestamp(string childFieldName, TimeUnit expectedUnit, string? expectedTimezone, out DateTimeOffset value, out string? error) => _projector.TryGetTimestamp(RowIndex, childFieldName, expectedUnit, expectedTimezone, out value, out error);
    /// <summary>
    /// Tries to read a Decimal128 child field from the current struct row.
    /// </summary>
    public bool TryGetDecimal128(string childFieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) => _projector.TryGetDecimal128(RowIndex, childFieldName, expectedPrecision, expectedScale, out value, out error);
    /// <summary>
    /// Tries to read a Decimal256 child field from the current struct row.
    /// </summary>
    public bool TryGetDecimal256(string childFieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) => _projector.TryGetDecimal256(RowIndex, childFieldName, expectedPrecision, expectedScale, out value, out error);
    /// <summary>
    /// Tries to read a List&lt;String&gt; child field from the current struct row.
    /// </summary>
    public bool TryGetStringList(string childFieldName, out IReadOnlyList<string> values, out string? error) => _projector.TryGetStringList(RowIndex, childFieldName, out values, out error);
    /// <summary>
    /// Tries to read a List&lt;Int32&gt; child field from the current struct row.
    /// </summary>
    public bool TryGetInt32List(string childFieldName, out IReadOnlyList<int> values, out string? error) => _projector.TryGetInt32List(RowIndex, childFieldName, out values, out error);
    /// <summary>
    /// Tries to read a Map&lt;String, Int32&gt; child field from the current struct row.
    /// </summary>
    public bool TryGetStringInt32Map(string childFieldName, out IReadOnlyDictionary<string, int> values, out string? error) => _projector.TryGetStringInt32Map(RowIndex, childFieldName, out values, out error);
    /// <summary>
    /// Tries to project a nested struct child field from the current struct row.
    /// </summary>
    public bool TryProjectStruct<TModel>(
        string childFieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        ArrowStructRowProjector<TModel> projector,
        out TModel? model,
        out string? error) =>
        _projector.TryProjectStruct(RowIndex, childFieldName, expectedChildFieldNames, expectedChildTypeIds, projector, out model, out error);
}

/// <summary>
/// Projects one struct-row view into a higher-level model.
/// </summary>
public delegate bool ArrowStructRowProjector<TModel>(
    ArrowStructRowAccessor row,
    out TModel? model,
    out string? error);

internal sealed class ArrowStructColumnProjector
{
    private readonly StructArray _structArray;
    private readonly string _fieldName;
    private readonly IReadOnlyList<ArrowTypeId> _childTypeIds;
    private readonly Dictionary<string, int> _childIndices;

    public ArrowStructColumnProjector(
        StructArray structArray,
        string fieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds)
    {
        ArgumentNullException.ThrowIfNull(structArray);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedChildFieldNames);
        ArgumentNullException.ThrowIfNull(expectedChildTypeIds);

        if (expectedChildFieldNames.Count != expectedChildTypeIds.Count)
        {
            throw new ArgumentException("Expected struct child-field-name and type-id lists to have the same length.");
        }

        if (structArray.Fields.Count != expectedChildFieldNames.Count)
        {
            throw new ArgumentException($"Expected struct field '{fieldName}' to expose {expectedChildFieldNames.Count} child arrays but got {structArray.Fields.Count}.");
        }

        _structArray = structArray;
        _fieldName = fieldName;
        _childTypeIds = expectedChildTypeIds;
        _childIndices = new Dictionary<string, int>(expectedChildFieldNames.Count, StringComparer.Ordinal);
        for (var index = 0; index < expectedChildFieldNames.Count; index++)
        {
            _childIndices[expectedChildFieldNames[index]] = index;
        }
    }

    public bool TryGetString(int rowIndex, string childFieldName, out string value, out string? error)
    {
        value = string.Empty;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.String, out StringArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        value = column.GetString(rowIndex, Encoding.UTF8) ?? string.Empty;
        error = null;
        return true;
    }

    public bool TryGetInt32(int rowIndex, string childFieldName, out int value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Int32, out Int32Array? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetInt64(int rowIndex, string childFieldName, out long value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Int64, out Int64Array? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetBoolean(int rowIndex, string childFieldName, out bool value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Boolean, out BooleanArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetFloat(int rowIndex, string childFieldName, out float value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Float, out FloatArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetDouble(int rowIndex, string childFieldName, out double value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Double, out DoubleArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetBinary(int rowIndex, string childFieldName, out byte[] value, out string? error)
    {
        value = System.Array.Empty<byte>();
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Binary, out BinaryArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        value = column.GetBytes(rowIndex).ToArray();
        error = null;
        return true;
    }

    public bool TryGetDate32(int rowIndex, string childFieldName, DateUnit expectedUnit, out DateOnly value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Date32, out Date32Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Data.DataType is not Date32Type dateType || dateType.Unit != expectedUnit)
        {
            var actualUnit = column.Data.DataType is Date32Type actualType ? actualType.Unit.ToString() : column.Data.DataType.Name;
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to use Date32({expectedUnit}) but got {actualUnit}.";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetDateOnly(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetTimestamp(int rowIndex, string childFieldName, TimeUnit expectedUnit, string? expectedTimezone, out DateTimeOffset value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Timestamp, out TimestampArray? column, out error) || column is null)
        {
            return false;
        }

        if (column.Data.DataType is not TimestampType timestampType)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to be materialized as TimestampType but got {column.Data.DataType.Name}.";
            return false;
        }

        if (timestampType.Unit != expectedUnit || !string.Equals(timestampType.Timezone, expectedTimezone, StringComparison.Ordinal))
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to use Timestamp({expectedUnit}, {(expectedTimezone ?? "<null>")}) but got Timestamp({timestampType.Unit}, {(timestampType.Timezone ?? "<null>")}).";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetTimestamp(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetDecimal128(int rowIndex, string childFieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Decimal128, out Decimal128Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to use Decimal128({expectedPrecision}, {expectedScale}) but got Decimal128({column.Precision}, {column.Scale}).";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetDecimal256(int rowIndex, string childFieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Decimal256, out Decimal256Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to use Decimal256({expectedPrecision}, {expectedScale}) but got Decimal256({column.Precision}, {column.Scale}).";
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        var actual = column.GetValue(rowIndex);
        if (actual is null)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' value at row {rowIndex} to be non-null.";
            return false;
        }

        value = actual.Value;
        error = null;
        return true;
    }

    public bool TryGetStringList(int rowIndex, string childFieldName, out IReadOnlyList<string> values, out string? error)
    {
        values = System.Array.Empty<string>();
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.List, out ListArray? column, out error) || column is null)
        {
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryGetStringListValue(column, $"{_fieldName}.{childFieldName}", rowIndex, out var rowValues, out error))
        {
            return false;
        }

        values = rowValues?.ToArray() ?? System.Array.Empty<string>();
        error = null;
        return true;
    }

    public bool TryGetInt32List(int rowIndex, string childFieldName, out IReadOnlyList<int> values, out string? error)
    {
        values = System.Array.Empty<int>();
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.List, out ListArray? column, out error) || column is null)
        {
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryGetInt32ListValue(column, $"{_fieldName}.{childFieldName}", rowIndex, out var rowValues, out error))
        {
            return false;
        }

        values = rowValues?.ToArray() ?? System.Array.Empty<int>();
        error = null;
        return true;
    }

    public bool TryGetStringInt32Map(int rowIndex, string childFieldName, out IReadOnlyDictionary<string, int> values, out string? error)
    {
        values = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Map, out MapArray? column, out error) || column is null)
        {
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryGetStringInt32MapValue(column, $"{_fieldName}.{childFieldName}", rowIndex, out var rowValues, out error))
        {
            return false;
        }

        values = rowValues is null ? new Dictionary<string, int>(StringComparer.Ordinal) : new Dictionary<string, int>(rowValues, StringComparer.Ordinal);
        error = null;
        return true;
    }

    public bool TryProjectStruct<TModel>(
        int rowIndex,
        string childFieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        ArrowStructRowProjector<TModel> projector,
        out TModel? model,
        out string? error)
    {
        model = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Struct, out StructArray? column, out error) || column is null)
        {
            return false;
        }

        if (!TryEnsureRowIndex(rowIndex, childFieldName, column.Length, out error))
        {
            return false;
        }

        ArrowStructColumnProjector nestedProjector;
        try
        {
            nestedProjector = new ArrowStructColumnProjector(column, $"{_fieldName}.{childFieldName}", expectedChildFieldNames, expectedChildTypeIds);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        return projector(new ArrowStructRowAccessor(nestedProjector, rowIndex), out model, out error);
    }

    private bool TryGetTypedChildColumn<TArray>(string childFieldName, ArrowTypeId expectedTypeId, out TArray? column, out string? error)
        where TArray : class
    {
        column = null;
        if (!TryResolveChildIndex(childFieldName, expectedTypeId, out var childIndex, out error))
        {
            return false;
        }

        var rawColumn = _structArray.Fields[childIndex];
        if (rawColumn is not TArray typedColumn)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to be materialized as {typeof(TArray).Name} but got {rawColumn.GetType().Name}.";
            return false;
        }

        column = typedColumn;
        error = null;
        return true;
    }

    private bool TryResolveChildIndex(string childFieldName, ArrowTypeId expectedTypeId, out int childIndex, out string? error)
    {
        if (!_childIndices.TryGetValue(childFieldName, out childIndex))
        {
            error = $"Expected struct field '{_fieldName}' to contain child field '{childFieldName}', but it was not found.";
            return false;
        }

        var configuredType = _childTypeIds[childIndex];
        if (configuredType != expectedTypeId)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to have configured type {expectedTypeId} but contract declared {configuredType}.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryEnsureRowIndex(int rowIndex, string childFieldName, int length, out string? error)
    {
        if (rowIndex < 0 || rowIndex >= length)
        {
            error = $"Expected struct field '{_fieldName}.{childFieldName}' to contain row {rowIndex}, but it has {length} rows.";
            return false;
        }

        error = null;
        return true;
    }
}
