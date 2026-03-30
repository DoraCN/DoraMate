using System.Text;

namespace DoraOperator;

/// <summary>
/// Stable error codes for managed DoraOperator failures.
/// </summary>
public enum DoraOperatorErrorCode
{
    Unknown = 0,
    NativeLibraryLoadFailed,
    InitializationFailed,
    EventHandlingFailed,
    DropFailed,
    OutputSendFailed,
    ArrowOutputSendFailed,
    RecordBatchOutputSendFailed,
    ArrowPayloadConversionFailed,
    ArrowPayloadMissing,
    SchemaValidationFailed,
    ContractValidationFailed,
    LifecycleViolation,
    InvalidNativeHandle,
    InvalidOperatorContext,
}

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

    public string Operation { get; }
    public string? Detail { get; }
    public string? OperatorId { get; }
    public string? NodeId { get; }
    public string? DataflowId { get; }
    public DoraNativeLibraryDiagnostics NativeLibrary { get; }
    public string CurrentDirectory { get; }
    public string BaseDirectory { get; }
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

public class DoraOperatorException : Exception
{
    public DoraOperatorException(string message)
        : base(message)
    {
    }

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

    public DoraOperatorErrorCode ErrorCode { get; } = DoraOperatorErrorCode.Unknown;
    public string? Operation { get; }
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
