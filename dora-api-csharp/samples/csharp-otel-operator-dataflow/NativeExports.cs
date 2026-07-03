using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DoraOperator;

namespace OtelOperator;

internal static class NativeExports
{
    [UnmanagedCallersOnly(EntryPoint = "dora_init_operator", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeDoraInitResult DoraInitOperator()
    {
        return OperatorEntrypoint<TraceOperator>.InitOperator();
    }

    [UnmanagedCallersOnly(EntryPoint = "dora_drop_operator", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeDoraResult DoraDropOperator(nint operatorContext)
    {
        return OperatorEntrypoint<TraceOperator>.DropOperator(operatorContext);
    }

    [UnmanagedCallersOnly(EntryPoint = "dora_on_event", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeOnEventResult DoraOnEvent(
        nint eventPtr,
        nint sendOutputPtr,
        nint operatorContext)
    {
        return OperatorEntrypoint<TraceOperator>.OnEvent(
            eventPtr,
            sendOutputPtr,
            operatorContext);
    }
}
