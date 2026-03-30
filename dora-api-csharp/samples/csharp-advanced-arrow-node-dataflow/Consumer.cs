using Apache.Arrow;
using CSharpAdvancedArrowNodeDataflow;
using DoraNode;

namespace CSharpAdvancedArrowNodeConsumer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# advanced Arrow node consumer...");

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
                    if (!ev.TryReadExpectedRecordBatch(
                            RichAdvancedArrowContract.ExpectedRowCount,
                            RichAdvancedArrowContract.ExpectedFieldNames,
                            RichAdvancedArrowContract.ExpectedTypeIds,
                            out var batch,
                            out var readError))
                    {
                        Console.Error.WriteLine(readError ?? "Input did not contain the expected advanced Arrow RecordBatch payload.");
                        Environment.Exit(1);
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

    private static bool TryValidate(RecordBatch batch, out string summary, out string? error)
    {
        summary = string.Empty;
        error = null;

        if (!ArrowRecordBatchAssertions.TryGetInt32Column(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[0],
                RichAdvancedArrowContract.ExpectedIds,
                out var idArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetDate32Column(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[1],
                RichAdvancedArrowContract.ExpectedDateUnit,
                RichAdvancedArrowContract.ExpectedCreatedDates,
                out var createdArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetTimestampColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[2],
                RichAdvancedArrowContract.ExpectedTimestampUnit,
                RichAdvancedArrowContract.ExpectedTimestampTimezone,
                RichAdvancedArrowContract.ExpectedEventTimes,
                out var eventTimeArray,
                out error) ||
            !ArrowRecordBatchAssertions.TryGetBinaryColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[3],
                RichAdvancedArrowContract.ExpectedPayloads,
                out var payloadArray,
                out error))
        {
            return false;
        }

        if (idArray is null || createdArray is null || eventTimeArray is null || payloadArray is null)
        {
            error = "Advanced Arrow column assertions unexpectedly returned null arrays.";
            return false;
        }

        summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("NODE_ARROW_ADVANCED_OK");
        return true;
    }
}
