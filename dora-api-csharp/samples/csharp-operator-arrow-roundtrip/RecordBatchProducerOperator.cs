using Apache.Arrow;
using DoraOperator;

namespace CSharpArrowRoundtrip;

public sealed class RecordBatchProducerOperator : DoraOperatorBase
{
    private const string NonArrowPayloadText = "not-arrow-bytes";
    private bool _sent;

    protected override OnEventResult OnEvent(OperatorEvent ev, SendOutput sendOutput)
    {
        if (ev is InputEvent && !_sent)
        {
            var sendResult = SendTestPayload(sendOutput);
            if (!sendResult.IsSuccess)
            {
                return OnEventResult.Err(sendResult.Error ?? "Failed to send test payload.");
            }

            _sent = true;
            return OnEventResult.Continue();
        }

        if (ev is InputClosedEvent or StopEvent)
        {
            return OnEventResult.Stop();
        }

        if (ev is ErrorEvent error)
        {
            return OnEventResult.Err(error.Message);
        }

        return OnEventResult.Continue();
    }

    private static DoraResult SendTestPayload(SendOutput sendOutput)
    {
        using var payloadOwner = CreatePayloadOwner(GetTestMode());
        return sendOutput.Send("batch", payloadOwner.Payload);
    }

    private static ArrowTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ARROW_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => ArrowTestMode.Normal,
            "non-arrow-bytes" => ArrowTestMode.NonArrowBytes,
            "schema-mismatch" => ArrowTestMode.SchemaMismatch,
            "empty-batch" => ArrowTestMode.EmptyBatch,
            _ => throw new InvalidOperationException($"Unsupported Arrow test mode '{rawMode}'.")
        };
    }

    private static RecordBatch CreateRecordBatch(ArrowTestMode mode)
    {
        var firstFieldName = mode == ArrowTestMode.SchemaMismatch ? "label" : "name";
        var empty = mode == ArrowTestMode.EmptyBatch;
        return RoundtripContractFixture.CreateRecordBatch(firstFieldName, empty);
    }

    private static PayloadOwner CreatePayloadOwner(ArrowTestMode mode)
    {
        if (mode == ArrowTestMode.NonArrowBytes)
        {
            return new PayloadOwner(DoraOutputPayload.TextPayload(NonArrowPayloadText));
        }

        var recordBatch = CreateRecordBatch(mode);
        return new PayloadOwner(DoraOutputPayload.RecordBatchPayload(recordBatch), recordBatch);
    }

    private sealed class PayloadOwner : IDisposable
    {
        private readonly IDisposable? _ownedResource;

        public PayloadOwner(DoraOutputPayload payload, IDisposable? ownedResource = null)
        {
            Payload = payload;
            _ownedResource = ownedResource;
        }

        public DoraOutputPayload Payload { get; }

        public void Dispose()
        {
            _ownedResource?.Dispose();
        }
    }

    private enum ArrowTestMode
    {
        Normal,
        NonArrowBytes,
        SchemaMismatch,
        EmptyBatch
    }
}
