using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraOperator;

/// <summary>
/// Helper methods for validating common Arrow schema and record-batch expectations.
/// </summary>
public static class ArrowSchemaValidation
{
    /// <summary>
    /// Validates that a schema contains the expected number of fields.
    /// </summary>
    public static bool TryValidateFieldCount(Schema schema, int expectedFieldCount, out string? error)
    {
        ArgumentNullException.ThrowIfNull(schema);

        error = null;
        var fields = schema.FieldsList;
        if (fields.Count != expectedFieldCount)
        {
            error = $"Expected {expectedFieldCount} schema fields but got {fields.Count}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a schema field by index, name, and Arrow type.
    /// </summary>
    public static bool TryValidateField(
        Schema schema,
        int index,
        string expectedFieldName,
        ArrowTypeId expectedTypeId,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFieldName);

        error = null;
        var fields = schema.FieldsList;
        if (index < 0 || index >= fields.Count)
        {
            error = $"Expected schema field index {index} to exist, but schema has {fields.Count} fields.";
            return false;
        }

        var field = fields[index];
        if (!string.Equals(field.Name, expectedFieldName, StringComparison.Ordinal))
        {
            error = $"Expected field {index} to be '{expectedFieldName}' but got '{field.Name}'.";
            return false;
        }

        if (field.DataType.TypeId != expectedTypeId)
        {
            error = $"Expected field '{field.Name}' to have type {expectedTypeId} but got {field.DataType.TypeId}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the index of a field by name.
    /// </summary>
    public static bool TryGetFieldIndex(
        Schema schema,
        string expectedFieldName,
        out int index,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFieldName);

        var fields = schema.FieldsList;
        for (index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].Name, expectedFieldName, StringComparison.Ordinal))
            {
                error = null;
                return true;
            }
        }

        index = -1;
        error = $"Expected schema to contain field '{expectedFieldName}', but it was not found.";
        return false;
    }

    /// <summary>
    /// Validates a schema field by name and Arrow type.
    /// </summary>
    public static bool TryValidateField(
        Schema schema,
        string expectedFieldName,
        ArrowTypeId expectedTypeId,
        out int index,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFieldName);

        if (!TryGetFieldIndex(schema, expectedFieldName, out index, out error))
        {
            return false;
        }

        var field = schema.FieldsList[index];
        if (field.DataType.TypeId != expectedTypeId)
        {
            error = $"Expected field '{field.Name}' to have type {expectedTypeId} but got {field.DataType.TypeId}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a binary field.
    /// </summary>
    public static bool TryValidateBinaryField(
        Schema schema,
        string expectedFieldName,
        out int index,
        out string? error)
    {
        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.Binary, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not BinaryType)
        {
            error = $"Expected field '{expectedFieldName}' to use BinaryType.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a Date32 field with the expected unit.
    /// </summary>
    public static bool TryValidateDate32Field(
        Schema schema,
        string expectedFieldName,
        DateUnit expectedUnit,
        out int index,
        out string? error)
    {
        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.Date32, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not Date32Type dateType)
        {
            error = $"Expected field '{expectedFieldName}' to use Date32Type.";
            return false;
        }

        if (dateType.Unit != expectedUnit)
        {
            error = $"Expected field '{expectedFieldName}' to use {expectedUnit} date unit but got {dateType.Unit}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a timestamp field with the expected unit and timezone.
    /// </summary>
    public static bool TryValidateTimestampField(
        Schema schema,
        string expectedFieldName,
        TimeUnit expectedUnit,
        string? expectedTimezone,
        out int index,
        out string? error)
    {
        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.Timestamp, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not TimestampType timestampType)
        {
            error = $"Expected field '{expectedFieldName}' to use TimestampType.";
            return false;
        }

        if (timestampType.Unit != expectedUnit)
        {
            error = $"Expected field '{expectedFieldName}' to use {expectedUnit} timestamp unit but got {timestampType.Unit}.";
            return false;
        }

        if (!string.Equals(timestampType.Timezone, expectedTimezone, StringComparison.Ordinal))
        {
            error = $"Expected field '{expectedFieldName}' to use timezone '{expectedTimezone}' but got '{timestampType.Timezone}'.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a Decimal128 field with the expected precision and scale.
    /// </summary>
    public static bool TryValidateDecimal128Field(
        Schema schema,
        string expectedFieldName,
        int expectedPrecision,
        int expectedScale,
        out int index,
        out string? error)
    {
        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.Decimal128, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not Decimal128Type decimalType)
        {
            error = $"Expected field '{expectedFieldName}' to use Decimal128Type.";
            return false;
        }

        if (decimalType.Precision != expectedPrecision)
        {
            error = $"Expected field '{expectedFieldName}' to use precision {expectedPrecision} but got {decimalType.Precision}.";
            return false;
        }

        if (decimalType.Scale != expectedScale)
        {
            error = $"Expected field '{expectedFieldName}' to use scale {expectedScale} but got {decimalType.Scale}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a Decimal256 field with the expected precision and scale.
    /// </summary>
    public static bool TryValidateDecimal256Field(
        Schema schema,
        string expectedFieldName,
        int expectedPrecision,
        int expectedScale,
        out int index,
        out string? error)
    {
        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.Decimal256, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not Decimal256Type decimalType)
        {
            error = $"Expected field '{expectedFieldName}' to use Decimal256Type.";
            return false;
        }

        if (decimalType.Precision != expectedPrecision)
        {
            error = $"Expected field '{expectedFieldName}' to use precision {expectedPrecision} but got {decimalType.Precision}.";
            return false;
        }

        if (decimalType.Scale != expectedScale)
        {
            error = $"Expected field '{expectedFieldName}' to use scale {expectedScale} but got {decimalType.Scale}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a list field with the expected value type.
    /// </summary>
    public static bool TryValidateListField(
        Schema schema,
        string expectedFieldName,
        ArrowTypeId expectedValueTypeId,
        out int index,
        out string? error) =>
        TryValidateListField(schema, expectedFieldName, expectedValueFieldName: null, expectedValueTypeId, out index, out error);

    /// <summary>
    /// Validates that a schema field is a list field with the expected value field name and type.
    /// </summary>
    public static bool TryValidateListField(
        Schema schema,
        string expectedFieldName,
        string? expectedValueFieldName,
        ArrowTypeId expectedValueTypeId,
        out int index,
        out string? error)
    {
        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.List, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not ListType listType)
        {
            error = $"Expected field '{expectedFieldName}' to use ListType.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedValueFieldName) &&
            !string.Equals(listType.ValueField.Name, expectedValueFieldName, StringComparison.Ordinal))
        {
            error = $"Expected list field '{expectedFieldName}' to use value field '{expectedValueFieldName}' but got '{listType.ValueField.Name}'.";
            return false;
        }

        if (listType.ValueDataType.TypeId != expectedValueTypeId)
        {
            error = $"Expected list field '{expectedFieldName}' to use value type {expectedValueTypeId} but got {listType.ValueDataType.TypeId}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a map field with the expected key and value types.
    /// </summary>
    public static bool TryValidateMapField(
        Schema schema,
        string expectedFieldName,
        ArrowTypeId expectedKeyTypeId,
        ArrowTypeId expectedValueTypeId,
        out int index,
        out string? error) =>
        TryValidateMapField(
            schema,
            expectedFieldName,
            expectedKeyFieldName: null,
            expectedValueFieldName: null,
            expectedKeyTypeId,
            expectedValueTypeId,
            out index,
            out error);

    /// <summary>
    /// Validates that a schema field is a map field with the expected key/value field names and types.
    /// </summary>
    public static bool TryValidateMapField(
        Schema schema,
        string expectedFieldName,
        string? expectedKeyFieldName,
        string? expectedValueFieldName,
        ArrowTypeId expectedKeyTypeId,
        ArrowTypeId expectedValueTypeId,
        out int index,
        out string? error)
    {
        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.Map, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not MapType mapType)
        {
            error = $"Expected field '{expectedFieldName}' to use MapType.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedKeyFieldName) &&
            !string.Equals(mapType.KeyField.Name, expectedKeyFieldName, StringComparison.Ordinal))
        {
            error = $"Expected map field '{expectedFieldName}' to use key field '{expectedKeyFieldName}' but got '{mapType.KeyField.Name}'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedValueFieldName) &&
            !string.Equals(mapType.ValueField.Name, expectedValueFieldName, StringComparison.Ordinal))
        {
            error = $"Expected map field '{expectedFieldName}' to use value field '{expectedValueFieldName}' but got '{mapType.ValueField.Name}'.";
            return false;
        }

        if (mapType.KeyField.DataType.TypeId != expectedKeyTypeId)
        {
            error = $"Expected map field '{expectedFieldName}' to use key type {expectedKeyTypeId} but got {mapType.KeyField.DataType.TypeId}.";
            return false;
        }

        if (mapType.ValueField.DataType.TypeId != expectedValueTypeId)
        {
            error = $"Expected map field '{expectedFieldName}' to use value type {expectedValueTypeId} but got {mapType.ValueField.DataType.TypeId}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema field is a struct field with the expected child-field contract.
    /// </summary>
    public static bool TryValidateStructField(
        Schema schema,
        string expectedFieldName,
        IReadOnlyList<string> expectedChildFieldNames,
        IReadOnlyList<ArrowTypeId> expectedChildTypeIds,
        out int index,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(expectedChildFieldNames);
        ArgumentNullException.ThrowIfNull(expectedChildTypeIds);

        if (expectedChildFieldNames.Count != expectedChildTypeIds.Count)
        {
            index = -1;
            error = "Expected struct child-field-name and type-id lists to have the same length.";
            return false;
        }

        if (!TryValidateField(schema, expectedFieldName, ArrowTypeId.Struct, out index, out error))
        {
            return false;
        }

        if (schema.FieldsList[index].DataType is not StructType structType)
        {
            error = $"Expected field '{expectedFieldName}' to use StructType.";
            return false;
        }

        if (structType.Fields.Count != expectedChildFieldNames.Count)
        {
            error = $"Expected struct field '{expectedFieldName}' to contain {expectedChildFieldNames.Count} child fields but got {structType.Fields.Count}.";
            return false;
        }

        for (var childIndex = 0; childIndex < expectedChildFieldNames.Count; childIndex++)
        {
            var actualField = structType.Fields[childIndex];
            var expectedChildFieldName = expectedChildFieldNames[childIndex];
            if (!string.Equals(actualField.Name, expectedChildFieldName, StringComparison.Ordinal))
            {
                error = $"Expected struct field '{expectedFieldName}' child {childIndex} to be '{expectedChildFieldName}' but got '{actualField.Name}'.";
                return false;
            }

            var expectedChildTypeId = expectedChildTypeIds[childIndex];
            if (actualField.DataType.TypeId != expectedChildTypeId)
            {
                error = $"Expected struct field '{expectedFieldName}' child '{actualField.Name}' to have type {expectedChildTypeId} but got {actualField.DataType.TypeId}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates that a schema matches the expected field-name and type sequence.
    /// </summary>
    public static bool TryValidateSchema(
        Schema schema,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<ArrowTypeId> expectedTypeIds,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        ArgumentNullException.ThrowIfNull(expectedTypeIds);

        error = null;
        if (expectedFieldNames.Count != expectedTypeIds.Count)
        {
            error = "Expected field-name and type-id lists to have the same length.";
            return false;
        }

        if (!TryValidateFieldCount(schema, expectedFieldNames.Count, out error))
        {
            return false;
        }

        for (var index = 0; index < expectedFieldNames.Count; index++)
        {
            if (!TryValidateField(schema, index, expectedFieldNames[index], expectedTypeIds[index], out error))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that a record batch contains the expected number of columns.
    /// </summary>
    public static bool TryValidateColumnCount(RecordBatch recordBatch, int expectedColumnCount, out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);

        error = null;
        if (recordBatch.ColumnCount != expectedColumnCount)
        {
            error = $"Expected {expectedColumnCount} columns but got {recordBatch.ColumnCount}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a record batch contains the expected number of rows.
    /// </summary>
    public static bool TryValidateRowCount(RecordBatch recordBatch, long expectedRowCount, out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);

        error = null;
        if (recordBatch.Length != expectedRowCount)
        {
            error = $"Expected {expectedRowCount} rows but got {recordBatch.Length}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the row count, column count, and schema of a record batch.
    /// </summary>
    public static bool TryValidateRecordBatch(
        RecordBatch recordBatch,
        long? expectedRowCount,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<ArrowTypeId> expectedTypeIds,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        ArgumentNullException.ThrowIfNull(expectedTypeIds);

        error = null;

        if (!TryValidateColumnCount(recordBatch, expectedFieldNames.Count, out error))
        {
            return false;
        }

        if (expectedRowCount is long rowCount && !TryValidateRowCount(recordBatch, rowCount, out error))
        {
            return false;
        }

        return TryValidateSchema(recordBatch.Schema, expectedFieldNames, expectedTypeIds, out error);
    }
}
