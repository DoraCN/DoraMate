using DoraNodeRegressionRunner;

var tests = new (string Name, Action Body)[]
{
    ("Roundtrip summary matches standalone example output", RegressionTests.RoundtripSummaryMatchesStandaloneExampleOutput),
    ("Roundtrip schema validation rejects schema mismatch", RegressionTests.RoundtripSchemaValidationRejectsSchemaMismatch),
    ("Roundtrip schema validation rejects empty batch", RegressionTests.RoundtripSchemaValidationRejectsEmptyBatch),
    ("Roundtrip assertions cover basic scalar columns", RegressionTests.RoundtripAssertionsCoverBasicScalarColumns),
    ("Advanced summary matches standalone example output", RegressionTests.AdvancedSummaryMatchesStandaloneExampleOutput),
    ("Advanced assertions cover date timestamp binary columns", RegressionTests.AdvancedAssertionsCoverDateTimestampBinaryColumns),
    ("Complex list and struct projectors match standalone models", RegressionTests.ComplexListAndStructProjectorsMatchStandaloneModels),
    ("Complex contract projects expected model", RegressionTests.ComplexContractProjectsExpectedModel),
    ("Complex contract failure summary matches standalone example format", RegressionTests.ComplexContractFailureSummaryMatchesStandaloneExampleFormat)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"[PASS] {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"[FAIL] {test.Name}");
        Console.Error.WriteLine(ex);
    }
}

if (failures > 0)
{
    Console.Error.WriteLine($"DoraNode regression tests failed: {failures}.");
    return 1;
}

Console.WriteLine($"DoraNode regression tests passed: {tests.Length}.");
return 0;