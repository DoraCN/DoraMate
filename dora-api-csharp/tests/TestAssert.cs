using Xunit;

internal static class TestAssert
{
    public static void True(bool condition, string? message)
    {
        Assert.True(condition, message ?? "Expected condition to be true.");
    }

    public static void False(bool condition, string message)
    {
        Assert.False(condition, message);
    }

    public static void NotNull<T>(T? value, string label)
    {
        Assert.True(value is not null, $"Expected '{label}' to be non-null.");
    }

    public static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        Assert.True(
            EqualityComparer<T>.Default.Equals(expected, actual),
            $"Expected {label} to be '{expected}' but got '{actual}'.");
    }

    public static void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T>? actual, string label)
    {
        NotNull(actual, label);
        Assert.True(
            expected.Count == actual!.Count,
            $"Expected {label} to contain {expected.Count} values but got {actual.Count}.");

        for (var index = 0; index < expected.Count; index++)
        {
            var actualValue = actual[index];
            Assert.True(
                EqualityComparer<T>.Default.Equals(expected[index], actualValue),
                $"Expected {label}[{index}] to be '{expected[index]}' but got '{actualValue}'.");
        }
    }

    public static void SequenceMatrixEqual<T>(IReadOnlyList<IReadOnlyList<T>> expected, IReadOnlyList<IReadOnlyList<T>>? actual, string label)
    {
        NotNull(actual, label);
        Assert.True(
            expected.Count == actual!.Count,
            $"Expected {label} to contain {expected.Count} rows but got {actual.Count}.");

        for (var rowIndex = 0; rowIndex < expected.Count; rowIndex++)
        {
            SequenceEqual(expected[rowIndex], actual[rowIndex], $"{label}[{rowIndex}]");
        }
    }

    public static void ByteEqual(byte[] expected, byte[] actual, string label)
    {
        Assert.True(
            expected.SequenceEqual(actual),
            $"Expected {label} to be '{BitConverter.ToString(expected)}' but got '{BitConverter.ToString(actual)}'.");
    }

    public static void ByteMatrixEqual(IReadOnlyList<byte[]> expected, IReadOnlyList<byte[]>? actual, string label)
    {
        NotNull(actual, label);
        Assert.True(
            expected.Count == actual!.Count,
            $"Expected {label} to contain {expected.Count} rows but got {actual.Count}.");

        for (var index = 0; index < expected.Count; index++)
        {
            ByteEqual(expected[index], actual[index], $"{label}[{index}]");
        }
    }

    public static void Contains(string? actual, string expectedSubstring, string label)
    {
        Assert.True(
            actual is not null && actual.IndexOf(expectedSubstring, StringComparison.Ordinal) >= 0,
            $"Expected {label} to contain '{expectedSubstring}' but got '{actual ?? "<null>"}'.");
    }
}
