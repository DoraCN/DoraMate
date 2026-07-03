using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DoraOperator;

/// <summary>
/// Low-level P/Invoke declarations for the Dora C operator API.
/// </summary>
internal static class NativeMethods
{
    private const string NativeLibraryName = "dora_operator_api_c";
    private static readonly object LoadSync = new();
    private static IntPtr s_nativeLibraryHandle;
    private static string? s_loadedLibraryPath;

    private static readonly string NativeLibraryFileName =
        OperatingSystem.IsWindows() ? "dora_operator_api_c.dll" :
        OperatingSystem.IsMacOS() ? "libdora_operator_api_c.dylib" :
        "libdora_operator_api_c.so";

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

            var explicitPath = Environment.GetEnvironmentVariable("DORA_OPERATOR_API_C_PATH");
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

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_read_input_id")]
    public static extern IntPtr ReadInputId(IntPtr input);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_input_id")]
    public static extern void FreeInputId(IntPtr inputId);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_read_input_open_telemetry_context")]
    public static extern IntPtr ReadInputOpenTelemetryContext(IntPtr input);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_input_open_telemetry_context")]
    public static extern void FreeInputOpenTelemetryContext(IntPtr context);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_input_has_bytes")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool InputHasBytes(IntPtr input);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_input_has_arrow")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool InputHasArrow(IntPtr input);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_read_data")]
    public static extern NativeTypes.NativeVecU8 ReadData(IntPtr input);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_data")]
    public static extern void FreeData(NativeTypes.NativeVecU8 data);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_take_input_arrow_data")]
    public static extern NativeTypes.NativeArrowPayload TakeInputArrowData(IntPtr input);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_operator_free_arrow_array")]
    public static extern void FreeArrowArray(IntPtr array);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_operator_free_arrow_schema")]
    public static extern void FreeArrowSchema(IntPtr schema);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_operator_free_arrow_payload")]
    public static extern void FreeArrowPayload(NativeTypes.NativeArrowPayload payload);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_operator_arrow_payload_to_ipc_bytes")]
    public static extern NativeTypes.NativeDoraResult ArrowPayloadToIpcBytes(
        IntPtr array,
        IntPtr schema,
        out NativeTypes.NativeVecU8 outBytes);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_result_from_error")]
    public static extern NativeTypes.NativeDoraResult CreateErrorResult(byte[] errorUtf8);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_free_result")]
    public static extern void FreeResult(NativeTypes.NativeDoraResult result);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_operator_output")]
    public static extern NativeTypes.NativeDoraResult SendOperatorOutput(
        IntPtr sendOutput,
        byte[] outputIdUtf8,
        byte[] data,
        nuint dataLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_operator_output_with_metadata")]
    public static extern NativeTypes.NativeDoraResult SendOperatorOutputWithMetadata(
        IntPtr sendOutput,
        byte[] outputIdUtf8,
        byte[] data,
        nuint dataLen,
        byte[] openTelemetryContextUtf8);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_operator_arrow_output")]
    public static extern NativeTypes.NativeDoraResult SendOperatorArrowOutput(
        IntPtr sendOutput,
        byte[] outputIdUtf8,
        IntPtr array,
        IntPtr schema);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_operator_arrow_output_with_metadata")]
    public static extern NativeTypes.NativeDoraResult SendOperatorArrowOutputWithMetadata(
        IntPtr sendOutput,
        byte[] outputIdUtf8,
        IntPtr array,
        IntPtr schema,
        byte[] openTelemetryContextUtf8);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_operator_arrow_ipc_output")]
    public static extern NativeTypes.NativeDoraResult SendOperatorArrowIpcOutput(
        IntPtr sendOutput,
        byte[] outputIdUtf8,
        byte[] data,
        nuint dataLen);

    [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dora_send_operator_arrow_ipc_output_with_metadata")]
    public static extern NativeTypes.NativeDoraResult SendOperatorArrowIpcOutputWithMetadata(
        IntPtr sendOutput,
        byte[] outputIdUtf8,
        byte[] data,
        nuint dataLen,
        byte[] openTelemetryContextUtf8);
}
