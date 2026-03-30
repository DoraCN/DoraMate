namespace DoraOperator;

public enum OperatorEventKind
{
    Input,
    InputClosed,
    Stop,
    Error,
    Unknown,
}

public abstract class OperatorEvent
{
    public abstract OperatorEventKind Kind { get; }
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

public sealed class InputEvent : OperatorEvent
{
    internal InputEvent(RawEvent raw, Input input)
        : base(raw)
    {
        Input = input;
    }

    public Input Input { get; }
    public override OperatorEventKind Kind => OperatorEventKind.Input;
}

public sealed class InputClosedEvent : OperatorEvent
{
    internal InputClosedEvent(RawEvent raw, string inputId)
        : base(raw)
    {
        InputId = inputId;
    }

    public string InputId { get; }
    public override OperatorEventKind Kind => OperatorEventKind.InputClosed;
}

public sealed class StopEvent : OperatorEvent
{
    internal StopEvent(RawEvent raw)
        : base(raw)
    {
    }

    public override OperatorEventKind Kind => OperatorEventKind.Stop;
}

public sealed class ErrorEvent : OperatorEvent
{
    internal ErrorEvent(RawEvent raw, string message)
        : base(raw)
    {
        Message = message;
    }

    public string Message { get; }
    public override OperatorEventKind Kind => OperatorEventKind.Error;
}

public sealed class UnknownEvent : OperatorEvent
{
    internal UnknownEvent(RawEvent raw)
        : base(raw)
    {
    }

    public override OperatorEventKind Kind => OperatorEventKind.Unknown;
}
