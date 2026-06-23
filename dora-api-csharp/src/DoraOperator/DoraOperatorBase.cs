using System.Collections.ObjectModel;
using System.Reflection;

namespace DoraOperator;

/// <summary>
/// Base class for managed Dora operators that participate in the operator ABI lifecycle.
/// </summary>
public abstract class DoraOperatorBase
{
    private static readonly IReadOnlyDictionary<string, string> EmptyEnvironmentVariables =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    private OperatorInitContext? _initContext;
    private bool? _hasLegacyRawEventOverride;

    /// <summary>
    /// Initializes the operator before event processing begins.
    /// </summary>
    /// <returns>The initialization result returned to the Dora runtime.</returns>
    internal protected virtual InitResult Init()
    {
        return InitResult.Ok();
    }

    /// <summary>
    /// Initializes the operator with resolved runtime context information.
    /// </summary>
    /// <param name="context">The operator initialization context created from the runtime environment.</param>
    /// <returns>The initialization result returned to the Dora runtime.</returns>
    internal protected virtual InitResult Init(OperatorInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Init();
    }

    /// <summary>
    /// Gets the initialization context captured for the current operator instance.
    /// </summary>
    protected OperatorInitContext? InitContext => _initContext;
    /// <summary>
    /// Gets the runtime configuration resolved for the current operator instance.
    /// </summary>
    protected OperatorRuntimeConfig? RuntimeConfig => _initContext?.RuntimeConfig;
    /// <summary>
    /// Gets the operator identifier assigned by the Dora runtime.
    /// </summary>
    protected string? OperatorId => _initContext?.OperatorId;
    /// <summary>
    /// Gets the hosting node identifier assigned by the Dora runtime.
    /// </summary>
    protected string? NodeId => _initContext?.NodeId;
    /// <summary>
    /// Gets the dataflow identifier assigned by the Dora runtime.
    /// </summary>
    protected string? DataflowId => _initContext?.DataflowId;
    /// <summary>
    /// Gets the environment variables exposed to the operator at startup.
    /// </summary>
    protected IReadOnlyDictionary<string, string> EnvironmentVariables =>
        _initContext?.EnvironmentVariables ?? EmptyEnvironmentVariables;

    /// <summary>
    /// Handles a raw ABI event using the legacy event surface.
    /// </summary>
    /// <param name="ev">The raw event received from the runtime.</param>
    /// <param name="sendOutput">The low-level delegate used to emit output.</param>
    /// <returns>The event handling result returned to the Dora runtime.</returns>
    internal protected virtual OnEventResult OnEvent(RawEvent ev, SendOutput sendOutput)
    {
        throw new NotSupportedException(
            $"{GetType().Name} must override either OnEvent(RawEvent, SendOutput) or OnEvent(OperatorEvent, SendOutput).");
    }

    /// <summary>
    /// Handles a typed operator event using the modern event surface.
    /// </summary>
    /// <param name="ev">The typed operator event received from the runtime.</param>
    /// <param name="sendOutput">The low-level delegate used to emit output.</param>
    /// <returns>The event handling result returned to the Dora runtime.</returns>
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

    /// <summary>
    /// Handles an input event delivered to the operator.
    /// </summary>
    /// <param name="ev">The typed input event.</param>
    /// <param name="output">The high-level output helper bound to the current operator.</param>
    /// <returns>The event handling result returned to the Dora runtime.</returns>
    internal protected virtual OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Continue());
    }

    /// <summary>
    /// Handles a notification that an input stream has closed.
    /// </summary>
    /// <param name="ev">The typed input-closed event.</param>
    /// <param name="output">The high-level output helper bound to the current operator.</param>
    /// <returns>The event handling result returned to the Dora runtime.</returns>
    internal protected virtual OnEventResult OnInputClosed(InputClosedEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Continue());
    }

    /// <summary>
    /// Handles a stop request delivered by the runtime.
    /// </summary>
    /// <param name="ev">The typed stop event.</param>
    /// <param name="output">The high-level output helper bound to the current operator.</param>
    /// <returns>The event handling result returned to the Dora runtime.</returns>
    internal protected virtual OnEventResult OnStop(StopEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Stop());
    }

    /// <summary>
    /// Handles an error event surfaced by the runtime.
    /// </summary>
    /// <param name="ev">The typed error event.</param>
    /// <param name="output">The high-level output helper bound to the current operator.</param>
    /// <returns>The event handling result returned to the Dora runtime.</returns>
    internal protected virtual OnEventResult OnError(ErrorEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, () => OnEventResult.Err(ev.Message));
    }

    /// <summary>
    /// Handles an event type that does not have a dedicated typed override.
    /// </summary>
    /// <param name="ev">The typed operator event.</param>
    /// <param name="output">The high-level output helper bound to the current operator.</param>
    /// <returns>The event handling result returned to the Dora runtime.</returns>
    internal protected virtual OnEventResult OnUnknown(OperatorEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);
        return ForwardLegacyRawEventOrDefault(ev.Raw, output, static () => OnEventResult.Continue());
    }

    /// <summary>
    /// Releases operator-owned state before the runtime drops the operator instance.
    /// </summary>
    /// <param name="operatorContext">The operator context handle returned from <see cref="Init()"/>.</param>
    internal protected virtual void Drop(nint operatorContext)
    {
    }

    internal void SetInitContext(OperatorInitContext context)
    {
        _initContext = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Captures a structured diagnostics snapshot using the current operator context.
    /// </summary>
    /// <param name="operation">The logical operation being recorded.</param>
    /// <param name="detail">Optional contextual detail for the operation.</param>
    /// <returns>A diagnostics snapshot enriched with runtime metadata.</returns>
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
