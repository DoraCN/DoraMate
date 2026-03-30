using System.Text;
using Apache.Arrow;
using DoraNode;
using CSharpArrowNodeDataflow;

namespace CSharpArrowNodeConsumer;

class Program
{
    private const string ExpectedNonArrowPayload = "not-arrow-bytes";

    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# Arrow node consumer...");

        try
        {
            using var node = new DoraNode.DoraNode();

            while (true)
            {
                using var ev = node.Next();
                if (ev is null)
                {
                    Console.WriteLine("Consumer event stream closed");
                    break;
                }

                if (ev.Type == EventType.Input)
                {
                    var mode = GetTestMode();

                    if (mode == ArrowTestMode.NonArrowBytes)
                    {
                        if (ev.TryReadRecordBatch(out var unexpectedBatch) && unexpectedBatch is not null)
                        {
                            unexpectedBatch.Dispose();
                            Console.Error.WriteLine("TryReadRecordBatch unexpectedly succeeded for non-Arrow input.");
                            Environment.Exit(1);
                        }

                        if (ev.TryReadExpectedRecordBatch(
                                RichArrowContract.ExpectedRowCount,
                                RichArrowContract.ExpectedFieldNames,
                                RichArrowContract.ExpectedTypeIds,
                                out var unexpectedExpectedBatch,
                                out var expectedReadError,
                                out var expectedErrorCode))
                        {
                            unexpectedExpectedBatch?.Dispose();
                            Console.Error.WriteLine("TryReadExpectedRecordBatch unexpectedly succeeded for non-Arrow input.");
                            Environment.Exit(1);
                        }

                        if (expectedErrorCode != DoraNodeErrorCode.ArrowPayloadMissing)
                        {
                            Console.Error.WriteLine(
                                $"Expected TryReadExpectedRecordBatch error code '{DoraNodeErrorCode.ArrowPayloadMissing}' but got '{expectedErrorCode}'.");
                            Environment.Exit(1);
                        }

                        var payloadText = ev.Data is { Length: > 0 } data
                            ? Encoding.UTF8.GetString(data)
                            : string.Empty;

                        if (!string.Equals(payloadText, ExpectedNonArrowPayload, StringComparison.Ordinal))
                        {
                            Console.Error.WriteLine($"Expected raw payload '{ExpectedNonArrowPayload}' but got '{payloadText}'.");
                            Environment.Exit(1);
                        }

                        Console.WriteLine(
                            $"NODE_ARROW_EXPECTED_NON_ARROW_FAILURE_OK code={expectedErrorCode} bytes={payloadText}");
                        return;
                    }

                    if (!ev.TryReadExpectedRecordBatch(
                            RichArrowContract.ExpectedRowCount,
                            RichArrowContract.ExpectedFieldNames,
                            RichArrowContract.ExpectedTypeIds,
                            out var batch,
                            out var readError,
                            out var errorCode))
                    {
                        if (!TryHandleExpectedValidationFailure(mode, readError, errorCode, out var summary, out var failureError))
                        {
                            Console.Error.WriteLine(failureError ?? "Consumer input did not contain an expected Arrow RecordBatch.");
                            Environment.Exit(1);
                        }

                        Console.WriteLine(summary);
                        return;
                    }

                    if (batch is null)
                    {
                        Console.Error.WriteLine("TryReadExpectedRecordBatch succeeded but returned a null RecordBatch.");
                        Environment.Exit(1);
                    }

                    using (batch)
                    {
                        if (!TryValidate(batch, out var summary, out var error))
                        {
                            Console.Error.WriteLine(error);
                            Environment.Exit(1);
                        }

                        Console.WriteLine(summary);
                        return;
                    }
                }

                if (ev.Type == EventType.Stop)
                {
                    Console.WriteLine("Consumer received stop event");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Consumer error: {ex.Message}");
            Environment.Exit(1);
        }
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

    private static bool TryValidate(RecordBatch batch, out string summary, out string? error)
    {
        summary = string.Empty;
        error = null;

        if (!ArrowRecordBatchAssertions.TryGetStringColumn(
                batch,
                RichArrowContract.ExpectedFieldNames[0],
                RichArrowContract.ExpectedNames,
                out var nameArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetInt32Column(
                batch,
                RichArrowContract.ExpectedFieldNames[1],
                RichArrowContract.ExpectedCounts,
                out var countArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetBooleanColumn(
                batch,
                RichArrowContract.ExpectedFieldNames[2],
                RichArrowContract.ExpectedActive,
                out var activeArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetInt64Column(
                batch,
                RichArrowContract.ExpectedFieldNames[3],
                RichArrowContract.ExpectedTotals,
                out var totalArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetFloatColumn(
                batch,
                RichArrowContract.ExpectedFieldNames[4],
                RichArrowContract.ExpectedRatios,
                out var ratioArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetDoubleColumn(
                batch,
                RichArrowContract.ExpectedFieldNames[5],
                RichArrowContract.ExpectedScores,
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

        summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("NODE_ARROW_ROUNDTRIP_OK");
        return true;
    }

    private static bool TryHandleExpectedValidationFailure(
        ArrowTestMode mode,
        string? actualError,
        DoraNodeErrorCode actualErrorCode,
        out string summary,
        out string? error)
    {
        summary = string.Empty;
        error = actualError;

        var (expectedErrorCode, expectedError) = mode switch
        {
            ArrowTestMode.SchemaMismatch => (
                DoraNodeErrorCode.SchemaValidationFailed,
                "Expected field 0 to be 'name' but got 'label'."),
            ArrowTestMode.EmptyBatch => (
                DoraNodeErrorCode.SchemaValidationFailed,
                "Expected 2 rows but got 0."),
            _ => (DoraNodeErrorCode.Unknown, null)
        };

        if (expectedError is null)
        {
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
            ? $"NODE_ARROW_EXPECTED_SCHEMA_MISMATCH_OK code={actualErrorCode} error={actualError}"
            : $"NODE_ARROW_EXPECTED_EMPTY_BATCH_OK code={actualErrorCode} error={actualError}";
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
