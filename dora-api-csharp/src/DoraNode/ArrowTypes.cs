using System.Runtime.InteropServices;
using System.Threading;

namespace DoraNode;

/// <summary>
/// Managed owner for a native ArrowArray allocated by the Dora node ABI.
/// </summary>
public sealed class ArrowArray : IDisposable
{
    private nint _handle;

    internal ArrowArray(nint handle)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(handle, 0);
        _handle = handle;
    }

    public long Length => ReadNative().Length;
    public long NullCount => ReadNative().NullCount;
    public long Offset => ReadNative().Offset;
    public long BufferCount => ReadNative().NBuffers;
    public long ChildCount => ReadNative().NChildren;
    public nint BuffersPointer => (nint)ReadNative().Buffers;
    public nint ChildrenPointer => (nint)ReadNative().Children;
    public nint DictionaryPointer => (nint)ReadNative().Dictionary;
    public nint ReleasePointer => (nint)ReadNative().Release;
    public nint PrivateDataPointer => (nint)ReadNative().PrivateData;
    public bool HasReleaseCallback => ReadNative().HasRelease;

    internal nint DetachHandle()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        GC.SuppressFinalize(this);
        return handle;
    }

    public void Dispose()
    {
        ReleaseHandle();
        GC.SuppressFinalize(this);
    }

    ~ArrowArray()
    {
        ReleaseHandle();
    }

    private NativeTypes.NativeArrowArray ReadNative()
    {
        return Marshal.PtrToStructure<NativeTypes.NativeArrowArray>((IntPtr)EnsureHandle());
    }

    private nint EnsureHandle()
    {
        var handle = Volatile.Read(ref _handle);
        if (handle == 0)
        {
            throw new ObjectDisposedException(nameof(ArrowArray));
        }

        return handle;
    }

    private void ReleaseHandle()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle == 0)
        {
            return;
        }

        try
        {
            NativeMethods.EnsureLoaded();
            NativeMethods.FreeArrowArray((IntPtr)handle);
        }
        catch
        {
        }
    }
}

/// <summary>
/// Managed owner for a native ArrowSchema allocated by the Dora node ABI.
/// </summary>
public sealed class ArrowSchema : IDisposable
{
    private nint _handle;

    internal ArrowSchema(nint handle)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(handle, 0);
        _handle = handle;
    }

    public string? Format => PtrToUtf8(ReadNative().Format);
    public string? Name => PtrToUtf8(ReadNative().Name);
    public nint MetadataPointer => (nint)ReadNative().Metadata;
    public long Flags => ReadNative().Flags;
    public long ChildCount => ReadNative().NChildren;
    public nint ChildrenPointer => (nint)ReadNative().Children;
    public nint DictionaryPointer => (nint)ReadNative().Dictionary;
    public nint ReleasePointer => (nint)ReadNative().Release;
    public nint PrivateDataPointer => (nint)ReadNative().PrivateData;
    public bool HasReleaseCallback => ReadNative().HasRelease;

    internal nint DetachHandle()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        GC.SuppressFinalize(this);
        return handle;
    }

    public void Dispose()
    {
        ReleaseHandle();
        GC.SuppressFinalize(this);
    }

    ~ArrowSchema()
    {
        ReleaseHandle();
    }

    private NativeTypes.NativeArrowSchema ReadNative()
    {
        return Marshal.PtrToStructure<NativeTypes.NativeArrowSchema>((IntPtr)EnsureHandle());
    }

    private nint EnsureHandle()
    {
        var handle = Volatile.Read(ref _handle);
        if (handle == 0)
        {
            throw new ObjectDisposedException(nameof(ArrowSchema));
        }

        return handle;
    }

    private void ReleaseHandle()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle == 0)
        {
            return;
        }

        try
        {
            NativeMethods.EnsureLoaded();
            NativeMethods.FreeArrowSchema((IntPtr)handle);
        }
        catch
        {
        }
    }

    private static string? PtrToUtf8(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}

/// <summary>
/// Managed owner for an Arrow array/schema pair transferred through the Dora node ABI.
/// </summary>
public sealed class ArrowPayload : IDisposable
{
    public ArrowPayload(ArrowArray array, ArrowSchema schema)
    {
        Array = array ?? throw new ArgumentNullException(nameof(array));
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public ArrowArray Array { get; }
    public ArrowSchema Schema { get; }

    internal static ArrowPayload? FromNative(NativeTypes.NativeArrowPayload nativePayload)
    {
        if (!nativePayload.HasAnyHandle)
        {
            return null;
        }

        if (!nativePayload.IsComplete)
        {
            NativeMethods.EnsureLoaded();
            NativeMethods.FreeArrowPayload(nativePayload);
            throw new InvalidOperationException("Native Arrow payload was incomplete.");
        }

        return new ArrowPayload(
            new ArrowArray((nint)nativePayload.Array),
            new ArrowSchema((nint)nativePayload.Schema));
    }

    internal (nint ArrayHandle, nint SchemaHandle) DetachHandles()
    {
        return (Array.DetachHandle(), Schema.DetachHandle());
    }

    public void Dispose()
    {
        Array.Dispose();
        Schema.Dispose();
    }
}
