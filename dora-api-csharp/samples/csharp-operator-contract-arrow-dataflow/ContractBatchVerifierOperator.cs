using DoraOperator;

namespace CSharpOperatorContractArrow;

public sealed class ContractBatchVerifierOperator : DoraOperatorBase
{
    private bool _verified;

    protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(output);

        if (_verified)
        {
            return OnEventResult.Continue();
        }

        var mode = GetTestMode();
        if (!ev.Input.TryReadModel(
                ContractArrowFixture.Contract,
                out ComplexOperatorBatchModel? model,
                out var error,
                out var errorCode))
        {
            if (mode == ArrowTestMode.ContractFailure)
            {
                if (errorCode != DoraOperatorErrorCode.ContractValidationFailed)
                {
                    return OnEventResult.Err(
                        DoraOperatorErrorCode.ContractValidationFailed,
                        $"Expected contract failure code '{DoraOperatorErrorCode.ContractValidationFailed}' but got '{errorCode}'.");
                }

                if (string.IsNullOrWhiteSpace(error))
                {
                    return OnEventResult.Err(
                        DoraOperatorErrorCode.ContractValidationFailed,
                        "Expected a contract validation error message but got none.");
                }

                output.SendOrThrow("summary", ContractArrowFixture.CreateExpectedContractFailureSummary(errorCode, error));
                _verified = true;
                return OnEventResult.Continue();
            }

            return OnEventResult.Err(
                errorCode,
                error ?? "Input did not contain the expected Arrow RecordBatch payload.");
        }

        if (model is null)
        {
            return OnEventResult.Err("TryReadModel succeeded but returned a null model.");
        }

        if (!ContractArrowFixture.TryValidateModel(model, out error))
        {
            return OnEventResult.Err(error ?? "Projected Arrow model validation failed.");
        }

        output.SendOrThrow("summary", ContractArrowFixture.CreateSuccessSummary());
        _verified = true;
        return OnEventResult.Continue();
    }

    protected override OnEventResult OnInputClosed(InputClosedEvent ev, OperatorOutput output)
    {
        ArgumentNullException.ThrowIfNull(ev);
        return _verified
            ? OnEventResult.Stop()
            : OnEventResult.Err(
                DoraOperatorErrorCode.LifecycleViolation,
                "Arrow input closed before operator contract verification completed.");
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
