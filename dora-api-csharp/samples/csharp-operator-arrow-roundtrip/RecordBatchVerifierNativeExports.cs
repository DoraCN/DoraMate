using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DoraOperator;

namespace CSharpArrowRoundtrip;

internal static class RecordBatchVerifierNativeExports
{
    [UnmanagedCallersOnly(EntryPoint = "dora_init_operator", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeDoraInitResult DoraInitOperator()
    {
        return OperatorEntrypoint<RecordBatchVerifierOperator>.InitOperator();
    }

    [UnmanagedCallersOnly(EntryPoint = "dora_drop_operator", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeDoraResult DoraDropOperator(nint operatorContext)
    {
        return OperatorEntrypoint<RecordBatchVerifierOperator>.DropOperator(operatorContext);
    }

    [UnmanagedCallersOnly(EntryPoint = "dora_on_event", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeOnEventResult DoraOnEvent(
        nint eventPtr,
        nint sendOutputPtr,
        nint operatorContext)
    {
        return OperatorEntrypoint<RecordBatchVerifierOperator>.OnEvent(
            eventPtr,
            sendOutputPtr,
            operatorContext);
    }
}
