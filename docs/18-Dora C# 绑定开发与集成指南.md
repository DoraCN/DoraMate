# 18 - Dora C# 绑定开发与集成指南

> 本指南面向当前 `dora-api-csharp/` 工作区，结合收尾计划、OpenTelemetry 集成状态和性能基准结果重新整理。
> 最后更新：2026-07-06
> 当前源码版本：`0.10.0`

---

## 1. 文档定位

`dora-api-csharp/` 是 DoraMate 仓库中的 Dora C# 语言绑定工作区。它不是前端模块，也不是 LocalAgent，而是一套面向 C# 开发者的 SDK、模板、样例、测试和发布脚本集合。

本文回答四个问题：

- C# 绑定当前能用于哪些 Dora node / operator 场景
- 如何从 NuGet、模板、样例和源码工作区开始开发
- 构建、smoke、回归、性能基准应按什么顺序执行
- 当前有哪些明确边界，尤其是异步读取、NativeAOT、Arrow 和 OpenTelemetry

本文不是上游 Dora 官方文档的复述，而是 DoraMate 当前仓库的落地口径。

---

## 2. 当前结论

截至当前仓库状态，Dora C# 绑定已经具备 v1.0 级别的主要能力：

| 模块 | 状态 | 说明 |
| ---- | ---- | ---- |
| DoraNode SDK | 可用 | 支持同步/异步读取、bytes/string/Arrow 输出、诊断错误码、OpenTelemetry 上下文 |
| DoraOperator SDK | 可用 | 支持 NativeAOT operator、类型化事件、输出发送、OpenTelemetry 上下文 |
| Arrow 集成 | 可用 | 支持 Arrow C ABI、IPC、RecordBatch、Contract、Projector、Assertions 和高级类型覆盖 |
| NuGet 包 | 已产品化 | `DoraMate.DoraNode` / `DoraMate.DoraOperator` 已公开发布过 `v0.9.0`，当前源码版本为 `0.10.0` |
| dotnet new 模板 | 可用 | `DoraMate.Templates` 当前源码版本为 `0.10.0`，提供 `dora-node` / `dora-operator` |
| OpenTelemetry | 已完成 | Node / Operator 接入 .NET `Activity` / `ActivitySource`，并支持 metadata-aware 输出传播 |
| CI / smoke | 已接入 | 完整示例构建、最小 bytes smoke、OTel Node / Operator smoke、跨平台 matrix |
| 回归测试 | 已迁移 | xUnit：DoraNode 13 个、DoraOperator 16 个测试用例 |
| 性能基准 | 已建立 baseline | C# bytes 链路与 Rust 原生 benchmark 可复现对比 |
| C7 异步深度重构 | 暂不开发 | Dora Runtime / C ABI 当前没有真正原生异步事件订阅能力，C# 单侧重构无法达成目标 |

关键判断：

> 当前 C# 绑定已经不是实验性 P/Invoke 骨架，而是一套可构建、可运行、可发布、可回归验证的 Dora C# SDK。后续重点不再是补齐基础能力，而是围绕性能、文档、跨平台结果和上游 Runtime 能力继续迭代。

---

## 3. 仓库定位与目录

`dora-api-csharp/` 可以按职责理解为以下几个部分：

| 目录 | 职责 |
| ---- | ---- |
| `src/DoraNode/` | 独立 C# node SDK，面向可执行进程节点 |
| `src/DoraOperator/` | C# NativeAOT operator SDK，面向 Dora runtime 加载的共享库 |
| `samples/` | 可运行样例，也是 smoke 和能力地图 |
| `tests/` | xUnit 回归测试项目 |
| `scripts/` | native 构建、NuGet 打包、模板安装、smoke、benchmark 脚本 |
| `templates/` | `dotnet new` 模板包 |
| `third_party/dora/` | Dora 上游快照，用于构建 native C ABI |
| `artifacts/` | native、dotnet、sample、NuGet、smoke、benchmark 输出 |

与 DoraMate 其他模块的关系：

- `doramate-frontend` 负责可视化编辑、节点模板入口和 YAML 生成。
- `doramate-localagent` 负责本地运行时桥接和 API。
- `dora-api-csharp` 负责 C# 语言绑定、样例、模板、native ABI 构建和验证基线。

---

## 4. 两种开发模型

### 4.1 独立 C# Node

适合：

- 用 C# 写独立可执行节点
- 以普通进程方式参与 Dora dataflow
- 需要直接控制事件循环、输出发送和资源释放

最小示例：

```csharp
using DoraNode;

using var node = new DoraNode.DoraNode();

while (true)
{
    using var ev = node.Next();
    if (ev is null || ev.Type == EventType.Stop)
    {
        break;
    }

    if (ev.Type == EventType.Input)
    {
        using var activity = ev.StartActivity("process-input");
        node.SendOutputOrThrow("output", "hello from csharp");
    }
}
```

主要 API：

- `Next()`
- `NextAsync(CancellationToken)`
- `ReadAllEventsAsync(CancellationToken)`
- `SendOutput(...)` / `SendOutputOrThrow(...)`
- `SendOutputWithCurrentActivity(...)`
- `SendArrow(...)`
- `SendRecordBatch(...)`
- `DoraEvent.OpenTelemetryContext`
- `DoraEvent.StartActivity(...)`

### 4.2 C# NativeAOT Operator

适合：

- 用 C# 写可被 Dora runtime 直接加载的 operator
- 需要更紧密的运行时集成
- 能接受 NativeAOT、共享库和 C ABI 导出约束

最小业务代码：

```csharp
using DoraOperator;

public sealed class MyOperator : DoraOperatorBase
{
    protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
    {
        using var activity = ev.Input.StartActivity("operator-input");
        output.SendWithCurrentActivity("output", ev.Input.GetData());
        return OnEventResult.Continue();
    }
}
```

NativeAOT operator 还需要导出入口：

```csharp
using System.Runtime.CompilerServices;
using DoraOperator;

public static class NativeExports
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

主要 API：

- `DoraOperatorBase`
- `OnInput(...)` / `OnInputClosed(...)` / `OnStop(...)` / `OnError(...)`
- `InputEvent` / `InputClosedEvent` / `StopEvent` / `ErrorEvent`
- `OperatorOutput.SendOrThrow(...)`
- `OperatorOutput.SendWithCurrentActivity(...)`
- `OperatorEntrypoint<T>`
- `Input.OpenTelemetryContext`
- `Input.StartActivity(...)`

---

## 5. 安装与创建项目

### 5.1 从 NuGet 使用

当前公开发布过的稳定包口径为 `v0.9.0`：

```powershell
dotnet add package DoraMate.DoraNode --version 0.9.0
dotnet add package DoraMate.DoraOperator --version 0.9.0
dotnet new install DoraMate.Templates::0.9.0
```

说明：

- `docs/34` 记录的公开发布版本是 `v0.9.0`。
- 当前仓库源码和模板已同步到 `0.10.0`。
- 若需要使用当前 `0.10.0` 源码版本，应从本仓库本地打包并使用本地 NuGet 源；若要公开发布 `0.10.0`，需设置 `NUGET_API_KEY` 后执行发布脚本。

### 5.2 使用 dotnet new 模板

创建 C# node：

```powershell
dotnet new dora-node -n MyDoraNode
cd MyDoraNode
dotnet build
```

创建 C# operator：

```powershell
dotnet new dora-operator -n MyDoraOperator
cd MyDoraOperator
dotnet publish -c Release
```

从源码工作区安装当前模板：

```powershell
cd dora-api-csharp
pwsh ./scripts/build-templates.ps1
```

或直接从模板目录安装：

```powershell
cd dora-api-csharp
pwsh ./scripts/install-templates.ps1
```

---

## 6. 源码工作区构建顺序

推荐在仓库根目录执行：

```powershell
cd dora-api-csharp
pwsh ./scripts/bootstrap-dora.ps1
pwsh ./scripts/build-native.ps1
dotnet restore ./dora-api-csharp.sln -p:NuGetAudit=false
dotnet build ./dora-api-csharp.sln -c Release -p:NuGetAudit=false
pwsh ./scripts/smoke-csharp-bindings.ps1
```

这几步分别解决：

1. 准备 Dora 上游 native source。
2. 构建 node/operator C ABI 动态库。
3. 还原 NuGet 依赖。
4. 编译 SDK、样例、测试和模板项目。
5. 运行真实 Dora dataflow smoke，验证不仅能编译，也能运行。

不要只用 `dotnet build` 判断绑定可用性。`dotnet build` 只能证明 C# 项目能编译，不能证明 native 库可加载、operator 可被 Dora runtime 调用、Arrow payload 可往返、OpenTelemetry context 可连续传播。

---

## 7. 样例地图

当前 `samples/` 覆盖 13 个场景：

| 样例 | 说明 |
| ---- | ---- |
| `csharp-dataflow/` | 最小 C# node |
| `csharp-multi-node/` | 多 C# node bytes 链路 |
| `csharp-operator-dataflow/` | 最小 C# NativeAOT operator |
| `csharp-arrow-node-dataflow/` | node -> node Arrow RecordBatch |
| `csharp-advanced-arrow-node-dataflow/` | Union、FixedSizeBinary、Duration、Interval 等高级 Arrow 类型 |
| `csharp-complex-arrow-contract-node-dataflow/` | Node 侧 contract / typed model |
| `csharp-node-operator-arrow-dataflow/` | node -> operator -> node Arrow 链路 |
| `csharp-operator-arrow-roundtrip/` | Operator 内部 Arrow round-trip |
| `csharp-operator-contract-arrow-dataflow/` | Operator 侧 contract / projector / assertion |
| `csharp-async-node-dataflow/` | 当前异步读取 API 与生命周期边界 |
| `csharp-otel-dataflow/` | C# Node OpenTelemetry trace continuity |
| `csharp-otel-operator-dataflow/` | C# Operator OpenTelemetry trace continuity |
| `csharp-benchmark-dataflow/` | C# bytes latency / throughput baseline |

推荐学习顺序：

1. `csharp-dataflow/`
2. `csharp-operator-dataflow/`
3. `csharp-arrow-node-dataflow/`
4. `csharp-node-operator-arrow-dataflow/`
5. `csharp-otel-dataflow/`
6. `csharp-async-node-dataflow/`
7. `csharp-benchmark-dataflow/`

---

## 8. 验证入口

### 8.1 xUnit 回归

```powershell
cd dora-api-csharp
dotnet test ./tests/DoraNode.RegressionRunner/DoraNode.RegressionRunner.csproj -c Release -p:NuGetAudit=false
dotnet test ./tests/DoraOperator.RegressionRunner/DoraOperator.RegressionRunner.csproj -c Release -p:NuGetAudit=false
```

当前测试规模：

- DoraNode：13 个测试用例
- DoraOperator：16 个测试用例

覆盖重点：

- Arrow schema / projector / contract
- 高级 Arrow 类型
- OpenTelemetry context 解析和注入
- 生命周期与诊断错误语义

### 8.2 统一 smoke

```powershell
cd dora-api-csharp
pwsh ./scripts/smoke-csharp-bindings.ps1
```

统一 smoke 是当前最重要的运行链路验收入口。它比单纯 `dotnet test` 更能发现 native 库、Dora CLI、NativeAOT、dataflow YAML 和进程路径问题。

### 8.3 OpenTelemetry smoke

```powershell
cd dora-api-csharp
pwsh ./scripts/smoke-csharp-otel-dataflow.ps1
pwsh ./scripts/smoke-csharp-otel-operator-dataflow.ps1
```

这两个脚本会验证：

- producer、C# transform/operator、consumer 在同一 trace 中
- 下游 parent span 指向 C# 当前 activity
- Node 和 Operator 的 metadata-aware send 没有断链

### 8.4 性能基准

```powershell
pwsh ./dora-api-csharp/scripts/benchmark-csharp-bindings.ps1 `
  -SkipBuild `
  -IncludeRust `
  -ThroughputMessages 100 `
  -TimeoutSeconds 180 `
  -DoraPath ./dora-api-csharp/third_party/dora/target/release/dora.exe
```

输出位置：

- `dora-api-csharp/artifacts/benchmark/`

`docs/36` 的 baseline 结论：

- C# bytes 链路已能完成 10 档 payload 的 latency / throughput 测量。
- 小 payload 存在明显固定开销，可能来自 P/Invoke、事件对象创建、byte materialization 和调度成本。
- 512B 以上多数 payload 与 Rust 差距收敛到约 1.0x-1.8x。
- 16KB 以上大 payload throughput 基本接近 Rust。
- 当前数据是 baseline，不应作为最终性能门禁；后续需要多轮、warmup、GC/allocation 指标。

---

## 9. Arrow 数据面

Node 与 Operator 两侧都支持 Arrow 数据通道。能力包括：

- Arrow C ABI 托管包装
- Arrow IPC bytes
- `RecordBatch` 读写
- Schema validation
- Contract / typed model
- Projector
- Assertions
- Summary
- 高级类型覆盖：Union、FixedSizeBinary、Duration、Interval 等

推荐实践：

- 普通 bytes/string 用 `SendOutput(...)`。
- 结构化数据优先使用 `RecordBatch`。
- 跨模块契约优先使用 contract / projector / assertion helper。
- 不要为已有 Arrow 场景重复设计私有二进制协议。

---

## 10. OpenTelemetry 集成

OpenTelemetry 已完成端到端集成，核心目标是让 C# 不成为 Dora trace 链路的断点。

### 10.1 Node 使用方式

```csharp
using DoraNode;

using var node = new DoraNode.DoraNode();

while (node.Next() is { } ev)
{
    using var activity = ev.StartActivity("csharp-node.process");
    activity?.SetTag("dora.input.id", ev.Id);

    var payload = ev.Data ?? Array.Empty<byte>();
    node.SendOutputWithCurrentActivity("output", payload);
}
```

### 10.2 Operator 使用方式

```csharp
protected override OnEventResult OnInput(InputEvent ev, OperatorOutput output)
{
    using var activity = ev.Input.StartActivity("csharp-operator.process");
    activity?.SetTag("dora.input.id", ev.Input.Id);

    output.SendWithCurrentActivity("output", ev.Input.GetData());
    return OnEventResult.Continue();
}
```

### 10.3 设计口径

- SDK 核心只依赖 .NET BCL 的 `System.Diagnostics`。
- 不强制引入 OpenTelemetry exporter 或 hosting 包。
- 用户如果要导出到 Jaeger / Tempo / Collector，可自行安装 OpenTelemetry 生态包。
- 当前 Node / Operator 都提供 `DoraTelemetry.ActivitySource`，名称分别为 `DoraMate.DoraNode` 和 `DoraMate.DoraOperator`。
- 输出发送会优先注入当前 `Activity.Current` 对应的 trace context。

---

## 11. 异步读取现状与 C7 结论

当前 `DoraNode` 提供：

- `NextAsync(CancellationToken)`
- `ReadAllEventsAsync(CancellationToken)`

但必须明确：这不是 Dora Runtime 原生异步推送。

当前实现仍基于：

- C# 后台 event pump
- `Channel<DoraEvent>`
- native 同步阻塞 `dora_next_event`

经检查 Dora Runtime 当前没有提供真正的原生异步事件订阅 / 推送能力：

- Rust `EventStream.recv_async()` 是异步消费外观，底层仍有事件接收线程。
- Runtime 边界仍是 `DaemonRequest::NextEvent` / `DaemonReply::NextEvents` request-response。
- C API 只暴露同步阻塞 `dora_next_event`。
- 没有 callback、async handle、poll fd 或连续事件帧接口可供 C# 绑定。

因此 C7 “从后台线程泵改为真正原生异步推送（`ValueTask` / `PipeReader` 模式）”暂不开发。C# 单侧重构只能包装同步读取，无法消除后台线程泵，也无法获得真正 native async 性能收益。

当前异步 API 的推荐用法：

- 可以用于 C# 代码风格上的 async 集成。
- 不要把它理解为零线程、零阻塞、高性能 native async。
- 同一个 `DoraNode` 实例不要混用 `Next()` 和 `NextAsync()`。
- 同一个 `DoraNode` 实例不要并发发起多个未完成的 `NextAsync()`。

---

## 12. 生命周期与错误处理

### 12.1 读取模式互斥

同一个 `DoraNode` 实例只应选择一种读取模式：

- 同步：`Next()`
- 异步：`NextAsync(...)` / `ReadAllEventsAsync(...)`

混用会被视为生命周期违规。

### 12.2 禁止异步读取并发重入

同一个 `DoraNode` 实例不应同时存在多个未完成的 `NextAsync(...)`。这不是吞吐扩展方式，而是错误用法。

### 12.3 native 对象生命周期

不要在生命周期外访问：

- `DoraEvent`
- Operator 侧 `Input`
- Arrow native payload

需要跨作用域保留的数据，应在有效生命周期内 materialize 或复制。

### 12.4 按错误码分支

推荐捕获：

- `DoraException`
- `DoraOperatorException`

并按 `ErrorCode` 分支，不要依赖异常文本。常见错误类别：

- `LifecycleViolation`
- `InvalidNativeHandle`
- native ABI / runtime failure

---

## 13. NativeAOT 与 native 库加载

C# Node 和 Operator 都依赖 Dora native C ABI：

- Node 使用 `dora_node_api_c`
- Operator 使用 `dora_operator_api_c`

常见失败原因：

- 未执行 `build-native.ps1`
- `artifacts/native/<rid>/` 缺少动态库
- 进程架构与动态库架构不一致
- NativeAOT 工具链未安装
- 将 operator 当成普通托管 DLL，而不是 NativeAOT 共享库

典型错误：

- `DllNotFoundException`
- `BadImageFormatException`
- NativeAOT publish 失败
- Dora runtime 加载 operator 失败

处理顺序：

1. 检查 native artifact 是否存在。
2. 检查 RID 和进程架构。
3. 检查 NativeAOT publish 输出。
4. 再排查业务代码。

---

## 14. 发布与版本

当前发布链路包含三个包：

| 包 | 当前源码版本 | 说明 |
| -- | ------------ | ---- |
| `DoraMate.DoraNode` | `0.10.0` | C# Node SDK |
| `DoraMate.DoraOperator` | `0.10.0` | C# Operator SDK |
| `DoraMate.Templates` | `0.10.0` | `dotnet new` 模板包 |

打包：

```powershell
cd dora-api-csharp
pwsh ./scripts/package-nuget.ps1
```

发布：

```powershell
cd dora-api-csharp
pwsh ./scripts/publish-nuget.ps1
```

注意：

- 公开 NuGet 发布需要 `NUGET_API_KEY`。
- 发布前应先跑 `dotnet test`、统一 smoke、模板创建验证。
- `docs/34` 中记录 `v0.9.0` 已公开发布；当前源码已经推进到 `0.10.0`，本地打包链路就绪，公开发布需单独执行 NuGet publish。

---

## 15. CI 与发布门禁

推荐发布前检查：

```powershell
cd dora-api-csharp
pwsh ./scripts/build-native.ps1
dotnet build ./dora-api-csharp.sln -c Release -p:NuGetAudit=false
dotnet test ./tests/DoraNode.RegressionRunner/DoraNode.RegressionRunner.csproj -c Release -p:NuGetAudit=false
dotnet test ./tests/DoraOperator.RegressionRunner/DoraOperator.RegressionRunner.csproj -c Release -p:NuGetAudit=false
pwsh ./scripts/smoke-csharp-bindings.ps1
pwsh ./scripts/smoke-csharp-otel-dataflow.ps1
pwsh ./scripts/smoke-csharp-otel-operator-dataflow.ps1
pwsh ./scripts/build-templates.ps1
```

CI 已覆盖：

- native 构建
- SDK / sample 构建
- xUnit 回归
- 最小 bytes smoke
- OTel Node smoke
- OTel Operator smoke
- Windows / Linux / macOS matrix

Linux / macOS 的最终状态以 GitHub Actions 实际运行结果为准。

---

## 16. 性能边界

根据 `docs/36` 的 C11 baseline：

- C# 在小 payload 下有固定开销，应避免把极高频小消息场景误判为 C# 最佳路径。
- 512B 以上 payload 的延迟差距明显收敛。
- 大 payload throughput 基本接近 Rust 原生节点。
- 当前 benchmark 仍需多轮统计，不能只凭单次结果做性能承诺。

后续性能工程优先级：

1. 减少小消息 materialization。
2. 增加 zero-copy / span reader API。
3. 增加多轮 benchmark runner。
4. 采集 GC / allocation 指标。
5. 等 Dora Runtime / C ABI 提供真正 async event subscription 后，再重新评估 C7。

---

## 17. 推荐实践

开发实践：

- 新项目优先用 `dotnet new dora-node` 或 `dotnet new dora-operator` 起步。
- 结构化数据优先用 Arrow `RecordBatch` 和 contract helper。
- 处理输入时用 `StartActivity(...)`，输出时用 `SendWithCurrentActivity(...)`。
- 异常处理按 `ErrorCode` 分支。
- 不要混用同步/异步读取。
- 不要缓存 native event/input 到生命周期外。

验证实践：

- 改 SDK 公共 API 必须补 xUnit 回归。
- 改 native ABI 必须跑 native build 和 smoke。
- 改 operator 必须验证 NativeAOT publish。
- 改 OpenTelemetry 必须跑两个 OTel smoke。
- 改性能路径必须跑 benchmark，并保存 artifact。

发布实践：

- 版本号以仓库根 `VERSION` 和三个 csproj 为准。
- 发布前先本地 pack，再用模板创建项目验证。
- NuGet 发布后再更新 README / QUICKSTART / 本指南中的版本口径。

---

## 18. 参考文档

- [34 - Dora C# 绑定收尾计划](34-Dora%20C#绑定收尾计划.md)
- [35 - Dora C# 绑定之 OpenTelemetry 集成开发计划](35-Dora%20C#绑定之OpenTelemetry集成开发计划.md)
- [36 - Dora C# 绑定性能基准测试报告](36-Dora%20C#绑定性能基准测试报告.md)
- [dora-api-csharp/README.md](../dora-api-csharp/README.md)
- [dora-api-csharp/QUICKSTART.md](../dora-api-csharp/QUICKSTART.md)
- [dora-api-csharp/BUILD.md](../dora-api-csharp/BUILD.md)
- [dora-api-csharp/PROJECT_STRUCTURE.md](../dora-api-csharp/PROJECT_STRUCTURE.md)
- [dora-api-csharp/src/DoraNode/DoraNode.cs](../dora-api-csharp/src/DoraNode/DoraNode.cs)
- [dora-api-csharp/src/DoraOperator/DoraOperatorBase.cs](../dora-api-csharp/src/DoraOperator/DoraOperatorBase.cs)
- [dora-api-csharp/samples/](../dora-api-csharp/samples/)
- [dora-api-csharp/tests/](../dora-api-csharp/tests/)
- [dora-api-csharp/scripts/](../dora-api-csharp/scripts/)

---

## 19. 总结

当前 Dora C# 绑定的实际状态可以概括为：

> Node / Operator / Arrow / OpenTelemetry / 模板 / NuGet / smoke / regression / benchmark 已形成闭环；C7 原生异步深度重构受限于 Dora Runtime / C ABI 当前能力，暂不作为 C# 侧开发项。

对新开发者，最稳的路径是：

1. 安装或本地构建 SDK 与模板。
2. 跑通最小 node 和最小 operator。
3. 再验证 Arrow、OpenTelemetry 和 async 边界样例。
4. 最后根据性能需求运行 benchmark。

这条路径最贴近当前仓库的真实成熟度，也最容易把问题定位在 SDK、native ABI、Dora runtime、模板或用户代码中的正确层次。
