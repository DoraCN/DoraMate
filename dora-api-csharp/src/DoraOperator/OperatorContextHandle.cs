using System.Runtime.InteropServices;

namespace DoraOperator;

internal static class OperatorContextHandle
{
    public static nint Create(OperatorHost host)
    {
        var handle = GCHandle.Alloc(host, GCHandleType.Normal);
        return GCHandle.ToIntPtr(handle);
    }

    public static bool TryGetHost(nint context, out OperatorHost? host)
    {
        host = null;
        if (context == 0)
        {
            return false;
        }

        try
        {
            var handle = GCHandle.FromIntPtr((IntPtr)context);
            host = handle.Target as OperatorHost;
            return host is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static OperatorHost GetHost(nint context)
    {
        if (!TryGetHost(context, out var host))
        {
            throw DoraOperatorException.Create(
                "Invalid operator_context handle.",
                DoraOperatorErrorCode.InvalidOperatorContext,
                operation: "ResolveOperatorContext",
                initContext: null);
        }

        return host!;
    }

    public static void Free(nint context)
    {
        if (context == 0)
        {
            return;
        }

        try
        {
            var handle = GCHandle.FromIntPtr((IntPtr)context);
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
