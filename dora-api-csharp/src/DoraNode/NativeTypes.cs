using System.Runtime.InteropServices;

namespace DoraNode;

/// <summary>
/// Native ABI types used by the Dora node binding.
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
