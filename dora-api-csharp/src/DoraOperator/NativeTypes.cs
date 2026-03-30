using System.Runtime.InteropServices;
using System.Text;

namespace DoraOperator;

/// <summary>
/// Native ABI types for bridging the C operator API into the managed DoraOperator API.
/// </summary>
public static class NativeTypes
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeVecU8
    {
        public IntPtr Ptr;
        public nuint Len;
        public nuint Cap;

        public readonly bool IsNullOrEmpty => Ptr == IntPtr.Zero || Len == 0;

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

        public readonly string? ToUtf8String()
        {
            if (IsNullOrEmpty)
            {
                return null;
            }

            return Encoding.UTF8.GetString(ToArray());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDoraResult
    {
        public IntPtr Error;

        public readonly bool IsSuccess => Error == IntPtr.Zero;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDoraInitResult
    {
        public NativeDoraResult Result;
        public IntPtr OperatorContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeOnEventResult
    {
        public NativeDoraResult Result;
        public DoraStatus Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRawEvent
    {
        public IntPtr Input;
        public NativeVecU8 InputClosed;

        [MarshalAs(UnmanagedType.I1)]
        public bool Stop;

        public NativeVecU8 Error;

        public readonly bool HasInput => Input != IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArcDynFn1DoraResultOutput
    {
        public IntPtr EnvPtr;
        public IntPtr Call;
        public IntPtr Release;
        public IntPtr Retain;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeSendOutput
    {
        public NativeArcDynFn1DoraResultOutput SendOutput;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMetadata
    {
        public NativeVecU8 OpenTelemetryContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArrowArray
    {
        public long Length;
        public long NullCount;
        public long Offset;
        public long NBuffers;
        public long NChildren;
        public IntPtr Buffers;
        public IntPtr Children;
        public IntPtr Dictionary;
        public IntPtr Release;
        public IntPtr PrivateData;

        public readonly bool HasRelease => Release != IntPtr.Zero;
        public readonly bool HasDictionary => Dictionary != IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArrowSchema
    {
        public IntPtr Format;
        public IntPtr Name;
        public IntPtr Metadata;
        public long Flags;
        public long NChildren;
        public IntPtr Children;
        public IntPtr Dictionary;
        public IntPtr Release;
        public IntPtr PrivateData;

        public readonly bool HasRelease => Release != IntPtr.Zero;
        public readonly bool HasDictionary => Dictionary != IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeArrowPayload
    {
        public IntPtr Array;
        public IntPtr Schema;

        public readonly bool HasAnyHandle => Array != IntPtr.Zero || Schema != IntPtr.Zero;
        public readonly bool IsComplete => Array != IntPtr.Zero && Schema != IntPtr.Zero;
    }
}
