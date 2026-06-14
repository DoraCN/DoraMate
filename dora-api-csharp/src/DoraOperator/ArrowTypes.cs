using System.Runtime.InteropServices;
using System.Threading;

namespace DoraOperator;

/// <summary>
/// Managed owner for a native ArrowArray allocated by the Dora operator ABI.
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
    /// Gets the number of native buffers described by the array.
    /// </summary>
    public long BufferCount => ReadNative().NBuffers;
    /// <summary>
    /// Gets the number of child arrays referenced by the array.
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
    /// Gets the native pointer to the dictionary array, when present.
    /// </summary>
    public nint DictionaryPointer => (nint)ReadNative().Dictionary;
    /// <summary>
    /// Gets the native release callback pointer for the array.
    /// </summary>
    public nint ReleasePointer => (nint)ReadNative().Release;
    /// <summary>
    /// Gets the native private-data pointer associated with the array.
    /// </summary>
    public nint PrivateDataPointer => (nint)ReadNative().PrivateData;
    /// <summary>
    /// Gets a value indicating whether the array exposes a native release callback.
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
/// Managed owner for a native ArrowSchema allocated by the Dora operator ABI.
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
    /// Gets the schema field name, when present.
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
    /// Gets a value indicating whether the schema exposes a native release callback.
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
/// Managed owner for an Arrow array/schema pair transferred through the Dora operator ABI.
/// </summary>
public sealed class ArrowPayload : IDisposable
{
    /// <summary>
    /// Creates a managed Arrow payload wrapper from an array/schema pair.
    /// </summary>
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

public static class SendOutputExtensions
{
    /// <summary>
    /// Sends a UTF-8 string payload through the low-level send-output delegate.
    /// </summary>
    public static DoraResult Send(this SendOutput sendOutput, string outputId, string data)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        return sendOutput(outputId, System.Text.Encoding.UTF8.GetBytes(data));
    }

    /// <summary>
    /// Sends a byte payload from a memory buffer through the low-level send-output delegate.
    /// </summary>
    public static DoraResult Send(this SendOutput sendOutput, string outputId, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        return sendOutput(outputId, data.ToArray());
    }

    /// <summary>
    /// Serializes and sends a record batch through the low-level send-output delegate.
    /// </summary>
    public static DoraResult Send(this SendOutput sendOutput, string outputId, Apache.Arrow.RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        ArgumentNullException.ThrowIfNull(recordBatch);
        return sendOutput.SendRecordBatch(outputId, recordBatch);
    }

    /// <summary>
    /// Sends an Arrow payload through the low-level send-output delegate.
    /// </summary>
    public static DoraResult Send(this SendOutput sendOutput, string outputId, ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        ArgumentNullException.ThrowIfNull(payload);
        return sendOutput.SendArrow(outputId, payload);
    }

    /// <summary>
    /// Sends an Arrow array/schema pair through the low-level send-output delegate.
    /// </summary>
    public static DoraResult Send(this SendOutput sendOutput, string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        return sendOutput.SendArrow(outputId, array, schema);
    }

    /// <summary>
    /// Sends a unified payload wrapper through the low-level send-output delegate.
    /// </summary>
    public static DoraResult Send(this SendOutput sendOutput, string outputId, DoraOutputPayload payload)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        ArgumentNullException.ThrowIfNull(payload);

        return payload.Kind switch
        {
            DoraOutputPayloadKind.Bytes => sendOutput(outputId, payload.Bytes!),
            DoraOutputPayloadKind.Text => sendOutput.Send(outputId, payload.Text!),
            DoraOutputPayloadKind.ArrowPayload => sendOutput.Send(outputId, payload.ArrowPayload!),
            DoraOutputPayloadKind.ArrowPair => sendOutput.Send(outputId, payload.ArrowArray!, payload.ArrowSchema!),
            DoraOutputPayloadKind.RecordBatch => sendOutput.Send(outputId, payload.RecordBatch!),
            _ => DoraResult.Err($"Unsupported output payload kind '{payload.Kind}'.")
        };
    }

    /// <summary>
    /// Sends an Arrow payload through the low-level send-output delegate.
    /// </summary>
    public static DoraResult SendArrow(this SendOutput sendOutput, string outputId, ArrowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        ArgumentNullException.ThrowIfNull(payload);
        return SendOutputBridge.SendArrow(sendOutput, outputId, payload);
    }

    /// <summary>
    /// Sends an Arrow array/schema pair through the low-level send-output delegate.
    /// </summary>
    public static DoraResult SendArrow(this SendOutput sendOutput, string outputId, ArrowArray array, ArrowSchema schema)
    {
        ArgumentNullException.ThrowIfNull(sendOutput);
        ArgumentNullException.ThrowIfNull(array);
        ArgumentNullException.ThrowIfNull(schema);
        return SendOutputBridge.SendArrow(sendOutput, outputId, array, schema);
    }
}
