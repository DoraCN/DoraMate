using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraOperator;

/// <summary>
/// Describes a validated Arrow record-batch contract that can be projected into a typed model.
/// </summary>
public interface IArrowRecordBatchContract<TModel>
{
    /// <summary>
    /// Gets the expected row count, or <see langword="null"/> when row count is unconstrained.
    /// </summary>
    long? ExpectedRowCount { get; }

    /// <summary>
    /// Gets the expected field names in schema order.
    /// </summary>
    IReadOnlyList<string> ExpectedFieldNames { get; }

    /// <summary>
    /// Gets the expected Arrow type IDs in schema order.
    /// </summary>
    IReadOnlyList<ArrowTypeId> ExpectedTypeIds { get; }

    /// <summary>
    /// Validates and projects the supplied record batch into a typed model.
    /// </summary>
    bool TryRead(RecordBatch recordBatch, out TModel? model, out string? error);
}

/// <summary>
/// Base implementation for schema-first Arrow record-batch contracts.
/// </summary>
public abstract class ArrowRecordBatchContract<TModel> : IArrowRecordBatchContract<TModel>
{
    /// <summary>
    /// Initializes a schema-first record-batch contract.
    /// </summary>
    protected ArrowRecordBatchContract(
        long? expectedRowCount,
        IReadOnlyList<string> expectedFieldNames,
        IReadOnlyList<ArrowTypeId> expectedTypeIds)
    {
        ArgumentNullException.ThrowIfNull(expectedFieldNames);
        ArgumentNullException.ThrowIfNull(expectedTypeIds);

        ExpectedRowCount = expectedRowCount;
        ExpectedFieldNames = expectedFieldNames;
        ExpectedTypeIds = expectedTypeIds;
    }

    /// <inheritdoc />
    public long? ExpectedRowCount { get; }
    /// <inheritdoc />
    public IReadOnlyList<string> ExpectedFieldNames { get; }
    /// <inheritdoc />
    public IReadOnlyList<ArrowTypeId> ExpectedTypeIds { get; }

    /// <inheritdoc />
    public bool TryRead(RecordBatch recordBatch, out TModel? model, out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);

        model = default;
        if (!ArrowSchemaValidation.TryValidateRecordBatch(
                recordBatch,
                ExpectedRowCount,
                ExpectedFieldNames,
                ExpectedTypeIds,
                out error))
        {
            return false;
        }

        return TryMap(recordBatch, out model, out error);
    }

    /// <summary>
    /// Projects each record-batch row into a typed row model using the contract schema.
    /// </summary>
    protected bool TryProjectRows<TRow>(
        RecordBatch recordBatch,
        ArrowRecordBatchRowProjector<TRow> projector,
        out IReadOnlyList<TRow>? rows,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentNullException.ThrowIfNull(projector);

        return ArrowRecordBatchProjector.TryProjectRows(
            recordBatch,
            ExpectedFieldNames,
            ExpectedTypeIds,
            projector,
            out rows,
            out error);
    }

    /// <summary>
    /// Projects each row and composes the projected rows into the final model.
    /// </summary>
    protected bool TryProjectModel<TRow>(
        RecordBatch recordBatch,
        ArrowRecordBatchRowProjector<TRow> projector,
        Func<IReadOnlyList<TRow>, TModel> modelFactory,
        out TModel? model,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(modelFactory);

        model = default;
        if (!TryProjectRows(recordBatch, projector, out IReadOnlyList<TRow>? rows, out error))
        {
            return false;
        }

        if (rows is null)
        {
            error = "RecordBatch row projection succeeded but returned a null row collection.";
            return false;
        }

        model = modelFactory(rows);
        error = null;
        return true;
    }

    /// <summary>
    /// Maps a validated record batch into the target model type.
    /// </summary>
    protected abstract bool TryMap(RecordBatch recordBatch, out TModel? model, out string? error);
}
