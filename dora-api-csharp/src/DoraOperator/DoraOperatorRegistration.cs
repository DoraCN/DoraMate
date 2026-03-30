namespace DoraOperator;

public static class DoraOperatorRegistration
{
    public static void RegisterFactory(OperatorFactory factory)
    {
        OperatorExports.RegisterFactory(factory);
    }
}
