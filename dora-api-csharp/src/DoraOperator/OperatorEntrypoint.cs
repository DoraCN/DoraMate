using System.Threading;

namespace DoraOperator;

/// <summary>
/// Exposes native-callable entrypoints for a managed Dora operator type.
/// </summary>
/// <typeparam name="TOperator">The managed operator type to instantiate for callbacks.</typeparam>
public static class OperatorEntrypoint<TOperator>
    where TOperator : DoraOperatorBase, new()
{
    private static int s_factoryRegistered;

    /// <summary>
    /// Initializes the managed operator entrypoint and forwards to the registered export.
    /// </summary>
    /// <returns>The native initialization result returned to the Dora runtime.</returns>
    public static NativeTypes.NativeDoraInitResult InitOperator()
    {
        EnsureFactoryRegistered();
        return OperatorExports.DoraInitOperatorExport();
    }

    /// <summary>
    /// Drops the managed operator instance associated with the supplied context handle.
    /// </summary>
    /// <param name="operatorContext">The native operator context handle.</param>
    /// <returns>The native drop result returned to the Dora runtime.</returns>
    public static NativeTypes.NativeDoraResult DropOperator(nint operatorContext)
    {
        EnsureFactoryRegistered();
        return OperatorExports.DoraDropOperatorExport((IntPtr)operatorContext);
    }

    /// <summary>
    /// Dispatches a native operator event into the managed operator implementation.
    /// </summary>
    /// <param name="eventPtr">The native raw-event pointer.</param>
    /// <param name="sendOutputPtr">The native send-output delegate pointer.</param>
    /// <param name="operatorContext">The native operator context handle.</param>
    /// <returns>The native on-event result returned to the Dora runtime.</returns>
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
