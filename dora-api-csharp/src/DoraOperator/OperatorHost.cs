namespace DoraOperator;

internal sealed class OperatorHost
{
    private readonly DoraOperatorBase _operator;
    private nint _operatorContext;
    private OperatorInitContext? _initContext;

    public OperatorHost(DoraOperatorBase op)
    {
        _operator = op;
    }

    public InitResult Init()
    {
        try
        {
            _initContext = OperatorInitContext.CreateFromEnvironment();
            _operator.SetInitContext(_initContext);
            var result = _operator.Init(_initContext);
            if (result.IsSuccess)
            {
                _operatorContext = result.OperatorContext;
                return InitResult.Ok(OperatorContextHandle.Create(this));
            }

            DoraOperatorRuntimeErrors.LogFailure(
                "init",
                "Init",
                result.Error ?? "Operator initialization failed.",
                _initContext);

            return InitResult.Err(
                result.Error
                ?? DoraOperatorRuntimeErrors.FormatMessage(
                    DoraOperatorErrorCode.InitializationFailed,
                    "Operator initialization failed."));
        }
        catch (Exception ex)
        {
            DoraOperatorRuntimeErrors.LogException("init", ex);
            return InitResult.Err(ex);
        }
    }

    public OnEventResult OnEvent(RawEvent ev, SendOutput sendOutput)
    {
        try
        {
            var managedEvent = OperatorEvent.FromRawEvent(ev);
            var result = _operator.OnEvent(managedEvent, sendOutput);
            if (!result.IsSuccess)
            {
                DoraOperatorRuntimeErrors.LogFailure(
                    "on_event",
                    GetOperationName(managedEvent),
                    result.Error ?? "Operator event handling failed.",
                    _initContext,
                    GetEventDetail(managedEvent));
            }

            return result;
        }
        catch (Exception ex)
        {
            DoraOperatorRuntimeErrors.LogException("on_event", ex);
            return OnEventResult.Err(ex);
        }
        finally
        {
            ev.InvalidateNativeAccess();
        }
    }

    public void Drop()
    {
        try
        {
            _operator.Drop(_operatorContext);
        }
        catch (Exception ex)
        {
            DoraOperatorRuntimeErrors.LogException("drop", ex);
        }
    }

    private static string GetOperationName(OperatorEvent ev)
    {
        return ev.Kind switch
        {
            OperatorEventKind.Input => "OnInput",
            OperatorEventKind.InputClosed => "OnInputClosed",
            OperatorEventKind.Stop => "OnStop",
            OperatorEventKind.Error => "OnError",
            _ => "OnUnknown"
        };
    }

    private static string? GetEventDetail(OperatorEvent ev)
    {
        return ev switch
        {
            InputEvent inputEvent => inputEvent.Input.Id,
            InputClosedEvent inputClosedEvent => inputClosedEvent.InputId,
            _ => null
        };
    }
}

public abstract class OperatorFactory
{
    public abstract DoraOperatorBase CreateOperator();
}
