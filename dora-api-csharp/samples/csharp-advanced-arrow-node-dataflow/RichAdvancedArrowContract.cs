using Apache.Arrow;
using Apache.Arrow.Arrays;
using Apache.Arrow.Memory;
using Apache.Arrow.Scalars;
using Apache.Arrow.Types;

namespace CSharpAdvancedArrowNodeDataflow;

internal static class RichAdvancedArrowContract
{
    public static readonly KeyValuePair<string, string>[] EmptyMetadata = [];

    public static readonly string[] ExpectedFieldNames =
    [
        "id",
        "created",
        "event_time",
        "payload",
        "fixed_payload",
        "processing_time",
        "billing_cycle",
        "retry_window",
        "maintenance_window",
        "result"
    ];

    public static readonly ArrowTypeId[] ExpectedTypeIds =
    [
        ArrowTypeId.Int32,
        ArrowTypeId.Date32,
        ArrowTypeId.Timestamp,
        ArrowTypeId.Binary,
        ArrowTypeId.FixedSizedBinary,
        ArrowTypeId.Duration,
        ArrowTypeId.Interval,
        ArrowTypeId.Interval,
        ArrowTypeId.Interval,
        ArrowTypeId.Union
    ];

    public static readonly int[] ExpectedIds = [7, 8];
    public static readonly DateOnly[] ExpectedCreatedDates =
    [
        new(2026, 3, 14),
        new(2026, 3, 15)
    ];

    public static readonly DateTimeOffset[] ExpectedEventTimes =
    [
        new(2026, 3, 14, 10, 30, 0, TimeSpan.Zero),
        new(2026, 3, 15, 8, 45, 30, TimeSpan.Zero)
    ];

    public static readonly byte[][] ExpectedPayloads =
    [
        [0x01, 0x02, 0x03],
        [0xAA, 0xBB, 0xCC, 0xDD]
    ];

    public static readonly byte[][] ExpectedFixedPayloads =
    [
        [0xDE, 0xAD, 0xBE, 0xEF],
        [0xCA, 0xFE, 0xBA, 0xBE]
    ];

    public static readonly TimeSpan[] ExpectedProcessingTimes =
    [
        TimeSpan.FromMilliseconds(125),
        TimeSpan.FromMilliseconds(250)
    ];

    public static readonly YearMonthInterval[] ExpectedBillingCycles =
    [
        new(1, 2),
        new(2, 3)
    ];

    public static readonly DayTimeInterval[] ExpectedRetryWindows =
    [
        new(1, 60_000),
        new(2, 120_000)
    ];

    public static readonly MonthDayNanosecondInterval[] ExpectedMaintenanceWindows =
    [
        new(1, 2, 3_000),
        new(4, 5, 6_000)
    ];

    public static readonly string[] ExpectedUnionChildFieldNames =
    [
        "status",
        "code"
    ];

    public static readonly ArrowTypeId[] ExpectedUnionChildTypeIds =
    [
        ArrowTypeId.String,
        ArrowTypeId.Int32
    ];

    public static readonly int[] ExpectedUnionTypeIds = [7, 11];
    public static readonly byte[] ExpectedUnionRowTypeIds = [(byte)ExpectedUnionTypeIds[0], (byte)ExpectedUnionTypeIds[1]];
    public static readonly string[] ExpectedUnionStatusValues = ["ok"];
    public static readonly int[] ExpectedUnionCodeValues = [42];

    public const TimeUnit ExpectedTimestampUnit = TimeUnit.Microsecond;
    public const string ExpectedTimestampTimezone = "UTC";
    public const DateUnit ExpectedDateUnit = DateUnit.Day;
    public const TimeUnit ExpectedDurationUnit = TimeUnit.Millisecond;
    public const int ExpectedFixedPayloadByteWidth = 4;

    public static int ExpectedRowCount => ExpectedIds.Length;

    public static RecordBatch CreateRecordBatch()
    {
        var schema = new Schema.Builder()
            .Field(new Field("id", new Int32Type(), nullable: false, EmptyMetadata))
            .Field(new Field("created", new Date32Type(), nullable: false, EmptyMetadata))
            .Field(new Field("event_time", new TimestampType(ExpectedTimestampUnit, ExpectedTimestampTimezone), nullable: false, EmptyMetadata))
            .Field(new Field("payload", new BinaryType(), nullable: false, EmptyMetadata))
            .Field(new Field("fixed_payload", new FixedSizeBinaryType(ExpectedFixedPayloadByteWidth), nullable: false, EmptyMetadata))
            .Field(new Field("processing_time", DurationType.Millisecond, nullable: false, EmptyMetadata))
            .Field(new Field("billing_cycle", new IntervalType(IntervalUnit.YearMonth), nullable: false, EmptyMetadata))
            .Field(new Field("retry_window", new IntervalType(IntervalUnit.DayTime), nullable: false, EmptyMetadata))
            .Field(new Field("maintenance_window", new IntervalType(IntervalUnit.MonthDayNanosecond), nullable: false, EmptyMetadata))
            .Field(
                new Field(
                    "result",
                    new UnionType(
                        new[]
                        {
                            new Field(ExpectedUnionChildFieldNames[0], new StringType(), nullable: false, EmptyMetadata),
                            new Field(ExpectedUnionChildFieldNames[1], new Int32Type(), nullable: false, EmptyMetadata)
                        },
                        ExpectedUnionTypeIds,
                        UnionMode.Dense),
                    nullable: false,
                    EmptyMetadata))
            .Build();

        var idBuilder = new Int32Array.Builder();
        foreach (var value in ExpectedIds)
        {
            idBuilder.Append(value);
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

        var payloadBuilder = new BinaryArray.Builder();
        foreach (var value in ExpectedPayloads)
        {
            payloadBuilder.Append((ReadOnlySpan<byte>)value);
        }

        var fixedPayloadBufferBuilder = new ArrowBuffer.Builder<byte>(ExpectedFixedPayloads.Length * ExpectedFixedPayloadByteWidth);
        foreach (var value in ExpectedFixedPayloads)
        {
            fixedPayloadBufferBuilder.Append((ReadOnlySpan<byte>)value);
        }

        var fixedPayloadArray = new FixedSizeBinaryArray(
            new ArrayData(
                new FixedSizeBinaryType(ExpectedFixedPayloadByteWidth),
                ExpectedRowCount,
                0,
                0,
                [ArrowBuffer.Empty, fixedPayloadBufferBuilder.Build(MemoryAllocator.Default.Value)],
                []));

        var processingTimeBuilder = new DurationArray.Builder(DurationType.Millisecond);
        foreach (var value in ExpectedProcessingTimes)
        {
            processingTimeBuilder.Append(value);
        }

        var billingCycleBuilder = new YearMonthIntervalArray.Builder();
        foreach (var value in ExpectedBillingCycles)
        {
            billingCycleBuilder.Append(value);
        }

        var retryWindowBuilder = new DayTimeIntervalArray.Builder();
        foreach (var value in ExpectedRetryWindows)
        {
            retryWindowBuilder.Append(value);
        }

        var maintenanceWindowBuilder = new MonthDayNanosecondIntervalArray.Builder();
        foreach (var value in ExpectedMaintenanceWindows)
        {
            maintenanceWindowBuilder.Append(value);
        }

        var unionStatusBuilder = new StringArray.Builder();
        foreach (var value in ExpectedUnionStatusValues)
        {
            unionStatusBuilder.Append(value);
        }

        var unionCodeBuilder = new Int32Array.Builder();
        foreach (var value in ExpectedUnionCodeValues)
        {
            unionCodeBuilder.Append(value);
        }

        var unionTypeIdBuilder = new ArrowBuffer.Builder<byte>(ExpectedUnionRowTypeIds.Length);
        unionTypeIdBuilder.Append(ExpectedUnionRowTypeIds);

        var unionOffsetBuilder = new ArrowBuffer.Builder<int>(ExpectedRowCount);
        unionOffsetBuilder.Append(0);
        unionOffsetBuilder.Append(0);

        var unionArray = new DenseUnionArray(
            new UnionType(
                new[]
                {
                    new Field(ExpectedUnionChildFieldNames[0], new StringType(), nullable: false, EmptyMetadata),
                    new Field(ExpectedUnionChildFieldNames[1], new Int32Type(), nullable: false, EmptyMetadata)
                },
                ExpectedUnionTypeIds,
                UnionMode.Dense),
            ExpectedRowCount,
            new IArrowArray[]
            {
                unionStatusBuilder.Build(),
                unionCodeBuilder.Build()
            },
            unionTypeIdBuilder.Build(MemoryAllocator.Default.Value),
            unionOffsetBuilder.Build(MemoryAllocator.Default.Value),
            0,
            0);

        var columns = new IArrowArray[]
        {
            idBuilder.Build(),
            createdBuilder.Build(),
            eventTimeBuilder.Build(),
            payloadBuilder.Build(),
            fixedPayloadArray,
            processingTimeBuilder.Build(MemoryAllocator.Default.Value),
            billingCycleBuilder.Build(MemoryAllocator.Default.Value),
            retryWindowBuilder.Build(MemoryAllocator.Default.Value),
            maintenanceWindowBuilder.Build(MemoryAllocator.Default.Value),
            unionArray
        };

        return new RecordBatch(schema, columns, length: ExpectedRowCount);
    }
}
