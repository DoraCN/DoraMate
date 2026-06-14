using System.Text;

namespace DoraNode;

/// <summary>
/// Stable error codes for managed DoraNode failures.
/// </summary>
public enum DoraNodeErrorCode
{
    /// <summary>
    /// An unspecified DoraNode failure occurred.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The native Dora node library could not be loaded.
    /// </summary>
    NativeLibraryLoadFailed,

    /// <summary>
    /// The node context could not be initialized from the Dora runtime environment.
    /// </summary>
    ContextInitializationFailed,

    /// <summary>
    /// Sending a byte or text output failed.
    /// </summary>
    OutputSendFailed,

    /// <summary>
    /// Sending an Arrow payload failed.
    /// </summary>
    ArrowOutputSendFailed,

    /// <summary>
    /// Sending an Arrow record batch failed.
    /// </summary>
    RecordBatchOutputSendFailed,

    /// <summary>
    /// Converting an Arrow payload to IPC bytes failed.
    /// </summary>
    ArrowPayloadConversionFailed,

    /// <summary>
    /// An expected Arrow payload was missing.
    /// </summary>
    ArrowPayloadMissing,

    /// <summary>
    /// Arrow schema validation failed.
    /// </summary>
    SchemaValidationFailed,

    /// <summary>
    /// Contract projection from an Arrow record batch failed.
    /// </summary>
    ContractValidationFailed,

    /// <summary>
    /// The caller violated a managed lifecycle rule.
    /// </summary>
    LifecycleViolation,

    /// <summary>
    /// A native handle was invalid or unusable.
    /// </summary>
    InvalidNativeHandle,
}

/// <summary>
/// Structured diagnostics for native Dora library discovery and loading.
/// </summary>
public sealed class DoraNativeLibraryDiagnostics
{
    internal DoraNativeLibraryDiagnostics(
        string libraryName,
        string libraryFileName,
        string? loadedLibraryPath,
        IReadOnlyList<string> candidatePaths)
    {
        LibraryName = libraryName;
        LibraryFileName = libraryFileName;
        LoadedLibraryPath = loadedLibraryPath;
        CandidatePaths = candidatePaths;
    }

    /// <summary>
    /// Gets the logical native library name used for P/Invoke resolution.
    /// </summary>
    public string LibraryName { get; }

    /// <summary>
    /// Gets the platform-specific native library file name.
    /// </summary>
    public string LibraryFileName { get; }

    /// <summary>
    /// Gets the library path that was successfully loaded, when available.
    /// </summary>
    public string? LoadedLibraryPath { get; }

    /// <summary>
    /// Gets the candidate library paths that were probed.
    /// </summary>
    public IReadOnlyList<string> CandidatePaths { get; }
}

/// <summary>
/// Managed diagnostics snapshot for DoraNode operations.
/// </summary>
public sealed class DoraNodeDiagnosticInfo
{
    internal DoraNodeDiagnosticInfo(
        string operation,
        string? detail,
        DoraNativeLibraryDiagnostics nativeLibrary,
        string currentDirectory,
        string baseDirectory,
        string? processPath)
    {
        Operation = operation;
        Detail = detail;
        NativeLibrary = nativeLibrary;
        CurrentDirectory = currentDirectory;
        BaseDirectory = baseDirectory;
        ProcessPath = processPath;
    }

    /// <summary>
    /// Gets the high-level operation that produced the diagnostics snapshot.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets an optional detail string associated with the operation.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets native library discovery and loading diagnostics.
    /// </summary>
    public DoraNativeLibraryDiagnostics NativeLibrary { get; }

    /// <summary>
    /// Gets the current working directory of the process.
    /// </summary>
    public string CurrentDirectory { get; }

    /// <summary>
    /// Gets the application base directory.
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Gets the current process executable path, when available.
    /// </summary>
    public string? ProcessPath { get; }

    internal static DoraNodeDiagnosticInfo Capture(string operation, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        return new DoraNodeDiagnosticInfo(
            operation,
            detail,
            NativeMethods.CaptureNativeLibraryDiagnostics(),
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
            Environment.ProcessPath);
    }

    /// <summary>
    /// Formats the diagnostics snapshot as a human-readable multi-line string.
    /// </summary>
    public string ToDisplayString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Operation: {Operation}");

        if (!string.IsNullOrWhiteSpace(Detail))
        {
            builder.AppendLine($"Detail: {Detail}");
        }

        builder.AppendLine($"BaseDirectory: {BaseDirectory}");
        builder.AppendLine($"CurrentDirectory: {CurrentDirectory}");

        if (!string.IsNullOrWhiteSpace(ProcessPath))
        {
            builder.AppendLine($"ProcessPath: {ProcessPath}");
        }

        builder.AppendLine($"NativeLibraryName: {NativeLibrary.LibraryName}");
        builder.AppendLine($"NativeLibraryFileName: {NativeLibrary.LibraryFileName}");
        builder.AppendLine($"LoadedLibraryPath: {NativeLibrary.LoadedLibraryPath ?? "<not loaded>"}");
        builder.AppendLine("CandidateLibraryPaths:");

        if (NativeLibrary.CandidatePaths.Count == 0)
        {
            builder.AppendLine("  (none)");
        }
        else
        {
            foreach (var candidatePath in NativeLibrary.CandidatePaths)
            {
                builder.AppendLine($"  - {candidatePath}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}

/// <summary>
/// Exception thrown when Dora node operations fail.
/// </summary>
public class DoraException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="DoraException"/> with a message.
    /// </summary>
    public DoraException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="DoraException"/> with a message and inner exception.
    /// </summary>
    public DoraException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal DoraException(
        string message,
        DoraNodeErrorCode errorCode,
        string operation,
        DoraNodeDiagnosticInfo diagnostics,
        Exception? innerException = null)
        : base(FormatMessage(errorCode, message), innerException)
    {
        ErrorCode = errorCode;
        Operation = operation;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the stable DoraNode error code associated with this exception.
    /// </summary>
    public DoraNodeErrorCode ErrorCode { get; } = DoraNodeErrorCode.Unknown;

    /// <summary>
    /// Gets the high-level operation that failed, when available.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Gets the structured diagnostics snapshot captured at failure time, when available.
    /// </summary>
    public DoraNodeDiagnosticInfo? Diagnostics { get; }

    internal static DoraException Create(
        string message,
        DoraNodeErrorCode errorCode,
        string operation,
        string? detail = null,
        Exception? innerException = null)
    {
        return new DoraException(
            message,
            errorCode,
            operation,
            DoraNodeDiagnosticInfo.Capture(operation, detail),
            innerException);
    }

    private static string FormatMessage(DoraNodeErrorCode errorCode, string message)
    {
        return errorCode == DoraNodeErrorCode.Unknown ? message : $"[{errorCode}] {message}";
    }

    /// <summary>
    /// Formats the exception, including structured diagnostics when they are available.
    /// </summary>
    public override string ToString()
    {
        if (Diagnostics is null)
        {
            return base.ToString();
        }

        return base.ToString()
            + Environment.NewLine
            + $"ErrorCode: {ErrorCode}"
            + Environment.NewLine
            + Environment.NewLine
            + Diagnostics.ToDisplayString();
    }
}
