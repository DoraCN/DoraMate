namespace DoraOperator;

/// <summary>
/// Identifies the high-level kind of operator event.
/// </summary>
public enum OperatorEventKind
{
    /// <summary>
    /// The event carries an input payload.
    /// </summary>
    Input,

    /// <summary>
    /// The event signals that an input channel was closed.
    /// </summary>
    InputClosed,

    /// <summary>
    /// The event requests the operator to stop.
    /// </summary>
    Stop,

    /// <summary>
    /// The event carries a runtime error.
    /// </summary>
    Error,

    /// <summary>
    /// The event could not be classified by this SDK version.
    /// </summary>
    Unknown,
}

/// <summary>
/// Base type for high-level operator events projected from <see cref="RawEvent"/>.
/// </summary>
public abstract class OperatorEvent
{
    /// <summary>
    /// Gets the projected event kind.
    /// </summary>
    public abstract OperatorEventKind Kind { get; }

    /// <summary>
    /// Gets the original low-level raw event.
    /// </summary>
    public RawEvent Raw { get; }

    internal static OperatorEvent FromRawEvent(RawEvent rawEvent)
    {
        ArgumentNullException.ThrowIfNull(rawEvent);

        if (rawEvent.Input is { } input)
        {
            return new InputEvent(rawEvent, input);
        }

        if (!string.IsNullOrEmpty(rawEvent.InputClosed))
        {
            return new InputClosedEvent(rawEvent, rawEvent.InputClosed);
        }

        if (rawEvent.Stop)
        {
            return new StopEvent(rawEvent);
        }

        if (!string.IsNullOrEmpty(rawEvent.Error))
        {
            return new ErrorEvent(rawEvent, rawEvent.Error);
        }

        return new UnknownEvent(rawEvent);
    }

    internal OperatorEvent(RawEvent raw)
    {
        Raw = raw;
    }
}

/// <summary>
/// Represents an operator input event.
/// </summary>
public sealed class InputEvent : OperatorEvent
{
    internal InputEvent(RawEvent raw, Input input)
        : base(raw)
    {
        Input = input;
    }

    /// <summary>
    /// Gets the input payload.
    /// </summary>
    public Input Input { get; }

    /// <inheritdoc />
    public override OperatorEventKind Kind => OperatorEventKind.Input;
}

/// <summary>
/// Represents an input-closed event.
/// </summary>
public sealed class InputClosedEvent : OperatorEvent
{
    internal InputClosedEvent(RawEvent raw, string inputId)
        : base(raw)
    {
        InputId = inputId;
    }

    /// <summary>
    /// Gets the ID of the input that was closed.
    /// </summary>
    public string InputId { get; }

    /// <inheritdoc />
    public override OperatorEventKind Kind => OperatorEventKind.InputClosed;
}

/// <summary>
/// Represents a stop event.
/// </summary>
public sealed class StopEvent : OperatorEvent
{
    internal StopEvent(RawEvent raw)
        : base(raw)
    {
    }

    /// <inheritdoc />
    public override OperatorEventKind Kind => OperatorEventKind.Stop;
}

/// <summary>
/// Represents an error event.
/// </summary>
public sealed class ErrorEvent : OperatorEvent
{
    internal ErrorEvent(RawEvent raw, string message)
        : base(raw)
    {
        Message = message;
    }

    /// <summary>
    /// Gets the runtime error message.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc />
    public override OperatorEventKind Kind => OperatorEventKind.Error;
}

/// <summary>
/// Represents an event that did not match any known high-level event shape.
/// </summary>
public sealed class UnknownEvent : OperatorEvent
{
    internal UnknownEvent(RawEvent raw)
        : base(raw)
    {
    }

    /// <inheritdoc />
    public override OperatorEventKind Kind => OperatorEventKind.Unknown;
}
