using System.Text;

namespace DoraNode;

/// <summary>
/// Stable error codes for managed DoraNode failures.
/// </summary>
public enum DoraNodeErrorCode
{
    Unknown = 0,
    NativeLibraryLoadFailed,
    ContextInitializationFailed,
    OutputSendFailed,
    ArrowOutputSendFailed,
    RecordBatchOutputSendFailed,
    ArrowPayloadConversionFailed,
    ArrowPayloadMissing,
    SchemaValidationFailed,
    ContractValidationFailed,
    LifecycleViolation,
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

    public string LibraryName { get; }
    public string LibraryFileName { get; }
    public string? LoadedLibraryPath { get; }
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

    public string Operation { get; }
    public string? Detail { get; }
    public DoraNativeLibraryDiagnostics NativeLibrary { get; }
    public string CurrentDirectory { get; }
    public string BaseDirectory { get; }
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
    public DoraException(string message)
        : base(message)
    {
    }

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

    public DoraNodeErrorCode ErrorCode { get; } = DoraNodeErrorCode.Unknown;
    public string? Operation { get; }
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
