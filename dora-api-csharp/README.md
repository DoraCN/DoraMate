# Dora C# 绑定

独立的 Dora C# 绑定仓库，目录位于 `DoraMate/dora-api-csharp`。

## 概览

这个仓库提供 Dora 的 C# 语言绑定，当前可以用于两类场景：

- 通过 `DoraNode` 编写独立 C# 节点
- 通过 `DoraOperator` 编写可被 Dora runtime 加载的 NativeAOT 共享库 Operator

当前仓库已经具备与上游 `apis/csharp` 基本一致的源码能力，并补齐了：

- `DoraNode` 同步 / 第一阶段异步事件读取
- `DoraOperator` NativeAOT 导出与运行时桥接
- Arrow `RecordBatch` 在 node / operator 间的读写
- contract / projector / assertion 等高阶 Arrow helper
- smoke 与 regression 覆盖

## 目录结构

- `src/DoraNode/`
  - C# 节点侧托管绑定库
- `src/DoraOperator/`
  - C# Operator 侧托管绑定库
- `samples/`
  - 独立仓库内的 C# dataflow 示例
- `scripts/`
  - bootstrap、native build、smoke、regression 脚本
- `tests/`
  - `DoraNode` / `DoraOperator` 回归 runner
- `artifacts/`
  - 统一的 .NET、sample、native 输出目录
- `third_party/dora/`
  - vendored Dora 上游源码快照

## 构建

### 前置要求

- .NET SDK 8.0 或更高版本
- Rust toolchain
- Dora CLI
- Windows 上建议使用 `pwsh`
- NativeAOT 所需本机工具链
  - Windows: Visual Studio Build Tools / MSVC
  - Linux/macOS: 本机 C/C++ 工具链

### 推荐构建顺序

1. 拉取 / 刷新 `third_party/dora`
2. 构建 native C ABI
3. 构建 `dora-api-csharp.sln`
4. 跑统一 smoke

首次初始化建议：

```powershell
pwsh ./scripts/bootstrap-dora.ps1
pwsh ./scripts/build-native.ps1
dotnet build ./dora-api-csharp.sln -c Release
pwsh ./scripts/smoke-csharp-bindings.ps1
```

如果本机没有可用的 `dora` CLI，可在部分 smoke 中使用 `-BuildCli` 或显式传入 `-DoraPath`。

## 使用

### 编写 Node

最小 node 代码形态如下：

```csharp
using System.Text;
using DoraNode;

using var node = new DoraNode.DoraNode();

while (true)
{
    using var ev = node.Next();
    if (ev is null)
    {
        break;
    }

    if (ev.Type == EventType.Input)
    {
        var payload = Encoding.UTF8.GetString(ev.Data ?? Array.Empty<byte>());
        Console.WriteLine($"input={ev.Id}, payload={payload}");
        node.SendOutputOrThrow("output", $"echo:{payload}");
    }
    else if (ev.Type == EventType.Stop)
    {
        break;
    }
}
```

`DoraNode` 当前支持：

- `Next()`
- `NextAsync(...)`
- `ReadAllEventsAsync(...)`
- bytes / string / Arrow / `RecordBatch` 输出发送

### 编写 Operator

最小 operator 代码形态如下：

```csharp
using DoraOperator;

public sealed class MyOperator : DoraOperatorBase
{
    protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        var text = ev.Input.GetUtf8String();
        output.SendOrThrow("output", $"processed:{text}");
        return OnEventResult.Continue();
    }
}
```

NativeAOT 导出可直接复用 `OperatorEntrypoint<T>`：

```csharp
using System.Runtime.CompilerServices;
using DoraOperator;

public static class MyOperatorExports
{
    [UnmanagedCallersOnly(EntryPoint = "dora_init_operator", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeDoraInitResult Init()
        => OperatorEntrypoint<MyOperator>.InitOperator();

    [UnmanagedCallersOnly(EntryPoint = "dora_drop_operator", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeDoraResult Drop(nint operatorContext)
        => OperatorEntrypoint<MyOperator>.DropOperator(operatorContext);

    [UnmanagedCallersOnly(EntryPoint = "dora_on_event", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static NativeTypes.NativeOnEventResult OnEvent(nint rawEvent, nint sendOutput, nint operatorContext)
        => OperatorEntrypoint<MyOperator>.OnEvent(rawEvent, sendOutput, operatorContext);
}
```

## 推荐验证方式

优先用统一 smoke，而不是只看 `dotnet build`。

```powershell
pwsh ./scripts/smoke-csharp-bindings.ps1
```

这会覆盖：

- operator Arrow round-trip
- operator contract Arrow
- node Arrow round-trip
- `node -> operator -> node` Arrow
- advanced Arrow
- complex Arrow contract
- async node

也可以单独跑：

```powershell
pwsh ./scripts/test-doranode-regression.ps1
pwsh ./scripts/test-doraoperator-regression.ps1
pwsh ./scripts/smoke-doraoperator-arrow-roundtrip.ps1
pwsh ./scripts/smoke-doraoperator-contract-arrow.ps1
pwsh ./scripts/smoke-csharp-node-arrow.ps1
pwsh ./scripts/smoke-csharp-node-operator-arrow.ps1
```

## 错误码处理建议

消费托管绑定时，优先根据稳定的 `ErrorCode` 分支，不要依赖异常字符串。

Node 侧示例：

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
    throw;
}
catch (DoraException ex) when (ex.ErrorCode == DoraNodeErrorCode.LifecycleViolation)
{
    // disposed / mixed sync-async reads / invalid lifecycle usage
}
```

Operator 侧示例：

```csharp
catch (DoraOperatorException ex) when (ex.ErrorCode == DoraOperatorErrorCode.LifecycleViolation)
{
    // cached Input / event used outside callback lifetime
}
```

建议的错误语义：

- `InvalidNativeHandle`
  - native 句柄缺失、损坏或运行时失败，应视为 runtime / ABI 失败
- `LifecycleViolation`
  - 调用方在对象生命周期之外访问托管包装，应修复调用方式而不是重试

## 平台说明

### Windows

- 当前验证最充分的平台
- native 库文件名：
  - `dora_node_api_c.dll`
  - `dora_operator_api_c.dll`
- loader 会优先探测仓库内 `artifacts/native/...`、`third_party/dora/target/...` 及常见输出目录

### Linux / macOS

- 基础加载逻辑已具备
- 需要自行确认本机工具链与 `.so` / `.dylib` 可发现性

## 常见问题

### `DllNotFoundException`

优先检查：

1. 是否已经执行 `pwsh ./scripts/build-native.ps1`
2. `artifacts/native/<rid>/` 是否存在对应动态库
3. 进程架构与 native 库架构是否一致

### `BadImageFormatException`

通常是架构不匹配，例如 x64 进程加载了 x86 动态库。

### NativeAOT Operator 构建失败

优先检查：

1. 本机 NativeAOT 工具链是否完整
2. 是否使用了示例中已有的 `dotnet publish -c Release`
3. 是否把 Operator 当成普通托管插件 DLL 处理

## 进一步阅读

- 构建说明：`BUILD.md`
- 快速开始：`QUICKSTART.md`
- 结构说明：`PROJECT_STRUCTURE.md`
