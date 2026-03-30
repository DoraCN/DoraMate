using Apache.Arrow.Types;
using DoraOperator;

namespace DoraOperatorRegressionRunner;

internal static class RegressionTests
{
    public static void RoundtripSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = RoundtripScenarioFixture.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("ARROW_ROUNDTRIP_OK");
        Expect.Equal(
            "ARROW_ROUNDTRIP_OK fields=name,count,active,total,ratio,score cols=6 rows=2 types=String,Int32,Boolean,Int64,Float,Double",
            summary,
            "roundtrip summary");
    }

    public static void RoundtripSchemaValidationRejectsSchemaMismatch()
    {
        using var batch = RoundtripScenarioFixture.CreateRecordBatch(firstFieldName: "label");
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RoundtripScenarioFixture.ExpectedRowCount,
            RoundtripScenarioFixture.ExpectedFieldNames,
            RoundtripScenarioFixture.ExpectedTypeIds,
            out var error);

        Expect.False(succeeded, "schema mismatch should fail validation");
        Expect.Equal("Expected field 0 to be 'name' but got 'label'.", error!, "schema mismatch error");
    }

    public static void RoundtripSchemaValidationRejectsEmptyBatch()
    {
        using var batch = RoundtripScenarioFixture.CreateRecordBatch(empty: true);
        var succeeded = ArrowSchemaValidation.TryValidateRecordBatch(
            batch,
            RoundtripScenarioFixture.ExpectedRowCount,
            RoundtripScenarioFixture.ExpectedFieldNames,
            RoundtripScenarioFixture.ExpectedTypeIds,
            out var error);

        Expect.False(succeeded, "empty batch should fail validation");
        Expect.Equal("Expected 2 rows but got 0.", error!, "empty batch error");
    }

    public static void ScalarColumnProjectionCoversAdvancedTypes()
    {
        var batch = AdvancedScalarFixture.CreateRecordBatch();

        Expect.True(
            ArrowRecordBatchProjector.TryProjectStringColumn(batch, AdvancedScalarFixture.NameFieldName, out var names, out var error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedNames, names, "names");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectInt32Column(batch, AdvancedScalarFixture.CountFieldName, out var counts, out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedCounts, counts, "counts");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectInt64Column(batch, AdvancedScalarFixture.TotalFieldName, out var totals, out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedTotals, totals, "totals");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectBooleanColumn(batch, AdvancedScalarFixture.ActiveFieldName, out var activeFlags, out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedActiveFlags, activeFlags, "active");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectFloatColumn(batch, AdvancedScalarFixture.RatioFieldName, out var ratios, out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedRatios, ratios, "ratios");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectDoubleColumn(batch, AdvancedScalarFixture.ScoreFieldName, out var scores, out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedScores, scores, "scores");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectBinaryColumn(batch, AdvancedScalarFixture.PayloadFieldName, out var payloads, out error),
            error);
        Expect.ByteMatrixEqual(AdvancedScalarFixture.ExpectedPayloads, payloads, "payloads");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectDate32Column(
                batch,
                AdvancedScalarFixture.CreatedFieldName,
                AdvancedScalarFixture.ExpectedDateUnit,
                out var createdDates,
                out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedCreatedDates, createdDates, "created");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectTimestampColumn(
                batch,
                AdvancedScalarFixture.EventTimeFieldName,
                AdvancedScalarFixture.ExpectedTimestampUnit,
                AdvancedScalarFixture.ExpectedTimestampTimezone,
                out var eventTimes,
                out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedEventTimes, eventTimes, "event_time");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectDecimal128Column(
                batch,
                AdvancedScalarFixture.Amount128FieldName,
                AdvancedScalarFixture.ExpectedDecimal128Precision,
                AdvancedScalarFixture.ExpectedDecimal128Scale,
                out var amounts128,
                out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedAmounts128, amounts128, "amount128");

        Expect.True(
            ArrowRecordBatchProjector.TryProjectDecimal256Column(
                batch,
                AdvancedScalarFixture.Amount256FieldName,
                AdvancedScalarFixture.ExpectedDecimal256Precision,
                AdvancedScalarFixture.ExpectedDecimal256Scale,
                out var amounts256,
                out error),
            error);
        Expect.SequenceEqual(AdvancedScalarFixture.ExpectedAmounts256, amounts256, "amount256");
    }

    public static void RowAccessorProjectsAdvancedArrowScalars()
    {
        var batch = AdvancedScalarFixture.CreateRecordBatch();

        Expect.True(
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

        Expect.NotNull(rows, "rows");
        Expect.Equal(AdvancedScalarFixture.ExpectedRowCount, rows!.Count, "row count");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var actual = rows[rowIndex];
            Expect.Equal(AdvancedScalarFixture.ExpectedNames[rowIndex], actual.Name, $"row {rowIndex} name");
            Expect.Equal(AdvancedScalarFixture.ExpectedCounts[rowIndex], actual.Count, $"row {rowIndex} count");
            Expect.Equal(AdvancedScalarFixture.ExpectedTotals[rowIndex], actual.Total, $"row {rowIndex} total");
            Expect.Equal(AdvancedScalarFixture.ExpectedActiveFlags[rowIndex], actual.Active, $"row {rowIndex} active");
            Expect.Equal(AdvancedScalarFixture.ExpectedRatios[rowIndex], actual.Ratio, $"row {rowIndex} ratio");
            Expect.Equal(AdvancedScalarFixture.ExpectedScores[rowIndex], actual.Score, $"row {rowIndex} score");
            Expect.ByteEqual(AdvancedScalarFixture.ExpectedPayloads[rowIndex], actual.Payload, $"row {rowIndex} payload");
            Expect.Equal(AdvancedScalarFixture.ExpectedCreatedDates[rowIndex], actual.Created, $"row {rowIndex} created");
            Expect.Equal(AdvancedScalarFixture.ExpectedEventTimes[rowIndex], actual.EventTime, $"row {rowIndex} event_time");
            Expect.Equal(AdvancedScalarFixture.ExpectedAmounts128[rowIndex], actual.Amount128, $"row {rowIndex} amount128");
            Expect.Equal(AdvancedScalarFixture.ExpectedAmounts256[rowIndex], actual.Amount256, $"row {rowIndex} amount256");
        }
    }

    public static void StructAccessorProjectsNestedAdvancedArrowScalars()
    {
        var batch = AdvancedStructFixture.CreateRecordBatch();

        Expect.True(
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

        Expect.NotNull(rows, "struct rows");
        Expect.Equal(AdvancedStructFixture.ExpectedRowCount, rows!.Count, "struct row count");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var actual = rows[rowIndex];
            Expect.Equal(AdvancedStructFixture.ExpectedNames[rowIndex], actual.Name, $"struct row {rowIndex} name");
            Expect.Equal(AdvancedStructFixture.ExpectedTotals[rowIndex], actual.Total, $"struct row {rowIndex} total");
            Expect.Equal(AdvancedStructFixture.ExpectedActiveFlags[rowIndex], actual.Active, $"struct row {rowIndex} active");
            Expect.Equal(AdvancedStructFixture.ExpectedRatios[rowIndex], actual.Ratio, $"struct row {rowIndex} ratio");
            Expect.Equal(AdvancedStructFixture.ExpectedScores[rowIndex], actual.Score, $"struct row {rowIndex} score");
            Expect.ByteEqual(AdvancedStructFixture.ExpectedPayloads[rowIndex], actual.Payload, $"struct row {rowIndex} payload");
            Expect.Equal(AdvancedStructFixture.ExpectedCreatedDates[rowIndex], actual.Created, $"struct row {rowIndex} created");
            Expect.Equal(AdvancedStructFixture.ExpectedEventTimes[rowIndex], actual.EventTime, $"struct row {rowIndex} event_time");
            Expect.Equal(AdvancedStructFixture.ExpectedAmounts128[rowIndex], actual.Amount128, $"struct row {rowIndex} amount128");
            Expect.Equal(AdvancedStructFixture.ExpectedAmounts256[rowIndex], actual.Amount256, $"struct row {rowIndex} amount256");
        }
    }

    public static void ComplexContractProjectsExpectedModel()
    {
        var batch = ComplexContractFixture.CreateRecordBatch();
        var contract = new ComplexContractFixture.ComplexOperatorBatchContract();

        Expect.True(contract.TryRead(batch, out var model, out var error), error);
        Expect.NotNull(model, "contract model");
        Expect.True(ComplexContractFixture.TryValidateModel(model!, out error), error);
    }

    public static void ComplexContractSummaryMatchesStandaloneExampleOutput()
    {
        using var batch = ComplexContractFixture.CreateRecordBatch();
        var summary = ArrowRecordBatchSummary.Create(batch).ToSummaryString("OPERATOR_ARROW_CONTRACT_OK");
        Expect.Equal(
            "OPERATOR_ARROW_CONTRACT_OK fields=id,budget,scores,metrics,details cols=5 rows=2 types=Int32,Decimal256,List,Map,Struct",
            summary,
            "contract summary");
    }

    public static void ComplexContractRejectsInvalidNestedFieldTypes()
    {
        var batch = ComplexContractFixture.CreateRecordBatch(invalidNestedSourceType: true);
        var contract = new ComplexContractFixture.ComplexOperatorBatchContract();

        var succeeded = contract.TryRead(batch, out _, out var error);
        Expect.False(succeeded, "invalid nested type should fail contract projection");
        Expect.NotNull(error, "contract error");
        Expect.Contains(error, $"{ComplexContractFixture.DetailsFieldName}.{ComplexContractFixture.DetailsSourceFieldName}", "contract error path");
    }

    public static void ComplexContractFailureSummaryMatchesStandaloneExampleFormat()
    {
        using var batch = ComplexContractFixture.CreateRecordBatch(invalidNestedSourceType: true);
        var contract = new ComplexContractFixture.ComplexOperatorBatchContract();

        var succeeded = contract.TryRead(batch, out _, out var error);
        Expect.False(succeeded, "invalid nested type should fail contract projection");
        Expect.NotNull(error, "contract error");

        var summary = $"OPERATOR_ARROW_CONTRACT_EXPECTED_FAILURE_OK code={DoraOperatorErrorCode.ContractValidationFailed} error={error}";
        Expect.Contains(summary, "OPERATOR_ARROW_CONTRACT_EXPECTED_FAILURE_OK code=ContractValidationFailed", "contract failure summary prefix");
        Expect.Contains(summary, $"{ComplexContractFixture.DetailsFieldName}.{ComplexContractFixture.DetailsSourceFieldName}", "contract failure summary path");
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

    public static void ByteEqual(byte[] expected, byte[] actual, string label)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException($"Expected {label} to be '{BitConverter.ToString(expected)}' but got '{BitConverter.ToString(actual)}'.");
        }
    }

    public static void ByteMatrixEqual(IReadOnlyList<byte[]> expected, IReadOnlyList<byte[]>? actual, string label)
    {
        NotNull(actual, label);
        if (expected.Count != actual!.Count)
        {
            throw new InvalidOperationException($"Expected {label} to contain {expected.Count} rows but got {actual.Count}.");
        }

        for (var index = 0; index < expected.Count; index++)
        {
            ByteEqual(expected[index], actual![index], $"{label}[{index}]");
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
