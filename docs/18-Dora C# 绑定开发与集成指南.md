# 18 - Dora C# 绑定开发与集成指南

## 1. 文档目的

本文档基于 `dora-api-csharp/` 目录下的当前程序实现、样例工程和现有说明文档整理，目的是回答四个问题：

- `dora-api-csharp` 在 DoraMate 仓库中到底承担什么角色
- 当前 C# 绑定已经支持哪些真实能力
- 应该如何从样例、构建、验证一路走到自己的 C# node / operator 开发
- 在生命周期、NativeAOT、Arrow 数据面和错误处理上，哪些边界必须先搞清楚

本文档不是对上游 Dora 官方文档的复述，而是针对当前仓库状态的“落地口径”说明。

---

## 2. 适用范围

本文档适用于以下使用场景：

- 需要在当前仓库中编写独立 C# node
- 需要编写可被 Dora runtime 加载的 C# NativeAOT operator
- 需要验证 C# 与 Dora 之间的 Arrow `RecordBatch` 数据通道
- 需要理解 `dora-api-csharp` 与 DoraMate 主工程之间的关系
- 需要快速判断当前 C# 绑定适合做什么、不适合做什么

本文档涉及目录：

- `dora-api-csharp/src/`
- `dora-api-csharp/samples/`
- `dora-api-csharp/scripts/`
- `dora-api-csharp/tests/`
- `dora-api-csharp/artifacts/`
- `dora-api-csharp/third_party/`

---

## 3. `dora-api-csharp` 的定位

在当前 DoraMate 仓库中，`dora-api-csharp` 不是前端模块，也不是 LocalAgent，而是一套独立的 Dora C# 语言绑定工作区。

它解决的核心问题是：

1. 让开发者可以通过 `DoraNode` 编写独立的 C# 节点进程
2. 让开发者可以通过 `DoraOperator` 编写 NativeAOT 共享库 Operator
3. 让 C# 侧能够收发普通字节消息、UTF-8 文本和 Arrow `RecordBatch`
4. 提供 smoke、regression、样例 dataflow 和 native 构建脚本，确保这套绑定不仅“能编译”，而且“能运行”

如果把整个仓库按职责拆分，可以把它理解为：

- `doramate-frontend`
  - 负责可视化编辑、YAML 生成、运行状态观察
- `doramate-localagent`
  - 负责本地文件系统、运行时桥接、HTTP / WebSocket API
- `dora-api-csharp`
  - 负责 C# 节点和 C# operator 的语言绑定、样例、构建和验证基线

因此，`dora-api-csharp` 更接近“开发者 SDK + 示例仓库”，而不是 DoraMate 终端用户直接操作的产品界面。

---

## 4. 当前已具备的能力

基于当前代码和样例，`dora-api-csharp` 已经具备以下真实能力。

### 4.1 C# Node 开发能力

`src/DoraNode/` 提供了独立 C# node 的托管 API，当前已经覆盖：

- `DoraNode` 生命周期管理
- 同步事件读取：`Next()`
- 第一阶段异步事件读取：`NextAsync(...)`
- 异步流式消费：`ReadAllEventsAsync(...)`
- 普通 bytes 输出发送
- UTF-8 string 输出发送
- Arrow payload / `RecordBatch` 输出发送
- 诊断信息与稳定错误码

这意味着当前仓库已经可以支持“独立 C# 可执行节点”这一类开发模式。

### 4.2 C# Operator 开发能力

`src/DoraOperator/` 提供了 C# Operator 的托管 API 和 NativeAOT 桥接层，当前已经覆盖：

- `DoraOperatorBase` 继承式开发模型
- `InputEvent`、`InputClosedEvent`、`StopEvent`、`ErrorEvent`
- `OperatorOutput.SendOrThrow(...)`
- `OnEventResult.Continue()` / `Stop()` / `Err(...)`
- `OperatorEntrypoint<T>` 通用导出入口
- Native ABI 导出辅助
- 生命周期与诊断错误码

这意味着当前仓库已经可以支持“C# 写业务逻辑，NativeAOT 输出共享库供 Dora runtime 加载”的开发模式。

### 4.3 Arrow 数据面能力

Node 与 Operator 两侧都已经补齐 Arrow 相关 helper，包括：

- `TryReadRecordBatch(...)`
- `SendRecordBatch(...)`
- `SendRecordBatchOrThrow(...)`
- schema validation
- contract 校验
- projector
- assertion
- summary

这不是一个只有 bytes/string 的最小绑定，而是已经支持结构化 Arrow 数据面的绑定。

### 4.5 C# 模板产品化能力

除了 SDK 和示例外，当前仓库已具备完整的 C# 模板产品化能力：

- 两个 SDK 已配置 NuGet 包元数据，可通过 `dotnet pack` 生成 `DoraMate.DoraNode` 和 `DoraMate.DoraOperator` 的 `.nupkg`
- 两个 `dotnet new` 模板（`dora-node` / `dora-operator`）可从本地路径或模板包安装，3 步创建新项目
- 模板包 `DoraMate.Templates` 可独立分发，通过 NuGet 安装到任意 .NET SDK
- 前端 `api.rs` 已注册 C# 模板条目，供 DoraMate "添加节点" 面板使用

这意味着当前仓库已经可以支持 "NuGet 包 + dotnet new 模板 + 前端模板库" 三种交付形态。

### 4.4 验证与回归能力

当前仓库不只提供 API，也已经形成了可重复验证的基线：

- `samples/` 下覆盖最小 node、最小 operator、Arrow round-trip、contract、async 等场景
- `tests/` 下有 `DoraNode.RegressionRunner` 和 `DoraOperator.RegressionRunner`
- `scripts/` 下有统一 smoke 入口与多个定向 smoke / regression 脚本

这点非常关键，因为 C# 绑定这类项目如果只有源码没有运行链路，很难判断问题来自 API、NativeAOT、native 库、还是 runtime 环境。

---

## 5. 目录结构与职责

当前 `dora-api-csharp/` 的目录可以按“核心库、样例、自动化、输出、上游快照”五层来理解。

### 5.1 `src/`

核心托管库目录：

- `src/DoraNode/`
  - C# node 侧 API
- `src/DoraOperator/`
  - C# operator 侧 API 与 runtime 桥接

其中：

- `DoraNode.cs`
  - node 主入口与读写能力
- `DoraEvent.cs`
  - node 输入事件包装
- `DoraNodeOutputExtensions.cs`
  - bytes / string / Arrow / `RecordBatch` 发送扩展
- `DoraOperatorBase.cs`
  - operator 继承基类
- `OperatorEvent.cs`
  - operator 事件模型
- `OperatorOutput.cs`
  - operator 输出发送统一入口
- `OperatorEntrypoint.cs`
  - NativeAOT operator 的通用导出桥

### 5.2 `samples/`

`samples/` 不是简单演示目录，而是当前支持面的“可运行地图”。

已覆盖的主要场景：

- `csharp-dataflow/`
  - 最小 node 示例
- `csharp-multi-node/`
  - 多节点示例
- `csharp-operator-dataflow/`
  - 最小 operator 示例
- `csharp-arrow-node-dataflow/`
  - node 到 node 的 Arrow `RecordBatch`
- `csharp-advanced-arrow-node-dataflow/`
  - 更丰富的 Arrow 标量类型
- `csharp-complex-arrow-contract-node-dataflow/`
  - contract + typed model
- `csharp-node-operator-arrow-dataflow/`
  - `node -> operator -> node` Arrow 链路
- `csharp-operator-arrow-roundtrip/`
  - operator 内部 Arrow round-trip
- `csharp-operator-contract-arrow-dataflow/`
  - operator 侧 contract / projector / assertion
- `csharp-async-node-dataflow/`
  - 异步 node 读取与生命周期边界验证

### 5.3 `scripts/`

当前自动化入口集中在 `scripts/`：

- `bootstrap-dora.ps1`
  - 刷新 `third_party/dora`
- `build-native.ps1`
  - 构建 native C ABI 并复制到统一输出目录
- `rebuild-csharp-operators.ps1`
  - 统一重建 NativeAOT operator 示例
- `package-nuget.ps1`
  - 打包 DoraNode / DoraOperator SDK 为 NuGet 包到 `artifacts/nuget/`
- `build-templates.ps1`
  - 构建模板包 `DoraMate.Templates.*.nupkg` 并安装到 dotnet new
- `install-templates.ps1`
  - 从本地 `templates/` 目录直接安装模板到 dotnet new（无需打包）
- `smoke-csharp-bindings.ps1`
  - 全量 smoke 入口（含模板验证步骤）
- `test-doranode-regression.ps1`
  - node 侧回归
- `test-doraoperator-regression.ps1`
  - operator 侧回归

这套脚本体系说明当前项目已经从“手工试验”走向“有固定验证入口”的状态。

### 5.4 `tests/`

当前测试目录主要包括两个回归 runner：

- `tests/DoraNode.RegressionRunner/`
- `tests/DoraOperator.RegressionRunner/`

它们的职责不是替代 smoke，而是对核心托管层做更稳定的回归校验。

### 5.5 `artifacts/`

当前仓库使用统一输出目录，而不是把各种产物散落在各项目默认路径中：

- `artifacts/native/`
  - native 动态库
- `artifacts/dotnet/`
  - 核心库与 regression runner 输出
- `artifacts/samples/`
  - sample 输出

这种目录策略对 DoraMate 主仓库很重要，因为它降低了多项目并行构建时的定位成本。

### 5.6 `third_party/`

`third_party/dora/` 是 vendored Dora 上游快照，主要用途是：

- 构建 `dora-node-api-c`
- 构建 `dora-operator-api-c`
- 为当前绑定仓库提供稳定的 native ABI 来源

它不是前端依赖目录，也不是让日常绑定逻辑直接改造的地方。

### 5.7 `templates/`

`templates/` 是 dotnet new 模板目录，包含两个可安装的模板：

- `templates/DoraMate.Templates.csproj`
  - 模板包项目，`PackageType=Template`，内容仅打包
- `templates/dora-node/`
  - `dora-node` 模板：最小 C# Dora node 项目
- `templates/dora-operator/`
  - `dora-operator` 模板：最小 C# NativeAOT operator 项目

模板不参与主 solution 的编译，仅作为 NuGet 模板包或本地路径安装到 dotnet new SDK。安装后可通过 `dotnet new dora-node` / `dotnet new dora-operator` 创建新项目。

---

## 6. 两种核心开发模型

理解 `dora-api-csharp`，最重要的是先分清 Node 和 Operator 是两种不同的开发模型。

### 6.1 模型 A：独立 C# Node

适用场景：

- 想用 C# 写一个独立进程节点
- 节点以可执行程序方式参与 dataflow
- 主要关心事件读取、业务逻辑处理和输出发送

典型代码形态：

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
        node.SendOutputOrThrow("output", "hello from csharp");
    }
}
```

这类模型的特点是：

- 思路直接
- 调试成本相对低
- 更像“常规 C# 控制台程序 + Dora runtime 事件循环”

### 6.2 模型 B：C# NativeAOT Operator

适用场景：

- 想把逻辑做成共享库，由 Dora runtime 直接加载
- 希望用 operator 形式参与数据流处理
- 能接受 NativeAOT 与 native ABI 导出要求

典型代码形态：

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

同时需要 Native 导出入口：

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

这类模型的特点是：

- 运行时集成更紧
- 部署产物是 NativeAOT 共享库
- 构建要求高于普通 node
- 生命周期边界比独立 node 更严格

---

## 7. 推荐学习与上手顺序

如果第一次接触当前仓库，不建议一上来就写自己的工程。更稳妥的顺序是先跑通样例，再进入定制开发。

### 7.1 第一阶段：先确认最小能力

推荐先看并运行：

1. `README.md`
2. `QUICKSTART.md`
3. `BUILD.md`
4. `samples/csharp-dataflow/`
5. `samples/csharp-operator-dataflow/`

目的：

- 确认普通 C# node 可运行
- 确认 NativeAOT operator 可运行
- 确认本机 native 库加载无误

### 7.2 第二阶段：再确认结构化数据面

接着运行：

1. `samples/csharp-arrow-node-dataflow/`
2. `samples/csharp-node-operator-arrow-dataflow/`
3. `samples/csharp-operator-arrow-roundtrip/`
4. `samples/csharp-operator-contract-arrow-dataflow/`

目的：

- 确认 Arrow `RecordBatch` 读写打通
- 确认 node 和 operator 两侧都能处理结构化 payload
- 确认 contract / projector / assertion helper 的真实可用性

### 7.3 第三阶段：最后理解异步与生命周期边界

最后再看：

1. `samples/csharp-async-node-dataflow/`
2. `tests/DoraNode.RegressionRunner/`
3. `tests/DoraOperator.RegressionRunner/`

这部分更重要，但不适合作为第一站，因为它讨论的是边界和错误，而不是最小 happy path。

### 7.4 第四阶段：使用 dotnet new 模板快速创建项目

在熟悉了基础开发模型后，可以使用仓库内置的模板快速创建新项目：

```powershell
# 安装模板（二选一）
pwsh ./scripts/build-templates.ps1          # 通过模板包安装
pwsh ./scripts/install-templates.ps1        # 从本地目录安装

# 创建 C# node 项目
dotnet new dora-node -n MyCustomNode
cd MyCustomNode
dotnet build

# 创建 C# operator 项目
dotnet new dora-operator -n MyCustomOp
cd MyCustomOp
dotnet publish -c Release
```

模板提供了 `--NodeName` / `--OperatorName` 和 `--TargetFramework` 参数用于定制。

---

## 8. 推荐构建与验证顺序

当前最可靠的做法，不是单独运行某个 `.csproj`，而是按仓库约定顺序构建和验证。

### 8.1 前置要求

- .NET SDK 8.0 或更高版本
- Rust toolchain
- Dora CLI
- Windows 上建议使用 `pwsh`
- NativeAOT 本机工具链
  - Windows: Visual Studio Build Tools / MSVC
  - Linux/macOS: 本机 C/C++ 工具链

### 8.2 推荐命令

在仓库根目录执行：

```powershell
cd dora-api-csharp
pwsh ./scripts/bootstrap-dora.ps1
pwsh ./scripts/build-native.ps1
dotnet build ./dora-api-csharp.sln -c Release
pwsh ./scripts/smoke-csharp-bindings.ps1
```

这四步分别解决：

1. 准备上游 Dora native 源码
2. 生成 C ABI 动态库
3. 统一编译 C# 核心库、sample 和 regression runner
4. 验证真实运行链路

如果需要验证模板：

```powershell
pwsh ./scripts/build-templates.ps1           # 构建 + 安装模板包
dotnet new dora-node -n TestNode             # 创建测试项目
dotnet build ./TestNode/TestNode.csproj      # 构建验证
```

如果需要为本地开发打包 NuGet：

```powershell
dotnet pack ./src/DoraNode/DoraNode.csproj -c Release -o artifacts/packages
dotnet pack ./src/DoraOperator/DoraOperator.csproj -c Release -o artifacts/packages
```

模板通过 `nuget.config` 中配置的本地源 `artifacts/packages` 自动解析依赖。

### 8.3 为什么不能只看 `dotnet build`

因为 `dotnet build` 只能说明：

- 项目能编译
- 引用关系基本正确

但它不能证明：

- native 库真的能被加载
- NativeAOT operator 真的能被 Dora runtime 调用
- Arrow `RecordBatch` 数据面真的能完整往返
- async 生命周期边界真的按预期工作

所以当前仓库的正确判断标准是：

> `build` 是编译检查，`smoke` 才是运行链路检查。

---

## 9. 样例地图与推荐命令

下面这组样例最值得优先使用。

### 9.1 最小 Node 示例

目录：

- `dora-api-csharp/samples/csharp-dataflow/`

适合验证：

- `DoraNode`
- `Next()`
- `SendOutputOrThrow(...)`

推荐命令：

```powershell
cd dora-api-csharp
dora run ./samples/csharp-dataflow/dataflow.yml
```

### 9.2 最小 Operator 示例

目录：

- `dora-api-csharp/samples/csharp-operator-dataflow/`

适合验证：

- `DoraOperatorBase`
- `OperatorOutput.SendOrThrow(...)`
- NativeAOT 导出与共享库加载

推荐命令：

```powershell
cd dora-api-csharp
dora run ./samples/csharp-operator-dataflow/dataflow.yml
```

### 9.3 Node -> Operator -> Node Arrow 示例

目录：

- `dora-api-csharp/samples/csharp-node-operator-arrow-dataflow/`

适合验证：

- `RecordBatch` 从 node 发出
- operator 读取并校验 Arrow schema
- operator 转发后由 node 再次读取

推荐命令：

```powershell
cd dora-api-csharp
dora run ./samples/csharp-node-operator-arrow-dataflow/dataflow.yml
```

### 9.4 Async Node 示例

目录：

- `dora-api-csharp/samples/csharp-async-node-dataflow/`

适合验证：

- `NextAsync(...)`
- `ReadAllEventsAsync(...)`
- 生命周期和并发读取限制

推荐命令：

```powershell
cd dora-api-csharp
dora run ./samples/csharp-async-node-dataflow/dataflow.yml
```

当前样例已覆盖以下模式：

- `normal`
- `cancel-before-input`
- `mixed-read`
- `concurrent-read`
- `stream-close`
- `dispose-pending-read`
- `native-failure`

例如：

```powershell
cd dora-api-csharp
$env:DORA_CSHARP_ASYNC_TEST_MODE = "native-failure"
$env:DORA_CSHARP_SIMULATE_NODE_ASYNC_NATIVE_FAILURE = "invalid-native-handle"
dora run ./samples/csharp-async-node-dataflow/dataflow.yml
```

---

## 10. 错误处理与生命周期边界

这部分是当前 C# 绑定最容易误用的地方。

### 10.1 不要混用同步和异步读取

同一个 `DoraNode` 实例，只应选择一种事件读取模式：

- 要么始终用 `Next()`
- 要么始终用 `NextAsync(...)` / `ReadAllEventsAsync(...)`

当前仓库明确把“混用同步/异步读取”归为生命周期违规，典型错误码是：

- `DoraNodeErrorCode.LifecycleViolation`

### 10.2 不要让异步读取并发重入

同一个 `DoraNode` 实例，不应同时发起多个未完成的 `NextAsync(...)`。

这同样会触发生命周期违规，而不是“某次读取偶尔失败”。

### 10.3 不要把 event / input 缓存到生命周期之外访问

无论是：

- `DoraEvent`
- operator 侧 `Input`

都不应该在有效生命周期外继续读取 native 数据。

当前仓库已经通过样例明确验证了两类错误语义：

- `LifecycleViolation`
  - 调用方在对象生命周期外访问 native 包装对象
- `InvalidNativeHandle`
  - native 句柄缺失、失效或 ABI 读取失败

### 10.4 错误分支应依赖错误码，而不是异常文本

当前仓库推荐的消费方式是：

- Node 侧捕获 `DoraException`
- Operator 侧捕获 `DoraOperatorException`
- 优先按 `ErrorCode` 分支

不要依赖异常字符串做逻辑判断，因为字符串更容易受实现细节影响。

---

## 11. NativeAOT 与本地库加载要点

当前 `dora-api-csharp` 的两个核心运行前提分别是：

- Node 依赖 native C ABI 动态库
- Operator 除了依赖 C ABI，还依赖 NativeAOT 共享库输出

### 11.1 常见失败点

最常见的运行问题通常集中在：

1. 没有执行 `build-native.ps1`
2. `artifacts/native/<rid>/` 中缺少对应动态库
3. 进程架构和动态库架构不一致
4. 把 operator 当成普通托管 DLL，而不是 NativeAOT 共享库

### 11.2 常见错误现象

典型错误包括：

- `DllNotFoundException`
- `BadImageFormatException`
- NativeAOT publish 失败

这些问题大多数不是业务逻辑 bug，而是构建产物、架构或工具链问题。

### 11.3 对 DoraMate 主仓库的意义

这一点对 DoraMate 很重要，因为它意味着：

- C# 节点和 operator 能否运行，不仅取决于前端 YAML 是否生成正确
- 还取决于本机是否具备 .NET、NativeAOT、Rust、Dora CLI 和 native ABI 动态库

换句话说，DoraMate 可以负责编排，但 C# 运行面仍然依赖本地开发环境完整性。

---

## 12. 与 DoraMate 主工程的关系

从 DoraMate 全仓库视角看，`dora-api-csharp` 当前的价值主要体现在三个方面。

### 12.1 为 C# 节点类型提供真实实现基础

`docs/17-DoraMate用户手册.md` 中已经把 `csharp_custom` 作为自定义节点类型之一。  
`dora-api-csharp` 则提供了这个方向真正可落地的 SDK、样例和验证路径。

也就是说：

- DoraMate 前端负责让用户在画布里表达 C# 节点
- `dora-api-csharp` 负责让这种节点在技术上可被实现和验证

### 12.2 模板产品化集成

当前仓库已内置 `dotnet new` 模板（`dora-node` / `dora-operator`），开发者可通过 3 步创建新的 C# 节点项目：

```powershell
dotnet new dora-node -n MyAnalysisNode
cd MyAnalysisNode
dotnet build
```

同时，前端 `doramate-frontend/src/utils/api.rs` 中已注册 `builtin_csharp_templates()` 函数，返回两个 C# 模板配置条目（`csharp-simple-node` / `csharp-operator`），供 DoraMate "添加节点" 面板使用。这意味着 DoraMate 前端可以在可视化编辑界面中将 C# 节点模板作为可选节点类型之一呈现给用户。

此外，`samples/` 目录里的最小 node、operator、Arrow、async 示例，也很适合作为开发者进一步定制时的参考工程。

### 12.3 为本地执行链路提供语言扩展面

当前 DoraMate 的本地执行链路并不只局限于 Rust / Python。  
`dora-api-csharp` 的存在说明仓库已经具备把 C# 纳入本地数据流生态的基础设施。

---

## 13. 当前限制与边界

虽然当前 `dora-api-csharp` 已经比较完整，但它仍有明确边界。

### 13.1 它不是面向终端用户的“可视化功能”

这个目录主要面向开发者，而不是 DoraMate 最终用户。  
最终用户通常不会直接操作 `src/`、`tests/` 或 `scripts/`。

### 13.2 Operator 开发门槛高于普通 Node

Node 可以作为普通 C# 可执行程序理解；Operator 则涉及：

- NativeAOT
- native ABI 导出
- 共享库加载
- 更严格的生命周期边界

因此，不应把两者视为同等复杂度。

### 13.3 `build` 成功不等于运行成功

这是当前目录最容易被误判的地方。  
真正决定项目是否可用的，是：

- native 库可发现
- runtime 可运行
- smoke 是否通过

### 13.4 当前更偏本地开发与本机验证

从目录结构、脚本设计和 NativeAOT 依赖看，当前工作流明显偏向：

- 本地机器开发
- 本地 Dora runtime 验证
- 本机 toolchain 完整安装

它不是一个“零环境依赖、拿来即用”的纯托管 NuGet 包交付形态。

---

## 14. 推荐实践

如果要在当前仓库基础上继续推进 C# 绑定开发，建议遵循下面这套实践。

1. 先跑 `smoke-csharp-bindings.ps1`，再改自己的代码。
2. 新功能优先补到 `samples/` 或 `tests/`，不要只留在临时工程里。
3. 处理异常时优先判断 `ErrorCode`，不要用异常文本做逻辑分支。
4. 同一个 `DoraNode` 实例只用一种读取模式，避免同步/异步混用。
5. 需要结构化数据时优先使用 Arrow `RecordBatch` 和现有 contract helper，不要重复发明自定义二进制协议。
6. 遇到 operator 问题先检查 NativeAOT 与动态库路径，再排查业务代码。

---

## 15. 总结

基于当前仓库真实状态，可以把 `dora-api-csharp` 的结论概括为：

> 它已经不是一个“只有最小 P/Invoke 骨架”的实验目录，而是一套具备 Node、Operator、Arrow、Async、Smoke、Regression 的 C# Dora 绑定工作区。

对 DoraMate 仓库而言，它的现实价值是：

- 为 C# 节点和 C# operator 提供真正可运行的实现基础
- 为未来的模板、示例和语言扩展提供参考资产
- 把 C# 纳入 Dora 本地数据流生态，而不只是停留在“理论支持”

当前最合适的使用方式不是直接跳到自定义复杂业务，而是：

1. 先跑通最小 node 和最小 operator
2. 再验证 Arrow 数据链路
3. 最后处理 async 生命周期与错误边界

这条路径与当前仓库的实际成熟度一致，也最不容易误判问题来源。

---

## 16. C# 模板产品化

当前仓库已将 C# SDK 从 "源码引用 + 示例项目" 升级为 "NuGet 包 + dotnet new 模板 + 前端模板库" 三种交付形态。本节详细说明模板产品化的实现内容和使用方式。

### 16.1 NuGet SDK 打包

两个核心 SDK 项目已配置完整的 NuGet 包元数据：

**DoraNode** (`src/DoraNode/DoraNode.csproj`)

- PackageId: `DoraMate.DoraNode`
- Version: `0.2.0`
- 同步/异步事件读取（`Next()` / `NextAsync()`）
- Arrow RecordBatch 全类型读写（15+ 投影方法）
- 跨平台原生库嵌入（win-x64 / linux-x64 / osx-x64）

**DoraOperator** (`src/DoraOperator/DoraOperator.csproj`)

- PackageId: `DoraMate.DoraOperator`
- Version: `0.2.0`
- NativeAOT operator 生命周期（`Init` / `OnInput` / `OnInputClosed` / `OnError` / `Drop`）
- Arrow RecordBatch 契约式验证（`IArrowRecordBatchContract<TModel>`）
- 回归测试工具集

打包脚本 `scripts/package-nuget.ps1`：

```powershell
dotnet pack src/DoraNode/DoraNode.csproj -c Release -o artifacts/nuget/ `
  -p:Authors="DoraMate" -p:RepositoryUrl=https://github.com/dora-rs/doramate
dotnet pack src/DoraOperator/DoraOperator.csproj -c Release -o artifacts/nuget/ `
  -p:Authors="DoraMate" -p:RepositoryUrl=https://github.com/dora-rs/doramate
```

### 16.2 dotnet new 模板

两个模板位于 `templates/` 目录，均可从本地路径安装或打包为 NuGet 模板包。

#### dora-node 模板

```text
templates/dora-node/
├── .template.config/template.json    # 身份标识：DoraMate.DoraNode.Template
├── CSharpNode.csproj                 # sourceName: CSharpNode → <Project>.csproj
└── CSharpNode.cs                     # 使用真实 SDK API 的同步事件循环
```

模板参数：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--NodeName` | `MyDoraNode` | Entry-point class / namespace |
| `--TargetFramework` | `net8.0` | 目标框架（`net8.0` / `net9.0`） |

使用示例：

```csharp
// CSharpNode.cs 核心代码
using DoraNode;

class Program
{
    static void Main(string[] args)
    {
        using var node = new DoraNode.DoraNode();
        while (true)
        {
            using var ev = node.Next();
            if (ev is null) break;
            // 处理事件、发送输出...
        }
    }
}
```

#### dora-operator 模板

```text
templates/dora-operator/
├── .template.config/template.json    # 身份标识：DoraMate.DoraOperator.Template
├── CSharpOperator.csproj             # NativeAOT 发布配置
├── CSharpOperator.cs                 # Operator 业务逻辑
└── NativeExports.cs                  # UnmanagedCallersOnly 入口点
```

模板参数：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--OperatorName` | `MyOperator` | Operator 类名和文件名 |
| `--TargetFramework` | `net8.0` | 目标框架（`net8.0` / `net9.0`） |

`NativeExports.cs` 包含三个导出入口，将 operator 暴露为 C ABI：

- `dora_init_operator` → `OperatorEntrypoint<MyOperator>.InitOperator()`
- `dora_drop_operator` → `OperatorEntrypoint<MyOperator>.DropOperator()`
- `dora_on_event` → `OperatorEntrypoint<MyOperator>.OnEvent()`

### 16.3 模板包 (Template Pack)

`templates/DoraMate.Templates.csproj` 是一个内容仅打包的项目（`PackageType=Template`），将两个模板目录打包为 NuGet 模板包。

构建脚本 `scripts/build-templates.ps1`：

1. `dotnet pack` → 生成 `DoraMate.Templates.*.nupkg` 到 `artifacts/templates/`
2. `dotnet new install` → 将模板注册到当前 .NET SDK
3. 完成后可通过 `dotnet new dora-node` / `dotnet new dora-operator` 调用

轻量安装脚本 `scripts/install-templates.ps1` 提供无需打包的直接安装方式：

```powershell
dotnet new install ./templates/dora-node/
dotnet new install ./templates/dora-operator/
dotnet new list | Select-String dora
```

### 16.4 Smoke 验证

`scripts/smoke-csharp-bindings.ps1` 包含模板验证步骤：

1. 检查模板是否已安装，未安装则自动安装
2. 创建 `dotnet new dora-node -n TestSmokeNode` 项目并 `dotnet build`
3. 创建 `dotnet new dora-operator -n TestSmokeOp` 项目并验证创建成功

### 16.5 前端模板库集成

在 `doramate-frontend/src/utils/api.rs` 中新增了 `builtin_csharp_templates()` 函数：

```rust
pub fn builtin_csharp_templates() -> Vec<NodeTemplateConfigEntry> {
    vec![
        NodeTemplateConfigEntry {
            node_type: "csharp-simple-node".into(),
            name: "C# Simple Node".into(),
            description: "A simple C# Dora node with event handling".into(),
            icon: String::new(), path: None,
            inputs: Some(vec!["input".into()]),
            outputs: Some(vec!["output".into()]),
        },
        NodeTemplateConfigEntry {
            node_type: "csharp-operator".into(),
            name: "C# Operator (NativeAOT)".into(),
            description: "A C# Dora operator using NativeAOT".into(),
            icon: String::new(), path: None,
            inputs: Some(vec!["input".into()]),
            outputs: Some(vec!["output".into()]),
        },
    ]
}
```

该函数返回两个 C# 模板配置条目，供前端 "添加节点" 面板使用。DoraMate 前端可以在可视化编辑界面中将 C# 节点模板作为可选节点类型之一呈现给用户。

### 16.6 版本协调

NuGet 版本号与 LocalAgent / 前端统一协调：

| 组件 | 版本 |
|-------|-------|
| DoraMate.DoraNode | 0.2.0 |
| DoraMate.DoraOperator | 0.2.0 |
| DoraMate.Templates | 0.2.0 |
| doramate-localagent | 0.2.0 |
| doramate-frontend | 0.2.0 |

### 16.7 本地开发流程

```powershell
# 1. 构建原生库
pwsh ./scripts/build-native.ps1

# 2. 打包 SDK 到本地 NuGet 源
dotnet pack src/DoraNode/DoraNode.csproj -c Release -o artifacts/packages
dotnet pack src/DoraOperator/DoraOperator.csproj -c Release -o artifacts/packages

# 3. 安装模板
pwsh ./scripts/build-templates.ps1

# 4. 创建节点项目
dotnet new dora-node -n MyAnalysisNode
cd MyAnalysisNode
dotnet restore   # 从本地源 + nuget.org 解析依赖
dotnet build
```

仓库已内置 `nuget.config`，自动包含 `artifacts/packages` 作为本地源。

## 17. 参考文件

- `dora-api-csharp/README.md`
- `dora-api-csharp/QUICKSTART.md`
- `dora-api-csharp/BUILD.md`
- `dora-api-csharp/PROJECT_STRUCTURE.md`
- `dora-api-csharp/src/DoraNode/DoraNode.cs`
- `dora-api-csharp/src/DoraNode/DoraNodeOutputExtensions.cs`
- `dora-api-csharp/src/DoraOperator/DoraOperatorBase.cs`
- `dora-api-csharp/src/DoraOperator/OperatorOutput.cs`
- `dora-api-csharp/src/DoraOperator/OperatorEntrypoint.cs`
- `dora-api-csharp/samples/csharp-dataflow/`
- `dora-api-csharp/samples/csharp-operator-dataflow/`
- `dora-api-csharp/samples/csharp-node-operator-arrow-dataflow/`
- `dora-api-csharp/samples/csharp-async-node-dataflow/`
- `dora-api-csharp/tests/DoraNode.RegressionRunner/`
- `dora-api-csharp/tests/DoraOperator.RegressionRunner/`

---

最后更新: 2026-05-13  
状态: 基于当前仓库程序内容与文档整理  
包含 C# 模板产品化内容（Section 16）
