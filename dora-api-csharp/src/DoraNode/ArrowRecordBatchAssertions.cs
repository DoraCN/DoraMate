using System.Text;
using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraNode;

/// <summary>
/// Assertion-style helpers for reading typed Arrow columns from a <see cref="RecordBatch"/>.
/// </summary>
public static class ArrowRecordBatchAssertions
{
    /// <summary>
    /// Tries to read a column at the given index as the expected managed Arrow array type.
    /// </summary>
    public static bool TryGetColumn<TArray>(
        RecordBatch recordBatch,
        int index,
        string expectedFieldName,
        ArrowTypeId expectedTypeId,
        out TArray? column,
        out string? error)
        where TArray : class
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateField(recordBatch.Schema, index, expectedFieldName, expectedTypeId, out error))
        {
            return false;
        }

        var rawColumn = recordBatch.Column(index);
        if (rawColumn is not TArray typedColumn)
        {
            error = $"Expected column '{expectedFieldName}' to be materialized as {typeof(TArray).Name} but got {rawColumn.GetType().Name}.";
            return false;
        }

        column = typedColumn;
        error = null;
        return true;
    }

    /// <summary>
    /// Tries to read a column by field name as the expected managed Arrow array type.
    /// </summary>
    public static bool TryGetColumn<TArray>(
        RecordBatch recordBatch,
        string expectedFieldName,
        ArrowTypeId expectedTypeId,
        out TArray? column,
        out string? error)
        where TArray : class
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateField(recordBatch.Schema, expectedFieldName, expectedTypeId, out var index, out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, expectedFieldName, expectedTypeId, out column, out error);
    }

    /// <summary>
    /// Tries to read a string column by field name.
    /// </summary>
    public static bool TryGetStringColumn(
        RecordBatch recordBatch,
        string fieldName,
        out StringArray? column,
        out string? error) =>
        TryGetColumn(recordBatch, fieldName, ArrowTypeId.String, out column, out error);

    /// <summary>
    /// Tries to read a string column by field name and validate its values.
    /// </summary>
    public static bool TryGetStringColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<string> expectedValues,
        out StringArray? column,
        out string? error)
    {
        if (!TryGetStringColumn(recordBatch, fieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected string column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertStringValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read an Int32 column by field name.
    /// </summary>
    public static bool TryGetInt32Column(
        RecordBatch recordBatch,
        string fieldName,
        out Int32Array? column,
        out string? error) =>
        TryGetColumn(recordBatch, fieldName, ArrowTypeId.Int32, out column, out error);

    /// <summary>
    /// Tries to read an Int32 column by field name and validate its values.
    /// </summary>
    public static bool TryGetInt32Column(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<int> expectedValues,
        out Int32Array? column,
        out string? error)
    {
        if (!TryGetInt32Column(recordBatch, fieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Int32 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertInt32Values(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read an Int64 column by field name.
    /// </summary>
    public static bool TryGetInt64Column(
        RecordBatch recordBatch,
        string fieldName,
        out Int64Array? column,
        out string? error) =>
        TryGetColumn(recordBatch, fieldName, ArrowTypeId.Int64, out column, out error);

    /// <summary>
    /// Tries to read an Int64 column by field name and validate its values.
    /// </summary>
    public static bool TryGetInt64Column(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<long> expectedValues,
        out Int64Array? column,
        out string? error)
    {
        if (!TryGetInt64Column(recordBatch, fieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Int64 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertInt64Values(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Boolean column by field name.
    /// </summary>
    public static bool TryGetBooleanColumn(
        RecordBatch recordBatch,
        string fieldName,
        out BooleanArray? column,
        out string? error) =>
        TryGetColumn(recordBatch, fieldName, ArrowTypeId.Boolean, out column, out error);

    /// <summary>
    /// Tries to read a Boolean column by field name and validate its values.
    /// </summary>
    public static bool TryGetBooleanColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<bool> expectedValues,
        out BooleanArray? column,
        out string? error)
    {
        if (!TryGetBooleanColumn(recordBatch, fieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Boolean column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertBooleanValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Float column by field name.
    /// </summary>
    public static bool TryGetFloatColumn(
        RecordBatch recordBatch,
        string fieldName,
        out FloatArray? column,
        out string? error) =>
        TryGetColumn(recordBatch, fieldName, ArrowTypeId.Float, out column, out error);

    /// <summary>
    /// Tries to read a Float column by field name and validate its values.
    /// </summary>
    public static bool TryGetFloatColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<float> expectedValues,
        out FloatArray? column,
        out string? error)
    {
        if (!TryGetFloatColumn(recordBatch, fieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Float column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertFloatValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Double column by field name.
    /// </summary>
    public static bool TryGetDoubleColumn(
        RecordBatch recordBatch,
        string fieldName,
        out DoubleArray? column,
        out string? error) =>
        TryGetColumn(recordBatch, fieldName, ArrowTypeId.Double, out column, out error);

    /// <summary>
    /// Tries to read a Double column by field name and validate its values.
    /// </summary>
    public static bool TryGetDoubleColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<double> expectedValues,
        out DoubleArray? column,
        out string? error)
    {
        if (!TryGetDoubleColumn(recordBatch, fieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Double column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertDoubleValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Binary column by field name.
    /// </summary>
    public static bool TryGetBinaryColumn(
        RecordBatch recordBatch,
        string fieldName,
        out BinaryArray? column,
        out string? error) =>
        TryGetColumn(recordBatch, fieldName, ArrowTypeId.Binary, out column, out error);

    /// <summary>
    /// Tries to read a Binary column by field name and validate its values.
    /// </summary>
    public static bool TryGetBinaryColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<byte[]> expectedValues,
        out BinaryArray? column,
        out string? error)
    {
        if (!TryGetBinaryColumn(recordBatch, fieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Binary column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertBinaryValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Date32 column by field name and validate its date unit.
    /// </summary>
    public static bool TryGetDate32Column(
        RecordBatch recordBatch,
        string fieldName,
        DateUnit expectedUnit,
        out Date32Array? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateDate32Field(recordBatch.Schema, fieldName, expectedUnit, out var index, out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.Date32, out column, out error);
    }

    /// <summary>
    /// Tries to read a Date32 column by field name, validate its date unit, and validate its values.
    /// </summary>
    public static bool TryGetDate32Column(
        RecordBatch recordBatch,
        string fieldName,
        DateUnit expectedUnit,
        IReadOnlyList<DateOnly> expectedValues,
        out Date32Array? column,
        out string? error)
    {
        if (!TryGetDate32Column(recordBatch, fieldName, expectedUnit, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Date32 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertDate32Values(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Timestamp column by field name and validate its unit and timezone.
    /// </summary>
    public static bool TryGetTimestampColumn(
        RecordBatch recordBatch,
        string fieldName,
        TimeUnit expectedUnit,
        string? expectedTimezone,
        out TimestampArray? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateTimestampField(
                recordBatch.Schema,
                fieldName,
                expectedUnit,
                expectedTimezone,
                out var index,
                out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.Timestamp, out column, out error);
    }

    /// <summary>
    /// Tries to read a Timestamp column by field name, validate its unit/timezone, and validate its values.
    /// </summary>
    public static bool TryGetTimestampColumn(
        RecordBatch recordBatch,
        string fieldName,
        TimeUnit expectedUnit,
        string? expectedTimezone,
        IReadOnlyList<DateTimeOffset> expectedValues,
        out TimestampArray? column,
        out string? error)
    {
        if (!TryGetTimestampColumn(recordBatch, fieldName, expectedUnit, expectedTimezone, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Timestamp column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertTimestampValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Decimal128 column by field name and validate its precision and scale.
    /// </summary>
    public static bool TryGetDecimal128Column(
        RecordBatch recordBatch,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        out Decimal128Array? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateDecimal128Field(
                recordBatch.Schema,
                fieldName,
                expectedPrecision,
                expectedScale,
                out var index,
                out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.Decimal128, out column, out error);
    }

    /// <summary>
    /// Tries to read a Decimal128 column by field name, validate its precision/scale, and validate its values.
    /// </summary>
    public static bool TryGetDecimal128Column(
        RecordBatch recordBatch,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        IReadOnlyList<decimal> expectedValues,
        out Decimal128Array? column,
        out string? error)
    {
        if (!TryGetDecimal128Column(recordBatch, fieldName, expectedPrecision, expectedScale, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Decimal128 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertDecimal128Values(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Decimal256 column by field name and validate its precision and scale.
    /// </summary>
    public static bool TryGetDecimal256Column(
        RecordBatch recordBatch,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        out Decimal256Array? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateDecimal256Field(
                recordBatch.Schema,
                fieldName,
                expectedPrecision,
                expectedScale,
                out var index,
                out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.Decimal256, out column, out error);
    }

    /// <summary>
    /// Tries to read a Decimal256 column by field name, validate its precision/scale, and validate its values.
    /// </summary>
    public static bool TryGetDecimal256Column(
        RecordBatch recordBatch,
        string fieldName,
        int expectedPrecision,
        int expectedScale,
        IReadOnlyList<decimal> expectedValues,
        out Decimal256Array? column,
        out string? error)
    {
        if (!TryGetDecimal256Column(recordBatch, fieldName, expectedPrecision, expectedScale, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Decimal256 column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertDecimal256Values(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a List&lt;String&gt; column by field name.
    /// </summary>
    public static bool TryGetStringListColumn(
        RecordBatch recordBatch,
        string fieldName,
        out ListArray? column,
        out string? error) =>
        TryGetStringListColumn(recordBatch, fieldName, expectedValueFieldName: null, out column, out error);

    /// <summary>
    /// Tries to read a List&lt;String&gt; column by field name and validate its child-field name.
    /// </summary>
    public static bool TryGetStringListColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedValueFieldName,
        out ListArray? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateListField(
                recordBatch.Schema,
                fieldName,
                expectedValueFieldName,
                ArrowTypeId.String,
                out var index,
                out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.List, out column, out error);
    }

    /// <summary>
    /// Tries to read a List&lt;String&gt; column by field name and validate its values.
    /// </summary>
    public static bool TryGetStringListColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<IReadOnlyList<string>> expectedValues,
        out ListArray? column,
        out string? error) =>
        TryGetStringListColumn(recordBatch, fieldName, expectedValueFieldName: null, expectedValues, out column, out error);

    /// <summary>
    /// Tries to read a List&lt;String&gt; column by field name, validate its child-field name, and validate its values.
    /// </summary>
    public static bool TryGetStringListColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedValueFieldName,
        IReadOnlyList<IReadOnlyList<string>> expectedValues,
        out ListArray? column,
        out string? error)
    {
        if (!TryGetStringListColumn(recordBatch, fieldName, expectedValueFieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected List<String> column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertStringListValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a List&lt;Int32&gt; column by field name.
    /// </summary>
    public static bool TryGetInt32ListColumn(
        RecordBatch recordBatch,
        string fieldName,
        out ListArray? column,
        out string? error) =>
        TryGetInt32ListColumn(recordBatch, fieldName, expectedValueFieldName: null, out column, out error);

    /// <summary>
    /// Tries to read a List&lt;Int32&gt; column by field name and validate its child-field name.
    /// </summary>
    public static bool TryGetInt32ListColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedValueFieldName,
        out ListArray? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateListField(
                recordBatch.Schema,
                fieldName,
                expectedValueFieldName,
                ArrowTypeId.Int32,
                out var index,
                out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.List, out column, out error);
    }

    /// <summary>
    /// Tries to read a List&lt;Int32&gt; column by field name and validate its values.
    /// </summary>
    public static bool TryGetInt32ListColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<IReadOnlyList<int>> expectedValues,
        out ListArray? column,
        out string? error) =>
        TryGetInt32ListColumn(recordBatch, fieldName, expectedValueFieldName: null, expectedValues, out column, out error);

    /// <summary>
    /// Tries to read a List&lt;Int32&gt; column by field name, validate its child-field name, and validate its values.
    /// </summary>
    public static bool TryGetInt32ListColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedValueFieldName,
        IReadOnlyList<IReadOnlyList<int>> expectedValues,
        out ListArray? column,
        out string? error)
    {
        if (!TryGetInt32ListColumn(recordBatch, fieldName, expectedValueFieldName, out column, out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected List<Int32> column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertInt32ListValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Map&lt;String, Int32&gt; column by field name.
    /// </summary>
    public static bool TryGetStringInt32MapColumn(
        RecordBatch recordBatch,
        string fieldName,
        out MapArray? column,
        out string? error) =>
        TryGetStringInt32MapColumn(
            recordBatch,
            fieldName,
            expectedKeyFieldName: null,
            expectedValueFieldName: null,
            out column,
            out error);

    /// <summary>
    /// Tries to read a Map&lt;String, Int32&gt; column by field name and validate its key/value field names.
    /// </summary>
    public static bool TryGetStringInt32MapColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedKeyFieldName,
        string? expectedValueFieldName,
        out MapArray? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateMapField(
                recordBatch.Schema,
                fieldName,
                expectedKeyFieldName,
                expectedValueFieldName,
                ArrowTypeId.String,
                ArrowTypeId.Int32,
                out var index,
                out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.Map, out column, out error);
    }

    /// <summary>
    /// Tries to read a Map&lt;String, Int32&gt; column by field name and validate its values.
    /// </summary>
    public static bool TryGetStringInt32MapColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<IReadOnlyDictionary<string, int>> expectedValues,
        out MapArray? column,
        out string? error) =>
        TryGetStringInt32MapColumn(
            recordBatch,
            fieldName,
            expectedKeyFieldName: null,
            expectedValueFieldName: null,
            expectedValues,
            out column,
            out error);

    /// <summary>
    /// Tries to read a Map&lt;String, Int32&gt; column by field name, validate its key/value field names, and validate its values.
    /// </summary>
    public static bool TryGetStringInt32MapColumn(
        RecordBatch recordBatch,
        string fieldName,
        string? expectedKeyFieldName,
        string? expectedValueFieldName,
        IReadOnlyList<IReadOnlyDictionary<string, int>> expectedValues,
        out MapArray? column,
        out string? error)
    {
        if (!TryGetStringInt32MapColumn(
                recordBatch,
                fieldName,
                expectedKeyFieldName,
                expectedValueFieldName,
                out column,
                out error))
        {
            return false;
        }

        if (column is null)
        {
            error = $"Expected Map<String, Int32> column '{fieldName}' but the resolved Arrow array was null.";
            return false;
        }

        return TryAssertStringInt32MapValues(column, fieldName, expectedValues, out error);
    }

    /// <summary>
    /// Tries to read a Struct column by field name and validate its child fields.
    /// </summary>
    public static bool TryGetStructColumn(
        RecordBatch recordBatch,
        string fieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        out StructArray? column,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        column = null;
        if (!ArrowSchemaValidation.TryValidateStructField(
                recordBatch.Schema,
                fieldName,
                expectedChildFieldNames,
                expectedChildTypeIds,
                out var index,
                out error))
        {
            return false;
        }

        return TryGetColumn(recordBatch, index, fieldName, ArrowTypeId.Struct, out column, out error);
    }

    /// <summary>
    /// Tries to read a typed child column from a validated Struct column.
    /// </summary>
    public static bool TryGetStructFieldColumn<TArray>(
        StructArray structArray,
        string parentFieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        string childFieldName,
        ArrowTypeId expectedTypeId,
        out TArray? column,
        out string? error)
        where TArray : class
    {
        ArgumentNullException.ThrowIfNull(structArray);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentFieldName);
        ArgumentNullException.ThrowIfNull(expectedChildFieldNames);
        ArgumentNullException.ThrowIfNull(expectedChildTypeIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(childFieldName);

        column = null;
        if (expectedChildFieldNames.Count != expectedChildTypeIds.Count)
        {
            error = "Expected struct child-field-name and type-id lists to have the same length.";
            return false;
        }

        var childIndex = -1;
        for (var index = 0; index < expectedChildFieldNames.Count; index++)
        {
            if (string.Equals(expectedChildFieldNames[index], childFieldName, StringComparison.Ordinal))
            {
                childIndex = index;
                break;
            }
        }

        if (childIndex < 0)
        {
            error = $"Expected struct field '{parentFieldName}' to contain child field '{childFieldName}', but it was not found.";
            return false;
        }

        var configuredType = expectedChildTypeIds[childIndex];
        if (configuredType != expectedTypeId)
        {
            error =
                $"Expected struct field '{parentFieldName}' child '{childFieldName}' to have configured type {expectedTypeId} but contract declared {configuredType}.";
            return false;
        }

        if (structArray.Fields.Count <= childIndex)
        {
            error =
                $"Expected struct field '{parentFieldName}' to expose child index {childIndex} for '{childFieldName}', but only {structArray.Fields.Count} child arrays were present.";
            return false;
        }

        var rawColumn = structArray.Fields[childIndex];
        if (rawColumn is not TArray typedColumn)
        {
            error =
                $"Expected struct field '{parentFieldName}.{childFieldName}' to be materialized as {typeof(TArray).Name} but got {rawColumn.GetType().Name}.";
            return false;
        }

        column = typedColumn;
        error = null;
        return true;
    }

    /// <summary>
    /// Tries to read a string child column from a validated Struct column.
    /// </summary>
    public static bool TryGetStructStringFieldColumn(
        StructArray structArray,
        string parentFieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        string childFieldName,
        out StringArray? column,
        out string? error) =>
        TryGetStructFieldColumn(
            structArray,
            parentFieldName,
            expectedChildFieldNames,
            expectedChildTypeIds,
            childFieldName,
            ArrowTypeId.String,
            out column,
            out error);

    /// <summary>
    /// Tries to read an Int32 child column from a validated Struct column.
    /// </summary>
    public static bool TryGetStructInt32FieldColumn(
        StructArray structArray,
        string parentFieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        string childFieldName,
        out Int32Array? column,
        out string? error) =>
        TryGetStructFieldColumn(
            structArray,
            parentFieldName,
            expectedChildFieldNames,
            expectedChildTypeIds,
            childFieldName,
            ArrowTypeId.Int32,
            out column,
            out error);

    /// <summary>
    /// Validates that a string column contains the expected values in order.
    /// </summary>
    public static bool TryAssertStringValues(
        StringArray column,
        string fieldName,
        IReadOnlyList<string> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected string column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetString(index, Encoding.UTF8);
            var expected = expectedValues[index];
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                error = $"Expected string column '{fieldName}' value at row {index} to be '{expected}' but got '{actual}'.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that an Int32 column contains the expected values in order.
    /// </summary>
    public static bool TryAssertInt32Values(
        Int32Array column,
        string fieldName,
        IReadOnlyList<int> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Int32 column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetValue(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Int32 column '{fieldName}' value at row {index} to be {expected} but got {actual}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that an Int64 column contains the expected values in order.
    /// </summary>
    public static bool TryAssertInt64Values(
        Int64Array column,
        string fieldName,
        IReadOnlyList<long> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Int64 column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetValue(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Int64 column '{fieldName}' value at row {index} to be {expected} but got {actual}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Boolean column contains the expected values in order.
    /// </summary>
    public static bool TryAssertBooleanValues(
        BooleanArray column,
        string fieldName,
        IReadOnlyList<bool> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Boolean column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetValue(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Boolean column '{fieldName}' value at row {index} to be {expected} but got {actual}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Float column contains the expected values in order.
    /// </summary>
    public static bool TryAssertFloatValues(
        FloatArray column,
        string fieldName,
        IReadOnlyList<float> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Float column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetValue(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Float column '{fieldName}' value at row {index} to be {expected} but got {actual}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Double column contains the expected values in order.
    /// </summary>
    public static bool TryAssertDoubleValues(
        DoubleArray column,
        string fieldName,
        IReadOnlyList<double> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Double column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetValue(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Double column '{fieldName}' value at row {index} to be {expected} but got {actual}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Binary column contains the expected values in order.
    /// </summary>
    public static bool TryAssertBinaryValues(
        BinaryArray column,
        string fieldName,
        IReadOnlyList<byte[]> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Binary column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetBytes(index);
            var expected = expectedValues[index] ?? System.Array.Empty<byte>();
            if (!actual.SequenceEqual(expected))
            {
                error =
                    $"Expected Binary column '{fieldName}' value at row {index} to be {BitConverter.ToString(expected)} but got {BitConverter.ToString(actual.ToArray())}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Date32 column contains the expected values in order.
    /// </summary>
    public static bool TryAssertDate32Values(
        Date32Array column,
        string fieldName,
        IReadOnlyList<DateOnly> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Date32 column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetDateOnly(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Date32 column '{fieldName}' value at row {index} to be {expected:yyyy-MM-dd} but got {actual:yyyy-MM-dd}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Timestamp column contains the expected values in order.
    /// </summary>
    public static bool TryAssertTimestampValues(
        TimestampArray column,
        string fieldName,
        IReadOnlyList<DateTimeOffset> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Timestamp column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetTimestamp(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Timestamp column '{fieldName}' value at row {index} to be {expected:O} but got {actual:O}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Decimal128 column contains the expected values in order.
    /// </summary>
    public static bool TryAssertDecimal128Values(
        Decimal128Array column,
        string fieldName,
        IReadOnlyList<decimal> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Decimal128 column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetValue(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Decimal128 column '{fieldName}' value at row {index} to be {expected} but got {actual}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Decimal256 column contains the expected values in order.
    /// </summary>
    public static bool TryAssertDecimal256Values(
        Decimal256Array column,
        string fieldName,
        IReadOnlyList<decimal> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Decimal256 column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var index = 0; index < expectedValues.Count; index++)
        {
            var actual = column.GetValue(index);
            var expected = expectedValues[index];
            if (actual != expected)
            {
                error = $"Expected Decimal256 column '{fieldName}' value at row {index} to be {expected} but got {actual}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Tries to read one row from a validated List&lt;String&gt; column.
    /// </summary>
    public static bool TryGetStringListValue(
        ListArray column,
        string fieldName,
        int rowIndex,
        out IReadOnlyList<string>? values,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        values = null;
        if (rowIndex < 0 || rowIndex >= column.Length)
        {
            error = $"Expected List<String> column '{fieldName}' to contain row {rowIndex}, but it has {column.Length} rows.";
            return false;
        }

        if (column.Values is not StringArray valueArray)
        {
            error = $"Expected List<String> column '{fieldName}' to use StringArray values but got {column.Values.GetType().Name}.";
            return false;
        }

        var valueOffsets = column.ValueOffsets;
        var offset = valueOffsets[rowIndex];
        var length = column.GetValueLength(rowIndex);
        var rowValues = new string[length];
        for (var valueIndex = 0; valueIndex < length; valueIndex++)
        {
            rowValues[valueIndex] = valueArray.GetString(offset + valueIndex, Encoding.UTF8) ?? string.Empty;
        }

        values = rowValues;
        error = null;
        return true;
    }

    /// <summary>
    /// Tries to read one row from a validated List&lt;Int32&gt; column.
    /// </summary>
    public static bool TryGetInt32ListValue(
        ListArray column,
        string fieldName,
        int rowIndex,
        out IReadOnlyList<int>? values,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        values = null;
        if (rowIndex < 0 || rowIndex >= column.Length)
        {
            error = $"Expected List<Int32> column '{fieldName}' to contain row {rowIndex}, but it has {column.Length} rows.";
            return false;
        }

        if (column.Values is not Int32Array valueArray)
        {
            error = $"Expected List<Int32> column '{fieldName}' to use Int32Array values but got {column.Values.GetType().Name}.";
            return false;
        }

        var valueOffsets = column.ValueOffsets;
        var offset = valueOffsets[rowIndex];
        var length = column.GetValueLength(rowIndex);
        var rowValues = new int[length];
        for (var valueIndex = 0; valueIndex < length; valueIndex++)
        {
            var value = valueArray.GetValue(offset + valueIndex);
            if (value is null)
            {
                error =
                    $"Expected List<Int32> column '{fieldName}' row {rowIndex} value {valueIndex} to be non-null.";
                return false;
            }

            rowValues[valueIndex] = value.Value;
        }

        values = rowValues;
        error = null;
        return true;
    }

    /// <summary>
    /// Tries to read one row from a validated Map&lt;String, Int32&gt; column.
    /// </summary>
    public static bool TryGetStringInt32MapValue(
        MapArray column,
        string fieldName,
        int rowIndex,
        out IReadOnlyDictionary<string, int>? values,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        values = null;
        if (rowIndex < 0 || rowIndex >= column.Length)
        {
            error = $"Expected Map<String, Int32> column '{fieldName}' to contain row {rowIndex}, but it has {column.Length} rows.";
            return false;
        }

        if (column.Keys is not StringArray keyArray)
        {
            error = $"Expected Map<String, Int32> column '{fieldName}' to use StringArray keys but got {column.Keys.GetType().Name}.";
            return false;
        }

        if (column.Values is not Int32Array valueArray)
        {
            error = $"Expected Map<String, Int32> column '{fieldName}' to use Int32Array values but got {column.Values.GetType().Name}.";
            return false;
        }

        var valueOffsets = column.ValueOffsets;
        var offset = valueOffsets[rowIndex];
        var length = rowIndex + 1 < valueOffsets.Length
            ? valueOffsets[rowIndex + 1] - offset
            : keyArray.Length - offset;

        var rowValues = new Dictionary<string, int>(length, StringComparer.Ordinal);
        for (var valueIndex = 0; valueIndex < length; valueIndex++)
        {
            var actualIndex = offset + valueIndex;
            var key = keyArray.GetString(actualIndex, Encoding.UTF8);
            if (key is null)
            {
                error =
                    $"Expected Map<String, Int32> column '{fieldName}' row {rowIndex} key {valueIndex} to be non-null.";
                return false;
            }

            var value = valueArray.GetValue(actualIndex);
            if (value is null)
            {
                error =
                    $"Expected Map<String, Int32> column '{fieldName}' row {rowIndex} value {valueIndex} to be non-null.";
                return false;
            }

            if (!rowValues.TryAdd(key, value.Value))
            {
                error =
                    $"Expected Map<String, Int32> column '{fieldName}' row {rowIndex} keys to be unique, but duplicate key '{key}' was found.";
                return false;
            }
        }

        values = rowValues;
        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a List&lt;String&gt; column contains the expected values in order.
    /// </summary>
    public static bool TryAssertStringListValues(
        ListArray column,
        string fieldName,
        IReadOnlyList<IReadOnlyList<string>> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected List<String> column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var rowIndex = 0; rowIndex < expectedValues.Count; rowIndex++)
        {
            if (!TryGetStringListValue(column, fieldName, rowIndex, out var actualValues, out error))
            {
                return false;
            }

            var expectedRow = expectedValues[rowIndex] ?? System.Array.Empty<string>();
            if (actualValues is null || actualValues.Count != expectedRow.Count)
            {
                error =
                    $"Expected List<String> column '{fieldName}' row {rowIndex} to contain {expectedRow.Count} values but got {actualValues?.Count ?? 0}.";
                return false;
            }

            for (var valueIndex = 0; valueIndex < expectedRow.Count; valueIndex++)
            {
                var actual = actualValues[valueIndex];
                var expected = expectedRow[valueIndex];
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    error =
                        $"Expected List<String> column '{fieldName}' row {rowIndex} value {valueIndex} to be '{expected}' but got '{actual}'.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a Map&lt;String, Int32&gt; column contains the expected values in order.
    /// </summary>
    public static bool TryAssertStringInt32MapValues(
        MapArray column,
        string fieldName,
        IReadOnlyList<IReadOnlyDictionary<string, int>> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected Map<String, Int32> column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var rowIndex = 0; rowIndex < expectedValues.Count; rowIndex++)
        {
            if (!TryGetStringInt32MapValue(column, fieldName, rowIndex, out var actualValues, out error))
            {
                return false;
            }

            var expectedRow = expectedValues[rowIndex] ?? new Dictionary<string, int>(StringComparer.Ordinal);
            if (actualValues is null || actualValues.Count != expectedRow.Count)
            {
                error =
                    $"Expected Map<String, Int32> column '{fieldName}' row {rowIndex} to contain {expectedRow.Count} entries but got {actualValues?.Count ?? 0}.";
                return false;
            }

            foreach (var expectedEntry in expectedRow)
            {
                if (!actualValues.TryGetValue(expectedEntry.Key, out var actualValue))
                {
                    error =
                        $"Expected Map<String, Int32> column '{fieldName}' row {rowIndex} to contain key '{expectedEntry.Key}', but it was not found.";
                    return false;
                }

                if (actualValue != expectedEntry.Value)
                {
                    error =
                        $"Expected Map<String, Int32> column '{fieldName}' row {rowIndex} key '{expectedEntry.Key}' to be {expectedEntry.Value} but got {actualValue}.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a List&lt;Int32&gt; column contains the expected values in order.
    /// </summary>
    public static bool TryAssertInt32ListValues(
        ListArray column,
        string fieldName,
        IReadOnlyList<IReadOnlyList<int>> expectedValues,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(expectedValues);

        if (column.Length != expectedValues.Count)
        {
            error = $"Expected List<Int32> column '{fieldName}' to contain {expectedValues.Count} values but got {column.Length}.";
            return false;
        }

        for (var rowIndex = 0; rowIndex < expectedValues.Count; rowIndex++)
        {
            if (!TryGetInt32ListValue(column, fieldName, rowIndex, out var actualValues, out error))
            {
                return false;
            }

            var expectedRow = expectedValues[rowIndex] ?? System.Array.Empty<int>();
            if (actualValues is null || actualValues.Count != expectedRow.Count)
            {
                error =
                    $"Expected List<Int32> column '{fieldName}' row {rowIndex} to contain {expectedRow.Count} values but got {actualValues?.Count ?? 0}.";
                return false;
            }

            for (var valueIndex = 0; valueIndex < expectedRow.Count; valueIndex++)
            {
                var actual = actualValues[valueIndex];
                var expected = expectedRow[valueIndex];
                if (actual != expected)
                {
                    error =
                        $"Expected List<Int32> column '{fieldName}' row {rowIndex} value {valueIndex} to be {expected} but got {actual}.";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}
