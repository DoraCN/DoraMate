using Apache.Arrow;
using DoraOperator;

namespace CSharpArrowRoundtrip;

public sealed class RecordBatchVerifierOperator : DoraOperatorBase
{
    private bool _verified;

    protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);

        if (_verified)
        {
            return OnEventResult.Continue();
        }

        var mode = GetTestMode();

        if (mode == ArrowTestMode.NonArrowBytes)
        {
            return HandleExpectedNonArrowInput(ev.Input, output);
        }

        if (!ev.Input.HasArrow)
        {
            return OnEventResult.Err(
                DoraOperatorErrorCode.ArrowPayloadMissing,
                "Input did not expose an Arrow payload.");
        }

        if (!ev.Input.TryReadExpectedRecordBatch(
                RoundtripContractFixture.ExpectedRowCount,
                RoundtripContractFixture.ExpectedFieldNames,
                RoundtripContractFixture.ExpectedTypeIds,
                out var recordBatch,
                out var readError,
                out var errorCode))
        {
            if (!TryHandleExpectedValidationFailure(mode, readError, errorCode, out var summary, out var validationError))
            {
                return OnEventResult.Err(
                    errorCode,
                    validationError ?? "Input did not contain an expected Arrow RecordBatch payload.");
            }

            output.SendOrThrow("summary", summary);
            _verified = true;
            return OnEventResult.Continue();
        }

        if (recordBatch is null)
        {
            return OnEventResult.Err("TryReadExpectedRecordBatch succeeded but returned a null RecordBatch.");
        }

        using (recordBatch)
        {
            if (!TryValidateRecordBatch(recordBatch, out var summary, out var validationError))
            {
                return OnEventResult.Err(validationError ?? "Arrow RecordBatch validation failed.");
            }

            output.SendOrThrow("summary", summary);
        }

        _verified = true;
        return OnEventResult.Continue();
    }

    protected override OnEventResult OnInputClosed(InputClosedEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        return _verified
            ? OnEventResult.Stop()
            : OnEventResult.Err(
                DoraOperatorErrorCode.LifecycleViolation,
                "Arrow input closed before round-trip verification completed.");
    }

    private OnEventResult HandleExpectedNonArrowInput(Input input, OperatorOutput output)
    {
        if (!input.HasBytes)
        {
            return OnEventResult.Err(
                DoraOperatorErrorCode.ArrowPayloadMissing,
                "Expected byte-compatible payload for non-Arrow negative test.");
        }

        var payloadText = input.GetUtf8String();

        if (input.HasArrow)
        {
            return OnEventResult.Err("Input still exposed an Arrow payload after bytes were materialized.");
        }

        if (input.TryReadRecordBatch(out var unexpectedBatch) && unexpectedBatch is not null)
        {
            unexpectedBatch.Dispose();
            return OnEventResult.Err("TryReadRecordBatch unexpectedly succeeded after bytes were materialized.");
        }

        if (input.TryReadExpectedRecordBatch(
                RoundtripContractFixture.ExpectedRowCount,
                RoundtripContractFixture.ExpectedFieldNames,
                RoundtripContractFixture.ExpectedTypeIds,
                out var unexpectedExpectedBatch,
                out _,
                out var expectedErrorCode))
        {
            unexpectedExpectedBatch?.Dispose();
            return OnEventResult.Err("TryReadExpectedRecordBatch unexpectedly succeeded after bytes were materialized.");
        }

        if (expectedErrorCode != DoraOperatorErrorCode.ArrowPayloadMissing)
        {
            return OnEventResult.Err(
                $"Expected TryReadExpectedRecordBatch error code '{DoraOperatorErrorCode.ArrowPayloadMissing}' but got '{expectedErrorCode}'.");
        }

        var summary = $"ARROW_EXPECTED_NON_ARROW_PATH_OK code={expectedErrorCode} bytes={payloadText}";
        output.SendOrThrow("summary", summary);
        _verified = true;
        return OnEventResult.Continue();
    }

    private static ArrowTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ARROW_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => ArrowTestMode.Normal,
            "non-arrow-bytes" => ArrowTestMode.NonArrowBytes,
            "schema-mismatch" => ArrowTestMode.SchemaMismatch,
            "empty-batch" => ArrowTestMode.EmptyBatch,
            _ => throw new InvalidOperationException($"Unsupported Arrow test mode '{rawMode}'.")
        };
    }

    private static bool TryValidateRecordBatch(
        RecordBatch recordBatch,
        out string summary,
        out string? error)
    {
        summary = string.Empty;
        error = null;

        if (!ArrowRecordBatchAssertions.TryGetStringColumn(
                recordBatch,
                RoundtripContractFixture.ExpectedFieldNames[0],
                RoundtripContractFixture.ExpectedNames,
                out var nameArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetInt32Column(
                recordBatch,
                RoundtripContractFixture.ExpectedFieldNames[1],
                RoundtripContractFixture.ExpectedCounts,
                out var countArray,
                out error))
        {
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryGetBooleanColumn(
                recordBatch,
                RoundtripContractFixture.ExpectedFieldNames[2],
                out var activeArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetInt64Column(
                recordBatch,
                RoundtripContractFixture.ExpectedFieldNames[3],
                out var totalArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetFloatColumn(
                recordBatch,
                RoundtripContractFixture.ExpectedFieldNames[4],
                out var ratioArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetDoubleColumn(
                recordBatch,
                RoundtripContractFixture.ExpectedFieldNames[5],
                out var scoreArray,
                out error))
        {
            return false;
        }

        if (nameArray is null ||
            countArray is null ||
            activeArray is null ||
            totalArray is null ||
            ratioArray is null ||
            scoreArray is null)
        {
            error = "Typed Arrow column assertions unexpectedly returned null arrays.";
            return false;
        }

        if (!ArrowRecordBatchAssertions.TryAssertBooleanValues(
                activeArray,
                RoundtripContractFixture.ExpectedFieldNames[2],
                RoundtripContractFixture.ExpectedActive,
                out error) ||
            !ArrowRecordBatchAssertions.TryAssertInt64Values(
                totalArray,
                RoundtripContractFixture.ExpectedFieldNames[3],
                RoundtripContractFixture.ExpectedTotals,
                out error) ||
            !ArrowRecordBatchAssertions.TryAssertFloatValues(
                ratioArray,
                RoundtripContractFixture.ExpectedFieldNames[4],
                RoundtripContractFixture.ExpectedRatios,
                out error) ||
            !ArrowRecordBatchAssertions.TryAssertDoubleValues(
                scoreArray,
                RoundtripContractFixture.ExpectedFieldNames[5],
                RoundtripContractFixture.ExpectedScores,
                out error))
        {
            return false;
        }

        summary = ArrowRecordBatchSummary.Create(recordBatch).ToSummaryString("ARROW_ROUNDTRIP_OK");
        return true;
    }

    private static bool TryHandleExpectedValidationFailure(
        ArrowTestMode mode,
        string? actualError,
        DoraOperatorErrorCode actualErrorCode,
        out string summary,
        out string? error)
    {
        summary = string.Empty;
        error = actualError;

        var (expectedErrorCode, expectedError) = mode switch
        {
            ArrowTestMode.SchemaMismatch => (
                DoraOperatorErrorCode.SchemaValidationFailed,
                "Expected field 0 to be 'name' but got 'label'."),
            ArrowTestMode.EmptyBatch => (
                DoraOperatorErrorCode.SchemaValidationFailed,
                "Expected 2 rows but got 0."),
            _ => (DoraOperatorErrorCode.Unknown, null)
        };

        if (expectedError is null) {
            return actualError is null;
        }

        if (actualErrorCode != expectedErrorCode)
        {
            error = $"Expected validation error code '{expectedErrorCode}' but got '{actualErrorCode}'.";
            return false;
        }

        if (actualError is null)
        {
            error = "Validation unexpectedly succeeded for a negative Arrow test case.";
            return false;
        }

        if (!string.Equals(actualError, expectedError, StringComparison.Ordinal))
        {
            error = $"Expected validation error '{expectedError}' but got '{actualError}'.";
            return false;
        }

        summary = mode == ArrowTestMode.SchemaMismatch
            ? $"ARROW_EXPECTED_SCHEMA_MISMATCH_OK code={actualErrorCode} error={actualError}"
            : $"ARROW_EXPECTED_EMPTY_BATCH_OK code={actualErrorCode} error={actualError}";
        error = null;
        return true;
    }

    private enum ArrowTestMode
    {
        Normal,
        NonArrowBytes,
        SchemaMismatch,
        EmptyBatch
    }
}
