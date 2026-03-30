using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraNode;

/// <summary>
/// Describes a validated Arrow <see cref="RecordBatch"/> contract that can be projected into a typed model.
/// </summary>
public interface IArrowRecordBatchContract<TModel>
{
    long? ExpectedRowCount { get; }

    IReadOnlyList<string> ExpectedFieldNames { get; }

    IReadOnlyList<ArrowTypeId> ExpectedTypeIds { get; }

    bool TryRead(RecordBatch recordBatch, out TModel? model, out string? error);
}

/// <summary>
/// Base implementation for schema-first Arrow <see cref="RecordBatch"/> contracts.
/// </summary>
public abstract class ArrowRecordBatchContract<TModel> : IArrowRecordBatchContract<TModel>
{
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

    public long? ExpectedRowCount { get; }

    public IReadOnlyList<string> ExpectedFieldNames { get; }

    public IReadOnlyList<ArrowTypeId> ExpectedTypeIds { get; }

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

    protected abstract bool TryMap(RecordBatch recordBatch, out TModel? model, out string? error);
}
