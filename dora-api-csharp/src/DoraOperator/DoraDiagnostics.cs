using System.Text;

namespace DoraOperator;

/// <summary>
/// Stable error codes for managed DoraOperator failures.
/// </summary>
public enum DoraOperatorErrorCode
{
    /// <summary>
    /// An unspecified DoraOperator failure occurred.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The native Dora operator library could not be loaded.
    /// </summary>
    NativeLibraryLoadFailed,

    /// <summary>
    /// Operator initialization failed.
    /// </summary>
    InitializationFailed,

    /// <summary>
    /// Operator event handling failed.
    /// </summary>
    EventHandlingFailed,

    /// <summary>
    /// Operator shutdown or cleanup failed.
    /// </summary>
    DropFailed,

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

    /// <summary>
    /// The operator context pointer was invalid.
    /// </summary>
    InvalidOperatorContext,
}

/// <summary>
/// Structured diagnostics for native Dora operator library discovery and loading.
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
/// Managed diagnostics snapshot for DoraOperator operations.
/// </summary>
public sealed class DoraOperatorDiagnosticInfo
{
    internal DoraOperatorDiagnosticInfo(
        string operation,
        string? detail,
        string? operatorId,
        string? nodeId,
        string? dataflowId,
        DoraNativeLibraryDiagnostics nativeLibrary,
        string currentDirectory,
        string baseDirectory,
        string? processPath)
    {
        Operation = operation;
        Detail = detail;
        OperatorId = operatorId;
        NodeId = nodeId;
        DataflowId = dataflowId;
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
    /// Gets the current operator ID, when available.
    /// </summary>
    public string? OperatorId { get; }

    /// <summary>
    /// Gets the current node ID, when available.
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// Gets the current dataflow ID, when available.
    /// </summary>
    public string? DataflowId { get; }

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

    internal static DoraOperatorDiagnosticInfo Capture(
        string operation,
        string? detail,
        OperatorInitContext? initContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        return new DoraOperatorDiagnosticInfo(
            operation,
            detail,
            initContext?.OperatorId,
            initContext?.NodeId,
            initContext?.DataflowId,
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

        if (!string.IsNullOrWhiteSpace(OperatorId))
        {
            builder.AppendLine($"OperatorId: {OperatorId}");
        }

        if (!string.IsNullOrWhiteSpace(NodeId))
        {
            builder.AppendLine($"NodeId: {NodeId}");
        }

        if (!string.IsNullOrWhiteSpace(DataflowId))
        {
            builder.AppendLine($"DataflowId: {DataflowId}");
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
/// Exception thrown when Dora operator operations fail.
/// </summary>
public class DoraOperatorException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="DoraOperatorException"/> with a message.
    /// </summary>
    public DoraOperatorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="DoraOperatorException"/> with a message and inner exception.
    /// </summary>
    public DoraOperatorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal DoraOperatorException(
        string message,
        DoraOperatorErrorCode errorCode,
        string operation,
        DoraOperatorDiagnosticInfo diagnostics,
        Exception? innerException = null)
        : base(FormatMessage(errorCode, message), innerException)
    {
        ErrorCode = errorCode;
        Operation = operation;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the stable DoraOperator error code associated with this exception.
    /// </summary>
    public DoraOperatorErrorCode ErrorCode { get; } = DoraOperatorErrorCode.Unknown;

    /// <summary>
    /// Gets the high-level operation that failed, when available.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Gets the structured diagnostics snapshot captured at failure time, when available.
    /// </summary>
    public DoraOperatorDiagnosticInfo? Diagnostics { get; }

    internal static DoraOperatorException Create(
        string message,
        DoraOperatorErrorCode errorCode,
        string operation,
        OperatorInitContext? initContext,
        string? detail = null,
        Exception? innerException = null)
    {
        return new DoraOperatorException(
            message,
            errorCode,
            operation,
            DoraOperatorDiagnosticInfo.Capture(operation, detail, initContext),
            innerException);
    }

    private static string FormatMessage(DoraOperatorErrorCode errorCode, string message)
    {
        return errorCode == DoraOperatorErrorCode.Unknown ? message : $"[{errorCode}] {message}";
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

internal static class DoraOperatorRuntimeErrors
{
    public static string CreateSummaryLine(string stage, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(exception);

        var errorCode = exception is DoraOperatorException operatorException
            ? operatorException.ErrorCode
            : DoraOperatorErrorCode.Unknown;
        var operation = exception is DoraOperatorException { Operation: not null } opException
            ? opException.Operation!
            : "unknown";
        var message = GetSummaryMessage(FormatException(exception));

        return CreateSummaryLine(stage, errorCode, operation, message);
    }

    public static string CreateSummaryLine(
        string stage,
        DoraOperatorErrorCode errorCode,
        string operation,
        string? message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var normalizedStage = NormalizeToken(stage);
        var normalizedOperation = NormalizeToken(operation);
        var normalizedMessage = NormalizeValue(GetSummaryMessage(message));
        return $"[DoraOperatorRuntime] stage={normalizedStage} code={errorCode} operation={normalizedOperation} message=\"{normalizedMessage}\"";
    }

    public static string FormatMessage(DoraOperatorErrorCode errorCode, string? message)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Unknown operator error."
            : message;

        if (errorCode == DoraOperatorErrorCode.Unknown)
        {
            return normalizedMessage;
        }

        var prefix = $"[{errorCode}] ";
        return normalizedMessage.StartsWith(prefix, StringComparison.Ordinal)
            ? normalizedMessage
            : prefix + normalizedMessage;
    }

    public static string FormatException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is DoraOperatorException operatorException
            ? FormatMessage(operatorException.ErrorCode, operatorException.Message)
            : exception.Message;
    }

    public static void LogException(string stage, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(exception);

        Console.Error.WriteLine(CreateSummaryLine(stage, exception));

        if (exception is DoraOperatorException operatorException &&
            operatorException.Diagnostics is not null)
        {
            Console.Error.WriteLine("[DoraOperatorRuntime.Diagnostics.Begin]");
            Console.Error.WriteLine(operatorException.Diagnostics.ToDisplayString());
            Console.Error.WriteLine("[DoraOperatorRuntime.Diagnostics.End]");
        }
        else
        {
            Console.Error.WriteLine("[DoraOperatorRuntime.Exception.Begin]");
            Console.Error.WriteLine(exception);
            Console.Error.WriteLine("[DoraOperatorRuntime.Exception.End]");
        }
    }

    public static void LogFailure(
        string stage,
        string operation,
        string? message,
        OperatorInitContext? initContext,
        string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var errorCode = ExtractErrorCode(message, out var normalizedMessage);
        Console.Error.WriteLine(CreateSummaryLine(stage, errorCode, operation, normalizedMessage));

        if (initContext is null)
        {
            return;
        }

        var diagnostics = DoraOperatorDiagnosticInfo.Capture(operation, detail, initContext);
        Console.Error.WriteLine("[DoraOperatorRuntime.Diagnostics.Begin]");
        Console.Error.WriteLine(diagnostics.ToDisplayString());
        Console.Error.WriteLine("[DoraOperatorRuntime.Diagnostics.End]");
    }

    private static DoraOperatorErrorCode ExtractErrorCode(string? message, out string normalizedMessage)
    {
        normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Unknown operator error."
            : message.Trim();

        if (normalizedMessage.Length < 3 || normalizedMessage[0] != '[')
        {
            return DoraOperatorErrorCode.Unknown;
        }

        var closingBracketIndex = normalizedMessage.IndexOf(']');
        if (closingBracketIndex <= 1)
        {
            return DoraOperatorErrorCode.Unknown;
        }

        var candidate = normalizedMessage[1..closingBracketIndex];
        if (!Enum.TryParse(candidate, ignoreCase: false, out DoraOperatorErrorCode errorCode) ||
            !Enum.IsDefined(errorCode))
        {
            return DoraOperatorErrorCode.Unknown;
        }

        normalizedMessage = normalizedMessage[(closingBracketIndex + 1)..].TrimStart();
        return errorCode;
    }

    private static string GetSummaryMessage(string? message)
    {
        _ = ExtractErrorCode(message, out var normalizedMessage);
        return normalizedMessage;
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().Replace(' ', '_');
    }

    private static string NormalizeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown operator error."
            : value
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Trim();
    }
}
