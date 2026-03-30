using System.Threading;

namespace DoraOperator;

public static class OperatorEntrypoint<TOperator>
    where TOperator : DoraOperatorBase, new()
{
    private static int s_factoryRegistered;

    public static NativeTypes.NativeDoraInitResult InitOperator()
    {
        EnsureFactoryRegistered();
        return OperatorExports.DoraInitOperatorExport();
    }

    public static NativeTypes.NativeDoraResult DropOperator(nint operatorContext)
    {
        EnsureFactoryRegistered();
        return OperatorExports.DoraDropOperatorExport((IntPtr)operatorContext);
    }

    public static NativeTypes.NativeOnEventResult OnEvent(
        nint eventPtr,
        nint sendOutputPtr,
        nint operatorContext)
    {
        EnsureFactoryRegistered();
        return OperatorExports.DoraOnEventExport(
            (IntPtr)eventPtr,
            (IntPtr)sendOutputPtr,
            (IntPtr)operatorContext);
    }

    private static void EnsureFactoryRegistered()
    {
        if (Interlocked.Exchange(ref s_factoryRegistered, 1) != 0)
        {
            return;
        }

        DoraOperatorRegistration.RegisterFactory(new DefaultOperatorFactory<TOperator>());
    }

    private sealed class DefaultOperatorFactory<TConcreteOperator> : OperatorFactory
        where TConcreteOperator : DoraOperatorBase, new()
    {
        public override DoraOperatorBase CreateOperator()
        {
            return new TConcreteOperator();
        }
    }
}
