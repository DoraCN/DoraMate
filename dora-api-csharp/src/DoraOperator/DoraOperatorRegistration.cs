namespace DoraOperator;

/// <summary>
/// Registers operator factories for the current managed process.
/// </summary>
public static class DoraOperatorRegistration
{
    /// <summary>
    /// Registers the factory used to create managed operator instances for native callbacks.
    /// </summary>
    /// <param name="factory">The factory responsible for creating operator instances.</param>
    public static void RegisterFactory(OperatorFactory factory)
    {
        OperatorExports.RegisterFactory(factory);
    }
}
