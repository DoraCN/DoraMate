# C# Dataflow Example

This example demonstrates how to create a simple Dora node in C# that generates periodic data and processes it.

## Prerequisites

1. .NET SDK 8.0 or higher
2. Rust toolchain (to build the native C library)
3. Dora CLI installed

## Building the Native Library

First, build the Dora C API:

```bash
cd apis/c/node
cargo build --release
```

### Linux/macOS
The native library will be at: `target/release/libdora_node_api_c.so` (Linux) or `.dylib` (macOS)

### Windows
The native library will be at: `target/release\dora_node_api_c.dll`

## Building the C# Node

```bash
cd apis/csharp/examples/csharp-dataflow
dotnet build -c Release
```

## Running the Dataflow

Make sure the native library is in your PATH or LD_LIBRARY_PATH:

### Linux/macOS
```bash
export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:$(pwd)/../../../c/node/target/release
dora run ./dataflow.yml
```

### Windows (PowerShell)
```powershell
$env:PATH += ";$(cd ../../../c/node/target/release; pwd)"
dora run .\dataflow.yml
```

## Expected Output

You should see output similar to:

```
[14:23:15.123] Received input 'tick' with 0 bytes
  -> Sent output: C# processed: ...
[14:23:15.223] Received input 'tick' with 0 bytes
  -> Sent output: C# processed: ...
```

## Troubleshooting

### DllNotFoundException
If you get a `DllNotFoundException`, ensure:
1. The native library is built (`cargo build --release` in `apis/c/node`)
2. The native library is in the PATH/`LD_LIBRARY_PATH`
3. The architecture matches (x64 vs x86)

### Other Errors
- Check that .NET 8.0 SDK is installed: `dotnet --version`
- Ensure the Dora daemon is running: `dora daemon`
- Check dataflow YAML syntax

## Customizing

Edit `Program.cs` to customize the node behavior. The main loop:
1. Waits for events using `node.Next()`
2. Handles input events
3. Sends outputs using `node.SendOutput()`
4. Stops on a Stop event

## Node Async Error Handling

If you switch this example to `NextAsync(...)` or `ReadAllEventsAsync(...)`, prefer branching on stable `DoraNodeErrorCode` values instead of matching exception text:

```csharp
try
{
    await foreach (var ev in node.ReadAllEventsAsync(stoppingToken))
    {
        using (ev)
        {
            // Handle event...
        }
    }
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.InvalidNativeHandle)
{
    logger.LogError(ex, "Dora async read failed due to a native handle/runtime failure.");
    throw;
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
{
    logger.LogWarning(ex, "The node was disposed or sync/async reads were mixed on the same DoraNode instance.");
}
```

Interpretation:

- `InvalidNativeHandle`: treat as a runtime/ABI failure on the native event-stream side.
- `LifecycleViolation`: fix caller logic; typical causes are disposal races or mixing `Next()` and `NextAsync()` on the same node.
