using Apache.Arrow;
using Apache.Arrow.Types;

namespace DoraOperatorRegressionRunner;

internal static class RoundtripScenarioFixture
{
    public static readonly KeyValuePair<string, string>[] EmptyMetadata = [];

    public static readonly string[] ExpectedFieldNames =
    [
        "name",
        "count",
        "active",
        "total",
        "ratio",
        "score"
    ];

    public static readonly ArrowTypeId[] ExpectedTypeIds =
    [
        ArrowTypeId.String,
        ArrowTypeId.Int32,
        ArrowTypeId.Boolean,
        ArrowTypeId.Int64,
        ArrowTypeId.Float,
        ArrowTypeId.Double
    ];

    public static readonly string[] ExpectedNames = ["alpha", "beta"];
    public static readonly int[] ExpectedCounts = [1, 2];
    public static readonly bool[] ExpectedActive = [true, false];
    public static readonly long[] ExpectedTotals = [100L, 200L];
    public static readonly float[] ExpectedRatios = [1.5f, 2.5f];
    public static readonly double[] ExpectedScores = [3.25d, 4.75d];

    public static int ExpectedRowCount => ExpectedNames.Length;

    public static RecordBatch CreateRecordBatch(string firstFieldName = "name", bool empty = false)
    {
        var schema = new Schema.Builder()
            .Field(new Field(firstFieldName, new StringType(), nullable: false, EmptyMetadata))
            .Field(new Field("count", new Int32Type(), nullable: false, EmptyMetadata))
            .Field(new Field("active", new BooleanType(), nullable: false, EmptyMetadata))
            .Field(new Field("total", new Int64Type(), nullable: false, EmptyMetadata))
            .Field(new Field("ratio", new FloatType(), nullable: false, EmptyMetadata))
            .Field(new Field("score", new DoubleType(), nullable: false, EmptyMetadata))
            .Build();

        if (empty)
        {
            var emptyColumns = new IArrowArray[]
            {
                new StringArray.Builder().Build(),
                new Int32Array.Builder().Build(),
                new BooleanArray.Builder().Build(),
                new Int64Array.Builder().Build(),
                new FloatArray.Builder().Build(),
                new DoubleArray.Builder().Build()
            };

            return new RecordBatch(schema, emptyColumns, 0);
        }

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

        var activeBuilder = new BooleanArray.Builder();
        foreach (var value in ExpectedActive)
        {
            activeBuilder.Append(value);
        }

        var totalBuilder = new Int64Array.Builder();
        foreach (var value in ExpectedTotals)
        {
            totalBuilder.Append(value);
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

        var columns = new IArrowArray[]
        {
            nameBuilder.Build(),
            countBuilder.Build(),
            activeBuilder.Build(),
            totalBuilder.Build(),
            ratioBuilder.Build(),
            scoreBuilder.Build()
        };

        return new RecordBatch(schema, columns, ExpectedRowCount);
    }
}
