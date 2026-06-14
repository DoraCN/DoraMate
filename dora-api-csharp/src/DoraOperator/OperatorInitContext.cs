using System.Collections;
using System.Collections.ObjectModel;

namespace DoraOperator;

/// <summary>
/// Provides operator initialization data collected from the Dora runtime environment.
/// </summary>
public sealed class OperatorInitContext
{
    /// <summary>
    /// Gets a read-only snapshot of process environment variables.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

    /// <summary>
    /// Gets the raw runtime configuration YAML, when available.
    /// </summary>
    public string? RuntimeConfigYaml { get; }

    /// <summary>
    /// Gets the parsed runtime configuration, when parsing succeeded.
    /// </summary>
    public OperatorRuntimeConfig? RuntimeConfig { get; }

    /// <summary>
    /// Gets the current node ID from the parsed runtime configuration, when available.
    /// </summary>
    public string? NodeId => RuntimeConfig?.Node.NodeId;

    /// <summary>
    /// Gets the current dataflow ID from the parsed runtime configuration, when available.
    /// </summary>
    public string? DataflowId => RuntimeConfig?.Node.DataflowId;

    /// <summary>
    /// Gets the current operator ID from the parsed runtime configuration, when available.
    /// </summary>
    public string? OperatorId => RuntimeConfig?.Operator?.Id;

    private OperatorInitContext(
        IReadOnlyDictionary<string, string> environmentVariables,
        string? runtimeConfigYaml,
        OperatorRuntimeConfig? runtimeConfig)
    {
        EnvironmentVariables = environmentVariables;
        RuntimeConfigYaml = runtimeConfigYaml;
        RuntimeConfig = runtimeConfig;
    }

    /// <summary>
    /// Creates an initialization context from the current process environment.
    /// </summary>
    public static OperatorInitContext CreateFromEnvironment()
    {
        var environmentVariables = ReadEnvironmentVariables();
        environmentVariables.TryGetValue("DORA_RUNTIME_CONFIG", out var runtimeConfigYaml);

        OperatorRuntimeConfig? runtimeConfig = null;
        if (!string.IsNullOrWhiteSpace(runtimeConfigYaml))
        {
            runtimeConfig = OperatorRuntimeConfig.TryParse(runtimeConfigYaml);
        }

        return new OperatorInitContext(environmentVariables, runtimeConfigYaml, runtimeConfig);
    }

    /// <summary>
    /// Attempts to read an environment variable from the captured snapshot.
    /// </summary>
    public bool TryGetEnvironmentVariable(string name, out string value)
    {
        if (EnvironmentVariables.TryGetValue(name, out var resolved))
        {
            value = resolved;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static IReadOnlyDictionary<string, string> ReadEnvironmentVariables()
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is not null)
            {
                variables[key] = entry.Value.ToString() ?? string.Empty;
            }
        }

        return new ReadOnlyDictionary<string, string>(variables);
    }
}

/// <summary>
/// Represents the parsed Dora runtime configuration YAML relevant to an operator.
/// </summary>
public sealed class OperatorRuntimeConfig
{
    /// <summary>
    /// Gets the original YAML document.
    /// </summary>
    public string RawYaml { get; }

    /// <summary>
    /// Gets the parsed node runtime configuration.
    /// </summary>
    public OperatorRuntimeNodeConfig Node { get; }

    /// <summary>
    /// Gets the parsed operator definition, when available.
    /// </summary>
    public OperatorDefinitionConfig? Operator { get; }

    private OperatorRuntimeConfig(
        string rawYaml,
        OperatorRuntimeNodeConfig node,
        OperatorDefinitionConfig? op)
    {
        RawYaml = rawYaml;
        Node = node;
        Operator = op;
    }

    internal static OperatorRuntimeConfig? TryParse(string rawYaml)
    {
        try
        {
            var document = DoraYamlParser.Parse(rawYaml);
            var root = document as DoraYamlMapping;
            if (root is null)
            {
                return null;
            }

            var node = ParseNode(root);
            var op = ParseOperator(root);
            return new OperatorRuntimeConfig(rawYaml, node, op);
        }
        catch
        {
            return null;
        }
    }

    private static OperatorRuntimeNodeConfig ParseNode(DoraYamlMapping root)
    {
        var node = root.GetMapping("node");
        var runConfig = node?.GetMapping("run_config");

        return new OperatorRuntimeNodeConfig(
            dataflowId: node?.GetScalarValue("dataflow_id"),
            nodeId: node?.GetScalarValue("node_id"),
            inputs: ParseInputMap(runConfig?.GetMapping("inputs")),
            outputs: ParseScalarSequence(runConfig?.GetSequence("outputs")),
            isDynamic: ParseBoolean(node?.GetScalarValue("dynamic")));
    }

    private static OperatorDefinitionConfig? ParseOperator(DoraYamlMapping root)
    {
        var operators = root.GetSequence("operators");
        var firstOperator = operators?.Items.OfType<DoraYamlMapping>().FirstOrDefault();
        if (firstOperator is null)
        {
            return null;
        }

        string? sourceKind = null;
        string? sourceValue = null;

        foreach (var candidateKey in new[] { "shared-library", "python", "wasm" })
        {
            if (!firstOperator.TryGetValue(candidateKey, out var sourceNode))
            {
                continue;
            }

            sourceKind = candidateKey;
            sourceValue = sourceNode switch
            {
                DoraYamlScalar scalar => scalar.Value,
                _ => sourceNode?.ToDebugString(),
            };
            break;
        }

        return new OperatorDefinitionConfig(
            id: firstOperator.GetScalarValue("id"),
            name: firstOperator.GetScalarValue("name"),
            description: firstOperator.GetScalarValue("description"),
            inputs: ParseInputMap(firstOperator.GetMapping("inputs")),
            outputs: ParseScalarSequence(firstOperator.GetSequence("outputs")),
            sourceKind: sourceKind,
            sourceValue: sourceValue,
            build: firstOperator.GetScalarValue("build"),
            sendStdoutAs: firstOperator.GetScalarValue("send_stdout_as") ?? firstOperator.GetScalarValue("send-stdout-as"));
    }

    private static IReadOnlyDictionary<string, OperatorInputConfig> ParseInputMap(DoraYamlMapping? inputs)
    {
        var result = new Dictionary<string, OperatorInputConfig>(StringComparer.OrdinalIgnoreCase);
        if (inputs is null)
        {
            return new ReadOnlyDictionary<string, OperatorInputConfig>(result);
        }

        foreach (var pair in inputs.Entries)
        {
            var inputConfig = pair.Value switch
            {
                DoraYamlScalar scalar => new OperatorInputConfig(scalar.Value, null),
                DoraYamlMapping mapping => new OperatorInputConfig(
                    mapping.GetScalarValue("source"),
                    ParseNullableInt(mapping.GetScalarValue("queue_size"))),
                _ => new OperatorInputConfig(null, null),
            };

            result[pair.Key] = inputConfig;
        }

        return new ReadOnlyDictionary<string, OperatorInputConfig>(result);
    }

    private static IReadOnlyList<string> ParseScalarSequence(DoraYamlSequence? sequence)
    {
        if (sequence is null)
        {
            return Array.Empty<string>();
        }

        return sequence.Items
            .OfType<DoraYamlScalar>()
            .Select(item => item.Value)
            .ToArray();
    }

    private static bool? ParseBoolean(string? value)
    {
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}

/// <summary>
/// Represents the node-level runtime configuration relevant to an operator.
/// </summary>
public sealed class OperatorRuntimeNodeConfig
{
    internal OperatorRuntimeNodeConfig(
        string? dataflowId,
        string? nodeId,
        IReadOnlyDictionary<string, OperatorInputConfig> inputs,
        IReadOnlyList<string> outputs,
        bool? isDynamic)
    {
        DataflowId = dataflowId;
        NodeId = nodeId;
        Inputs = inputs;
        Outputs = outputs;
        IsDynamic = isDynamic;
    }

    /// <summary>
    /// Gets the current dataflow ID, when available.
    /// </summary>
    public string? DataflowId { get; }

    /// <summary>
    /// Gets the current node ID, when available.
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// Gets the configured node inputs.
    /// </summary>
    public IReadOnlyDictionary<string, OperatorInputConfig> Inputs { get; }

    /// <summary>
    /// Gets the configured node outputs.
    /// </summary>
    public IReadOnlyList<string> Outputs { get; }

    /// <summary>
    /// Gets whether the node is marked dynamic, when the flag was specified.
    /// </summary>
    public bool? IsDynamic { get; }
}

/// <summary>
/// Represents the operator definition section of the Dora runtime configuration.
/// </summary>
public sealed class OperatorDefinitionConfig
{
    internal OperatorDefinitionConfig(
        string? id,
        string? name,
        string? description,
        IReadOnlyDictionary<string, OperatorInputConfig> inputs,
        IReadOnlyList<string> outputs,
        string? sourceKind,
        string? sourceValue,
        string? build,
        string? sendStdoutAs)
    {
        Id = id;
        Name = name;
        Description = description;
        Inputs = inputs;
        Outputs = outputs;
        SourceKind = sourceKind;
        SourceValue = sourceValue;
        Build = build;
        SendStdoutAs = sendStdoutAs;
    }

    /// <summary>
    /// Gets the operator ID, when available.
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Gets the operator display name, when available.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the operator description, when available.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the configured operator inputs.
    /// </summary>
    public IReadOnlyDictionary<string, OperatorInputConfig> Inputs { get; }

    /// <summary>
    /// Gets the configured operator outputs.
    /// </summary>
    public IReadOnlyList<string> Outputs { get; }

    /// <summary>
    /// Gets the configured source kind, such as shared-library or python.
    /// </summary>
    public string? SourceKind { get; }

    /// <summary>
    /// Gets the configured source value associated with <see cref="SourceKind"/>.
    /// </summary>
    public string? SourceValue { get; }

    /// <summary>
    /// Gets the configured build command, when present.
    /// </summary>
    public string? Build { get; }

    /// <summary>
    /// Gets the configured send-stdout-as value, when present.
    /// </summary>
    public string? SendStdoutAs { get; }
}

/// <summary>
/// Represents the configuration for a single operator input.
/// </summary>
public sealed class OperatorInputConfig
{
    internal OperatorInputConfig(string? source, int? queueSize)
    {
        Source = source;
        QueueSize = queueSize;
    }

    /// <summary>
    /// Gets the configured upstream source ID, when present.
    /// </summary>
    public string? Source { get; }

    /// <summary>
    /// Gets the configured queue size, when present.
    /// </summary>
    public int? QueueSize { get; }
}

internal abstract class DoraYamlNode
{
    public virtual string? ToDebugString() => null;
}

internal sealed class DoraYamlScalar : DoraYamlNode
{
    public DoraYamlScalar(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public override string? ToDebugString() => Value;
}

internal sealed class DoraYamlMapping : DoraYamlNode
{
    public Dictionary<string, DoraYamlNode> Entries { get; } = new(StringComparer.Ordinal);

    public bool TryGetValue(string key, out DoraYamlNode? node)
    {
        if (Entries.TryGetValue(key, out var resolved))
        {
            node = resolved;
            return true;
        }

        node = null;
        return false;
    }

    public DoraYamlMapping? GetMapping(string key) =>
        TryGetValue(key, out var node) ? node as DoraYamlMapping : null;

    public DoraYamlSequence? GetSequence(string key) =>
        TryGetValue(key, out var node) ? node as DoraYamlSequence : null;

    public string? GetScalarValue(string key) =>
        TryGetValue(key, out var node) ? (node as DoraYamlScalar)?.Value : null;

    public override string? ToDebugString() => "{...}";
}

internal sealed class DoraYamlSequence : DoraYamlNode
{
    public List<DoraYamlNode> Items { get; } = new();

    public override string? ToDebugString() => $"[{Items.Count}]";
}

internal static class DoraYamlParser
{
    public static DoraYamlNode Parse(string yaml)
    {
        var parser = new Parser(yaml);
        return parser.ParseDocument();
    }

    private sealed class Parser
    {
        private readonly List<Line> _lines;
        private int _index;

        public Parser(string yaml)
        {
            _lines = Tokenize(yaml);
        }

        public DoraYamlNode ParseDocument()
        {
            SkipBlankLines();
            if (_index >= _lines.Count)
            {
                return new DoraYamlMapping();
            }

            return ParseNode(_lines[_index].Indent);
        }

        private DoraYamlNode ParseNode(int indent)
        {
            SkipBlankLines();
            if (_index >= _lines.Count)
            {
                return new DoraYamlScalar(string.Empty);
            }

            var line = _lines[_index];
            if (line.Indent < indent)
            {
                return new DoraYamlScalar(string.Empty);
            }

            return line.Content.StartsWith("- ", StringComparison.Ordinal)
                ? ParseSequence(indent)
                : ParseMapping(indent);
        }

        private DoraYamlMapping ParseMapping(int indent)
        {
            var mapping = new DoraYamlMapping();

            while (_index < _lines.Count)
            {
                SkipBlankLines();
                if (_index >= _lines.Count)
                {
                    break;
                }

                var line = _lines[_index];
                if (line.Indent < indent || line.Indent != indent || line.Content.StartsWith("- ", StringComparison.Ordinal))
                {
                    break;
                }

                var separatorIndex = line.Content.IndexOf(':');
                if (separatorIndex < 0)
                {
                    _index += 1;
                    continue;
                }

                var key = line.Content[..separatorIndex].Trim();
                var remainder = line.Content[(separatorIndex + 1)..].TrimStart();
                _index += 1;

                DoraYamlNode value;
                if (remainder is "|" or "|-" or ">")
                {
                    value = new DoraYamlScalar(ParseBlockScalar(indent));
                }
                else if (remainder.Length > 0)
                {
                    value = new DoraYamlScalar(ParseScalar(remainder));
                }
                else
                {
                    SkipBlankLines();
                    if (_index < _lines.Count && IsChildNode(indent, _lines[_index]))
                    {
                        value = ParseNode(_lines[_index].Indent);
                    }
                    else
                    {
                        value = new DoraYamlScalar(string.Empty);
                    }
                }

                mapping.Entries[key] = value;
            }

            return mapping;
        }

        private DoraYamlSequence ParseSequence(int indent)
        {
            var sequence = new DoraYamlSequence();

            while (_index < _lines.Count)
            {
                SkipBlankLines();
                if (_index >= _lines.Count)
                {
                    break;
                }

                var line = _lines[_index];
                if (line.Indent < indent || line.Indent != indent || !line.Content.StartsWith("- ", StringComparison.Ordinal))
                {
                    break;
                }

                var itemContent = line.Content[2..].TrimStart();
                _index += 1;

                if (itemContent.Length == 0)
                {
                    SkipBlankLines();
                    if (_index < _lines.Count && _lines[_index].Indent > indent)
                    {
                        sequence.Items.Add(ParseNode(_lines[_index].Indent));
                    }
                    else
                    {
                        sequence.Items.Add(new DoraYamlScalar(string.Empty));
                    }

                    continue;
                }

                if (TryParseInlineMapping(itemContent, out var key, out var remainder))
                {
                    var itemMapping = new DoraYamlMapping();
                    DoraYamlNode firstValue;

                    if (remainder is "|" or "|-" or ">")
                    {
                        firstValue = new DoraYamlScalar(ParseBlockScalar(indent));
                    }
                    else if (remainder.Length > 0)
                    {
                        firstValue = new DoraYamlScalar(ParseScalar(remainder));
                    }
                    else
                    {
                        SkipBlankLines();
                        if (_index < _lines.Count && IsChildNode(indent, _lines[_index]))
                        {
                            firstValue = ParseNode(_lines[_index].Indent);
                        }
                        else
                        {
                            firstValue = new DoraYamlScalar(string.Empty);
                        }
                    }

                    itemMapping.Entries[key] = firstValue;

                    SkipBlankLines();
                    if (_index < _lines.Count && _lines[_index].Indent > indent)
                    {
                        var extra = ParseMapping(_lines[_index].Indent);
                        foreach (var pair in extra.Entries)
                        {
                            itemMapping.Entries[pair.Key] = pair.Value;
                        }
                    }

                    sequence.Items.Add(itemMapping);
                    continue;
                }

                sequence.Items.Add(new DoraYamlScalar(ParseScalar(itemContent)));
            }

            return sequence;
        }

        private string ParseBlockScalar(int parentIndent)
        {
            var collected = new List<string>();
            var minimumContentIndent = int.MaxValue;
            var probeIndex = _index;

            while (probeIndex < _lines.Count)
            {
                var line = _lines[probeIndex];
                if (line.IsBlank)
                {
                    probeIndex += 1;
                    continue;
                }

                if (line.Indent <= parentIndent)
                {
                    break;
                }

                minimumContentIndent = Math.Min(minimumContentIndent, line.Indent);
                probeIndex += 1;
            }

            if (minimumContentIndent == int.MaxValue)
            {
                return string.Empty;
            }

            while (_index < _lines.Count)
            {
                var line = _lines[_index];
                if (line.IsBlank)
                {
                    collected.Add(string.Empty);
                    _index += 1;
                    continue;
                }

                if (line.Indent <= parentIndent)
                {
                    break;
                }

                var sliceStart = Math.Min(line.Raw.Length, minimumContentIndent);
                collected.Add(line.Raw[sliceStart..]);
                _index += 1;
            }

            return string.Join(Environment.NewLine, collected);
        }

        private static bool TryParseInlineMapping(string content, out string key, out string remainder)
        {
            var separatorIndex = content.IndexOf(':');
            if (separatorIndex <= 0)
            {
                key = string.Empty;
                remainder = string.Empty;
                return false;
            }

            key = content[..separatorIndex].Trim();
            if (key.Length == 0 || key.Contains(' '))
            {
                remainder = string.Empty;
                return false;
            }

            remainder = content[(separatorIndex + 1)..].TrimStart();
            return true;
        }

        private static string ParseScalar(string value)
        {
            if (value is "~" or "null" or "Null" or "NULL")
            {
                return string.Empty;
            }

            if (value.Length >= 2)
            {
                if ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
                {
                    return value[1..^1];
                }
            }

            return value;
        }

        private void SkipBlankLines()
        {
            while (_index < _lines.Count && _lines[_index].IsBlank)
            {
                _index += 1;
            }
        }

        private static bool IsChildNode(int parentIndent, Line line)
        {
            if (line.Indent > parentIndent)
            {
                return true;
            }

            return line.Indent == parentIndent
                && line.Content.StartsWith("- ", StringComparison.Ordinal);
        }

        private static List<Line> Tokenize(string yaml)
        {
            var lines = new List<Line>();
            foreach (var rawLine in yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                if (rawLine.StartsWith("---", StringComparison.Ordinal))
                {
                    continue;
                }

                var indent = 0;
                while (indent < rawLine.Length && rawLine[indent] == ' ')
                {
                    indent += 1;
                }

                var content = indent < rawLine.Length ? rawLine[indent..] : string.Empty;
                lines.Add(new Line(rawLine, content, indent));
            }

            return lines;
        }
    }

    private sealed record Line(string Raw, string Content, int Indent)
    {
        public bool IsBlank => string.IsNullOrWhiteSpace(Content);
    }
}
