using System.Text;
using Apache.Arrow;
using Apache.Arrow.Scalars;
using Apache.Arrow.Types;
using DoraOperator;
using Xunit;

namespace DoraOperatorRegressionRunner;

public sealed class RegressionTests
{
    [Fact]
    public void RoundtripSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = RoundtripScenarioFixture.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("ARROW_ROUNDTRIP_OK");
        TestAssert.Equal(
            "ARROW_ROUNDTRIP_OK fields=name,count,active,total,ratio,score cols=6 rows=2 types=String,Int32,Boolean,Int64,Float,Double",
            summary,
            "roundtrip summary");
    }

    [Fact]
    public void RoundtripSchemaValidationRejectsSchemaMismatch()
    {
        using var batch = RoundtripScenarioFixture.CreateRecordBatch(firstFieldName: "label");
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RoundtripScenarioFixture.ExpectedRowCount,
            RoundtripScenarioFixture.ExpectedFieldNames,
            RoundtripScenarioFixture.ExpectedTypeIds,
            out var error);

        TestAssert.False(succeeded, "schema mismatch should fail validation");
        TestAssert.Equal("Expected field 0 to be 'name' but got 'label'.", error!, "schema mismatch error");
    }

    [Fact]
    public void RoundtripSchemaValidationRejectsEmptyBatch()
    {
        using var batch = RoundtripScenarioFixture.CreateRecordBatch(empty: true);
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RoundtripScenarioFixture.ExpectedRowCount,
            RoundtripScenarioFixture.ExpectedFieldNames,
            RoundtripScenarioFixture.ExpectedTypeIds,
            out var error);

        TestAssert.False(succeeded, "empty batch should fail validation");
        TestAssert.Equal("Expected 2 rows but got 0.", error!, "empty batch error");
    }

    [Fact]
    public void ScalarColumnProjectionCoversAdvancedTypes()
    {
        var batch = AdvancedScalarFixture.CreateRecordBatch();

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectStringColumn(batch, AdvancedScalarFixture.NameFieldName, out var names, out var error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedNames, names, "names");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectInt32Column(batch, AdvancedScalarFixture.CountFieldName, out var counts, out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedCounts, counts, "counts");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectInt64Column(batch, AdvancedScalarFixture.TotalFieldName, out var totals, out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedTotals, totals, "totals");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectBooleanColumn(batch, AdvancedScalarFixture.ActiveFieldName, out var activeFlags, out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedActiveFlags, activeFlags, "active");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectFloatColumn(batch, AdvancedScalarFixture.RatioFieldName, out var ratios, out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedRatios, ratios, "ratios");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectDoubleColumn(batch, AdvancedScalarFixture.ScoreFieldName, out var scores, out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedScores, scores, "scores");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectBinaryColumn(batch, AdvancedScalarFixture.PayloadFieldName, out var payloads, out error),
            error);
        TestAssert.ByteMatrixEqual(AdvancedScalarFixture.ExpectedPayloads, payloads, "payloads");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectDate32Column(
                batch,
                AdvancedScalarFixture.CreatedFieldName,
                AdvancedScalarFixture.ExpectedDateUnit,
                out var createdDates,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedCreatedDates, createdDates, "created");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectTimestampColumn(
                batch,
                AdvancedScalarFixture.EventTimeFieldName,
                AdvancedScalarFixture.ExpectedTimestampUnit,
                AdvancedScalarFixture.ExpectedTimestampTimezone,
                out var eventTimes,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedEventTimes, eventTimes, "event_time");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectDecimal128Column(
                batch,
                AdvancedScalarFixture.Amount128FieldName,
                AdvancedScalarFixture.ExpectedDecimal128Precision,
                AdvancedScalarFixture.ExpectedDecimal128Scale,
                out var amounts128,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedAmounts128, amounts128, "amount128");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectDecimal256Column(
                batch,
                AdvancedScalarFixture.Amount256FieldName,
                AdvancedScalarFixture.ExpectedDecimal256Precision,
                AdvancedScalarFixture.ExpectedDecimal256Scale,
                out var amounts256,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedScalarFixture.ExpectedAmounts256, amounts256, "amount256");
    }

    [Fact]
    public void RowAccessorProjectsAdvancedArrowScalars()
    {
        var batch = AdvancedScalarFixture.CreateRecordBatch();

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectRows(
                batch,
                AdvancedScalarFixture.ExpectedFieldNames,
                AdvancedScalarFixture.ExpectedTypeIds,
                static (ArrowRecordBatchRowAccessor row, out ScalarRowModel? model, out string? error) =>
                {
                    model = null;
                    error = null;

                    if (!row.TryGetString(AdvancedScalarFixture.NameFieldName, out var name, out error) ||
                        !row.TryGetInt32(AdvancedScalarFixture.CountFieldName, out var count, out error) ||
                        !row.TryGetInt64(AdvancedScalarFixture.TotalFieldName, out var total, out error) ||
                        !row.TryGetBoolean(AdvancedScalarFixture.ActiveFieldName, out var active, out error) ||
                        !row.TryGetFloat(AdvancedScalarFixture.RatioFieldName, out var ratio, out error) ||
                        !row.TryGetDouble(AdvancedScalarFixture.ScoreFieldName, out var score, out error) ||
                        !row.TryGetBinary(AdvancedScalarFixture.PayloadFieldName, out var payload, out error) ||
                        !row.TryGetDate32(
                            AdvancedScalarFixture.CreatedFieldName,
                            AdvancedScalarFixture.ExpectedDateUnit,
                            out var created,
                            out error) ||
                        !row.TryGetTimestamp(
                            AdvancedScalarFixture.EventTimeFieldName,
                            AdvancedScalarFixture.ExpectedTimestampUnit,
                            AdvancedScalarFixture.ExpectedTimestampTimezone,
                            out var eventTime,
                            out error) ||
                        !row.TryGetDecimal128(
                            AdvancedScalarFixture.Amount128FieldName,
                            AdvancedScalarFixture.ExpectedDecimal128Precision,
                            AdvancedScalarFixture.ExpectedDecimal128Scale,
                            out var amount128,
                            out error) ||
                        !row.TryGetDecimal256(
                            AdvancedScalarFixture.Amount256FieldName,
                            AdvancedScalarFixture.ExpectedDecimal256Precision,
                            AdvancedScalarFixture.ExpectedDecimal256Scale,
                            out var amount256,
                            out error))
                    {
                        return false;
                    }

                    model = new ScalarRowModel(
                        name,
                        count,
                        total,
                        active,
                        ratio,
                        score,
                        payload,
                        created,
                        eventTime,
                        amount128,
                        amount256);
                    return true;
                },
                out var rows,
                out var projectionError),
            projectionError);

        TestAssert.NotNull(rows, "rows");
        TestAssert.Equal(AdvancedScalarFixture.ExpectedRowCount, rows!.Count, "row count");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var actual = rows[rowIndex];
            TestAssert.Equal(AdvancedScalarFixture.ExpectedNames[rowIndex], actual.Name, $"row {rowIndex} name");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedCounts[rowIndex], actual.Count, $"row {rowIndex} count");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedTotals[rowIndex], actual.Total, $"row {rowIndex} total");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedActiveFlags[rowIndex], actual.Active, $"row {rowIndex} active");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedRatios[rowIndex], actual.Ratio, $"row {rowIndex} ratio");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedScores[rowIndex], actual.Score, $"row {rowIndex} score");
            TestAssert.ByteEqual(AdvancedScalarFixture.ExpectedPayloads[rowIndex], actual.Payload, $"row {rowIndex} payload");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedCreatedDates[rowIndex], actual.Created, $"row {rowIndex} created");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedEventTimes[rowIndex], actual.EventTime, $"row {rowIndex} event_time");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedAmounts128[rowIndex], actual.Amount128, $"row {rowIndex} amount128");
            TestAssert.Equal(AdvancedScalarFixture.ExpectedAmounts256[rowIndex], actual.Amount256, $"row {rowIndex} amount256");
        }
    }

    [Fact]
    public void StructAccessorProjectsNestedAdvancedArrowScalars()
    {
        var batch = AdvancedStructFixture.CreateRecordBatch();

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectStructColumn(
                batch,
                AdvancedStructFixture.StructFieldName,
                AdvancedStructFixture.ExpectedChildFieldNames,
                AdvancedStructFixture.ExpectedChildTypeIds,
                static (ArrowStructRowAccessor row, out StructRowModel? model, out string? error) =>
                {
                    model = null;
                    error = null;

                    if (!row.TryGetString(AdvancedStructFixture.NameFieldName, out var name, out error) ||
                        !row.TryGetInt64(AdvancedStructFixture.TotalFieldName, out var total, out error) ||
                        !row.TryGetBoolean(AdvancedStructFixture.ActiveFieldName, out var active, out error) ||
                        !row.TryGetFloat(AdvancedStructFixture.RatioFieldName, out var ratio, out error) ||
                        !row.TryGetDouble(AdvancedStructFixture.ScoreFieldName, out var score, out error) ||
                        !row.TryGetBinary(AdvancedStructFixture.PayloadFieldName, out var payload, out error) ||
                        !row.TryGetDate32(
                            AdvancedStructFixture.CreatedFieldName,
                            AdvancedStructFixture.ExpectedDateUnit,
                            out var created,
                            out error) ||
                        !row.TryGetTimestamp(
                            AdvancedStructFixture.EventTimeFieldName,
                            AdvancedStructFixture.ExpectedTimestampUnit,
                            AdvancedStructFixture.ExpectedTimestampTimezone,
                            out var eventTime,
                            out error) ||
                        !row.TryGetDecimal128(
                            AdvancedStructFixture.Amount128FieldName,
                            AdvancedStructFixture.ExpectedDecimal128Precision,
                            AdvancedStructFixture.ExpectedDecimal128Scale,
                            out var amount128,
                            out error) ||
                        !row.TryGetDecimal256(
                            AdvancedStructFixture.Amount256FieldName,
                            AdvancedStructFixture.ExpectedDecimal256Precision,
                            AdvancedStructFixture.ExpectedDecimal256Scale,
                            out var amount256,
                            out error))
                    {
                        return false;
                    }

                    model = new StructRowModel(
                        name,
                        total,
                        active,
                        ratio,
                        score,
                        payload,
                        created,
                        eventTime,
                        amount128,
                        amount256);
                    return true;
                },
                out var rows,
                out var projectionError),
            projectionError);

        TestAssert.NotNull(rows, "struct rows");
        TestAssert.Equal(AdvancedStructFixture.ExpectedRowCount, rows!.Count, "struct row count");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var actual = rows[rowIndex];
            TestAssert.Equal(AdvancedStructFixture.ExpectedNames[rowIndex], actual.Name, $"struct row {rowIndex} name");
            TestAssert.Equal(AdvancedStructFixture.ExpectedTotals[rowIndex], actual.Total, $"struct row {rowIndex} total");
            TestAssert.Equal(AdvancedStructFixture.ExpectedActiveFlags[rowIndex], actual.Active, $"struct row {rowIndex} active");
            TestAssert.Equal(AdvancedStructFixture.ExpectedRatios[rowIndex], actual.Ratio, $"struct row {rowIndex} ratio");
            TestAssert.Equal(AdvancedStructFixture.ExpectedScores[rowIndex], actual.Score, $"struct row {rowIndex} score");
            TestAssert.ByteEqual(AdvancedStructFixture.ExpectedPayloads[rowIndex], actual.Payload, $"struct row {rowIndex} payload");
            TestAssert.Equal(AdvancedStructFixture.ExpectedCreatedDates[rowIndex], actual.Created, $"struct row {rowIndex} created");
            TestAssert.Equal(AdvancedStructFixture.ExpectedEventTimes[rowIndex], actual.EventTime, $"struct row {rowIndex} event_time");
            TestAssert.Equal(AdvancedStructFixture.ExpectedAmounts128[rowIndex], actual.Amount128, $"struct row {rowIndex} amount128");
            TestAssert.Equal(AdvancedStructFixture.ExpectedAmounts256[rowIndex], actual.Amount256, $"struct row {rowIndex} amount256");
        }
    }

    [Fact]
    public void ExtendedScalarProjectionCoversFixedDurationIntervalAndUnionAssertions()
    {
        using var batch = AdvancedExtendedFixture.CreateRecordBatch();

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectFixedSizeBinaryColumn(
                batch,
                AdvancedExtendedFixture.FixedPayloadFieldName,
                AdvancedExtendedFixture.ExpectedFixedPayloadByteWidth,
                out var fixedPayloads,
                out var error),
            error);
        TestAssert.ByteMatrixEqual(AdvancedExtendedFixture.ExpectedFixedPayloads, fixedPayloads, "fixed payloads");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectDurationColumn(
                batch,
                AdvancedExtendedFixture.ProcessingTimeFieldName,
                AdvancedExtendedFixture.ExpectedDurationUnit,
                out var processingTimes,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedExtendedFixture.ExpectedProcessingTimes, processingTimes, "processing times");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectYearMonthIntervalColumn(
                batch,
                AdvancedExtendedFixture.BillingCycleFieldName,
                out var billingCycles,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedExtendedFixture.ExpectedBillingCycles, billingCycles, "billing cycles");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectDayTimeIntervalColumn(
                batch,
                AdvancedExtendedFixture.RetryWindowFieldName,
                out var retryWindows,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedExtendedFixture.ExpectedRetryWindows, retryWindows, "retry windows");

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectMonthDayNanosecondIntervalColumn(
                batch,
                AdvancedExtendedFixture.MaintenanceWindowFieldName,
                out var maintenanceWindows,
                out error),
            error);
        TestAssert.SequenceEqual(AdvancedExtendedFixture.ExpectedMaintenanceWindows, maintenanceWindows, "maintenance windows");

        TestAssert.True(
            ArrowRecordBatchAssertions.TryGetDenseUnionColumn(
                batch,
                AdvancedExtendedFixture.ResultFieldName,
                AdvancedExtendedFixture.ExpectedUnionChildFieldNames,
                AdvancedExtendedFixture.ExpectedUnionChildTypeIds,
                AdvancedExtendedFixture.ExpectedUnionTypeIds,
                out var unionColumn,
                out error),
            error);

        TestAssert.NotNull(unionColumn, "union column");
        TestAssert.SequenceEqual(AdvancedExtendedFixture.ExpectedUnionRowTypeIds, unionColumn!.TypeIds.ToArray(), "union type ids");
        TestAssert.Equal("ok", ((StringArray)unionColumn.Fields[0]).GetString(0, Encoding.UTF8)!, "union status");
        TestAssert.Equal(42, ((Int32Array)unionColumn.Fields[1]).GetValue(0)!.Value, "union code");
    }

    [Fact]
    public void ExtendedRowAndStructAccessorsCoverFixedDurationAndIntervals()
    {
        using var batch = AdvancedExtendedFixture.CreateRecordBatch();

        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectRows(
                batch,
                AdvancedExtendedFixture.ExpectedFieldNames,
                AdvancedExtendedFixture.ExpectedTypeIds,
                static (ArrowRecordBatchRowAccessor row, out ExtendedScalarRowModel? model, out string? error) =>
                {
                    model = null;
                    error = null;

                    if (!row.TryGetFixedSizeBinary(
                            AdvancedExtendedFixture.FixedPayloadFieldName,
                            AdvancedExtendedFixture.ExpectedFixedPayloadByteWidth,
                            out var fixedPayload,
                            out error) ||
                        !row.TryGetDuration(
                            AdvancedExtendedFixture.ProcessingTimeFieldName,
                            AdvancedExtendedFixture.ExpectedDurationUnit,
                            out var processingTime,
                            out error) ||
                        !row.TryGetYearMonthInterval(AdvancedExtendedFixture.BillingCycleFieldName, out var billingCycle, out error) ||
                        !row.TryGetDayTimeInterval(AdvancedExtendedFixture.RetryWindowFieldName, out var retryWindow, out error) ||
                        !row.TryGetMonthDayNanosecondInterval(AdvancedExtendedFixture.MaintenanceWindowFieldName, out var maintenanceWindow, out error))
                    {
                        return false;
                    }

                    model = new ExtendedScalarRowModel(
                        fixedPayload,
                        processingTime,
                        billingCycle,
                        retryWindow,
                        maintenanceWindow);
                    return true;
                },
                out var rows,
                out var projectionError),
            projectionError);

        TestAssert.NotNull(rows, "extended rows");
        TestAssert.Equal(AdvancedExtendedFixture.ExpectedRowCount, rows!.Count, "extended row count");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var actual = rows[rowIndex];
            TestAssert.ByteEqual(AdvancedExtendedFixture.ExpectedFixedPayloads[rowIndex], actual.FixedPayload, $"extended row {rowIndex} fixed payload");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedProcessingTimes[rowIndex], actual.ProcessingTime, $"extended row {rowIndex} processing time");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedBillingCycles[rowIndex], actual.BillingCycle, $"extended row {rowIndex} billing cycle");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedRetryWindows[rowIndex], actual.RetryWindow, $"extended row {rowIndex} retry window");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedMaintenanceWindows[rowIndex], actual.MaintenanceWindow, $"extended row {rowIndex} maintenance window");
        }

        using var structBatch = AdvancedExtendedStructFixture.CreateRecordBatch();
        TestAssert.True(
            ArrowRecordBatchProjector.TryProjectStructColumn(
                structBatch,
                AdvancedExtendedStructFixture.StructFieldName,
                AdvancedExtendedStructFixture.ExpectedChildFieldNames,
                AdvancedExtendedStructFixture.ExpectedChildTypeIds,
                static (ArrowStructRowAccessor row, out ExtendedStructRowModel? model, out string? error) =>
                {
                    model = null;
                    error = null;

                    if (!row.TryGetFixedSizeBinary(
                            AdvancedExtendedStructFixture.FixedPayloadFieldName,
                            AdvancedExtendedFixture.ExpectedFixedPayloadByteWidth,
                            out var fixedPayload,
                            out error) ||
                        !row.TryGetDuration(
                            AdvancedExtendedStructFixture.ProcessingTimeFieldName,
                            AdvancedExtendedFixture.ExpectedDurationUnit,
                            out var processingTime,
                            out error) ||
                        !row.TryGetYearMonthInterval(AdvancedExtendedStructFixture.BillingCycleFieldName, out var billingCycle, out error) ||
                        !row.TryGetDayTimeInterval(AdvancedExtendedStructFixture.RetryWindowFieldName, out var retryWindow, out error) ||
                        !row.TryGetMonthDayNanosecondInterval(AdvancedExtendedStructFixture.MaintenanceWindowFieldName, out var maintenanceWindow, out error))
                    {
                        return false;
                    }

                    model = new ExtendedStructRowModel(
                        fixedPayload,
                        processingTime,
                        billingCycle,
                        retryWindow,
                        maintenanceWindow);
                    return true;
                },
                out var structRows,
                out projectionError),
            projectionError);

        TestAssert.NotNull(structRows, "extended struct rows");
        TestAssert.Equal(AdvancedExtendedStructFixture.ExpectedRowCount, structRows!.Count, "extended struct row count");
        for (var rowIndex = 0; rowIndex < structRows.Count; rowIndex++)
        {
            var actual = structRows[rowIndex];
            TestAssert.ByteEqual(AdvancedExtendedFixture.ExpectedFixedPayloads[rowIndex], actual.FixedPayload, $"extended struct row {rowIndex} fixed payload");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedProcessingTimes[rowIndex], actual.ProcessingTime, $"extended struct row {rowIndex} processing time");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedBillingCycles[rowIndex], actual.BillingCycle, $"extended struct row {rowIndex} billing cycle");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedRetryWindows[rowIndex], actual.RetryWindow, $"extended struct row {rowIndex} retry window");
            TestAssert.Equal(AdvancedExtendedFixture.ExpectedMaintenanceWindows[rowIndex], actual.MaintenanceWindow, $"extended struct row {rowIndex} maintenance window");
        }
    }

    [Fact]
    public void ComplexContractProjectsExpectedModel()
    {
        var batch = ComplexContractFixture.CreateRecordBatch();
        var contract = new ComplexContractFixture.ComplexOperatorBatchContract();

        TestAssert.True(contract.TryRead(batch, out var model, out var error), error);
        TestAssert.NotNull(model, "contract model");
        TestAssert.True(ComplexContractFixture.TryValidateModel(model!, out error), error);
    }

    [Fact]
    public void ComplexContractSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = ComplexContractFixture.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("OPERATOR_ARROW_CONTRACT_OK");
        TestAssert.Equal(
            "OPERATOR_ARROW_CONTRACT_OK fields=id,budget,scores,metrics,details cols=5 rows=2 types=Int32,Decimal256,List,Map,Struct",
            summary,
            "contract summary");
    }

    [Fact]
    public void ComplexContractRejectsInvalidNestedFieldTypes()
    {
        var batch = ComplexContractFixture.CreateRecordBatch(invalidNestedSourceType: true);
        var contract = new ComplexContractFixture.ComplexOperatorBatchContract();

        var succeeded = contract.TryRead(batch, out _, out var error);
        TestAssert.False(succeeded, "invalid nested type should fail contract projection");
        TestAssert.NotNull(error, "contract error");
        TestAssert.Contains(error, $"{ComplexContractFixture.DetailsFieldName}.{ComplexContractFixture.DetailsSourceFieldName}", "contract error path");
    }

    [Fact]
    public void ComplexContractFailureSummaryMatchesStandaloneExampleFormat()
    {
        using var batch = ComplexContractFixture.CreateRecordBatch(invalidNestedSourceType: true);
        var contract = new ComplexContractFixture.ComplexOperatorBatchContract();

        var succeeded = contract.TryRead(batch, out _, out var error);
        TestAssert.False(succeeded, "invalid nested type should fail contract projection");
        TestAssert.NotNull(error, "contract error");

        var summary = $"OPERATOR_ARROW_CONTRACT_EXPECTED_FAILURE_OK code={DoraOperatorErrorCode.ContractValidationFailed} error={error}";
        TestAssert.Contains(summary, "OPERATOR_ARROW_CONTRACT_EXPECTED_FAILURE_OK code=ContractValidationFailed", "contract failure summary prefix");
        TestAssert.Contains(summary, $"{ComplexContractFixture.DetailsFieldName}.{ComplexContractFixture.DetailsSourceFieldName}", "contract failure summary path");
    }
}

internal sealed record ScalarRowModel(
    string Name,
    int Count,
    long Total,
    bool Active,
    float Ratio,
    double Score,
    byte[] Payload,
    DateOnly Created,
    DateTimeOffset EventTime,
    decimal Amount128,
    decimal Amount256);

internal sealed record StructRowModel(
    string Name,
    long Total,
    bool Active,
    float Ratio,
    double Score,
    byte[] Payload,
    DateOnly Created,
    DateTimeOffset EventTime,
    decimal Amount128,
    decimal Amount256);

internal sealed record ExtendedScalarRowModel(
    byte[] FixedPayload,
    TimeSpan ProcessingTime,
    YearMonthInterval BillingCycle,
    DayTimeInterval RetryWindow,
    MonthDayNanosecondInterval MaintenanceWindow);

internal sealed record ExtendedStructRowModel(
    byte[] FixedPayload,
    TimeSpan ProcessingTime,
    YearMonthInterval BillingCycle,
    DayTimeInterval RetryWindow,
    MonthDayNanosecondInterval MaintenanceWindow);

