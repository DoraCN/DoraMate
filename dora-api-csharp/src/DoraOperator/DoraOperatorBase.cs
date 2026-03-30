using System.Collections.ObjectModel;
using System.Reflection;

namespace DoraOperator;

public abstract class DoraOperatorBase
{
    private static readonly IReadOnlyDictionary<string, string> EmptyEnvironmentVariables =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    private OperatorInitContext? _initContext;
    private bool? _hasLegacyRawEventOverride;

    internal protected virtual InitResult Init()
    {
        return InitResult.Ok();
    }

    internal protected virtual InitResult Init(OperatorInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Init();
    }

    protected OperatorInitContext? InitContext => _initContext;
    protected OperatorRuntimeConfig? RuntimeConfig => _initContext?.RuntimeConfig;
    protected string? OperatorId => _initContext?.OperatorId;
    protected string? NodeId => _initContext?.NodeId;
    protected string? DataflowId => _initContext?.DataflowId;
    protected IReadOnlyDictionary<string, string> EnvironmentVariables =>
        _initContext?.EnvironmentVariables ?? EmptyEnvironmentVariables;

    internal protected virtual OnEventResult OnEvent(RawEvent ev, SendOutput sendOutput)
    {
        throw new NotSupportedException(
            $"{GetType().Name} must override either OnEvent(RawEvent, SendOutput) or OnEvent(OperatorEvent, SendOutput).");
    }

    internal protected virtual OnEventResult OnEvent(OperatorEvent ev, SendOutput sendOutput)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(sendOutput);

        var output = new OperatorOutput(sendOutput, this);
        return ev switch
        {
            InputEvent inputEvent => OnInput(inputEvent, output),
            InputClosedEvent inputClosedEvent => OnInputClosed(inputClosedEvent, output),
            StopEvent stopEvent => OnStop(stopEvent, output),
            ErrorEvent errorEvent => OnError(errorEvent, output),
            _ => OnUnknown(ev, output),
        };
    }

    internal protected virtual OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Continue());
    }

    internal protected virtual OnEventResult OnInputClosed(InputClosedEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Continue());
    }

    internal protected virtual OnEventResult OnStop(StopEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Stop());
    }

    internal protected virtual OnEventResult OnError(ErrorEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, () => OnEventResult.Err(ev.Message));
    }

    internal protected virtual OnEventResult OnUnknown(OperatorEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Continue());
    }

    internal protected virtual void Drop(nint operatorContext)
    {
    }

    internal void SetInitContext(OperatorInitContext context)
    {
        _initContext = context ?? throw new ArgumentNullException(nameof(context));
    }

    protected DoraOperatorDiagnosticInfo CaptureDiagnostics(string operation, string? detail = null)
    {
        return DoraOperatorDiagnosticInfo.Capture(operation, detail, _initContext);
    }

    internal DoraOperatorException CreateDiagnosticException(
        string message,
        DoraOperatorErrorCode errorCode,
        string operation,
        string? detail = null,
        Exception? innerException = null)
    {
        return new DoraOperatorException(
            message,
            errorCode,
            operation,
            CaptureDiagnostics(operation, detail),
            innerException);
    }

    private OnEventResult ForwardLegacyRawEventOrDefault(
        RawEvent rawEvent,
        OperatorOutput output,
        Func<OnEventResult> defaultHandler)
    {
        if (HasLegacyRawEventOverride())
        {
            return OnEvent(rawEvent, output.Delegate);
        }

        return defaultHandler();
    }

    private bool HasLegacyRawEventOverride()
    {
        if (_hasLegacyRawEventOverride.HasValue)
        {
            return _hasLegacyRawEventOverride.Value;
        }

        var method = GetType().GetMethod(
            nameof(OnEvent),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(RawEvent), typeof(SendOutput) },
            modifiers: null);

        _hasLegacyRawEventOverride = method is not null && method.DeclaringType != typeof(DoraOperatorBase);
        return _hasLegacyRawEventOverride.Value;
    }
}
