using Apache.Arrow;
using Apache.Arrow.Memory;
using Apache.Arrow.Types;
using DoraOperator;

namespace DoraOperatorRegressionRunner;

internal static class AdvancedScalarFixture
{
    public static readonly KeyValuePair<string, string>[] EmptyMetadata = [];

    public const string NameFieldName = "name";
    public const string CountFieldName = "count";
    public const string TotalFieldName = "total";
    public const string ActiveFieldName = "active";
    public const string RatioFieldName = "ratio";
    public const string ScoreFieldName = "score";
    public const string PayloadFieldName = "payload";
    public const string CreatedFieldName = "created";
    public const string EventTimeFieldName = "event_time";
    public const string Amount128FieldName = "amount128";
    public const string Amount256FieldName = "amount256";

    public static readonly string[] ExpectedFieldNames =
    [
        NameFieldName,
        CountFieldName,
        TotalFieldName,
        ActiveFieldName,
        RatioFieldName,
        ScoreFieldName,
        PayloadFieldName,
        CreatedFieldName,
        EventTimeFieldName,
        Amount128FieldName,
        Amount256FieldName
    ];

    public static readonly ArrowTypeId[] ExpectedTypeIds =
    [
        ArrowTypeId.String,
        ArrowTypeId.Int32,
        ArrowTypeId.Int64,
        ArrowTypeId.Boolean,
        ArrowTypeId.Float,
        ArrowTypeId.Double,
        ArrowTypeId.Binary,
        ArrowTypeId.Date32,
        ArrowTypeId.Timestamp,
        ArrowTypeId.Decimal128,
        ArrowTypeId.Decimal256
    ];

    public static readonly string[] ExpectedNames = ["alpha", "beta"];
    public static readonly int[] ExpectedCounts = [3, 7];
    public static readonly long[] ExpectedTotals = [30L, 70L];
    public static readonly bool[] ExpectedActiveFlags = [true, false];
    public static readonly float[] ExpectedRatios = [1.25f, 2.5f];
    public static readonly double[] ExpectedScores = [10.5d, 20.25d];
    public static readonly byte[][] ExpectedPayloads =
    [
        [0x01, 0x02, 0x03],
        [0xAA, 0xBB]
    ];

    public static readonly DateOnly[] ExpectedCreatedDates =
    [
        new(2026, 3, 14),
        new(2026, 3, 15)
    ];

    public static readonly DateTimeOffset[] ExpectedEventTimes =
    [
        new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero),
        new(2026, 3, 15, 16, 45, 30, TimeSpan.Zero)
    ];

    public static readonly decimal[] ExpectedAmounts128 = [12.34m, 56.78m];
    public static readonly decimal[] ExpectedAmounts256 = [123456.78m, 901234.56m];

    public const DateUnit ExpectedDateUnit = DateUnit.Day;
    public const TimeUnit ExpectedTimestampUnit = TimeUnit.Microsecond;
    public const string ExpectedTimestampTimezone = "UTC";
    public const int ExpectedDecimal128Precision = 18;
    public const int ExpectedDecimal128Scale = 2;
    public const int ExpectedDecimal256Precision = 38;
    public const int ExpectedDecimal256Scale = 2;

    public static int ExpectedRowCount => ExpectedNames.Length;

    public static RecordBatch CreateRecordBatch()
    {
        var schema = new Schema.Builder()
            .Field(new Field(NameFieldName, new StringType(), nullable: false, EmptyMetadata))
            .Field(new Field(CountFieldName, new Int32Type(), nullable: false, EmptyMetadata))
            .Field(new Field(TotalFieldName, new Int64Type(), nullable: false, EmptyMetadata))
            .Field(new Field(ActiveFieldName, new BooleanType(), nullable: false, EmptyMetadata))
            .Field(new Field(RatioFieldName, new FloatType(), nullable: false, EmptyMetadata))
            .Field(new Field(ScoreFieldName, new DoubleType(), nullable: false, EmptyMetadata))
            .Field(new Field(PayloadFieldName, new BinaryType(), nullable: false, EmptyMetadata))
            .Field(new Field(CreatedFieldName, new Date32Type(), nullable: false, EmptyMetadata))
            .Field(new Field(EventTimeFieldName, new TimestampType(ExpectedTimestampUnit, ExpectedTimestampTimezone), nullable: false, EmptyMetadata))
            .Field(new Field(Amount128FieldName, new Decimal128Type(ExpectedDecimal128Precision, ExpectedDecimal128Scale), nullable: false, EmptyMetadata))
            .Field(new Field(Amount256FieldName, new Decimal256Type(ExpectedDecimal256Precision, ExpectedDecimal256Scale), nullable: false, EmptyMetadata))
            .Build();

        var nameBuilder = new StringArray.Builder();
        foreach (var value in ExpectedNames)
        {
            nameBuilder.Append(value);
        }

        var countBuilder = new Int32Array.Builder();
        foreach (var value in ExpectedCounts)
        {
            countBuilder.Append(value);
        }

        var totalBuilder = new Int64Array.Builder();
        foreach (var value in ExpectedTotals)
        {
            totalBuilder.Append(value);
        }

        var activeBuilder = new BooleanArray.Builder();
        foreach (var value in ExpectedActiveFlags)
        {
            activeBuilder.Append(value);
        }

        var ratioBuilder = new FloatArray.Builder();
        foreach (var value in ExpectedRatios)
        {
            ratioBuilder.Append(value);
        }

        var scoreBuilder = new DoubleArray.Builder();
        foreach (var value in ExpectedScores)
        {
            scoreBuilder.Append(value);
        }

        var payloadBuilder = new BinaryArray.Builder();
        foreach (var value in ExpectedPayloads)
        {
            payloadBuilder.Append((ReadOnlySpan<byte>)value);
        }

        var createdBuilder = new Date32Array.Builder();
        foreach (var value in ExpectedCreatedDates)
        {
            createdBuilder.Append(value.ToDateTime(TimeOnly.MinValue));
        }

        var eventTimeBuilder = new TimestampArray.Builder(new TimestampType(ExpectedTimestampUnit, ExpectedTimestampTimezone));
        foreach (var value in ExpectedEventTimes)
        {
            eventTimeBuilder.Append(value);
        }

        var amount128Builder = new Decimal128Array.Builder(new Decimal128Type(ExpectedDecimal128Precision, ExpectedDecimal128Scale));
        foreach (var value in ExpectedAmounts128)
        {
            amount128Builder.Append(value);
        }

        var amount256Builder = new Decimal256Array.Builder(new Decimal256Type(ExpectedDecimal256Precision, ExpectedDecimal256Scale));
        foreach (var value in ExpectedAmounts256)
        {
            amount256Builder.Append(value);
        }

        var columns = new IArrowArray[]
        {
            nameBuilder.Build(),
            countBuilder.Build(),
            totalBuilder.Build(),
            activeBuilder.Build(),
            ratioBuilder.Build(),
            scoreBuilder.Build(),
            payloadBuilder.Build(),
            createdBuilder.Build(),
            eventTimeBuilder.Build(),
            amount128Builder.Build(),
            amount256Builder.Build()
        };

        return new RecordBatch(schema, columns, ExpectedRowCount);
    }
}

internal static class AdvancedStructFixture
{
    public static readonly KeyValuePair<string, string>[] EmptyMetadata = [];

    public const string StructFieldName = "details";
    public const string NameFieldName = "name";
    public const string TotalFieldName = "total";
    public const string ActiveFieldName = "active";
    public const string RatioFieldName = "ratio";
    public const string ScoreFieldName = "score";
    public const string PayloadFieldName = "payload";
    public const string CreatedFieldName = "created";
    public const string EventTimeFieldName = "event_time";
    public const string Amount128FieldName = "amount128";
    public const string Amount256FieldName = "amount256";

    public static readonly string[] ExpectedChildFieldNames =
    [
        NameFieldName,
        TotalFieldName,
        ActiveFieldName,
        RatioFieldName,
        ScoreFieldName,
        PayloadFieldName,
        CreatedFieldName,
        EventTimeFieldName,
        Amount128FieldName,
        Amount256FieldName
    ];

    public static readonly ArrowTypeId[] ExpectedChildTypeIds =
    [
        ArrowTypeId.String,
        ArrowTypeId.Int64,
        ArrowTypeId.Boolean,
        ArrowTypeId.Float,
        ArrowTypeId.Double,
        ArrowTypeId.Binary,
        ArrowTypeId.Date32,
        ArrowTypeId.Timestamp,
        ArrowTypeId.Decimal128,
        ArrowTypeId.Decimal256
    ];

    public static readonly string[] ExpectedNames = ["planner", "reconciler"];
    public static readonly long[] ExpectedTotals = [101L, 202L];
    public static readonly bool[] ExpectedActiveFlags = [true, true];
    public static readonly float[] ExpectedRatios = [3.5f, 7.25f];
    public static readonly double[] ExpectedScores = [99.9d, 88.8d];
    public static readonly byte[][] ExpectedPayloads =
    [
        [0x10, 0x20],
        [0x30, 0x40, 0x50]
    ];

    public static readonly DateOnly[] ExpectedCreatedDates =
    [
        new(2026, 4, 1),
        new(2026, 4, 2)
    ];

    public static readonly DateTimeOffset[] ExpectedEventTimes =
    [
        new(2026, 4, 1, 11, 15, 0, TimeSpan.Zero),
        new(2026, 4, 2, 12, 45, 30, TimeSpan.Zero)
    ];

    public static readonly decimal[] ExpectedAmounts128 = [7.77m, 8.88m];
    public static readonly decimal[] ExpectedAmounts256 = [777777.77m, 888888.88m];

    public const DateUnit ExpectedDateUnit = DateUnit.Day;
    public const TimeUnit ExpectedTimestampUnit = TimeUnit.Microsecond;
    public const string ExpectedTimestampTimezone = "UTC";
    public const int ExpectedDecimal128Precision = 18;
    public const int ExpectedDecimal128Scale = 2;
    public const int ExpectedDecimal256Precision = 38;
    public const int ExpectedDecimal256Scale = 2;

    public static int ExpectedRowCount => ExpectedNames.Length;

    public static RecordBatch CreateRecordBatch()
    {
        var structFields = new Field[]
        {
            new(NameFieldName, new StringType(), nullable: false, EmptyMetadata),
            new(TotalFieldName, new Int64Type(), nullable: false, EmptyMetadata),
            new(ActiveFieldName, new BooleanType(), nullable: false, EmptyMetadata),
            new(RatioFieldName, new FloatType(), nullable: false, EmptyMetadata),
            new(ScoreFieldName, new DoubleType(), nullable: false, EmptyMetadata),
            new(PayloadFieldName, new BinaryType(), nullable: false, EmptyMetadata),
            new(CreatedFieldName, new Date32Type(), nullable: false, EmptyMetadata),
            new(EventTimeFieldName, new TimestampType(ExpectedTimestampUnit, ExpectedTimestampTimezone), nullable: false, EmptyMetadata),
            new(Amount128FieldName, new Decimal128Type(ExpectedDecimal128Precision, ExpectedDecimal128Scale), nullable: false, EmptyMetadata),
            new(Amount256FieldName, new Decimal256Type(ExpectedDecimal256Precision, ExpectedDecimal256Scale), nullable: false, EmptyMetadata)
        };

        var schema = new Schema.Builder()
            .Field(new Field(StructFieldName, new StructType(structFields), nullable: false, EmptyMetadata))
            .Build();

        var nameBuilder = new StringArray.Builder();
        foreach (var value in ExpectedNames)
        {
            nameBuilder.Append(value);
        }

        var totalBuilder = new Int64Array.Builder();
        foreach (var value in ExpectedTotals)
        {
            totalBuilder.Append(value);
        }

        var activeBuilder = new BooleanArray.Builder();
        foreach (var value in ExpectedActiveFlags)
        {
            activeBuilder.Append(value);
        }

        var ratioBuilder = new FloatArray.Builder();
        foreach (var value in ExpectedRatios)
        {
            ratioBuilder.Append(value);
        }

        var scoreBuilder = new DoubleArray.Builder();
        foreach (var value in ExpectedScores)
        {
            scoreBuilder.Append(value);
        }

        var payloadBuilder = new BinaryArray.Builder();
        foreach (var value in ExpectedPayloads)
        {
            payloadBuilder.Append((ReadOnlySpan<byte>)value);
        }

        var createdBuilder = new Date32Array.Builder();
        foreach (var value in ExpectedCreatedDates)
        {
            createdBuilder.Append(value.ToDateTime(TimeOnly.MinValue));
        }

        var eventTimeBuilder = new TimestampArray.Builder(new TimestampType(ExpectedTimestampUnit, ExpectedTimestampTimezone));
        foreach (var value in ExpectedEventTimes)
        {
            eventTimeBuilder.Append(value);
        }

        var amount128Builder = new Decimal128Array.Builder(new Decimal128Type(ExpectedDecimal128Precision, ExpectedDecimal128Scale));
        foreach (var value in ExpectedAmounts128)
        {
            amount128Builder.Append(value);
        }

        var amount256Builder = new Decimal256Array.Builder(new Decimal256Type(ExpectedDecimal256Precision, ExpectedDecimal256Scale));
        foreach (var value in ExpectedAmounts256)
        {
            amount256Builder.Append(value);
        }

        var columns = new IArrowArray[]
        {
            new StructArray(
                new StructType(structFields),
                ExpectedRowCount,
                new IArrowArray[]
                {
                    nameBuilder.Build(),
                    totalBuilder.Build(),
                    activeBuilder.Build(),
                    ratioBuilder.Build(),
                    scoreBuilder.Build(),
                    payloadBuilder.Build(),
                    createdBuilder.Build(),
                    eventTimeBuilder.Build(),
                    amount128Builder.Build(),
                    amount256Builder.Build()
                },
                ArrowBuffer.Empty,
                0,
                0)
        };

        return new RecordBatch(schema, columns, ExpectedRowCount);
    }
}

internal static class ComplexContractFixture
{
    public static readonly KeyValuePair<string, string>[] EmptyMetadata = [];

    public const string IdFieldName = "id";
    public const string BudgetFieldName = "budget";
    public const string ScoresFieldName = "scores";
    public const string ScoresValueFieldName = "score";
    public const string MetricsFieldName = "metrics";
    public const string MetricsKeyFieldName = "metric";
    public const string MetricsValueFieldName = "amount";
    public const string DetailsFieldName = "details";
    public const string DetailsSourceFieldName = "source";
    public const string DetailsSamplesFieldName = "samples";
    public const string DetailsSamplesValueFieldName = "sample";
    public const string DetailsTagsFieldName = "tags";
    public const string DetailsTagsKeyFieldName = "tag";
    public const string DetailsTagsValueFieldName = "weight";

    public static readonly string[] ExpectedFieldNames =
    [
        IdFieldName,
        BudgetFieldName,
        ScoresFieldName,
        MetricsFieldName,
        DetailsFieldName
    ];

    public static readonly ArrowTypeId[] ExpectedTypeIds =
    [
        ArrowTypeId.Int32,
        ArrowTypeId.Decimal256,
        ArrowTypeId.List,
        ArrowTypeId.Map,
        ArrowTypeId.Struct
    ];

    public static readonly string[] ExpectedDetailsFieldNames =
    [
        DetailsSourceFieldName,
        DetailsSamplesFieldName,
        DetailsTagsFieldName
    ];

    public static readonly ArrowTypeId[] ExpectedDetailsTypeIds =
    [
        ArrowTypeId.String,
        ArrowTypeId.List,
        ArrowTypeId.Map
    ];

    public static readonly int[] ExpectedIds = [201, 202];
    public static readonly decimal[] ExpectedBudgets = [123456.78m, 901234.56m];
    public static readonly int[][] ExpectedScores =
    [
        [1, 3, 5],
        [2, 4, 6, 8]
    ];

    public static readonly IReadOnlyDictionary<string, int>[] ExpectedMetrics =
    [
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["cpu"] = 65,
            ["memory"] = 128
        },
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["cpu"] = 42,
            ["memory"] = 256,
            ["disk"] = 99
        }
    ];

    public static readonly string[] ExpectedSources = ["planner", "reconciler"];
    public static readonly int[][] ExpectedSamples =
    [
        [11, 12],
        [21, 22, 23]
    ];

    public static readonly IReadOnlyDictionary<string, int>[] ExpectedDetailTags =
    [
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["trusted"] = 1,
            ["edge"] = 2
        },
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["batch"] = 3,
            ["priority"] = 5
        }
    ];

    public const int ExpectedDecimalPrecision = 38;
    public const int ExpectedDecimalScale = 2;

    public static int ExpectedRowCount => ExpectedIds.Length;

    public static RecordBatch CreateRecordBatch(bool invalidNestedSourceType = false)
    {
        var scoresValueField = new Field(ScoresValueFieldName, new Int32Type(), nullable: false, EmptyMetadata);
        var samplesValueField = new Field(DetailsSamplesValueFieldName, new Int32Type(), nullable: false, EmptyMetadata);
        var metricsKeyField = new Field(MetricsKeyFieldName, new StringType(), nullable: false, EmptyMetadata);
        var metricsValueField = new Field(MetricsValueFieldName, new Int32Type(), nullable: false, EmptyMetadata);
        var detailTagsKeyField = new Field(DetailsTagsKeyFieldName, new StringType(), nullable: false, EmptyMetadata);
        var detailTagsValueField = new Field(DetailsTagsValueFieldName, new Int32Type(), nullable: false, EmptyMetadata);
        var detailFields = new Field[]
        {
            invalidNestedSourceType
                ? new(DetailsSourceFieldName, new Int32Type(), nullable: false, EmptyMetadata)
                : new(DetailsSourceFieldName, new StringType(), nullable: false, EmptyMetadata),
            new(DetailsSamplesFieldName, new ListType(samplesValueField), nullable: false, EmptyMetadata),
            new(DetailsTagsFieldName, new MapType(detailTagsKeyField, detailTagsValueField, false), nullable: false, EmptyMetadata)
        };

        var schema = new Schema.Builder()
            .Field(new Field(IdFieldName, new Int32Type(), nullable: false, EmptyMetadata))
            .Field(new Field(BudgetFieldName, new Decimal256Type(ExpectedDecimalPrecision, ExpectedDecimalScale), nullable: false, EmptyMetadata))
            .Field(new Field(ScoresFieldName, new ListType(scoresValueField), nullable: false, EmptyMetadata))
            .Field(new Field(MetricsFieldName, new MapType(metricsKeyField, metricsValueField, false), nullable: false, EmptyMetadata))
            .Field(new Field(DetailsFieldName, new StructType(detailFields), nullable: false, EmptyMetadata))
            .Build();

        var idBuilder = new Int32Array.Builder();
        foreach (var value in ExpectedIds)
        {
            idBuilder.Append(value);
        }

        var budgetBuilder = new Decimal256Array.Builder(new Decimal256Type(ExpectedDecimalPrecision, ExpectedDecimalScale));
        foreach (var value in ExpectedBudgets)
        {
            budgetBuilder.Append(value);
        }

        var scoresBuilder = new ListArray.Builder(scoresValueField);
        var scoresValueBuilder = (Int32Array.Builder)scoresBuilder.ValueBuilder;
        foreach (var row in ExpectedScores)
        {
            scoresBuilder.Append();
            foreach (var value in row)
            {
                scoresValueBuilder.Append(value);
            }
        }

        var metricsBuilder = new MapArray.Builder(new MapType(metricsKeyField, metricsValueField, false));
        var metricsKeyBuilder = (StringArray.Builder)metricsBuilder.KeyBuilder;
        var metricsValueBuilder = (Int32Array.Builder)metricsBuilder.ValueBuilder;
        foreach (var row in ExpectedMetrics)
        {
            metricsBuilder.Append();
            foreach (var entry in row)
            {
                metricsKeyBuilder.Append(entry.Key);
                metricsValueBuilder.Append(entry.Value);
            }
        }

        var detailSourceBuilder = new StringArray.Builder();
        foreach (var value in ExpectedSources)
        {
            detailSourceBuilder.Append(value);
        }

        var invalidDetailSourceBuilder = new Int32Array.Builder();
        for (var index = 0; index < ExpectedSources.Length; index++)
        {
            invalidDetailSourceBuilder.Append(index + 1);
        }

        var detailSamplesBuilder = new ListArray.Builder(samplesValueField);
        var detailSamplesValueBuilder = (Int32Array.Builder)detailSamplesBuilder.ValueBuilder;
        foreach (var row in ExpectedSamples)
        {
            detailSamplesBuilder.Append();
            foreach (var value in row)
            {
                detailSamplesValueBuilder.Append(value);
            }
        }

        var detailTagsBuilder = new MapArray.Builder(new MapType(detailTagsKeyField, detailTagsValueField, false));
        var detailTagsKeyBuilder = (StringArray.Builder)detailTagsBuilder.KeyBuilder;
        var detailTagsValueBuilder = (Int32Array.Builder)detailTagsBuilder.ValueBuilder;
        foreach (var row in ExpectedDetailTags)
        {
            detailTagsBuilder.Append();
            foreach (var entry in row)
            {
                detailTagsKeyBuilder.Append(entry.Key);
                detailTagsValueBuilder.Append(entry.Value);
            }
        }

        var columns = new IArrowArray[]
        {
            idBuilder.Build(),
            budgetBuilder.Build(),
            scoresBuilder.Build(),
            metricsBuilder.Build(MemoryAllocator.Default.Value),
            new StructArray(
                new StructType(detailFields),
                ExpectedRowCount,
                new IArrowArray[]
                {
                    invalidNestedSourceType
                        ? invalidDetailSourceBuilder.Build()
                        : detailSourceBuilder.Build(),
                    detailSamplesBuilder.Build(),
                    detailTagsBuilder.Build(MemoryAllocator.Default.Value)
                },
                ArrowBuffer.Empty,
                0,
                0)
        };

        return new RecordBatch(schema, columns, ExpectedRowCount);
    }

    public static bool TryValidateModel(ComplexOperatorBatchModel model, out string? error)
    {
        ArgumentNullException.ThrowIfNull(model);

        error = null;
        if (model.Rows.Count != ExpectedRowCount)
        {
            error = $"Expected {ExpectedRowCount} rows but got {model.Rows.Count}.";
            return false;
        }

        for (var rowIndex = 0; rowIndex < ExpectedRowCount; rowIndex++)
        {
            var row = model.Rows[rowIndex];
            if (row.Id != ExpectedIds[rowIndex])
            {
                error = $"Expected row {rowIndex} id to be {ExpectedIds[rowIndex]} but got {row.Id}.";
                return false;
            }

            if (row.Budget != ExpectedBudgets[rowIndex])
            {
                error = $"Expected row {rowIndex} budget to be {ExpectedBudgets[rowIndex]} but got {row.Budget}.";
                return false;
            }

            if (!row.Scores.SequenceEqual(ExpectedScores[rowIndex]))
            {
                error = $"Expected row {rowIndex} scores to be [{string.Join(",", ExpectedScores[rowIndex])}] but got [{string.Join(",", row.Scores)}].";
                return false;
            }

            if (!TryValidateMap(row.Metrics, ExpectedMetrics[rowIndex], $"row {rowIndex} metrics", out error))
            {
                return false;
            }

            if (!string.Equals(row.Details.Source, ExpectedSources[rowIndex], StringComparison.Ordinal))
            {
                error = $"Expected row {rowIndex} details.source to be '{ExpectedSources[rowIndex]}' but got '{row.Details.Source}'.";
                return false;
            }

            if (!row.Details.Samples.SequenceEqual(ExpectedSamples[rowIndex]))
            {
                error = $"Expected row {rowIndex} details.samples to be [{string.Join(",", ExpectedSamples[rowIndex])}] but got [{string.Join(",", row.Details.Samples)}].";
                return false;
            }

            if (!TryValidateMap(row.Details.Tags, ExpectedDetailTags[rowIndex], $"row {rowIndex} details.tags", out error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateMap(
        IReadOnlyDictionary<string, int> actual,
        IReadOnlyDictionary<string, int> expected,
        string label,
        out string? error)
    {
        error = null;
        if (actual.Count != expected.Count)
        {
            error = $"Expected {label} to contain {expected.Count} entries but got {actual.Count}.";
            return false;
        }

        foreach (var entry in expected)
        {
            if (!actual.TryGetValue(entry.Key, out var actualValue))
            {
                error = $"Expected {label} to contain key '{entry.Key}', but it was not found.";
                return false;
            }

            if (actualValue != entry.Value)
            {
                error = $"Expected {label} key '{entry.Key}' to be {entry.Value} but got {actualValue}.";
                return false;
            }
        }

        return true;
    }

    internal sealed class ComplexOperatorBatchContract : ArrowRecordBatchContract<ComplexOperatorBatchModel>
    {
        public ComplexOperatorBatchContract()
            : base(
                ComplexContractFixture.ExpectedRowCount,
                ComplexContractFixture.ExpectedFieldNames,
                ComplexContractFixture.ExpectedTypeIds)
        {
        }

        protected override bool TryMap(RecordBatch recordBatch, out ComplexOperatorBatchModel? model, out string? error)
            => TryProjectModel(
                recordBatch,
                static (ArrowRecordBatchRowAccessor row, out ComplexOperatorRowModel? rowModel, out string? projectionError) =>
                {
                    rowModel = null;
                    projectionError = null;

                    if (!row.TryGetInt32(IdFieldName, out var id, out projectionError) ||
                        !row.TryGetDecimal256(BudgetFieldName, ExpectedDecimalPrecision, ExpectedDecimalScale, out var budget, out projectionError) ||
                        !row.TryGetInt32List(ScoresFieldName, out var scores, out projectionError) ||
                        !row.TryGetStringInt32Map(MetricsFieldName, out var metrics, out projectionError) ||
                        !row.TryProjectStruct<ComplexOperatorDetailModel>(
                            DetailsFieldName,
                            ExpectedDetailsFieldNames,
                            ExpectedDetailsTypeIds,
                            static (ArrowStructRowAccessor detailRow, out ComplexOperatorDetailModel? detail, out string? detailError) =>
                            {
                                detail = null;
                                detailError = null;

                                if (!detailRow.TryGetString(DetailsSourceFieldName, out var source, out detailError) ||
                                    !detailRow.TryGetInt32List(DetailsSamplesFieldName, out var samples, out detailError) ||
                                    !detailRow.TryGetStringInt32Map(DetailsTagsFieldName, out var tags, out detailError))
                                {
                                    return false;
                                }

                                detail = new ComplexOperatorDetailModel(
                                    source,
                                    samples.ToArray(),
                                    new Dictionary<string, int>(tags, StringComparer.Ordinal));
                                return true;
                            },
                            out var detail,
                            out projectionError))
                    {
                        return false;
                    }

                    if (detail is null)
                    {
                        projectionError = $"Expected struct field '{DetailsFieldName}' projection to return a non-null model.";
                        return false;
                    }

                    rowModel = new ComplexOperatorRowModel(
                        id,
                        budget,
                        scores.ToArray(),
                        new Dictionary<string, int>(metrics, StringComparer.Ordinal),
                        detail);
                    return true;
                },
                static rows => new ComplexOperatorBatchModel(rows),
                out model,
                out error);
    }
}

internal sealed record ComplexOperatorBatchModel(IReadOnlyList<ComplexOperatorRowModel> Rows);

internal sealed record ComplexOperatorRowModel(
    int Id,
    decimal Budget,
    IReadOnlyList<int> Scores,
    IReadOnlyDictionary<string, int> Metrics,
    ComplexOperatorDetailModel Details);

internal sealed record ComplexOperatorDetailModel(
    string Source,
    IReadOnlyList<int> Samples,
    IReadOnlyDictionary<string, int> Tags);
