# C# Dora Node Benchmark

Measures baseline C# Node latency and throughput with the same payload sizes used by Dora's Rust benchmark example.

Run from the repository root:

```powershell
pwsh ./dora-api-csharp/scripts/benchmark-csharp-bindings.ps1
```

The sink prints human-readable lines plus CSV-like `BENCH,...` rows that the script collects into `artifacts/benchmark`.
