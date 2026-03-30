using Apache.Arrow;

namespace DoraOperator;

/// <summary>
/// Managed output helper passed to higher-level operator event handlers.
/// </summary>
public sealed class OperatorOutput
{
    private readonly SendOutput _sendOutput;
    private readonly DoraOperatorBase _owner;

    internal OperatorOutput(SendOutput sendOutput, DoraOperatorBase owner)
    {
        _sendOutput = sendOutput ?? throw new ArgumentNullException(nameof(sendOutput));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal SendOutput Delegate => _sendOutput;

    public DoraResult Send(string outputId, byte[] data)
    {
        data ??= System.Array.Empty<byte>();
        return _sendOutput(outputId, data);
    }

    public DoraResult Send(string outputId, ReadOnlyMemory<byte> data)
    {
        return _sendOutput.Send(outputId, data);
    }

    public DoraResult Send(string outputId, string data)
    {
        return _sendOutput.Send(outputId, data);
    }

    public DoraResult Send(string outputId, RecordBatch recordBatch)
    {
        return _sendOutput.Send(outputId, recordBatch);
    }

    public DoraResult Send(string outputId, ArrowPayload payload)
    {
        return _sendOutput.Send(outputId, payload);
    }

    public DoraResult Send(string outputId, ArrowArray array, ArrowSchema schema)
    {
        return _sendOutput.Send(outputId, array, schema);
    }

    public DoraResult Send(string outputId, DoraOutputPayload payload)
    {
        return _sendOutput.Send(outputId, payload);
    }

    public void SendOrThrow(string outputId, byte[] data)
    {
        EnsureSuccess(Send(outputId, data), outputId, DoraOperatorErrorCode.OutputSendFailed);
    }

    public void SendOrThrow(string outputId, ReadOnlyMemory<byte> data)
    {
        EnsureSuccess(Send(outputId, data), outputId, DoraOperatorErrorCode.OutputSendFailed);
    }

    public void SendOrThrow(string outputId, string data)
    {
        EnsureSuccess(Send(outputId, data), outputId, DoraOperatorErrorCode.OutputSendFailed);
    }

    public void SendOrThrow(string outputId, RecordBatch recordBatch)
    {
        EnsureSuccess(Send(outputId, recordBatch), outputId, DoraOperatorErrorCode.RecordBatchOutputSendFailed);
    }

    public void SendOrThrow(string outputId, ArrowPayload payload)
    {
        EnsureSuccess(Send(outputId, payload), outputId, DoraOperatorErrorCode.ArrowOutputSendFailed);
    }

    public void SendOrThrow(string outputId, ArrowArray array, ArrowSchema schema)
    {
        EnsureSuccess(Send(outputId, array, schema), outputId, DoraOperatorErrorCode.ArrowOutputSendFailed);
    }

    public void SendOrThrow(string outputId, DoraOutputPayload payload)
    {
        var errorCode = payload.Kind switch
        {
            DoraOutputPayloadKind.RecordBatch => DoraOperatorErrorCode.RecordBatchOutputSendFailed,
            DoraOutputPayloadKind.ArrowPayload or DoraOutputPayloadKind.ArrowPair => DoraOperatorErrorCode.ArrowOutputSendFailed,
            _ => DoraOperatorErrorCode.OutputSendFailed,
        };
        EnsureSuccess(Send(outputId, payload), outputId, errorCode);
    }

    private void EnsureSuccess(DoraResult result, string outputId, DoraOperatorErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return;
        }

        throw _owner.CreateDiagnosticException(
            result.Error ?? $"Failed to send operator output '{outputId}'.",
            errorCode,
            operation: "SendOperatorOutput",
            detail: outputId);
    }
}
