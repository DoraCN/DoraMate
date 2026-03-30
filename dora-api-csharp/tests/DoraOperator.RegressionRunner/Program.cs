using DoraOperatorRegressionRunner;

var tests = new (string Name, Action Body)[]
{
    ("Roundtrip summary matches standalone example output", RegressionTests.RoundtripSummaryMatchesStandaloneExampleOutput),
    ("Roundtrip schema validation rejects schema mismatch", RegressionTests.RoundtripSchemaValidationRejectsSchemaMismatch),
    ("Roundtrip schema validation rejects empty batch", RegressionTests.RoundtripSchemaValidationRejectsEmptyBatch),
    ("Scalar column projection covers advanced Arrow types", RegressionTests.ScalarColumnProjectionCoversAdvancedTypes),
    ("Row accessor projects advanced Arrow scalars", RegressionTests.RowAccessorProjectsAdvancedArrowScalars),
    ("Struct accessor projects nested advanced Arrow scalars", RegressionTests.StructAccessorProjectsNestedAdvancedArrowScalars),
    ("Complex contract projects nested list map struct payloads", RegressionTests.ComplexContractProjectsExpectedModel),
    ("Complex contract summary matches standalone example output", RegressionTests.ComplexContractSummaryMatchesStandaloneExampleOutput),
    ("Complex contract rejects invalid nested field types", RegressionTests.ComplexContractRejectsInvalidNestedFieldTypes),
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
    Console.Error.WriteLine($"DoraOperator regression tests failed: {failures}.");
    return 1;
}

Console.WriteLine($"DoraOperator regression tests passed: {tests.Length}.");
return 0;
