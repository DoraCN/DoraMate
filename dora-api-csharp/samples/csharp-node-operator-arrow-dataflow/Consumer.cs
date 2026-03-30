using Apache.Arrow;
using CSharpNodeOperatorArrowDataflow;
using DoraNode;

namespace CSharpNodeOperatorArrowConsumer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# node->operator Arrow consumer...");

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
                            expectedRowCount: RichArrowContract.ExpectedNames.Length,
                            RichArrowContract.ExpectedFieldNames,
                            RichArrowContract.ExpectedTypeIds,
                            out var batch,
                            out var readError) ||
                        batch is null)
                    {
                        Console.Error.WriteLine(readError ?? "Consumer input did not contain an expected Arrow RecordBatch.");
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

        summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("NODE_OPERATOR_NODE_ARROW_OK");
        return true;
    }
}
