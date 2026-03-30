# C# 绑定项目结构

本文档描述 `dora-api-csharp/` 当前的真实结构与职责划分。

最新口径是：

- `src/DoraNode` 已是正式托管 Node API
- `src/DoraOperator` 已是正式托管 Operator API
- `samples/` 覆盖 bytes、Arrow、contract、operator、async 等主要场景
- `scripts/` 中的统一 smoke 是当前最重要的回归保护面
- `dora-api-csharp.sln` 已包含所有 C# sample 项目

## 顶层结构

```text
dora-api-csharp/
├── README.md
├── BUILD.md
├── QUICKSTART.md
├── PROJECT_STRUCTURE.md
├── MIGRATION_CHECKLIST.md
├── dora-api-csharp.sln
├── src/
├── samples/
├── scripts/
├── tests/
├── artifacts/
└── third_party/
```

各目录职责：

- `README.md`
  - 仓库总览、基础用法、错误码消费建议
- `BUILD.md`
  - 构建、输出目录、native 依赖定位
- `QUICKSTART.md`
  - 最短路径上手指南
- `PROJECT_STRUCTURE.md`
  - 当前结构与职责说明
- `MIGRATION_CHECKLIST.md`
  - 独立仓库迁移与对齐清单
- `dora-api-csharp.sln`
  - 当前主 solution，包含核心库、回归 runner、全部 sample

## src

`src/` 下放两个核心库项目：

```text
src/
├── DoraNode/
└── DoraOperator/
```

### `src/DoraNode`

`DoraNode` 是独立 C# 节点进程的托管 API。

主要文件：

- `DoraNode.cs`
  - 主入口
  - `Next()` / `NextAsync(...)` / `ReadAllEventsAsync(...)`
  - 输出发送
- `DoraEvent.cs`
  - 托管事件包装
- `EventType.cs`
  - 事件类型枚举
- `NativeMethods.cs`
  - Node C ABI 的 P/Invoke 绑定
- `NativeTypes.cs`
  - native 结构体映射
- `DoraDiagnostics.cs`
  - `DoraException`、错误码与运行时诊断
- `DoraNodeOutputExtensions.cs`
  - bytes / string / Arrow / `RecordBatch` 输出扩展

Arrow 相关文件：

- `ArrowTypes.cs`
- `ArrowSchemaValidation.cs`
- `ArrowRecordBatchBridge.cs`
- `ArrowRecordBatchAssertions.cs`
- `ArrowRecordBatchContract.cs`
- `ArrowRecordBatchProjector.cs`
- `ArrowRecordBatchSummary.cs`

### `src/DoraOperator`

`DoraOperator` 是 C# NativeAOT Operator 的托管 API 和运行时桥接层。

主要文件：

- `DoraOperatorBase.cs`
  - 用户继承基类
- `OperatorEvent.cs`
  - `InputEvent` / `InputClosedEvent` / `StopEvent` / `ErrorEvent`
- `OperatorTypes.cs`
  - 输入、结果、状态等基础类型
- `OperatorInitContext.cs`
  - 初始化上下文、runtime 配置快照
- `OperatorOutput.cs`
  - 统一输出发送入口
- `OperatorEntrypoint.cs`
  - 通用 init / on_event / drop 桥接
- `OperatorExports.cs`
  - native ABI 导出封装
- `OperatorHost.cs`
  - 托管对象生命周期管理
- `RawEventMarshaller.cs`
  - native event 到托管事件转换
- `SendOutputBridge.cs`
  - native send_output 到托管发送器桥接
- `NativeMethods.cs`
  - Operator C ABI 的 P/Invoke 绑定
- `NativeTypes.cs`
  - native 结构体映射
- `DoraDiagnostics.cs`
  - `DoraOperatorException` 与运行时诊断

Arrow 相关文件：

- `ArrowTypes.cs`
- `ArrowSchemaValidation.cs`
- `ArrowRecordBatchBridge.cs`
- `ArrowRecordBatchAssertions.cs`
- `ArrowRecordBatchContract.cs`
- `ArrowRecordBatchProjector.cs`
- `ArrowRecordBatchSummary.cs`

附加文件：

- `DoraOperatorBinding.cs`
  - 独立仓库自用绑定元信息文件，不属于上游必须文件

## samples

`samples/` 是当前支持面的真实说明，也是 smoke 的基础。

当前目录：

```text
samples/
├── csharp-advanced-arrow-node-dataflow/
├── csharp-arrow-node-dataflow/
├── csharp-async-node-dataflow/
├── csharp-complex-arrow-contract-node-dataflow/
├── csharp-dataflow/
├── csharp-multi-node/
├── csharp-node-operator-arrow-dataflow/
├── csharp-operator-arrow-roundtrip/
├── csharp-operator-contract-arrow-dataflow/
└── csharp-operator-dataflow/
```

分组说明：

- `csharp-dataflow/`
  - 最小 node 示例
- `csharp-multi-node/`
  - 多节点 producer / consumer 示例
- `csharp-operator-dataflow/`
  - 最小 operator 示例
- `csharp-arrow-node-dataflow/`
  - 纯 node Arrow round-trip
- `csharp-advanced-arrow-node-dataflow/`
  - 更丰富的 Arrow 标量类型
- `csharp-complex-arrow-contract-node-dataflow/`
  - contract + typed model 示例
- `csharp-node-operator-arrow-dataflow/`
  - `node -> operator -> node` Arrow 链路
- `csharp-operator-arrow-roundtrip/`
  - operator 内部 Arrow round-trip
- `csharp-operator-contract-arrow-dataflow/`
  - operator 侧 contract / 高阶 Arrow 示例
- `csharp-async-node-dataflow/`
  - async node 读取与生命周期边界

## scripts

`scripts/` 是当前仓库的自动化入口。

主要脚本：

- `bootstrap-dora.ps1`
  - 刷新 `third_party/dora`
- `build-native.ps1`
  - 构建并复制 native C ABI
- `rebuild-csharp-operators.ps1`
  - 统一重建 NativeAOT Operator 示例
- `smoke-csharp-bindings.ps1`
  - 全量 smoke 入口
- `smoke-doraoperator-arrow-roundtrip.ps1`
  - operator Arrow round-trip smoke
- `smoke-doraoperator-contract-arrow.ps1`
  - operator contract Arrow smoke
- `smoke-csharp-node-arrow.ps1`
  - node Arrow smoke
- `smoke-csharp-node-operator-arrow.ps1`
  - node -> operator -> node smoke
- `smoke-csharp-advanced-arrow.ps1`
  - advanced Arrow smoke
- `smoke-csharp-complex-arrow-contract.ps1`
  - complex contract smoke
- `test-doranode-regression.ps1`
  - `DoraNode` 回归 runner
- `test-doraoperator-regression.ps1`
  - `DoraOperator` 回归 runner
- `SmokeCommon.ps1`
  - 统一日志、超时、退出码、清理与诊断

## tests

`tests/` 下主要是两个回归 runner 项目：

```text
tests/
├── DoraNode.RegressionRunner/
└── DoraOperator.RegressionRunner/
```

职责：

- `DoraNode.RegressionRunner`
  - Node 侧 Arrow / contract / projector 回归
- `DoraOperator.RegressionRunner`
  - Operator 侧 Arrow / contract / projector 回归

## artifacts

`artifacts/` 是统一输出目录，不再使用上游 `output/`。

结构大致如下：

```text
artifacts/
├── native/
├── dotnet/
└── samples/
```

分层说明：

- `artifacts/native/`
  - `dora_node_api_c` / `dora_operator_api_c`
- `artifacts/dotnet/`
  - `DoraNode` / `DoraOperator` / regression runner
- `artifacts/samples/`
  - 所有 sample 项目输出

## third_party

`third_party/dora/` 是 vendored Dora 上游快照。

特点：

- 普通目录，不是 git submodule
- 用于构建 native C ABI
- 不在这个目录里做绑定功能改动

## 解决方案结构

`dora-api-csharp.sln` 当前包含：

- `src/` 下两个核心库
- `tests/` 下两个回归 runner
- `samples/` 下所有 C# sample 项目

这意味着：

- 可以用 IDE 统一浏览所有 C# 项目
- 可以做 solution 级编译检查
- 但功能是否正常仍应以 smoke 为准

## 推荐阅读顺序

如果第一次进入这个仓库，建议按以下顺序阅读：

1. `README.md`
2. `QUICKSTART.md`
3. `BUILD.md`
4. 一个最小 node 示例
   - `samples/csharp-dataflow/`
5. 一个最小 operator 示例
   - `samples/csharp-operator-dataflow/`
6. 一个 Arrow 端到端示例
   - `samples/csharp-node-operator-arrow-dataflow/`
7. 统一 smoke
   - `scripts/smoke-csharp-bindings.ps1`
