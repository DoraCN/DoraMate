using Apache.Arrow;
using Apache.Arrow.Types;
using DoraNode;

namespace CSharpComplexArrowNodeDataflow;

internal static class RichComplexArrowContract
{
    public static readonly KeyValuePair<string, string>[] EmptyMetadata = [];

    public const string IdFieldName = "id";
    public const string AmountFieldName = "amount";
    public const string TagsFieldName = "tags";
    public const string TagsValueFieldName = "tag";
    public const string MetaFieldName = "meta";
    public const string MetaSourceFieldName = "source";
    public const string MetaPriorityFieldName = "priority";

    public static readonly string[] ExpectedFieldNames =
    [
        IdFieldName,
        AmountFieldName,
        TagsFieldName,
        MetaFieldName
    ];

    public static readonly ArrowTypeId[] ExpectedTypeIds =
    [
        ArrowTypeId.Int32,
        ArrowTypeId.Decimal128,
        ArrowTypeId.List,
        ArrowTypeId.Struct
    ];

    public static readonly string[] ExpectedMetaFieldNames =
    [
        MetaSourceFieldName,
        MetaPriorityFieldName
    ];

    public static readonly ArrowTypeId[] ExpectedMetaTypeIds =
    [
        ArrowTypeId.String,
        ArrowTypeId.Int32
    ];

    public static readonly int[] ExpectedIds = [101, 102];
    public static readonly decimal[] ExpectedAmounts = [12.34m, 56.78m];
    public static readonly string[][] ExpectedTags =
    [
        ["sensor", "critical"],
        ["batch", "nightly", "v2"]
    ];

    public static readonly string[] ExpectedSources = ["erp", "scheduler"];
    public static readonly int[] ExpectedPriorities = [1, 5];

    public const int ExpectedDecimalPrecision = 18;
    public const int ExpectedDecimalScale = 2;

    public static int ExpectedRowCount => ExpectedIds.Length;

    public static readonly ComplexBatchContract Contract = new();

    public const string ContractFailureSummaryPrefix = "NODE_ARROW_COMPLEX_EXPECTED_CONTRACT_FAILURE_OK";

    public static RecordBatch CreateRecordBatch(bool invalidNestedPriorityType = false)
    {
        var tagsValueField = new Field(TagsValueFieldName, new StringType(), nullable: false, EmptyMetadata);
        var metaFields = new Field[]
        {
            new(MetaSourceFieldName, new StringType(), nullable: false, EmptyMetadata),
            invalidNestedPriorityType
                ? new(MetaPriorityFieldName, new StringType(), nullable: false, EmptyMetadata)
                : new(MetaPriorityFieldName, new Int32Type(), nullable: false, EmptyMetadata)
        };

        var schema = new Schema.Builder()
            .Field(new Field(IdFieldName, new Int32Type(), nullable: false, EmptyMetadata))
            .Field(new Field(AmountFieldName, new Decimal128Type(ExpectedDecimalPrecision, ExpectedDecimalScale), nullable: false, EmptyMetadata))
            .Field(new Field(TagsFieldName, new ListType(tagsValueField), nullable: false, EmptyMetadata))
            .Field(new Field(MetaFieldName, new StructType(metaFields), nullable: false, EmptyMetadata))
            .Build();

        var idBuilder = new Int32Array.Builder();
        foreach (var value in ExpectedIds)
        {
            idBuilder.Append(value);
        }

        var amountBuilder = new Decimal128Array.Builder(new Decimal128Type(ExpectedDecimalPrecision, ExpectedDecimalScale));
        foreach (var value in ExpectedAmounts)
        {
            amountBuilder.Append(value);
        }

        var tagsBuilder = new ListArray.Builder(tagsValueField);
        var tagsValueBuilder = (StringArray.Builder)tagsBuilder.ValueBuilder;
        foreach (var row in ExpectedTags)
        {
            tagsBuilder.Append();
            foreach (var value in row)
            {
                tagsValueBuilder.Append(value);
            }
        }

        var sourceBuilder = new StringArray.Builder();
        foreach (var value in ExpectedSources)
        {
            sourceBuilder.Append(value);
        }

        var priorityBuilder = new Int32Array.Builder();
        foreach (var value in ExpectedPriorities)
        {
            priorityBuilder.Append(value);
        }

        var invalidPriorityBuilder = new StringArray.Builder();
        foreach (var value in ExpectedPriorities)
        {
            invalidPriorityBuilder.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var columns = new IArrowArray[]
        {
            idBuilder.Build(),
            amountBuilder.Build(),
            tagsBuilder.Build(),
            new StructArray(
                new StructType(metaFields),
                ExpectedRowCount,
                new IArrowArray[]
                {
                    sourceBuilder.Build(),
                    invalidNestedPriorityType
                        ? invalidPriorityBuilder.Build()
                        : priorityBuilder.Build()
                },
                ArrowBuffer.Empty,
                0,
                0)
        };

        return new RecordBatch(schema, columns, length: ExpectedRowCount);
    }

    public static bool TryValidateModel(ComplexBatchModel model, out string? error)
    {
        ArgumentNullException.ThrowIfNull(model);

        error = null;
        if (model.Rows.Count != ExpectedRowCount)
        {
            error = $"Expected {ExpectedRowCount} model rows but got {model.Rows.Count}.";
            return false;
        }

        for (var index = 0; index < ExpectedRowCount; index++)
        {
            var row = model.Rows[index];
            if (row.Id != ExpectedIds[index])
            {
                error = $"Expected model row {index} id to be {ExpectedIds[index]} but got {row.Id}.";
                return false;
            }

            if (row.Amount != ExpectedAmounts[index])
            {
                error = $"Expected model row {index} amount to be {ExpectedAmounts[index]} but got {row.Amount}.";
                return false;
            }

            var expectedTags = ExpectedTags[index];
            if (row.Tags.Count != expectedTags.Length)
            {
                error = $"Expected model row {index} to contain {expectedTags.Length} tags but got {row.Tags.Count}.";
                return false;
            }

            for (var tagIndex = 0; tagIndex < expectedTags.Length; tagIndex++)
            {
                if (!string.Equals(row.Tags[tagIndex], expectedTags[tagIndex], StringComparison.Ordinal))
                {
                    error =
                        $"Expected model row {index} tag {tagIndex} to be '{expectedTags[tagIndex]}' but got '{row.Tags[tagIndex]}'.";
                    return false;
                }
            }

            if (!string.Equals(row.Meta.Source, ExpectedSources[index], StringComparison.Ordinal))
            {
                error =
                    $"Expected model row {index} meta source to be '{ExpectedSources[index]}' but got '{row.Meta.Source}'.";
                return false;
            }

            if (row.Meta.Priority != ExpectedPriorities[index])
            {
                error =
                    $"Expected model row {index} meta priority to be {ExpectedPriorities[index]} but got {row.Meta.Priority}.";
                return false;
            }
        }

        return true;
    }

    public static string CreateSuccessSummary()
    {
        var fields = string.Join(",", ExpectedFieldNames);
        var types = string.Join(",", ExpectedTypeIds.Select(typeId => typeId.ToString()));
        return $"NODE_ARROW_COMPLEX_OK fields={fields} cols={ExpectedFieldNames.Length} rows={ExpectedRowCount} types={types}";
    }

    public static string CreateExpectedContractFailureSummary(DoraNodeErrorCode errorCode, string error)
    {
        return $"{ContractFailureSummaryPrefix} code={errorCode} error={error}";
    }

    internal sealed class ComplexBatchContract : ArrowRecordBatchContract<ComplexBatchModel>
    {
        public ComplexBatchContract()
            : base(
                RichComplexArrowContract.ExpectedRowCount,
                RichComplexArrowContract.ExpectedFieldNames,
                RichComplexArrowContract.ExpectedTypeIds)
        {
        }

        protected override bool TryMap(RecordBatch recordBatch, out ComplexBatchModel? model, out string? error)
            => TryProjectModel(
                recordBatch,
                static (ArrowRecordBatchRowAccessor row, out ComplexRowModel? rowModel, out string? projectionError) =>
                {
                    rowModel = null;
                    projectionError = null;

                    if (!row.TryGetInt32(IdFieldName, out var id, out projectionError) ||
                        !row.TryGetDecimal128(
                            AmountFieldName,
                            ExpectedDecimalPrecision,
                            ExpectedDecimalScale,
                            out var amount,
                            out projectionError) ||
                        !row.TryGetStringList(TagsFieldName, out var tags, out projectionError) ||
                        !row.TryProjectStruct<ComplexMetaModel>(
                            MetaFieldName,
                            ExpectedMetaFieldNames,
                            ExpectedMetaTypeIds,
                            static (ArrowStructRowAccessor metaRow, out ComplexMetaModel? meta, out string? metaError) =>
                            {
                                meta = null;
                                metaError = null;

                                if (!metaRow.TryGetString(MetaSourceFieldName, out var source, out metaError) ||
                                    !metaRow.TryGetInt32(MetaPriorityFieldName, out var priority, out metaError))
                                {
                                    return false;
                                }

                                meta = new ComplexMetaModel(source, priority);
                                return true;
                            },
                            out var meta,
                            out projectionError))
                    {
                        return false;
                    }

                    if (meta is null)
                    {
                        projectionError = $"Expected Struct column '{MetaFieldName}' projection to return a non-null model.";
                        return false;
                    }

                    rowModel = new ComplexRowModel(id, amount, tags.ToArray(), meta);
                    return true;
                },
                static rows => new ComplexBatchModel(rows),
                out model,
                out error);
    }
}

internal sealed record ComplexBatchModel(IReadOnlyList<ComplexRowModel> Rows);

internal sealed record ComplexRowModel(int Id, decimal Amount, IReadOnlyList<string> Tags, ComplexMetaModel Meta);

internal sealed record ComplexMetaModel(string Source, int Priority);
