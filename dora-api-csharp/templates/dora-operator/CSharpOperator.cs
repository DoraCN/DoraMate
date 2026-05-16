using DoraOperator;
using System.Text;

namespace MyOperator;

public sealed class MyOperator : DoraOperatorBase
{
    private int _counter;

    protected override InitResult Init()
    {
        Console.WriteLine("MyOperator initialized");
        return InitResult.Ok();
    }

    protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);

        _counter += 1;
        var message = ev.Input.GetUtf8String();
        var response = Encoding.UTF8.GetBytes($"C# operator processed #{_counter}: {message}");

        output.SendOrThrow("output", response);
        Console.WriteLine($"Processed event #{_counter}");

        return OnEventResult.Stop();
    }

    protected override OnEventResult OnInputClosed(InputClosedEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        Console.WriteLine($"Input closed: {ev.InputId}");
        return OnEventResult.Stop();
    }

    protected override OnEventResult OnError(ErrorEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        Console.Error.WriteLine($"Operator error: {ev.Message}");
        return OnEventResult.Err(ev.Message);
    }
}