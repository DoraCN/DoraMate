using CSharpAdvancedArrowNodeDataflow;
using CSharpArrowNodeDataflow;
using CSharpComplexArrowNodeDataflow;
using DoraNode;

namespace DoraNodeRegressionRunner;

internal static class RegressionTests
{
    public static void RoundtripSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = RichArrowContract.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("NODE_ARROW_ROUNDTRIP_OK");
        Expect.Equal(
            "NODE_ARROW_ROUNDTRIP_OK fields=name,count,active,total,ratio,score cols=6 rows=2 types=String,Int32,Boolean,Int64,Float,Double",
            summary,
            "roundtrip summary");
    }

    public static void RoundtripSchemaValidationRejectsSchemaMismatch()
    {
        using var batch = RichArrowContract.CreateRecordBatch(firstFieldName: "label");
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RichArrowContract.ExpectedRowCount,
            RichArrowContract.ExpectedFieldNames,
            RichArrowContract.ExpectedTypeIds,
            out var error);

        Expect.False(succeeded, "schema mismatch should fail validation");
        Expect.Equal("Expected field 0 to be 'name' but got 'label'.", error!, "schema mismatch error");
    }

    public static void RoundtripSchemaValidationRejectsEmptyBatch()
    {
        using var batch = RichArrowContract.CreateRecordBatch(empty: true);
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RichArrowContract.ExpectedRowCount,
            RichArrowContract.ExpectedFieldNames,
            RichArrowContract.ExpectedTypeIds,
            out var error);

        Expect.False(succeeded, "empty batch should fail validation");
        Expect.Equal("Expected 2 rows but got 0.", error!, "empty batch error");
    }

    public static void RoundtripAssertionsCoverBasicScalarColumns()
    {
        using var batch = RichArrowContract.CreateRecordBatch();

        Expect.True(
            ArrowRecordBatchAssertions.TryGetStringColumn(batch, RichArrowContract.ExpectedFieldNames[0], RichArrowContract.ExpectedNames, out _, out var error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetInt32Column(batch, RichArrowContract.ExpectedFieldNames[1], RichArrowContract.ExpectedCounts, out _, out error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetBooleanColumn(batch, RichArrowContract.ExpectedFieldNames[2], RichArrowContract.ExpectedActive, out _, out error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetInt64Column(batch, RichArrowContract.ExpectedFieldNames[3], RichArrowContract.ExpectedTotals, out _, out error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetFloatColumn(batch, RichArrowContract.ExpectedFieldNames[4], RichArrowContract.ExpectedRatios, out _, out error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetDoubleColumn(batch, RichArrowContract.ExpectedFieldNames[5], RichArrowContract.ExpectedScores, out _, out error),
            error);
    }

    public static void AdvancedSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = RichAdvancedArrowContract.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("NODE_ARROW_ADVANCED_OK");
        Expect.Equal(
            "NODE_ARROW_ADVANCED_OK fields=id,created,event_time,payload cols=4 rows=2 types=Int32,Date32,Timestamp,Binary",
            summary,
            "advanced summary");
    }

    public static void AdvancedAssertionsCoverDateTimestampBinaryColumns()
    {
        using var batch = RichAdvancedArrowContract.CreateRecordBatch();

        Expect.True(
            ArrowRecordBatchAssertions.TryGetInt32Column(batch, RichAdvancedArrowContract.ExpectedFieldNames[0], RichAdvancedArrowContract.ExpectedIds, out _, out var error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetDate32Column(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[1],
                RichAdvancedArrowContract.ExpectedDateUnit,
                RichAdvancedArrowContract.ExpectedCreatedDates,
                out _,
                out error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetTimestampColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[2],
                RichAdvancedArrowContract.ExpectedTimestampUnit,
                RichAdvancedArrowContract.ExpectedTimestampTimezone,
                RichAdvancedArrowContract.ExpectedEventTimes,
                out _,
                out error),
            error);
        Expect.True(
            ArrowRecordBatchAssertions.TryGetBinaryColumn(batch, RichAdvancedArrowContract.ExpectedFieldNames[3], RichAdvancedArrowContract.ExpectedPayloads, out _, out error),
            error);
    }

    public static void ComplexListAndStructProjectorsMatchStandaloneModels()
    {
        using var batch = RichComplexArrowContract.CreateRecordBatch();

        Expect.True(
            ArrowRecordBatchProjector.TryProjectStringListColumn(
                batch,
                RichComplexArrowContract.TagsFieldName,
                RichComplexArrowContract.TagsValueFieldName,
                out var tagRows,
                out var error),
            error);

        var expectedTags = RichComplexArrowContract.ExpectedTags
            .Select(static row => (IReadOnlyList<string>)row)
            .ToArray();
        Expect.SequenceMatrixEqual(expectedTags, tagRows, "tags");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectStructColumn(
                batch,
                RichComplexArrowContract.MetaFieldName,
                RichComplexArrowContract.ExpectedMetaFieldNames,
                RichComplexArrowContract.ExpectedMetaTypeIds,
                static (ArrowStructRowAccessor row, out ComplexMetaModel? model, out string? projectionError) =>
                {
                    model = null;
                    projectionError = null;

                    if (!row.TryGetString(RichComplexArrowContract.MetaSourceFieldName, out var source, out projectionError) ||
                        !row.TryGetInt32(RichComplexArrowContract.MetaPriorityFieldName, out var priority, out projectionError))
                    {
                        return false;
                    }

                    model = new ComplexMetaModel(source, priority);
                    return true;
                },
                out var metaRows,
                out error),
            error);

        Expect.NotNull(metaRows, "meta rows");
        Expect.Equal(RichComplexArrowContract.ExpectedRowCount, metaRows!.Count, "meta row count");
        for (var rowIndex = 0; rowIndex < metaRows.Count; rowIndex++)
        {
            Expect.Equal(RichComplexArrowContract.ExpectedSources[rowIndex], metaRows[rowIndex].Source, $"meta row {rowIndex} source");
            Expect.Equal(RichComplexArrowContract.ExpectedPriorities[rowIndex], metaRows[rowIndex].Priority, $"meta row {rowIndex} priority");
        }
    }

    public static void ComplexContractProjectsExpectedModel()
    {
        using var batch = RichComplexArrowContract.CreateRecordBatch();
        Expect.True(RichComplexArrowContract.Contract.TryRead(batch, out ComplexBatchModel? model, out var error), error);
        Expect.NotNull(model, "contract model");
        Expect.True(RichComplexArrowContract.TryValidateModel(model!, out error), error);
    }

    public static void ComplexContractFailureSummaryMatchesStandaloneExampleFormat()
    {
        using var batch = RichComplexArrowContract.CreateRecordBatch(invalidNestedPriorityType: true);
        var succeeded = RichComplexArrowContract.Contract.TryRead(batch, out _, out var error);

        Expect.False(succeeded, "invalid nested type should fail contract projection");
        Expect.NotNull(error, "contract error");

        var summary = RichComplexArrowContract.CreateExpectedContractFailureSummary(
            DoraNodeErrorCode.ContractValidationFailed,
            error!);

        Expect.Contains(
            summary,
            "NODE_ARROW_COMPLEX_EXPECTED_CONTRACT_FAILURE_OK code=ContractValidationFailed",
            "failure summary prefix");
        Expect.Contains(summary, "meta.priority", "failure summary path");
    }
}

internal static class Expect
{
    public static void True(bool condition, string? message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message ?? "Expected condition to be true.");
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void NotNull<T>(T? value, string label)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"Expected '{label}' to be non-null.");
        }
    }

    public static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {label} to be '{expected}' but got '{actual}'.");
        }
    }

    public static void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T>? actual, string label)
    {
        NotNull(actual, label);
        if (expected.Count != actual!.Count)
        {
            throw new InvalidOperationException($"Expected {label} to contain {expected.Count} values but got {actual.Count}.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var actualValue = actual[index];
            if (!EqualityComparer<T>.Default.Equals(expected[index], actualValue))
            {
                throw new InvalidOperationException($"Expected {label}[{index}] to be '{expected[index]}' but got '{actualValue}'.");
            }
        }
    }

    public static void SequenceMatrixEqual<T>(IReadOnlyList<IReadOnlyList<T>> expected, IReadOnlyList<IReadOnlyList<T>>? actual, string label)
    {
        NotNull(actual, label);
        if (expected.Count != actual!.Count)
        {
            throw new InvalidOperationException($"Expected {label} to contain {expected.Count} rows but got {actual.Count}.");
        }

        for (var rowIndex = 0; rowIndex < expected.Count; rowIndex++)
        {
            SequenceEqual(expected[rowIndex], actual![rowIndex], $"{label}[{rowIndex}]");
        }
    }

    public static void Contains(string? actual, string expectedSubstring, string label)
    {
        if (actual is null || actual.IndexOf(expectedSubstring, StringComparison.Ordinal) < 0)
        {
            throw new InvalidOperationException($"Expected {label} to contain '{expectedSubstring}' but got '{actual ?? "<null>"}'.");
        }
    }
}