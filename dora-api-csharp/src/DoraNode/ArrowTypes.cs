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

    /// <summary>
    /// Gets the logical length of the Arrow array.
    /// </summary>
    public long Length => ReadNative().Length;
    /// <summary>
    /// Gets the number of null values in the Arrow array.
    /// </summary>
    public long NullCount => ReadNative().NullCount;
    /// <summary>
    /// Gets the logical offset of the Arrow array.
    /// </summary>
    public long Offset => ReadNative().Offset;
    /// <summary>
    /// Gets the number of buffers described by the Arrow array.
    /// </summary>
    public long BufferCount => ReadNative().NBuffers;
    /// <summary>
    /// Gets the number of child arrays referenced by the Arrow array.
    /// </summary>
    public long ChildCount => ReadNative().NChildren;
    /// <summary>
    /// Gets the native pointer to the Arrow buffer pointer array.
    /// </summary>
    public nint BuffersPointer => (nint)ReadNative().Buffers;
    /// <summary>
    /// Gets the native pointer to the Arrow child array pointer array.
    /// </summary>
    public nint ChildrenPointer => (nint)ReadNative().Children;
    /// <summary>
    /// Gets the native pointer to the Arrow dictionary array, when present.
    /// </summary>
    public nint DictionaryPointer => (nint)ReadNative().Dictionary;
    /// <summary>
    /// Gets the native release callback pointer for the Arrow array.
    /// </summary>
    public nint ReleasePointer => (nint)ReadNative().Release;
    /// <summary>
    /// Gets the native private-data pointer associated with the Arrow array.
    /// </summary>
    public nint PrivateDataPointer => (nint)ReadNative().PrivateData;
    /// <summary>
    /// Gets a value indicating whether the native Arrow array exposes a release callback.
    /// </summary>
    public bool HasReleaseCallback => ReadNative().HasRelease;

    internal nint DetachHandle()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        GC.SuppressFinalize(this);
        return handle;
    }

    /// <summary>
    /// Releases the native Arrow array handle.
    /// </summary>
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

    /// <summary>
    /// Gets the Arrow C data-interface format string.
    /// </summary>
    public string? Format => PtrToUtf8(ReadNative().Format);
    /// <summary>
    /// Gets the logical field name associated with the schema, when present.
    /// </summary>
    public string? Name => PtrToUtf8(ReadNative().Name);
    /// <summary>
    /// Gets the native pointer to the schema metadata buffer.
    /// </summary>
    public nint MetadataPointer => (nint)ReadNative().Metadata;
    /// <summary>
    /// Gets the schema flags defined by the Arrow C data interface.
    /// </summary>
    public long Flags => ReadNative().Flags;
    /// <summary>
    /// Gets the number of child schemas referenced by this schema.
    /// </summary>
    public long ChildCount => ReadNative().NChildren;
    /// <summary>
    /// Gets the native pointer to the child schema pointer array.
    /// </summary>
    public nint ChildrenPointer => (nint)ReadNative().Children;
    /// <summary>
    /// Gets the native pointer to the dictionary schema, when present.
    /// </summary>
    public nint DictionaryPointer => (nint)ReadNative().Dictionary;
    /// <summary>
    /// Gets the native release callback pointer for the schema.
    /// </summary>
    public nint ReleasePointer => (nint)ReadNative().Release;
    /// <summary>
    /// Gets the native private-data pointer associated with the schema.
    /// </summary>
    public nint PrivateDataPointer => (nint)ReadNative().PrivateData;
    /// <summary>
    /// Gets a value indicating whether the native schema exposes a release callback.
    /// </summary>
    public bool HasReleaseCallback => ReadNative().HasRelease;

    internal nint DetachHandle()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        GC.SuppressFinalize(this);
        return handle;
    }

    /// <summary>
    /// Releases the native Arrow schema handle.
    /// </summary>
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
    /// <summary>
    /// Creates a managed Arrow payload wrapper from an array/schema pair.
    /// </summary>
    /// <param name="array">The managed Arrow array handle.</param>
    /// <param name="schema">The managed Arrow schema handle.</param>
    public ArrowPayload(ArrowArray array, ArrowSchema schema)
    {
        Array = array ?? throw new ArgumentNullException(nameof(array));
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    /// <summary>
    /// Gets the managed Arrow array handle.
    /// </summary>
    public ArrowArray Array { get; }

    /// <summary>
    /// Gets the managed Arrow schema handle.
    /// </summary>
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

    /// <summary>
    /// Releases both the array and schema handles owned by this payload.
    /// </summary>
    public void Dispose()
    {
        Array.Dispose();
        Schema.Dispose();
    }
}
