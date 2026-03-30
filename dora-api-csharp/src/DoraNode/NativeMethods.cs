using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DoraNode;

/// <summary>
/// P/Invoke declarations for the native Dora node C API.
/// </summary>
internal static unsafe class NativeMethods
{
    private const string NativeLibraryName = "dora_node_api_c";
    private static readonly object LoadSync = new();
    private static IntPtr s_nativeLibraryHandle;
    private static string? s_loadedLibraryPath;

    private static readonly string NativeLibraryFileName =
        OperatingSystem.IsWindows() ? "dora_node_api_c.dll" :
        OperatingSystem.IsMacOS() ? "libdora_node_api_c.dylib" :
        "libdora_node_api_c.so";

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, (libraryName, _, _) =>
        {
            if (libraryName != NativeLibraryName)
            {
                return IntPtr.Zero;
            }

            if (s_nativeLibraryHandle != IntPtr.Zero)
            {
                return s_nativeLibraryHandle;
            }

            EnsureLoaded();
            return s_nativeLibraryHandle;
        });
    }

    public static void EnsureLoaded()
    {
        RuntimeHelpers.RunClassConstructor(typeof(NativeMethods).TypeHandle);

        if (s_nativeLibraryHandle != IntPtr.Zero)
        {
            return;
        }

        lock (LoadSync)
        {
            if (s_nativeLibraryHandle != IntPtr.Zero)
            {
                return;
            }

            var explicitPath = Environment.GetEnvironmentVariable("DORA_NODE_API_C_PATH");
            if (!string.IsNullOrWhiteSpace(explicitPath) &&
                File.Exists(explicitPath) &&
                NativeLibrary.TryLoad(explicitPath, out var explicitHandle))
            {
                s_nativeLibraryHandle = explicitHandle;
                s_loadedLibraryPath = explicitPath;
                return;
            }

            var diagnostics = new List<string>();
            foreach (var candidateDirectory in GetCandidateDirectories())
            {
                var nativeLibPath = Path.Combine(candidateDirectory, NativeLibraryFileName);
                diagnostics.Add(nativeLibPath);

                if (!File.Exists(nativeLibPath))
                {
                    continue;
                }

                if (NativeLibrary.TryLoad(nativeLibPath, out var handle))
                {
                    s_nativeLibraryHandle = handle;
                    s_loadedLibraryPath = nativeLibPath;
                    return;
                }
            }

            if (NativeLibrary.TryLoad(NativeLibraryFileName, out var fallbackHandle))
            {
                s_nativeLibraryHandle = fallbackHandle;
                s_loadedLibraryPath = NativeLibraryFileName;
                return;
            }

            var candidateList = string.Join(Environment.NewLine, diagnostics.Select(path => $"  - {path}"));
            throw new DllNotFoundException(
                $"Unable to load '{NativeLibraryFileName}'. Tried:{Environment.NewLine}{candidateList}");
        }
    }

    public static string GetLoadedLibraryPath()
    {
        EnsureLoaded();
        return s_loadedLibraryPath ?? NativeLibraryFileName;
    }

    internal static DoraNativeLibraryDiagnostics CaptureNativeLibraryDiagnostics()
    {
        return new DoraNativeLibraryDiagnostics(
            NativeLibraryName,
            NativeLibraryFileName,
            s_loadedLibraryPath,
            GetCandidateLibraryPaths());
    }

    private static IReadOnlyList<string> GetCandidateLibraryPaths()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidateDirectory in GetCandidateDirectories())
        {
            var nativeLibPath = Path.Combine(candidateDirectory, NativeLibraryFileName);
            if (seen.Add(nativeLibPath))
            {
                paths.Add(nativeLibPath);
            }
        }

        if (seen.Add(NativeLibraryFileName))
        {
            paths.Add(NativeLibraryFileName);
        }

        return paths.ToArray();
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seedDirectories = new List<string>();

        void AddSeed(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory) && seen.Add(directory))
            {
                seedDirectories.Add(directory);
            }
        }

        static string? TryGetAssemblyDirectory(Assembly assembly)
        {
            var location = assembly.Location;
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            return Path.GetDirectoryName(location);
        }

        AddSeed(AppContext.BaseDirectory);
        AddSeed(TryGetAssemblyDirectory(typeof(NativeMethods).Assembly));
        AddSeed(Environment.CurrentDirectory);

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            AddSeed(Path.GetDirectoryName(processPath));
        }

        foreach (var seedDirectory in seedDirectories)
        {
            foreach (var derivedDirectory in GetDerivedCandidateDirectories(seedDirectory))
            {
                if (seen.Add(derivedDirectory))
                {
                    yield return derivedDirectory;
                }
            }
        }

        foreach (var seedDirectory in seedDirectories)
        {
            yield return seedDirectory;
        }
    }

    private static IEnumerable<string> GetDerivedCandidateDirectories(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            yield break;
        }

        var rid = GetRuntimeIdentifier();
        for (var current = new DirectoryInfo(baseDirectory); current is not null; current = current.Parent)
        {
            yield return Path.Combine(current.FullName, "artifacts", "native", rid);
            yield return Path.Combine(current.FullName, "third_party", "dora", "target", "release");
        }
    }

    private static string GetRuntimeIdentifier()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx-x64";
        }

        return "linux-x64";
    }

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "init_dora_context_from_env")]
    public static extern IntPtr InitDoraContextFromEnv();

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "close_dora_event_stream")]
    public static extern void CloseDoraEventStream(IntPtr context);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "free_dora_context")]
    public static extern void FreeDoraContext(IntPtr context);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_next_event")]
    public static extern IntPtr DoraNextEvent(IntPtr context);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "free_dora_event")]
    public static extern void FreeDoraEvent(IntPtr doraEvent);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_event_type")]
    public static extern EventType ReadDoraEventType(IntPtr doraEvent);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_input_id")]
    public static extern void ReadDoraInputId(IntPtr doraEvent, out IntPtr outPtr, out UIntPtr outLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_input_data")]
    public static extern void ReadDoraInputData(IntPtr doraEvent, out IntPtr outPtr, out UIntPtr outLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_input_has_bytes")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ReadDoraInputHasBytes(IntPtr doraEvent);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_input_arrow_data")]
    public static extern NativeTypes.NativeArrowPayload ReadDoraInputArrowData(IntPtr doraEvent);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_arrow_payload_to_ipc_bytes")]
    public static extern int ArrowPayloadToIpcBytes(IntPtr array, IntPtr schema, out NativeTypes.NativeVecU8 outBytes);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_arrow_array")]
    public static extern void FreeArrowArray(IntPtr array);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_arrow_schema")]
    public static extern void FreeArrowSchema(IntPtr schema);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_arrow_payload")]
    public static extern void FreeArrowPayload(NativeTypes.NativeArrowPayload payload);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_owned_bytes")]
    public static extern void FreeOwnedBytes(NativeTypes.NativeVecU8 bytes);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_input_open_telemetry_context")]
    public static extern void ReadDoraInputOpenTelemetryContext(IntPtr doraEvent, out IntPtr outPtr, out UIntPtr outLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_input_closed_id")]
    public static extern void ReadDoraInputClosedId(IntPtr doraEvent, out IntPtr outPtr, out UIntPtr outLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_error_message")]
    public static extern void ReadDoraErrorMessage(IntPtr doraEvent, out IntPtr outPtr, out UIntPtr outLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "read_dora_input_timestamp")]
    public static extern ulong ReadDoraInputTimestamp(IntPtr doraEvent);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_output")]
    public static extern int DoraSendOutput(IntPtr context, byte[] idPtr, UIntPtr idLen, byte[] dataPtr, UIntPtr dataLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_output_arrow")]
    public static extern int DoraSendOutputArrow(IntPtr context, byte[] idPtr, UIntPtr idLen, IntPtr array, IntPtr schema);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_output_arrow_ipc")]
    public static extern int DoraSendOutputArrowIpc(IntPtr context, byte[] idPtr, UIntPtr idLen, byte[] dataPtr, UIntPtr dataLen);
}
