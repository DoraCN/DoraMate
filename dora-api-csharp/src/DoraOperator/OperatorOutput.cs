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

    /// <summary>
    /// Sends a raw byte payload to the specified operator output.
    /// </summary>
    public DoraResult Send(string outputId, byte[] data)
    {
        data ??= System.Array.Empty<byte>();
        return _sendOutput(outputId, data);
    }

    /// <summary>
    /// Sends a raw byte payload with an explicitly selected activity context.
    /// </summary>
    public DoraResult Send(string outputId, byte[] data, System.Diagnostics.ActivityContext? context)
    {
        data ??= System.Array.Empty<byte>();
        return _sendOutput.Send(outputId, data, context);
    }

    /// <summary>
    /// Sends a raw byte payload and injects the current activity context when present.
    /// </summary>
    public DoraResult SendWithCurrentActivity(string outputId, byte[] data)
    {
        data ??= System.Array.Empty<byte>();
        return _sendOutput.SendWithCurrentActivity(outputId, data);
    }

    /// <summary>
    /// Sends a raw byte payload from a memory buffer to the specified operator output.
    /// </summary>
    public DoraResult Send(string outputId, ReadOnlyMemory<byte> data)
    {
        return _sendOutput.Send(outputId, data);
    }

    /// <summary>
    /// Sends a UTF-8 string payload to the specified operator output.
    /// </summary>
    public DoraResult Send(string outputId, string data)
    {
        return _sendOutput.Send(outputId, data);
    }

    /// <summary>
    /// Sends a UTF-8 string payload with an explicitly selected activity context.
    /// </summary>
    public DoraResult Send(string outputId, string data, System.Diagnostics.ActivityContext? context)
    {
        return _sendOutput.Send(outputId, data, context);
    }

    /// <summary>
    /// Sends an Apache Arrow <see cref="RecordBatch"/> to the specified operator output.
    /// </summary>
    public DoraResult Send(string outputId, RecordBatch recordBatch)
    {
        return _sendOutput.Send(outputId, recordBatch);
    }

    /// <summary>
    /// Sends an Arrow payload to the specified operator output.
    /// </summary>
    public DoraResult Send(string outputId, ArrowPayload payload)
    {
        return _sendOutput.Send(outputId, payload);
    }

    /// <summary>
    /// Sends an Arrow array/schema pair to the specified operator output.
    /// </summary>
    public DoraResult Send(string outputId, ArrowArray array, ArrowSchema schema)
    {
        return _sendOutput.Send(outputId, array, schema);
    }

    /// <summary>
    /// Sends a unified payload wrapper to the specified operator output.
    /// </summary>
    public DoraResult Send(string outputId, DoraOutputPayload payload)
    {
        return _sendOutput.Send(outputId, payload);
    }

    /// <summary>
    /// Sends a byte payload and throws a diagnostic exception when the send fails.
    /// </summary>
    public void SendOrThrow(string outputId, byte[] data)
    {
        EnsureSuccess(Send(outputId, data), outputId, DoraOperatorErrorCode.OutputSendFailed);
    }

    /// <summary>
    /// Sends a byte payload from a memory buffer and throws when the send fails.
    /// </summary>
    public void SendOrThrow(string outputId, ReadOnlyMemory<byte> data)
    {
        EnsureSuccess(Send(outputId, data), outputId, DoraOperatorErrorCode.OutputSendFailed);
    }

    /// <summary>
    /// Sends a UTF-8 string payload and throws when the send fails.
    /// </summary>
    public void SendOrThrow(string outputId, string data)
    {
        EnsureSuccess(Send(outputId, data), outputId, DoraOperatorErrorCode.OutputSendFailed);
    }

    /// <summary>
    /// Sends a record batch and throws when the send fails.
    /// </summary>
    public void SendOrThrow(string outputId, RecordBatch recordBatch)
    {
        EnsureSuccess(Send(outputId, recordBatch), outputId, DoraOperatorErrorCode.RecordBatchOutputSendFailed);
    }

    /// <summary>
    /// Sends an Arrow payload and throws when the send fails.
    /// </summary>
    public void SendOrThrow(string outputId, ArrowPayload payload)
    {
        EnsureSuccess(Send(outputId, payload), outputId, DoraOperatorErrorCode.ArrowOutputSendFailed);
    }

    /// <summary>
    /// Sends an Arrow array/schema pair and throws when the send fails.
    /// </summary>
    public void SendOrThrow(string outputId, ArrowArray array, ArrowSchema schema)
    {
        EnsureSuccess(Send(outputId, array, schema), outputId, DoraOperatorErrorCode.ArrowOutputSendFailed);
    }

    /// <summary>
    /// Sends a unified payload wrapper and throws when the send fails.
    /// </summary>
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
