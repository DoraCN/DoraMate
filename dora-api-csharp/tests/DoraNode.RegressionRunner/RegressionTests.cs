using System.Text;
using Apache.Arrow;
using CSharpAdvancedArrowNodeDataflow;
using CSharpArrowNodeDataflow;
using CSharpComplexArrowNodeDataflow;
using DoraNode;
using Xunit;

namespace DoraNodeRegressionRunner;

public sealed class RegressionTests
{
    [Fact]
    public void RoundtripSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = RichArrowContract.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("NODE_ARROW_ROUNDTRIP_OK");
        TestAssert.Equal(
            "NODE_ARROW_ROUNDTRIP_OK fields=name,count,active,total,ratio,score cols=6 rows=2 types=String,Int32,Boolean,Int64,Float,Double",
            summary,
            "roundtrip summary");
    }

    [Fact]
    public void RoundtripSchemaValidationRejectsSchemaMismatch()
    {
        using var batch = RichArrowContract.CreateRecordBatch(firstFieldName: "label");
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RichArrowContract.ExpectedRowCount,
            RichArrowContract.ExpectedFieldNames,
            RichArrowContract.ExpectedTypeIds,
            out var error);

        TestAssert.False(succeeded, "schema mismatch should fail validation");
        TestAssert.Equal("Expected field 0 to be 'name' but got 'label'.", error!, "schema mismatch error");
    }

    [Fact]
    public void RoundtripSchemaValidationRejectsEmptyBatch()
    {
        using var batch = RichArrowContract.CreateRecordBatch(empty: true);
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RichArrowContract.ExpectedRowCount,
            RichArrowContract.ExpectedFieldNames,
            RichArrowContract.ExpectedTypeIds,
            out var error);

        TestAssert.False(succeeded, "empty batch should fail validation");
        TestAssert.Equal("Expected 2 rows but got 0.", error!, "empty batch error");
    }

    [Fact]
    public void RoundtripAssertionsCoverBasicScalarColumns()
    {
        using var batch = RichArrowContract.CreateRecordBatch();

        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetStringColumn(batch, RichArrowContract.ExpectedFieldNames[0], RichArrowContract.ExpectedNames, out _, out var error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetInt32Column(batch, RichArrowContract.ExpectedFieldNames[1], RichArrowContract.ExpectedCounts, out _, out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetBooleanColumn(batch, RichArrowContract.ExpectedFieldNames[2], RichArrowContract.ExpectedActive, out _, out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetInt64Column(batch, RichArrowContract.ExpectedFieldNames[3], RichArrowContract.ExpectedTotals, out _, out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetFloatColumn(batch, RichArrowContract.ExpectedFieldNames[4], RichArrowContract.ExpectedRatios, out _, out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetDoubleColumn(batch, RichArrowContract.ExpectedFieldNames[5], RichArrowContract.ExpectedScores, out _, out error),
            error);
    }

    [Fact]
    public void AdvancedSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = RichAdvancedArrowContract.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("NODE_ARROW_ADVANCED_OK");
        TestAssert.Equal(
            "NODE_ARROW_ADVANCED_OK fields=id,created,event_time,payload,fixed_payload,processing_time,billing_cycle,retry_window,maintenance_window,result cols=10 rows=2 types=Int32,Date32,Timestamp,Binary,FixedSizedBinary,Duration,Interval,Interval,Interval,Union",
            summary,
            "advanced summary");
    }

    [Fact]
    public void AdvancedAssertionsCoverExtendedArrowTypes()
    {
        using var batch = RichAdvancedArrowContract.CreateRecordBatch();

        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetInt32Column(batch, RichAdvancedArrowContract.ExpectedFieldNames[0], RichAdvancedArrowContract.ExpectedIds, out _, out var error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetDate32Column(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[1],
                RichAdvancedArrowContract.ExpectedDateUnit,
                RichAdvancedArrowContract.ExpectedCreatedDates,
                out _,
                out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetTimestampColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[2],
                RichAdvancedArrowContract.ExpectedTimestampUnit,
                RichAdvancedArrowContract.ExpectedTimestampTimezone,
                RichAdvancedArrowContract.ExpectedEventTimes,
                out _,
                out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetBinaryColumn(batch, RichAdvancedArrowContract.ExpectedFieldNames[3], RichAdvancedArrowContract.ExpectedPayloads, out _, out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetFixedSizeBinaryColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[4],
                RichAdvancedArrowContract.ExpectedFixedPayloadByteWidth,
                RichAdvancedArrowContract.ExpectedFixedPayloads,
                out _,
                out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetDurationColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[5],
                RichAdvancedArrowContract.ExpectedDurationUnit,
                RichAdvancedArrowContract.ExpectedProcessingTimes,
                out _,
                out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetYearMonthIntervalColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[6],
                RichAdvancedArrowContract.ExpectedBillingCycles,
                out _,
                out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetDayTimeIntervalColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[7],
                RichAdvancedArrowContract.ExpectedRetryWindows,
                out _,
                out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetMonthDayNanosecondIntervalColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[8],
                RichAdvancedArrowContract.ExpectedMaintenanceWindows,
                out _,
                out error),
            error);
        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetDenseUnionColumn(
                batch,
                RichAdvancedArrowContract.ExpectedFieldNames[9],
                RichAdvancedArrowContract.ExpectedUnionChildFieldNames,
                RichAdvancedArrowContract.ExpectedUnionChildTypeIds,
                RichAdvancedArrowContract.ExpectedUnionTypeIds,
                out var unionColumn,
                out error),
            error);

        TestAssert.NotNull(unionColumn, "union column");
        TestAssert.Equal(RichAdvancedArrowContract.ExpectedRowCount, unionColumn!.Length, "union length");
        TestAssert.SequenceEqual(RichAdvancedArrowContract.ExpectedUnionRowTypeIds, unionColumn.TypeIds.ToArray(), "union type ids");
        TestAssert.Equal("ok", ((StringArray)unionColumn.Fields[0]).GetString(0, Encoding.UTF8)!, "union status");
        TestAssert.Equal(42, ((Int32Array)unionColumn.Fields[1]).GetValue(0)!.Value, "union code");
    }

    [Fact]
    public void ComplexListAndStructProjectorsMatchStandaloneModels()
    {
        using var batch = RichComplexArrowContract.CreateRecordBatch();

        TestAssert.True(
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
        TestAssert.SequenceMatrixEqual(expectedTags, tagRows, "tags");

        TestAssert.True(
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

        TestAssert.NotNull(metaRows, "meta rows");
        TestAssert.Equal(RichComplexArrowContract.ExpectedRowCount, metaRows!.Count, "meta row count");
        for (var rowIndex = 0; rowIndex < metaRows.Count; rowIndex++)
        {
            TestAssert.Equal(RichComplexArrowContract.ExpectedSources[rowIndex], metaRows[rowIndex].Source, $"meta row {rowIndex} source");
            TestAssert.Equal(RichComplexArrowContract.ExpectedPriorities[rowIndex], metaRows[rowIndex].Priority, $"meta row {rowIndex} priority");
        }
    }

    [Fact]
    public void ComplexContractProjectsExpectedModel()
    {
        using var batch = RichComplexArrowContract.CreateRecordBatch();
        TestAssert.True(RichComplexArrowContract.Contract.TryRead(batch, out ComplexBatchModel? model, out var error), error);
        TestAssert.NotNull(model, "contract model");
        TestAssert.True(RichComplexArrowContract.TryValidateModel(model!, out error), error);
    }

    [Fact]
    public void ComplexContractFailureSummaryMatchesStandaloneExampleFormat()
    {
        using var batch = RichComplexArrowContract.CreateRecordBatch(invalidNestedPriorityType: true);
        var succeeded = RichComplexArrowContract.Contract.TryRead(batch, out _, out var error);

        TestAssert.False(succeeded, "invalid nested type should fail contract projection");
        TestAssert.NotNull(error, "contract error");

        var summary = RichComplexArrowContract.CreateExpectedContractFailureSummary(
            DoraNodeErrorCode.ContractValidationFailed,
            error!);

        TestAssert.Contains(
            summary,
            "NODE_ARROW_COMPLEX_EXPECTED_CONTRACT_FAILURE_OK code=ContractValidationFailed",
            "failure summary prefix");
        TestAssert.Contains(summary, "meta.priority", "failure summary path");
    }
}
