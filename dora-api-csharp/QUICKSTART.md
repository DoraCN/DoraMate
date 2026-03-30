# Dora C# 绑定快速开始

本指南的目标不是讲完整实现细节，而是先用最短路径确认：

- C# node 能跑
- C# operator 能跑
- Arrow 数据面能跑
- async node API 能跑

## 适用范围

当前 `dora-api-csharp` 已支持：

- 用 `DoraNode` 编写独立 C# 节点
- 用 `DoraOperator` 编写 NativeAOT 共享库 Operator
- 用 Arrow `RecordBatch` 在 node / operator 间传输结构化数据
- 用 `NextAsync(...)` / `ReadAllEventsAsync(...)` 做第一阶段异步事件消费

如果你的目标只是“先确认绑定能跑”，优先直接运行仓库已有示例和 smoke，不要先手工新建项目。

## 前置要求

- .NET SDK 8.0 或更高版本
- Rust toolchain
- Dora CLI
- Windows 上建议使用 `pwsh`

## 最短验证路径

### 1. 刷新上游 Dora

```powershell
pwsh ./scripts/bootstrap-dora.ps1
```

### 2. 构建 native C ABI

```powershell
pwsh ./scripts/build-native.ps1
```

### 3. 构建全部 C# 项目

```powershell
dotnet build ./dora-api-csharp.sln -c Release
```

### 4. 运行统一 smoke

```powershell
pwsh ./scripts/smoke-csharp-bindings.ps1
```

如果这里全绿，说明当前机器上的：

- `DoraNode`
- `DoraOperator`
- Arrow 数据面
- async node

都已经处于可运行状态。

## 推荐第一个运行示例

### A. 最小 Node 示例

```powershell
dora run ./samples/csharp-dataflow/dataflow.yml
```

它适合了解：

- `DoraNode.Next()`
- `DoraEvent`
- `SendOutput(...)`

### B. 最小 Operator 示例

```powershell
dora run ./samples/csharp-operator-dataflow/dataflow.yml
```

它适合确认：

- `DoraOperatorBase`
- NativeAOT Operator 导出
- `OperatorOutput.Send(...)`

### C. Arrow 端到端示例

```powershell
dora run ./samples/csharp-node-operator-arrow-dataflow/dataflow.yml
```

这个示例打通了：

- `DoraNode.SendRecordBatch(...)`
- `Input.TryReadRecordBatch(...)`
- `SendRecordBatch(...)`
- `DoraEvent.TryReadRecordBatch(...)`

### D. Async Node 示例

```powershell
dora run ./samples/csharp-async-node-dataflow/dataflow.yml
```

它用于确认：

- `NextAsync(...)`
- `ReadAllEventsAsync(...)`
- 取消、流关闭、生命周期边界

## 自己写一个最小 C# node

### 1. 新建控制台项目

```powershell
dotnet new console -o MyDoraNode
```

### 2. 引用 `DoraNode`

在 `.csproj` 中加入：

```xml
<ItemGroup>
  <ProjectReference Include="..\dora-api-csharp\src\DoraNode\DoraNode.csproj" />
</ItemGroup>
```

按你的真实目录关系调整路径。

### 3. 最小 node 代码

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
        var text = Encoding.UTF8.GetString(ev.Data ?? Array.Empty<byte>());
        Console.WriteLine($"input id={ev.Id} payload={text}");
        node.SendOutputOrThrow("output", $"echo:{text}");
    }
    else if (ev.Type == EventType.Stop)
    {
        break;
    }
}
```

## 自己写一个最小 C# operator

### 1. 继承 `DoraOperatorBase`

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

### 2. 导出 native ABI

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

## 推荐学习顺序

建议按这个顺序熟悉仓库：

1. `README.md`
2. `PROJECT_STRUCTURE.md`
3. `samples/csharp-dataflow/`
4. `samples/csharp-operator-dataflow/`
5. `samples/csharp-node-operator-arrow-dataflow/`
6. `samples/csharp-async-node-dataflow/`
7. `scripts/smoke-csharp-bindings.ps1`

## 常见不要这样做

### 1. 不要跳过 smoke 直接怀疑库本身

先跑仓库脚本，再排查你自己的 dataflow。

### 2. 不要混用 `Next()` 和 `NextAsync()`

同一个 `DoraNode` 实例只允许一种读取模式。

### 3. 不要把 `DoraEvent` / `Input` 缓存到生命周期之外再访问 native 数据

这类问题会被归类为：

- `LifecycleViolation`
- `InvalidNativeHandle`

### 4. 不要把 Operator 当作普通托管插件 DLL

当前 C# Operator 是 NativeAOT + native ABI 导出模型。

## 进一步阅读

- 总览：`README.md`
- 构建说明：`BUILD.md`
- 项目结构：`PROJECT_STRUCTURE.md`
- 迁移清单：`MIGRATION_CHECKLIST.md`
