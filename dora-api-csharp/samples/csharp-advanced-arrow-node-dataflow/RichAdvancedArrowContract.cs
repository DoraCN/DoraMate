using Apache.Arrow;
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
        "payload"
    ];

    public static readonly ArrowTypeId[] ExpectedTypeIds =
    [
        ArrowTypeId.Int32,
        ArrowTypeId.Date32,
        ArrowTypeId.Timestamp,
        ArrowTypeId.Binary
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

    public const TimeUnit ExpectedTimestampUnit = TimeUnit.Microsecond;
    public const string ExpectedTimestampTimezone = "UTC";
    public const DateUnit ExpectedDateUnit = DateUnit.Day;

    public static int ExpectedRowCount => ExpectedIds.Length;

    public static RecordBatch CreateRecordBatch()
    {
        var schema = new Schema.Builder()
            .Field(new Field("id", new Int32Type(), nullable: false, EmptyMetadata))
            .Field(new Field("created", new Date32Type(), nullable: false, EmptyMetadata))
            .Field(new Field("event_time", new TimestampType(ExpectedTimestampUnit, ExpectedTimestampTimezone), nullable: false, EmptyMetadata))
            .Field(new Field("payload", new BinaryType(), nullable: false, EmptyMetadata))
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

        var columns = new IArrowArray[]
        {
            idBuilder.Build(),
            createdBuilder.Build(),
            eventTimeBuilder.Build(),
            payloadBuilder.Build()
        };

        return new RecordBatch(schema, columns, length: ExpectedRowCount);
    }
}
