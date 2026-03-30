using System.Text;
using System.Reflection;
using DoraOperator;

namespace CSharpCounterOperator;

public sealed class CounterOperator : DoraOperatorBase
{
    private int _counter;
    private Input? _savedInput;

    protected override InitResult Init()
    {
        return InitResult.Ok();
    }

    protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);

        var mode = GetTestMode();
        if (mode == CounterOperatorTestMode.InvalidNativeHandle)
        {
            return HandleExpectedInvalidNativeHandle(output);
        }

        if (mode == CounterOperatorTestMode.LifecycleViolation)
        {
            _savedInput = ev.Input;
            return OnEventResult.Continue();
        }

        _counter += 1;
        var message = ev.Input.GetUtf8String();
        output.SendOrThrow("counter", Encoding.UTF8.GetBytes($"C# operator processed #{_counter}: {message}"));
        return OnEventResult.Stop();
    }

    protected override OnEventResult OnInputClosed(InputClosedEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);

        if (GetTestMode() != CounterOperatorTestMode.LifecycleViolation)
        {
            return OnEventResult.Stop();
        }

        if (_savedInput is null)
        {
            return OnEventResult.Err("Expected a saved input for lifecycle violation test but found none.");
        }

        try
        {
            _ = _savedInput.GetUtf8String();
            return OnEventResult.Err("Expected lifecycle violation when reading saved input after OnEvent.");
        }
        catch (DoraOperatorException ex) when (ex.ErrorCode == DoraOperatorErrorCode.LifecycleViolation)
        {
            output.SendOrThrow("counter", $"EXPECTED_LIFECYCLE_VIOLATION_OK code={ex.ErrorCode}");
            return OnEventResult.Stop();
        }
    }

    protected override OnEventResult OnError(ErrorEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        return OnEventResult.Err(ev.Message);
    }

    private static CounterOperatorTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_OPERATOR_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => CounterOperatorTestMode.Normal,
            "lifecycle-violation" => CounterOperatorTestMode.LifecycleViolation,
            "invalid-native-handle" => CounterOperatorTestMode.InvalidNativeHandle,
            _ => throw new InvalidOperationException($"Unsupported operator test mode '{rawMode}'.")
        };
    }

    private static OnEventResult HandleExpectedInvalidNativeHandle(OperatorOutput output)
    {
        try
        {
            var invalidInput = CreateInvalidNativeHandleInput();
            _ = invalidInput.GetUtf8String();
            return OnEventResult.Err("Expected invalid native handle access to throw.");
        }
        catch (DoraOperatorException ex) when (ex.ErrorCode == DoraOperatorErrorCode.InvalidNativeHandle)
        {
            output.SendOrThrow("counter", $"EXPECTED_INVALID_NATIVE_HANDLE_OK code={ex.ErrorCode}");
            return OnEventResult.Stop();
        }
    }

    private static Input CreateInvalidNativeHandleInput()
    {
        var constructor = typeof(Input).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string), typeof(string), typeof(nint)],
            modifiers: null);

        if (constructor is null)
        {
            throw new InvalidOperationException("Failed to resolve DoraOperator.Input internal constructor.");
        }

        return (Input)constructor.Invoke(["invalid-handle", null, (nint)0]);
    }

    private enum CounterOperatorTestMode
    {
        Normal,
        LifecycleViolation,
        InvalidNativeHandle
    }
}
