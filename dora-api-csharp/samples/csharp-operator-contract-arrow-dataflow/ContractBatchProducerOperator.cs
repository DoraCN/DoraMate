using DoraOperator;

namespace CSharpOperatorContractArrow;

public sealed class ContractBatchProducerOperator : DoraOperatorBase
{
    private bool _sent;

    protected override OnEventResult OnEvent(OperatorEvent ev, SendOutput sendOutput)
    {
        if (ev is InputEvent && !_sent)
        {
            using var recordBatch = ContractArrowFixture.CreateRecordBatch(
                invalidNestedSourceType: GetTestMode() == ArrowTestMode.ContractFailure);
            var sendResult = sendOutput.Send("batch", DoraOutputPayload.RecordBatchPayload(recordBatch));
            if (!sendResult.IsSuccess)
            {
                return OnEventResult.Err(sendResult.Error ?? "Failed to send contract Arrow RecordBatch.");
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

    private static ArrowTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ARROW_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => ArrowTestMode.Normal,
            "contract-failure" => ArrowTestMode.ContractFailure,
            _ => throw new InvalidOperationException($"Unsupported Arrow test mode '{rawMode}'.")
        };
    }

    private enum ArrowTestMode
    {
        Normal,
        ContractFailure
    }
}
