using CSharpComplexArrowNodeDataflow;
using DoraNode;

namespace CSharpComplexArrowNodeConsumer;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting C# complex Arrow node consumer...");

        try
        {
            using var node = new DoraNode.DoraNode();

            while (true)
            {
                using var ev = node.Next();
                if (ev is null)
                {
                    Console.WriteLine("Consumer event stream closed");
                    break;
                }

                if (ev.Type == EventType.Input)
                {
                    var mode = GetTestMode();
                    if (!ev.TryReadModel(
                            RichComplexArrowContract.Contract,
                            out ComplexBatchModel? model,
                            out var error,
                            out var errorCode))
                    {
                        if (mode == ArrowTestMode.ContractFailure)
                        {
                            if (errorCode != DoraNodeErrorCode.ContractValidationFailed)
                            {
                                Console.Error.WriteLine(
                                    $"Expected contract failure code '{DoraNodeErrorCode.ContractValidationFailed}' but got '{errorCode}'.");
                                Environment.Exit(1);
                            }

                            if (string.IsNullOrWhiteSpace(error))
                            {
                                Console.Error.WriteLine("Expected a contract validation error message but got none.");
                                Environment.Exit(1);
                            }

                            Console.WriteLine(RichComplexArrowContract.CreateExpectedContractFailureSummary(errorCode, error));
                            return;
                        }

                        Console.Error.WriteLine(error ?? "Input did not contain the expected complex Arrow RecordBatch payload.");
                        Environment.Exit(1);
                    }

                    if (model is null)
                    {
                        Console.Error.WriteLine("TryReadModel succeeded but returned a null model.");
                        Environment.Exit(1);
                    }

                    if (!RichComplexArrowContract.TryValidateModel(model, out error))
                    {
                        Console.Error.WriteLine(error);
                        Environment.Exit(1);
                    }

                    Console.WriteLine(RichComplexArrowContract.CreateSuccessSummary());
                    return;
                }

                if (ev.Type == EventType.Stop)
                {
                    Console.WriteLine("Consumer received stop event");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Consumer error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static ArrowTestMode GetTestMode()
    {
        var rawMode = Environment.GetEnvironmentVariable("DORA_CSHARP_ARROW_TEST_MODE");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "normal" => ArrowTestMode.Normal,
            "contract-failure" => ArrowTestMode.ContractFailure,
            _ => throw new InvalidOperationException($"Unsupported Arrow test mode '{rawMode}'.")
        };
    }

    private enum ArrowTestMode
    {
        Normal,
        ContractFailure
    }
}
