using Apache.Arrow;
using CSharpNodeOperatorArrowDataflow;
using DoraOperator;

namespace CSharpNodeOperatorArrowForwarder;

public sealed class RecordBatchForwardOperator : DoraOperatorBase
{
    private bool _forwarded;

    protected override OnEventResult OnEvent(OperatorEvent ev, SendOutput sendOutput)
    {
        if (ev is InputEvent { Input: var input } && !_forwarded)
        {
            if (!input.HasArrow)
            {
                return OnEventResult.Err(
                    DoraOperatorErrorCode.ArrowPayloadMissing,
                    "Input did not expose an Arrow payload.");
            }

            if (input.HasBytes)
            {
                return OnEventResult.Err("Expected structured Arrow payload, but input was byte-compatible.");
            }

            if (!input.TryReadExpectedRecordBatch(
                    expectedRowCount: RichArrowContract.ExpectedNames.Length,
                    RichArrowContract.ExpectedFieldNames,
                    RichArrowContract.ExpectedTypeIds,
                    out var recordBatch,
                    out var readError,
                    out var errorCode) ||
                recordBatch is null)
            {
                return OnEventResult.Err(
                    errorCode,
                    readError ?? "Input did not contain an expected Arrow RecordBatch payload.");
            }

            using (recordBatch)
            {
                if (!ArrowRecordBatchAssertions.TryGetStringColumn(
                        recordBatch,
                        RichArrowContract.ExpectedFieldNames[0],
                        RichArrowContract.ExpectedNames,
                        out _,
                        out var validationError))
                {
                    return OnEventResult.Err(validationError ?? "Arrow RecordBatch validation failed.");
                }

                if (!ArrowRecordBatchAssertions.TryGetInt32Column(
                        recordBatch,
                        RichArrowContract.ExpectedFieldNames[1],
                        RichArrowContract.ExpectedCounts,
                        out _,
                        out validationError))
                {
                    return OnEventResult.Err(validationError ?? "Arrow RecordBatch validation failed.");
                }

                if (!ArrowRecordBatchAssertions.TryGetBooleanColumn(
                        recordBatch,
                        RichArrowContract.ExpectedFieldNames[2],
                        out var activeColumn,
                        out validationError) ||
                    activeColumn is null ||
                    !ArrowRecordBatchAssertions.TryAssertBooleanValues(
                        activeColumn,
                        RichArrowContract.ExpectedFieldNames[2],
                        RichArrowContract.ExpectedActive,
                        out validationError))
                {
                    return OnEventResult.Err(validationError ?? "Arrow RecordBatch validation failed.");
                }

                if (!ArrowRecordBatchAssertions.TryGetInt64Column(
                        recordBatch,
                        RichArrowContract.ExpectedFieldNames[3],
                        out var totalsColumn,
                        out validationError) ||
                    totalsColumn is null ||
                    !ArrowRecordBatchAssertions.TryAssertInt64Values(
                        totalsColumn,
                        RichArrowContract.ExpectedFieldNames[3],
                        RichArrowContract.ExpectedTotals,
                        out validationError))
                {
                    return OnEventResult.Err(validationError ?? "Arrow RecordBatch validation failed.");
                }

                if (!ArrowRecordBatchAssertions.TryGetFloatColumn(
                        recordBatch,
                        RichArrowContract.ExpectedFieldNames[4],
                        out var ratiosColumn,
                        out validationError) ||
                    ratiosColumn is null ||
                    !ArrowRecordBatchAssertions.TryAssertFloatValues(
                        ratiosColumn,
                        RichArrowContract.ExpectedFieldNames[4],
                        RichArrowContract.ExpectedRatios,
                        out validationError))
                {
                    return OnEventResult.Err(validationError ?? "Arrow RecordBatch validation failed.");
                }

                if (!ArrowRecordBatchAssertions.TryGetDoubleColumn(
                        recordBatch,
                        RichArrowContract.ExpectedFieldNames[5],
                        out var scoresColumn,
                        out validationError) ||
                    scoresColumn is null ||
                    !ArrowRecordBatchAssertions.TryAssertDoubleValues(
                        scoresColumn,
                        RichArrowContract.ExpectedFieldNames[5],
                        RichArrowContract.ExpectedScores,
                        out validationError))
                {
                    return OnEventResult.Err(validationError ?? "Arrow RecordBatch validation failed.");
                }

                var sendResult = sendOutput.Send("batch", recordBatch);
                if (!sendResult.IsSuccess)
                {
                    return OnEventResult.Err(sendResult.Error ?? "Failed to forward Arrow RecordBatch.");
                }
            }

            _forwarded = true;
            return OnEventResult.Continue();
        }

        if (ev is InputClosedEvent)
        {
            return _forwarded
                ? OnEventResult.Stop()
                : OnEventResult.Err(
                    DoraOperatorErrorCode.LifecycleViolation,
                    "Arrow input closed before operator forwarding completed.");
        }

        if (ev is StopEvent)
        {
            return OnEventResult.Stop();
        }

        if (ev is ErrorEvent error)
        {
            return OnEventResult.Err(error.Message);
        }

        return OnEventResult.Continue();
    }
}