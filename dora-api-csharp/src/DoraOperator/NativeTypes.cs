using System.Runtime.InteropServices;
using System.Text;

namespace DoraOperator;

/// <summary>
/// Native ABI types for bridging the C operator API into the managed DoraOperator API.
/// </summary>
public static class NativeTypes
{
    /// <summary>
    /// Native vector of bytes used by the Dora operator ABI.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeVecU8
    {
        /// <summary>
        /// Pointer to the first byte in the vector.
        /// </summary>
        public IntPtr Ptr;
        /// <summary>
        /// Number of valid bytes in the vector.
        /// </summary>
        public nuint Len;
        /// <summary>
        /// Total native capacity allocated for the vector.
        /// </summary>
        public nuint Cap;

        /// <summary>
        /// Gets a value indicating whether the vector is null or has no payload bytes.
        /// </summary>
        public readonly bool IsNullOrEmpty => Ptr == IntPtr.Zero || Len == 0;

        /// <summary>
        /// Copies the native vector contents into a managed byte array.
        /// </summary>
        /// <returns>A managed copy of the native byte vector.</returns>
        public readonly byte[] ToArray()
        {
            if (IsNullOrEmpty)
            {
                return Array.Empty<byte>();
            }

            var length = checked((int)Len);
            var data = new byte[length];
            Marshal.Copy(Ptr, data, 0, length);
            return data;
        }

        /// <summary>
        /// Decodes the native byte vector as a UTF-8 string.
        /// </summary>
        /// <returns>The decoded string, or <see langword="null"/> when the vector is empty.</returns>
        public readonly string? ToUtf8String()
        {
            if (IsNullOrEmpty)
            {
                return null;
            }

            return Encoding.UTF8.GetString(ToArray());
        }
    }

    /// <summary>
    /// Native result container returned by Dora operator ABI functions.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDoraResult
    {
        /// <summary>
        /// Pointer to a native UTF-8 error payload, or <see cref="IntPtr.Zero"/> on success.
        /// </summary>
        public IntPtr Error;

        /// <summary>
        /// Gets a value indicating whether the result represents success.
        /// </summary>
        public readonly bool IsSuccess => Error == IntPtr.Zero;

        /// <summary>
        /// Reads the native error payload as a UTF-8 string.
        /// </summary>
        /// <returns>The managed error string, or <see langword="null"/> when the result is successful.</returns>
        public readonly string? ReadErrorUtf8()
        {
            if (Error == IntPtr.Zero)
            {
                return null;
            }

            var error = Marshal.PtrToStructure<NativeVecU8>(Error);
            return error.ToUtf8String();
        }
    }

    /// <summary>
    /// Native result payload returned by operator initialization callbacks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDoraInitResult
    {
        /// <summary>
        /// Status result for the initialization call.
        /// </summary>
        public NativeDoraResult Result;
        /// <summary>
        /// Native operator context pointer returned on successful initialization.
        /// </summary>
        public IntPtr OperatorContext;
    }

    /// <summary>
    /// Native result payload returned by operator event callbacks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeOnEventResult
    {
        /// <summary>
        /// Status result for the event callback.
        /// </summary>
        public NativeDoraResult Result;
        /// <summary>
        /// Dora status code describing whether to continue or stop.
        /// </summary>
        public DoraStatus Status;
    }

    /// <summary>
    /// Native raw-event union projected into a blittable managed representation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRawEvent
    {
        /// <summary>
        /// Pointer to the native input payload, when the event is an input event.
        /// </summary>
        public IntPtr Input;
        /// <summary>
        /// Native input identifier for input-closed events.
        /// </summary>
        public NativeVecU8 InputClosed;

        /// <summary>
        /// Indicates whether the runtime is requesting the operator to stop.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool Stop;

        /// <summary>
        /// Native error message payload for error events.
        /// </summary>
        public NativeVecU8 Error;

        /// <summary>
        /// Gets a value indicating whether the event carries an input payload.
        /// </summary>
        public readonly bool HasInput => Input != IntPtr.Zero;
    }

    /// <summary>
    /// Native vtable-like structure for the send-output callback.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArcDynFn1DoraResultOutput
    {
        /// <summary>
        /// Native environment pointer associated with the callback object.
        /// </summary>
        public IntPtr EnvPtr;
        /// <summary>
        /// Native function pointer used to invoke the callback.
        /// </summary>
        public IntPtr Call;
        /// <summary>
        /// Native function pointer used to release the callback object.
        /// </summary>
        public IntPtr Release;
        /// <summary>
        /// Native function pointer used to retain the callback object.
        /// </summary>
        public IntPtr Retain;
    }

    /// <summary>
    /// Native wrapper for the send-output callback object.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeSendOutput
    {
        /// <summary>
        /// The native send-output callback bundle.
        /// </summary>
        public NativeArcDynFn1DoraResultOutput SendOutput;
    }

    /// <summary>
    /// Native metadata attached to input events.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMetadata
    {
        /// <summary>
        /// Serialized OpenTelemetry context bytes associated with the input event.
        /// </summary>
        public NativeVecU8 OpenTelemetryContext;
    }

    /// <summary>
    /// Native Arrow array structure exposed by the Arrow C data interface.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArrowArray
    {
        /// <summary>
        /// Logical length of the Arrow array.
        /// </summary>
        public long Length;
        /// <summary>
        /// Number of null values contained in the array.
        /// </summary>
        public long NullCount;
        /// <summary>
        /// Logical offset into the physical buffers.
        /// </summary>
        public long Offset;
        /// <summary>
        /// Number of buffer pointers described by the array.
        /// </summary>
        public long NBuffers;
        /// <summary>
        /// Number of child arrays referenced by the array.
        /// </summary>
        public long NChildren;
        /// <summary>
        /// Pointer to the buffer-pointer array.
        /// </summary>
        public IntPtr Buffers;
        /// <summary>
        /// Pointer to the child-array pointer array.
        /// </summary>
        public IntPtr Children;
        /// <summary>
        /// Pointer to the dictionary array, when present.
        /// </summary>
        public IntPtr Dictionary;
        /// <summary>
        /// Native release callback pointer.
        /// </summary>
        public IntPtr Release;
        /// <summary>
        /// Native private-data pointer associated with the array.
        /// </summary>
        public IntPtr PrivateData;

        /// <summary>
        /// Gets a value indicating whether a native release callback is present.
        /// </summary>
        public readonly bool HasRelease => Release != IntPtr.Zero;
        /// <summary>
        /// Gets a value indicating whether a dictionary array is attached.
        /// </summary>
        public readonly bool HasDictionary => Dictionary != IntPtr.Zero;
    }

    /// <summary>
    /// Native Arrow schema structure exposed by the Arrow C data interface.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArrowSchema
    {
        /// <summary>
        /// Pointer to the Arrow format string.
        /// </summary>
        public IntPtr Format;
        /// <summary>
        /// Pointer to the field name string, when present.
        /// </summary>
        public IntPtr Name;
        /// <summary>
        /// Pointer to the metadata buffer.
        /// </summary>
        public IntPtr Metadata;
        /// <summary>
        /// Arrow schema flags defined by the C data interface.
        /// </summary>
        public long Flags;
        /// <summary>
        /// Number of child schemas referenced by this schema.
        /// </summary>
        public long NChildren;
        /// <summary>
        /// Pointer to the child-schema pointer array.
        /// </summary>
        public IntPtr Children;
        /// <summary>
        /// Pointer to the dictionary schema, when present.
        /// </summary>
        public IntPtr Dictionary;
        /// <summary>
        /// Native release callback pointer.
        /// </summary>
        public IntPtr Release;
        /// <summary>
        /// Native private-data pointer associated with the schema.
        /// </summary>
        public IntPtr PrivateData;

        /// <summary>
        /// Gets a value indicating whether a native release callback is present.
        /// </summary>
        public readonly bool HasRelease => Release != IntPtr.Zero;
        /// <summary>
        /// Gets a value indicating whether a dictionary schema is attached.
        /// </summary>
        public readonly bool HasDictionary => Dictionary != IntPtr.Zero;
    }

    /// <summary>
    /// Native Arrow payload pairing an array handle with a schema handle.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArrowPayload
    {
        /// <summary>
        /// Pointer to the native Arrow array handle.
        /// </summary>
        public IntPtr Array;
        /// <summary>
        /// Pointer to the native Arrow schema handle.
        /// </summary>
        public IntPtr Schema;

        /// <summary>
        /// Gets a value indicating whether either native handle is present.
        /// </summary>
        public readonly bool HasAnyHandle => Array != IntPtr.Zero || Schema != IntPtr.Zero;
        /// <summary>
        /// Gets a value indicating whether both native handles are present.
        /// </summary>
        public readonly bool IsComplete => Array != IntPtr.Zero && Schema != IntPtr.Zero;
    }
}
