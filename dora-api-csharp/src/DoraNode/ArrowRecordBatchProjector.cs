using System.Text;
using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraNode;

/// <summary>
/// Projects validated Arrow columns and struct rows into higher-level managed models.
/// </summary>
public static class ArrowRecordBatchProjector
{
    /// <summary>
    /// Projects a List&lt;String&gt; column into managed row lists.
    /// </summary>
    public static bool TryProjectStringListColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<IReadOnlyList<string>>? rows,
        out string? error) =>
        TryProjectStringListColumn(recordBatch, fieldName, expectedValueFieldName: null, out rows, out error);

    /// <summary>
    /// Projects a List&lt;String&gt; column into managed row lists and validates its child-field name.
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
    /// Projects a List&lt;Int32&gt; column into managed row lists.
    /// </summary>
    public static bool TryProjectInt32ListColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<IReadOnlyList<int>>? rows,
        out string? error) =>
        TryProjectInt32ListColumn(recordBatch, fieldName, expectedValueFieldName: null, out rows, out error);

    /// <summary>
    /// Projects a List&lt;Int32&gt; column into managed row lists and validates its child-field name.
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
    /// Projects a Map&lt;String, Int32&gt; column into managed row dictionaries.
    /// </summary>
    public static bool TryProjectStringInt32MapColumn(
        RecordBatch recordBatch,
        string fieldName,
        out IReadOnlyList<IReadOnlyDictionary<string, int>>? rows,
        out string? error) =>
        TryProjectStringInt32MapColumn(
            recordBatch,
            fieldName,
            expectedKeyFieldName: null,
            expectedValueFieldName: null,
            out rows,
            out error);

    /// <summary>
    /// Projects a Map&lt;String, Int32&gt; column into managed row dictionaries and validates its key/value field names.
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
    /// Projects a Struct column into managed row models.
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
        if (!ArrowRecordBatchAssertions.TryGetStructColumn(
                recordBatch,
                fieldName,
                expectedChildFieldNames,
                expectedChildTypeIds,
                out var column,
                out error))
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

    /// <summary>
    /// Projects each record-batch row into a managed model using typed top-level field accessors.
    /// </summary>
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
            columnProjector = new ArrowStructColumnProjector(
                structArray,
                fieldName,
                expectedChildFieldNames,
                expectedChildTypeIds);
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

            projectedRows.Add(
                values is null
                    ? new Dictionary<string, int>(StringComparer.Ordinal)
                    : new Dictionary<string, int>(values, StringComparer.Ordinal));
        }

        rows = projectedRows;
        error = null;
        return true;
    }
}

/// <summary>
/// Row-level accessor for projecting Arrow RecordBatch rows into managed models.
/// </summary>
public sealed class ArrowRecordBatchRowAccessor
{
    private readonly ArrowRecordBatchColumnProjector _projector;

    internal ArrowRecordBatchRowAccessor(ArrowRecordBatchColumnProjector projector, int rowIndex)
    {
        _projector = projector;
        RowIndex = rowIndex;
    }

    /// <summary>
    /// Gets the current record-batch row index.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Reads a non-null string field value from the current record-batch row.
    /// </summary>
    public bool TryGetString(string fieldName, out string value, out string? error) =>
        _projector.TryGetString(RowIndex, fieldName, out value, out error);

    /// <summary>
    /// Reads a non-null Int32 field value from the current record-batch row.
    /// </summary>
    public bool TryGetInt32(string fieldName, out int value, out string? error) =>
        _projector.TryGetInt32(RowIndex, fieldName, out value, out error);

    /// <summary>
    /// Reads a non-null Decimal128 field value from the current record-batch row.
    /// </summary>
    public bool TryGetDecimal128(string fieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) =>
        _projector.TryGetDecimal128(RowIndex, fieldName, expectedPrecision, expectedScale, out value, out error);

    /// <summary>
    /// Reads a non-null Decimal256 field value from the current record-batch row.
    /// </summary>
    public bool TryGetDecimal256(string fieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) =>
        _projector.TryGetDecimal256(RowIndex, fieldName, expectedPrecision, expectedScale, out value, out error);

    /// <summary>
    /// Reads a List&lt;String&gt; field value from the current record-batch row.
    /// </summary>
    public bool TryGetStringList(string fieldName, out IReadOnlyList<string> values, out string? error) =>
        _projector.TryGetStringList(RowIndex, fieldName, out values, out error);

    /// <summary>
    /// Reads a List&lt;Int32&gt; field value from the current record-batch row.
    /// </summary>
    public bool TryGetInt32List(string fieldName, out IReadOnlyList<int> values, out string? error) =>
        _projector.TryGetInt32List(RowIndex, fieldName, out values, out error);

    /// <summary>
    /// Reads a Map&lt;String, Int32&gt; field value from the current record-batch row.
    /// </summary>
    public bool TryGetStringInt32Map(string fieldName, out IReadOnlyDictionary<string, int> values, out string? error) =>
        _projector.TryGetStringInt32Map(RowIndex, fieldName, out values, out error);

    /// <summary>
    /// Projects a Struct field into a managed sub-model for the current record-batch row.
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
/// Delegate used to project a validated Arrow RecordBatch row into a managed model.
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
            throw new ArgumentException(
                $"Expected record batch to expose {expectedFieldNames.Count} columns but got {recordBatch.ColumnCount}.");
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

    public bool TryGetDecimal128(
        int rowIndex,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        out decimal value,
        out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Decimal128, out Decimal128Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error =
                $"Expected column '{fieldName}' to use Decimal128({expectedPrecision}, {expectedScale}) but got Decimal128({column.Precision}, {column.Scale}).";
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

    public bool TryGetDecimal256(
        int rowIndex,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        out decimal value,
        out string? error)
    {
        value = default;
        if (!TryGetTypedColumn(fieldName, ArrowTypeId.Decimal256, out Decimal256Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error =
                $"Expected column '{fieldName}' to use Decimal256({expectedPrecision}, {expectedScale}) but got Decimal256({column.Precision}, {column.Scale}).";
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

        values = rowValues is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(rowValues, StringComparer.Ordinal);
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
            nestedProjector = new ArrowStructColumnProjector(
                column,
                fieldName,
                expectedChildFieldNames,
                expectedChildTypeIds);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        return projector(new ArrowStructRowAccessor(nestedProjector, rowIndex), out model, out error);
    }

    private bool TryGetTypedColumn<TArray>(
        string fieldName,
        ArrowTypeId expectedTypeId,
        out TArray? column,
        out string? error)
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
            error =
                $"Expected column '{fieldName}' to be materialized as {typeof(TArray).Name} but got {rawColumn.GetType().Name}.";
            return false;
        }

        column = typedColumn;
        error = null;
        return true;
    }

    private bool TryResolveIndex(
        string fieldName,
        ArrowTypeId expectedTypeId,
        out int index,
        out string? error)
    {
        if (!_indices.TryGetValue(fieldName, out index))
        {
            error = $"Expected record batch to contain field '{fieldName}', but it was not found.";
            return false;
        }

        var configuredType = _typeIds[index];
        if (configuredType != expectedTypeId)
        {
            error =
                $"Expected field '{fieldName}' to have configured type {expectedTypeId} but contract declared {configuredType}.";
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

/// <summary>
/// Row-level accessor for projecting Arrow Struct rows into managed models.
/// </summary>
public sealed class ArrowStructRowAccessor
{
    private readonly ArrowStructColumnProjector _projector;

    internal ArrowStructRowAccessor(ArrowStructColumnProjector projector, int rowIndex)
    {
        _projector = projector;
        RowIndex = rowIndex;
    }

    /// <summary>
    /// Gets the current struct row index.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Reads a non-null string child value from the current struct row.
    /// </summary>
    public bool TryGetString(string childFieldName, out string value, out string? error) =>
        _projector.TryGetString(RowIndex, childFieldName, out value, out error);

    /// <summary>
    /// Reads a non-null Int32 child value from the current struct row.
    /// </summary>
    public bool TryGetInt32(string childFieldName, out int value, out string? error) =>
        _projector.TryGetInt32(RowIndex, childFieldName, out value, out error);

    /// <summary>
    /// Reads a non-null Decimal128 child value from the current struct row.
    /// </summary>
    public bool TryGetDecimal128(string childFieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) =>
        _projector.TryGetDecimal128(RowIndex, childFieldName, expectedPrecision, expectedScale, out value, out error);

    /// <summary>
    /// Reads a non-null Decimal256 child value from the current struct row.
    /// </summary>
    public bool TryGetDecimal256(string childFieldName, int expectedPrecision, int expectedScale, out decimal value, out string? error) =>
        _projector.TryGetDecimal256(RowIndex, childFieldName, expectedPrecision, expectedScale, out value, out error);

    /// <summary>
    /// Reads a List&lt;String&gt; child value from the current struct row.
    /// </summary>
    public bool TryGetStringList(string childFieldName, out IReadOnlyList<string> values, out string? error) =>
        _projector.TryGetStringList(RowIndex, childFieldName, out values, out error);

    /// <summary>
    /// Reads a List&lt;Int32&gt; child value from the current struct row.
    /// </summary>
    public bool TryGetInt32List(string childFieldName, out IReadOnlyList<int> values, out string? error) =>
        _projector.TryGetInt32List(RowIndex, childFieldName, out values, out error);

    /// <summary>
    /// Reads a Map&lt;String, Int32&gt; child value from the current struct row.
    /// </summary>
    public bool TryGetStringInt32Map(string childFieldName, out IReadOnlyDictionary<string, int> values, out string? error) =>
        _projector.TryGetStringInt32Map(RowIndex, childFieldName, out values, out error);

    /// <summary>
    /// Projects a nested Struct child into a managed sub-model for the current row.
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
/// Delegate used to project a validated Arrow Struct row into a managed model.
/// </summary>
public delegate bool ArrowStructRowProjector<TModel>(
    ArrowStructRowAccessor row,
    out TModel? model,
    out string? error);

internal sealed class ArrowStructColumnProjector
{
    private readonly StructArray _structArray;
    private readonly string _fieldName;
    private readonly IReadOnlyList<string> _childFieldNames;
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
            throw new ArgumentException(
                $"Expected struct field '{fieldName}' to expose {expectedChildFieldNames.Count} child arrays but got {structArray.Fields.Count}.");
        }

        _structArray = structArray;
        _fieldName = fieldName;
        _childFieldNames = expectedChildFieldNames;
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

    public bool TryGetDecimal128(
        int rowIndex,
        string childFieldName,
        int expectedPrecision,
        int expectedScale,
        out decimal value,
        out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Decimal128, out Decimal128Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error =
                $"Expected struct field '{_fieldName}.{childFieldName}' to use Decimal128({expectedPrecision}, {expectedScale}) but got Decimal128({column.Precision}, {column.Scale}).";
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

    public bool TryGetDecimal256(
        int rowIndex,
        string childFieldName,
        int expectedPrecision,
        int expectedScale,
        out decimal value,
        out string? error)
    {
        value = default;
        if (!TryGetTypedChildColumn(childFieldName, ArrowTypeId.Decimal256, out Decimal256Array? column, out error) || column is null)
        {
            return false;
        }

        if (column.Precision != expectedPrecision || column.Scale != expectedScale)
        {
            error =
                $"Expected struct field '{_fieldName}.{childFieldName}' to use Decimal256({expectedPrecision}, {expectedScale}) but got Decimal256({column.Precision}, {column.Scale}).";
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

        values = rowValues is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(rowValues, StringComparer.Ordinal);
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
            nestedProjector = new ArrowStructColumnProjector(
                column,
                $"{_fieldName}.{childFieldName}",
                expectedChildFieldNames,
                expectedChildTypeIds);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        return projector(new ArrowStructRowAccessor(nestedProjector, rowIndex), out model, out error);
    }

    private bool TryGetTypedChildColumn<TArray>(
        string childFieldName,
        ArrowTypeId expectedTypeId,
        out TArray? column,
        out string? error)
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
            error =
                $"Expected struct field '{_fieldName}.{childFieldName}' to be materialized as {typeof(TArray).Name} but got {rawColumn.GetType().Name}.";
            return false;
        }

        column = typedColumn;
        error = null;
        return true;
    }

    private bool TryResolveChildIndex(
        string childFieldName,
        ArrowTypeId expectedTypeId,
        out int childIndex,
        out string? error)
    {
        if (!_childIndices.TryGetValue(childFieldName, out childIndex))
        {
            error = $"Expected struct field '{_fieldName}' to contain child field '{childFieldName}', but it was not found.";
            return false;
        }

        var configuredType = _childTypeIds[childIndex];
        if (configuredType != expectedTypeId)
        {
            error =
                $"Expected struct field '{_fieldName}.{childFieldName}' to have configured type {expectedTypeId} but contract declared {configuredType}.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryEnsureRowIndex(int rowIndex, string childFieldName, int length, out string? error)
    {
        if (rowIndex < 0 || rowIndex >= length)
        {
            error =
                $"Expected struct field '{_fieldName}.{childFieldName}' to contain row {rowIndex}, but it has {length} rows.";
            return false;
        }

        error = null;
        return true;
    }
}
