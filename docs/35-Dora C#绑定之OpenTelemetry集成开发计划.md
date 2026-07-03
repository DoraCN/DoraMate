# 35 - Dora C# 绑定之 OpenTelemetry 集成开发计划

> 面向 Dora C# 绑定的端到端追踪能力建设计划。目标不是仅暴露 OTel 字符串，而是将 Dora 运行时传递的上下文接入 .NET `Activity` / `ActivitySource`，并在 C# 节点继续向下游传播 trace context。
> 编制日期：2026-06-27
> 完成状态：✅ 五个阶段均已开发完成；本地 Windows 端到端 smoke / 回归测试已通过，三平台 CI 门禁已接入 `dora-csharp-cross-platform` workflow。

---

## 目录

1. [背景与目标](#背景与目标)
2. [现状评估](#现状评估)
3. [目标能力](#目标能力)
4. [总体方案](#总体方案)
5. [实施阶段](#实施阶段)
6. [API 设计建议](#api-设计建议)
7. [测试与验收](#测试与验收)
8. [工作量评估](#工作量评估)
9. [风险与应对](#风险与应对)

---

## 背景与目标

Dora 运行时已经具备 OpenTelemetry 上下文传播基础：输入消息的 metadata 中可以携带 `open_telemetry_context`，Rust / Python 等语言侧可以基于该上下文建立 span 关系。当前 C# 绑定已经能读取该上下文，但尚未接入 .NET 原生可观测性模型。

本计划的目标是补齐 C# 绑定的 OpenTelemetry 集成，使 C# 节点和 Operator 能够参与 Dora 数据流的端到端追踪：

- C# 收到输入时，根据 Dora 传入的 OTel 上下文创建 .NET `Activity`
- 用户处理输入期间，`Activity.Current` 能正确指向当前 Dora 输入 span
- C# 发送输出时，将当前 `Activity` 上下文重新注入 Dora metadata
- 下游 Rust / Python / C# 节点能继续挂接到同一条 trace
- 可通过 Jaeger / Tempo / OpenTelemetry Collector 等后端观察完整链路

该模块主要解决生产环境可观测性问题：定位端到端延迟、分析链路瓶颈、将异常和日志挂到具体 trace/span 上，并避免混合语言数据流在 C# 节点处中断追踪。

---

## 现状评估

### 已具备能力

| 模块               | 当前能力                                                                    |
| ------------------ | --------------------------------------------------------------------------- |
| `DoraNode`       | `DoraEvent.OpenTelemetryContext` 已暴露输入事件携带的序列化 OTel 上下文   |
| `DoraOperator`   | `Input.OpenTelemetryContext` 已暴露 Operator 输入携带的序列化 OTel 上下文 |
| 原生 Dora runtime  | metadata 中已有`open_telemetry_context` 约定                              |
| Rust / Python 生态 | 已有上下文反序列化、span parent 设置和重新序列化路径                        |

### 已关闭缺口

| 原缺口                                     | 当前状态                                           |
| ------------------------------------------ | -------------------------------------------------- |
| C# 层没有`ActivitySource`                | ✅ 已新增 Node / Operator `DoraTelemetry.ActivitySource` |
| C# 层没有上下文解析 / 注入 helper          | ✅ 已新增上下文解析、序列化、`StartActivity` helper |
| Node 发送输出不支持 metadata               | ✅ 已补齐 bytes / Arrow / Arrow IPC / RecordBatch metadata-aware send |
| Operator 发送输出不支持 metadata           | ✅ 已补齐 operator bytes / Arrow / Arrow IPC metadata-aware send |
| Arrow / RecordBatch 输出缺少 metadata 重载 | ✅ 已覆盖高级输出路径的 metadata 注入              |
| 缺少端到端验证                             | ✅ 已通过 Node / Operator smoke 自动验证 trace id / parent span 连续性 |

### 关键判断

只在 C# 层创建 `Activity` 属于"本进程追踪"，可以快速完成，但不能称为端到端追踪。真正的端到端能力必须同时完成：

1. 输入 OTel context -> .NET `ActivityContext`
2. .NET `Activity.Current` -> Dora output metadata
3. 下游节点能从 metadata 继续恢复 parent context

因此本计划按完整端到端追踪设计，包含必要的 native C API / Rust FFI 补齐工作。

---

## 目标能力

### 用户体验目标

C# Node 典型使用方式：

```csharp
using var node = new DoraNode.DoraNode();

while (node.Next() is { } ev)
{
    using var activity = ev.StartActivity("process-input");
    activity?.SetTag("dora.input.id", ev.Id);

    var data = ev.Data ?? Array.Empty<byte>();
    var output = Transform(data);

    node.SendOutput("output", output);
}
```

C# Operator 典型使用方式：

```csharp
protected override OnEventResult OnInput(Input input, SendOutput sendOutput)
{
    using var activity = input.StartActivity("operator-input");
    activity?.SetTag("dora.input.id", input.Id);

    var output = Transform(input.Data);
    return sendOutput.SendWithCurrentActivity("output", output);
}
```

用户无需手动解析 `traceparent`，也无需手动拼接 Dora metadata。默认路径应能做到：

- 有上游上下文时，创建子 span
- 没有上游上下文时，创建新的 root span
- 发送输出时，默认注入 `Activity.Current`
- 用户仍可选择手动传入 `ActivityContext` 或关闭自动注入

### 追踪语义目标

| Span              | 建议名称                  | 说明                         |
| ----------------- | ------------------------- | ---------------------------- |
| Node 输入处理     | `dora.node.process`     | C# Node 处理一个输入事件     |
| Operator 输入处理 | `dora.operator.process` | C# Operator 处理一个输入事件 |
| Node 输出发送     | `dora.node.send`        | 可选 span，记录输出发送耗时  |
| Operator 输出发送 | `dora.operator.send`    | 可选 span，记录输出发送耗时  |

建议 tags：

| Tag                   | 说明                                |
| --------------------- | ----------------------------------- |
| `dora.node.id`      | 当前 Dora 节点 ID，能获取则填       |
| `dora.dataflow.id`  | 当前 dataflow ID，能获取则填        |
| `dora.input.id`     | 输入 ID                             |
| `dora.output.id`    | 输出 ID                             |
| `dora.event.type`   | 输入 / Stop / Error 等事件类型      |
| `dora.payload.kind` | bytes / arrow / record_batch        |
| `dora.payload.size` | byte payload 大小，能低成本获取则填 |
| `dora.error.code`   | SDK 错误码                          |

---

## 总体方案

### 架构分层

```
┌─────────────────────────────────────────────┐
│ 用户代码                                     │
│ using var activity = ev.StartActivity(...)  │
│ node.SendOutput("out", data)                │
└──────────────────────┬──────────────────────┘
                       │
┌──────────────────────▼──────────────────────┐
│ C# Telemetry Helper                          │
│ DoraTelemetry / DoraActivitySource           │
│ - Parse Dora OTel context                    │
│ - Start Activity with parent                 │
│ - Inject Activity.Current into metadata      │
└──────────────────────┬──────────────────────┘
                       │
┌──────────────────────▼──────────────────────┐
│ C# Node / Operator SDK                       │
│ - SendOutput metadata overloads              │
│ - SendArrow metadata overloads               │
│ - SendRecordBatch metadata overloads         │
└──────────────────────┬──────────────────────┘
                       │
┌──────────────────────▼──────────────────────┐
│ Native C API / Rust FFI                      │
│ - send_output_with_metadata                  │
│ - send_operator_output_with_metadata         │
│ - Arrow / IPC metadata variants              │
└──────────────────────┬──────────────────────┘
                       │
┌──────────────────────▼──────────────────────┐
│ Dora Runtime Metadata                         │
│ open_telemetry_context = traceparent:...;    │
└─────────────────────────────────────────────┘
```

### 上下文格式

Dora Rust 侧当前序列化形式是一个简单文本映射：

```text
traceparent:00-...;tracestate:...;
```

C# 侧需要实现双向转换：

- `string openTelemetryContext` -> `ActivityContext`
- `ActivityContext` / `Activity.Current` -> `open_telemetry_context` 字符串

建议优先支持 W3C Trace Context：

- `traceparent`
- `tracestate`

Baggage 可作为后续扩展，不作为第一版端到端验收的硬要求。

### NuGet 依赖策略

第一版建议只依赖 .NET BCL 中的 `System.Diagnostics`：

- `Activity`
- `ActivitySource`
- `ActivityContext`
- `ActivityTraceId`
- `ActivitySpanId`

不在 SDK 核心包里强制引入 exporter 或 OpenTelemetry hosting 依赖。用户项目如需导出到 Jaeger / Tempo / Collector，自行安装：

- `OpenTelemetry`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`

这样可以保持 `DoraMate.DoraNode` / `DoraMate.DoraOperator` 轻量，同时兼容 .NET 标准可观测性生态。

---

## 实施阶段

### 完成总览

| 阶段 | 状态 | 验收结果 |
| ---- | ---- | -------- |
| 阶段一：托管 Activity 集成 | ✅ 已完成 | `DoraTelemetry`、`TryGetActivityContext`、`StartActivity` 已覆盖 Node / Operator，回归测试通过 |
| 阶段二：Node 输出 metadata 传播 | ✅ 已完成 | bytes / Arrow / Arrow IPC / RecordBatch metadata-aware send 已补齐，Node OTel smoke 通过 |
| 阶段三：Operator 输出 metadata 传播 | ✅ 已完成 | operator bytes / Arrow / Arrow IPC metadata-aware send 已补齐，input metadata native 读取入口已补齐，Operator OTel smoke 通过 |
| 阶段四：示例与文档 | ✅ 已完成 | 新增 `csharp-otel-dataflow` 与 `csharp-otel-operator-dataflow` 示例，README / QUICKSTART 已更新 |
| 阶段五：回归测试与 CI 门禁 | ✅ 已完成 | Node / Operator OTel smoke 已自动验证 trace id 与 parent span 连续性；三平台 CI workflow 已接入 |

最新本地验收结果：

- `dotnet test dora-api-csharp/tests/DoraNode.RegressionRunner/DoraNode.RegressionRunner.csproj -c Release -p:NuGetAudit=false --no-restore`：✅ 13/13 通过
- `dotnet test dora-api-csharp/tests/DoraOperator.RegressionRunner/DoraOperator.RegressionRunner.csproj -c Release -p:NuGetAudit=false --no-restore`：✅ 16/16 通过
- `pwsh ./dora-api-csharp/scripts/smoke-csharp-otel-dataflow.ps1 -SkipBuild -TimeoutSeconds 45 -DoraPath ./dora-api-csharp/third_party/dora/target/release/dora.exe`：✅ 通过
- `pwsh ./dora-api-csharp/scripts/smoke-csharp-otel-operator-dataflow.ps1 -TimeoutSeconds 60 -DoraPath ./dora-api-csharp/third_party/dora/target/release/dora.exe`：✅ 通过
- `cargo build -p dora-cli --release --locked`：✅ 通过

说明：跨平台门禁已经接入 GitHub Actions 的 `windows-2022` / `ubuntu-latest` / `macos-13` matrix；当前文档记录的是本地 Windows 验收通过与 CI 配置完成，Linux / macOS 的最终运行结果以 GitHub Actions 实际执行为准。

### 阶段一：托管 Activity 集成

目标：让 C# 输入事件可以创建标准 .NET `Activity`。

状态：✅ 已完成。

涉及文件：

| 文件                                                  | 改动                                                                 |
| ----------------------------------------------------- | -------------------------------------------------------------------- |
| `dora-api-csharp/src/DoraNode/DoraTelemetry.cs`     | 新增 Node 侧 telemetry helper                                        |
| `dora-api-csharp/src/DoraOperator/DoraTelemetry.cs` | 新增 Operator 侧 telemetry helper                                    |
| `dora-api-csharp/src/DoraNode/DoraEvent.cs`         | 增加`GetActivityContext` / `StartActivity` 扩展入口              |
| `dora-api-csharp/src/DoraOperator/OperatorTypes.cs` | 增加`Input` 的 `GetActivityContext` / `StartActivity` 扩展入口 |

建议实现：

- 新增静态 `DoraTelemetry.ActivitySource`
- 新增 `TryParseContext(string?, out ActivityContext)`
- 新增 `SerializeContext(ActivityContext context)`
- 新增 `StartActivityFromContext(...)`
- 对空 context、格式错误 context 做容错，不能影响数据处理主链路

验收：

- 构造包含 `traceparent` 的 context 字符串后，`StartActivity` 创建的 span 与上游 trace id 一致
- 空 context 时能创建 root activity
- 非法 context 不抛出未处理异常，按 root activity 或 null activity 处理

### 阶段二：Node 输出 metadata 传播

目标：C# Node 发送 bytes / Arrow / RecordBatch 输出时能注入当前 trace context。

状态：✅ 已完成。

涉及文件：

| 文件                                                         | 改动                                     |
| ------------------------------------------------------------ | ---------------------------------------- |
| `third_party/dora/apis/c/node/src/lib.rs`                  | 新增带 metadata 的 C API                 |
| `third_party/dora/apis/c/node/node_api.h`                  | 导出新函数声明                           |
| `dora-api-csharp/src/DoraNode/NativeMethods.cs`            | 新增 P/Invoke                            |
| `dora-api-csharp/src/DoraNode/DoraNode.cs`                 | 新增 metadata / activity-aware 发送重载  |
| `dora-api-csharp/src/DoraNode/DoraNodeOutputExtensions.cs` | RecordBatch / typed output 透传 metadata |

建议新增原生函数：

```c
int dora_send_output_with_metadata(
    void *dora_context,
    char *id_ptr,
    size_t id_len,
    char *data_ptr,
    size_t data_len,
    char *open_telemetry_context_ptr,
    size_t open_telemetry_context_len);
```

Arrow / IPC 路径建议同步补齐：

- `dora_send_output_arrow_with_metadata`
- `dora_send_output_arrow_ipc_with_metadata`

验收：

- C# Node 处理输入并发送输出后，下游节点收到的 `OpenTelemetryContext` 包含新的 `traceparent`
- 下游解析出的 trace id 与上游一致
- parent span id 指向 C# Node 当前 activity

### 阶段三：Operator 输出 metadata 传播

目标：C# Operator 不再成为 trace 断点。

状态：✅ 已完成。

涉及文件：

| 文件                                                     | 改动                                                  |
| -------------------------------------------------------- | ----------------------------------------------------- |
| `third_party/dora/apis/rust/operator/types/src/lib.rs` | 为 operator output 写入真实`open_telemetry_context` |
| `third_party/dora/apis/c/operator/operator_types.h`    | 若需要，补充 metadata 相关 FFI                        |
| `dora-api-csharp/src/DoraOperator/NativeMethods.cs`    | 新增 metadata 发送入口                                |
| `dora-api-csharp/src/DoraOperator/SendOutputBridge.cs` | 注入`Activity.Current`                              |
| `dora-api-csharp/src/DoraOperator/OperatorOutput.cs`   | 增加 activity-aware helper                            |

完成说明：native operator FFI 已将 C# 传入的 context 写入 `Output.metadata.open_telemetry_context`，并补齐 input metadata 读取入口 `dora_read_input_open_telemetry_context` / `dora_free_input_open_telemetry_context`。

验收：

- C# Operator 输入 span 能挂到上游 trace
- C# Operator 发送的输出能继续携带当前 activity context
- 下游 Node / Operator 能继续作为子 span 接上

### 阶段四：示例与文档

目标：用户能快速理解和验证 OpenTelemetry 集成。

状态：✅ 已完成。

新增或更新：

| 文件                                              | 改动                         |
| ------------------------------------------------- | ---------------------------- |
| `dora-api-csharp/samples/csharp-otel-dataflow/` | 新增最小端到端追踪示例       |
| `dora-api-csharp/README.md`                     | 增加 OTel 使用章节           |
| `dora-api-csharp/QUICKSTART.md`                 | 增加 ActivitySource 配置片段 |
| `dora-api-csharp/templates/`                    | 可选加入 OTel 注释示例       |

示例 dataflow 建议：

```text
producer -> csharp-transform -> consumer
```

示例应支持：

- console exporter 或 OTLP exporter
- 输出 trace id / span id 便于本地 smoke 验证
- 不强制用户启动 Jaeger 才能跑通

### 阶段五：回归测试与 CI 门禁

目标：用自动化证明端到端上下文不断链。

状态：✅ 已完成。

测试分层：

| 层级           | 用例                                     |
| -------------- | ---------------------------------------- |
| 单元测试       | Dora context 字符串解析 / 序列化         |
| 单元测试       | Activity parent-child 关系               |
| SDK 测试       | SendOutput 默认注入`Activity.Current`  |
| native smoke   | Node metadata send / receive             |
| dataflow smoke | producer -> C# -> consumer trace id 连续 |
| 跨平台 smoke   | Windows / Linux / macOS 最小链路验证     |

CI 当前已纳入单元测试覆盖的 metadata-aware API、最小 bytes 链路、Node OTel smoke、Operator OTel smoke 和三平台 matrix。Arrow / RecordBatch metadata 路径由 SDK 回归测试覆盖，后续可按需增加专用 dataflow smoke。

当前实现已将 Node 与 Operator 的 OTel bytes smoke 纳入 `dora-csharp-cross-platform` 三平台矩阵：

- `scripts/smoke-csharp-otel-dataflow.ps1`
- `scripts/smoke-csharp-otel-operator-dataflow.ps1`

CI workflow 同时上传 `dora-api-csharp/artifacts/smoke/**`，便于失败时定位 producer / operator / consumer 的 trace id 与 span id。

---

## API 设计建议

### DoraNode API

```csharp
public sealed class DoraEvent
{
    public bool TryGetActivityContext(out ActivityContext context);
    public Activity? StartActivity(string? name = null, ActivityKind kind = ActivityKind.Consumer);
}
```

```csharp
public sealed class DoraNode
{
    public bool SendOutput(string outputId, byte[] data, ActivityContext? activityContext);
    public bool SendOutputWithCurrentActivity(string outputId, byte[] data);

    public bool SendArrow(string outputId, ArrowPayload payload, ActivityContext? activityContext);
    public bool SendRecordBatch(string outputId, RecordBatch batch, ActivityContext? activityContext);
}
```

默认策略建议：

- 现有 `SendOutput(outputId, data)` 保持兼容，但内部可自动使用 `Activity.Current`
- 如果担心行为变化，可以增加 `DoraTelemetry.AutoInjectCurrentActivity` 开关，默认开启
- 显式传入 `ActivityContext?` 的重载优先级高于 `Activity.Current`

### DoraOperator API

```csharp
public sealed class Input
{
    public bool TryGetActivityContext(out ActivityContext context);
    public Activity? StartActivity(string? name = null, ActivityKind kind = ActivityKind.Consumer);
}
```

```csharp
public static class SendOutputTelemetryExtensions
{
    public static DoraResult SendWithCurrentActivity(this SendOutput sendOutput, string outputId, byte[] data);
    public static DoraResult Send(this SendOutput sendOutput, string outputId, byte[] data, ActivityContext? context);
}
```

若当前 `SendOutput` 是 delegate，不便扩展 metadata 参数，可以保留 delegate 兼容层，并在内部 `SendOutputDispatcher` 中提供 metadata-aware 方法。

---

## 测试与验收

### 最小验收标准

| 验收项            | 标准                                                                   |
| ----------------- | ---------------------------------------------------------------------- |
| 输入接入 Activity | C# 收到带`traceparent` 的输入后，创建的 activity trace id 与上游一致 |
| 输出继续传播      | C# 发送输出后，下游收到新的`open_telemetry_context`                  |
| parent-child 正确 | 下游 activity parent span id 指向 C# 发送时的 current activity         |
| 无上下文兼容      | 输入无 OTel context 时仍可正常处理并创建 root span                     |
| 错误上下文兼容    | 非法 context 不导致节点崩溃                                            |
| 现有 API 兼容     | 不破坏现有 C# 示例和回归测试                                           |

### 完整验收标准

| 验收项              | 标准                                                                      |
| ------------------- | ------------------------------------------------------------------------- |
| Node bytes 链路     | producer -> C# Node -> consumer trace 连续                                |
| Operator bytes 链路 | producer -> C# Operator -> consumer trace 连续                            |
| Arrow 链路          | Arrow 输出同样携带 metadata                                               |
| RecordBatch 链路    | IPC / RecordBatch 输出同样携带 metadata                                   |
| 异步读取链路        | `NextAsync` / `ReadAllEventsAsync` 下 Activity 使用方式清晰且测试通过 |
| 三平台验证          | Windows / Linux / macOS 最小 OTel smoke 通过                              |
| 文档示例            | README / sample 能指导用户接入 OTLP exporter                              |

---

## 工作量评估

| 阶段                                | 难度 | 预估     |
| ----------------------------------- | ---- | -------- |
| 阶段一：托管 Activity 集成          | 中   | 1-2 天   |
| 阶段二：Node 输出 metadata 传播     | 中高 | 1-2 天   |
| 阶段三：Operator 输出 metadata 传播 | 中高 | 1-2 天   |
| 阶段四：示例与文档                  | 中   | 0.5-1 天 |
| 阶段五：回归测试与 CI 门禁          | 中高 | 1-2 天   |

推荐排期：

```text
最小端到端版本：3-5 天
完整产品化版本：5-7 天
```

其中最容易低估的是 native metadata 发送路径。若只实现 Activity 包装，工作量约 1-2 天；但该方案不能满足"端到端追踪"目标。

---

## 风险与应对

| 风险                                     | 影响                        | 应对                                                         |
| ---------------------------------------- | --------------------------- | ------------------------------------------------------------ |
| native C API 不支持 metadata             | C# 输出无法传播 trace       | 新增 metadata-aware send 函数，并保持旧函数兼容              |
| Operator`SendOutput` delegate 形态较窄 | 难以传入 ActivityContext    | 在`SendOutputDispatcher` 增加扩展方法，必要时新增包装类型  |
| Dora context 字符串格式较简单            | 冒号 / 分号解析边界可能出错 | 第一版只支持 W3C trace keys，并对非法输入容错                |
| 强引 OpenTelemetry 包导致 SDK 变重       | NuGet 依赖膨胀              | 核心 SDK 只依赖`System.Diagnostics`，exporter 交给用户项目 |
| 自动注入改变现有发送语义                 | 可能引入隐性行为变化        | 提供开关，保留显式无 metadata 发送路径                       |
| Arrow / RecordBatch 路径遗漏             | 高级示例 trace 断链         | 将 bytes 作为第一门禁，Arrow / IPC 作为完整验收              |
| 跨平台 native 构建差异                   | CI 不稳定                   | 已接入三平台 matrix；失败时上传 smoke logs 辅助定位           |

---

## 结论

OpenTelemetry 集成的核心价值是让 C# 节点进入 Dora 的统一可观测性体系。该模块不只是把字符串转成 `Activity`，而是要完成"输入恢复上下文、处理期间产生活动、输出继续传播上下文"的闭环。

当前五个阶段已完成：bytes / Arrow / Arrow IPC / RecordBatch 的 metadata-aware 发送路径已补齐，Node 与 Operator 端到端 smoke 已自动验证 trace id / parent span 连续性，跨平台 CI 门禁已接入三平台 matrix。后续若继续增强，可补充真实 OTLP exporter 示例、Baggage 传播和更细粒度性能基准。
