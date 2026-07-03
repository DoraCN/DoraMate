# 34 - Dora C# 绑定收尾计划

> 基于 `dora-csharp-progress-report.txt` 的现状评估与后续工作计划。
> 编制日期：2026-06-06

---

## 目录

1. [现状概览](#1-现状概览)
2. [总体完成度](#2-总体完成度)
3. [收尾工作项](#3-收尾工作项)
4. [工作量估算与排期](#4-工作量估算与排期)
5. [最终发布检查清单](#5-最终发布检查清单)
6. [相关文档索引](#6-相关文档索引)

---

## 1. 现状概览

### 1.1 总体评估

**完成度：90-95%（v1.0 级别）**

Dora C# 绑定层已经过多轮开发与迭代，核心功能覆盖了常规节点（Node）和 Operator 开发的绝大部分场景。当前状态可以支撑实际用户的 C# 数据流节点开发，但距离正式公开发布（nuget.org）还有几项必须完成的工作。

### 1.2 仓库结构

| 目录 / 项目                           | 说明                                              |
| ------------------------------------- | ------------------------------------------------- |
| `dora-api-csharp/src/DoraNode/`     | DoraNode SDK 源码（25 个原生 P/Invoke 绑定）      |
| `dora-api-csharp/src/DoraOperator/` | DoraOperator SDK 源码（15+ 个原生 P/Invoke 绑定） |
| `dora-api-csharp/tests/`            | DoraNode / DoraOperator 回归测试                  |
| `dora-api-csharp/templates/`        | dotnet new 模板（dora-node / dora-operator）      |
| `dora-api-csharp/scripts/`          | 构建、打包、冒烟脚本                              |
| `dora-api-csharp/samples/`          | 10 个示例项目                                     |

---

## 2. 总体完成度

### 2.1 ✅ 已完成

| 模块                       | 完成度    | 说明                                                                                                                                                                                                                    |
| -------------------------- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DoraNode SDK**     | ~95%      | 节点生命周期、同步/异步事件读取、多种输出发送、OpenTelemetry 上下文、诊断信息、错误码                                                                                                                                   |
| **DoraOperator SDK** | ~90%      | `DoraOperatorBase` 抽象基类（Init/OnEvent/OnInput/OnStop）、`OperatorEntrypoint` 泛型桥接、类型化输出                                                                                                               |
| **Arrow 集成层**     | ~95%      | Arrow C ABI 托管包装（Array/Schema/Payload）、IPC 序列化、Schema 校验、类型化映射（Contract/Projector）、断言辅助，已补齐 Union / FixedSizeBinary / Duration / Interval 等高级类型覆盖，并同步覆盖 Node / Operator 两侧 |
| **P/Invoke 绑定**    | 100%      | 自定义 `DllImportResolver` 多目录搜索，Node 端 25 个、Operator 端 15+ 个原生函数                                                                                                                                      |
| **示例项目**         | 10 个     | 覆盖最小节点、多节点、Operator、Arrow 往返、异步、契约等场景                                                                                                                                                            |
| **回归测试**         | 21 个用例 | 已迁移到 xUnit：DoraNode 9 个 + DoraOperator 12 个，统一通过 `dotnet test` 运行，并补齐高级 Arrow 类型断言 / 投影 / 契约覆盖                                                                                          |
| **基础设施**         | ✅        | 解决方案 `.sln`（28 项目）、`bootstrap-dora.ps1` + `build-native.ps1`、NuGet 打包、dotnet new 模板                                                                                                                |
| **NuGet 公开发布**   | ✅        | `DoraMate.DoraNode` / `DoraMate.DoraOperator` `v0.9.0` 已发布到 nuget.org，发布脚本与 GitHub Actions workflow 已补齐                                                                                              |
| **OpenTelemetry 集成** | ✅      | 已完成 Node / Operator 的 .NET `Activity` 接入、metadata-aware send、端到端 trace continuity smoke 与三平台 CI 门禁接入                                                                                              |
| **CI 集成**          | ✅        | 已接入完整示例构建、最小 bytes smoke、OTel Node smoke、OTel Operator smoke 与三平台 matrix                                                                                                                              |

### 2.2 ⏳ 未完成 / 待改进

| 项目               | 当前状态                   | 目标                                                                                                                                 |
| ------------------ | -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| 异步深度支持       | 后台线程泵模式             | 真正原生异步推送                                                                                                                     |
| macOS / Linux 验证 | 加载逻辑已支持但未充分测试 | 三平台全部验证通过                                                                                                                   |
| 单元测试框架       | ✅ 已迁移到 xUnit          | `DoraNode.RegressionRunner` / `DoraOperator.RegressionRunner` 已切换为标准测试项目，保留现有 19 个回归用例并接入 `dotnet test` |
| CI 冒烟覆盖        | 只构建了一个示例           | 跑完整冒烟套件                                                                                                                       |

---

## 3. 收尾工作项

### 3.1 🔴 高优先级（v1.0 发布前必须完成）

| ID           | 事项                     | 说明                                                                                                                                                                                                                                                                                                                                                                                                         | 工作量 |
| ------------ | ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------ |
| **C1** | CI 完整冒烟套件          | ✅ 已完成：已补齐 `build-csharp-sample-projects.ps1`、`smoke-localagent-run-status-stop.ps1`、`smoke-csharp-bindings.ps1` 与 release gate 脚本，并将 `local-runtime-e2e.yml` / `local-runtime-multi-dataflow-smoke.yml` / `local-runtime-pr-gate.yml` / `local-runtime-standard-release-gate.yml` 接入完整 10 个 C# 示例的构建 + 冒烟流程；本地最新 `complete` 套件结果为 `10/10` 全部通过 | 1-2 天 |
| **C2** | NuGet 包发布到 nuget.org | ✅ 已完成：`DoraMate.DoraNode` 和 `DoraMate.DoraOperator` 的 `v0.9.0` 已成功发布到 `nuget.org`；仓库同时补充了手动发布脚本 `scripts/publish-nuget.ps1` 与 `dora-csharp-nuget-publish.yml` workflow，后续可通过 `NUGET_API_KEY` 复用发布流程                                                                                                                                                    | 0.5 天 |
| **C3** | 跨平台验证               | ✅ 已完成门禁接入：已补齐 `build-native.ps1`，新增 `dora-csharp-cross-platform.yml` workflow，并将 native 构建、SDK/sample 构建、最小 bytes smoke、OTel Node smoke、OTel Operator smoke 纳入 `windows-2022` / `ubuntu-latest` / `macos-13` matrix；Linux / macOS 最终结果以 GitHub Actions 实际执行为准                                                                                 | 1-2 天 |
| **C4** | .NET SDK 版本确认        | ✅ 已完成（详见[docs/33](33-DoraMate项目截止到2026年5月29日的完整工作评估.md)）— CI 用 .NET 8、所有 csproj target net8.0、无 .NET 10 特有 API                                                                                                                                                                                                                                                                  | 0      |

### 3.2 🟡 中优先级（v1.0 建议包含）

| ID           | 事项                | 说明                                                                                                                                                                                                                                                 | 工作量 |
| ------------ | ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| **C5** | 单元测试框架迁移    | ✅ 已完成：已将两套自定义 `Expect` 控制台回归 runner 迁移为 xUnit 测试项目，删除 `Program.cs` 手写入口，统一改为 `dotnet test`；现有 19 个回归测试用例已全部迁移并本地通过                                                                     | 1 天   |
| **C6** | 高级 Arrow 类型覆盖 | ✅ 已完成：已为 DoraNode / DoraOperator 两侧补齐 Union / FixedSizeBinary / Duration / Interval 等高级 Arrow 类型的 Schema 校验、断言 / 投影辅助、契约样例与回归覆盖；当前本地 `dotnet test` 结果为 DoraNode `9/9`、DoraOperator `12/12` 全通过 | 1-2 天 |
| **C7** | 异步深度支持重构    | 从后台线程泵模式改为真正的原生异步推送（`ValueTask` / `PipeReader` 模式），提升高性能场景表现                                                                                                                                                    | 2-3 天 |

### 3.3 🟢 低优先级（v1.0 后可迭代）

| ID            | 事项               | 说明                                                                                                                                                                                                                                          |
| ------------- | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **C8**  | OpenTelemetry 集成 | ✅ 已完成：已将 Dora OTel 上下文接入 .NET `Activity` / `ActivitySource`，补齐 Node / Operator metadata-aware send，新增 OTel Node / Operator 示例与 smoke，并自动验证 trace id / parent span 连续性 |
| **C9**  | API 文档完善       | ✅ 已完成：已为 DoraNode / DoraOperator 公共 API、Arrow Contract / Projector / Assertions / Diagnostics 等主要类型补齐 XML Doc，`dotnet build dora-api-csharp.sln` 当前为 `0 warning / 0 error`；API 文档站点生成可在后续发布阶段单独接入 |
| **C10** | 模板在线安装       | ✅ 已完成：`DoraMate.Templates` 模板包已完成版本同步、模板依赖升级、本地 pack/install/build 验证，并接入与 `DoraMate.DoraNode` / `DoraMate.DoraOperator` 共用的 NuGet 发布链路；当前可通过 `dotnet new install DoraMate.Templates` 进行在线安装 |
| **C11** | 基准测试           | ✅ baseline 已完成：新增 C# benchmark dataflow 与自动化脚本，对比 C# 绑定和 Rust 原生节点 bytes 链路的 latency / throughput，并形成 [docs/36](36-Dora%20C%23%E7%BB%91%E5%AE%9A%E6%80%A7%E8%83%BD%E5%9F%BA%E5%87%86%E6%B5%8B%E8%AF%95%E6%8A%A5%E5%91%8A.md) |

---

## 4. 工作量估算与排期

### 4.1 v1.0 发布必须完成项（🔴）

```
C1  CI 完整冒烟套件      ✅ 已完成
C2  NuGet 公开发布       ✅ 已完成
C3  跨平台验证           ✅ 已完成门禁接入
                       ━━━━━━━━━━━━━━━━
剩余                     0 天
```

### 4.2 v1.0 建议包含项（🟡）

```
C5  单元测试框架迁移      ████████████████  1 天
C6  高级 Arrow 类型       ████████████████  1-2 天
C7  异步深度重构          ████████████████████████████  2-3 天
                       ━━━━━━━━━━━━━━━━
累计                      3-6 天
```

### 4.3 推荐排期方案

**方案 A：最小化 v1.0（仅必须项）**

- C1 CI 冒烟、C2 NuGet 发布、C3 跨平台门禁接入均已完成
- **剩余：0 天**

**方案 B：标准 v1.0（必须项 + 建议项）**

- C1 / C2 / C3 / C5 / C6 均已完成
- 剩余主要是 C7 异步深度重构
- **剩余：2-3 天**

**方案 C：完整 v1.0（所有项）**

- 已完成 C1 / C2 / C3 / C5 / C6 / C8 / C9 / C10
- 剩余 C7 异步深度重构；C11 已完成 baseline，后续多轮统计 / GC 指标可作为性能工程迭代
- **剩余：2-3 天**

---

## 5. 最终发布检查清单

| 检查项                   | 状态 | 说明                                                                                                             |
| ------------------------ | ---- | ---------------------------------------------------------------------------------------------------------------- |
| CI 完整冒烟套件通过      | ✅   | 已接入完整 10 个示例构建 + 冒烟测试，最新 `complete` 套件本地验证为 `10/10` 全通过                           |
| macOS / Linux CI 通过    | ✅   | 已新增跨平台 matrix workflow，覆盖 native 构建、C# 构建、最小 bytes smoke、OTel Node smoke 与 OTel Operator smoke；最终平台运行结果以 GitHub Actions 为准 |
| OpenTelemetry 端到端追踪 | ✅   | Node / Operator `Activity` 接入与 metadata 传播已完成，本地 smoke 已验证 trace id / parent span 连续性             |
| 性能基准测试 baseline    | ✅   | 已新增 C# benchmark dataflow / runner，并完成 C# vs Rust latency / throughput baseline                              |
| NuGet 包可正常安装使用   | ✅   | v0.9.0 本地已验证                                                                                                |
| NuGet 包发布到 nuget.org | ✅   | `DoraMate.DoraNode` / `DoraMate.DoraOperator` `v0.9.0` 已成功公开发布，可供全球用户 `dotnet add package` |
| API 文档无 CS1591 警告   | ✅   | 已补齐 XML 注释，`dotnet build dora-api-csharp.sln` 为 `0 warning / 0 error`                                 |
| 版本号一致性             | ✅   | 全部组件同步到 v0.9.0                                                                                            |
| dotnet new 模板可用      | ✅   | 已验证 `dora-node` / `dora-operator`                                                                         |

---

## 6. 相关文档索引

| 文档                                                                                                                                                                 | 说明                                 |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------ |
| [dora-csharp-progress-report.txt](../dora-csharp-progress-report.txt)                                                                                                   | 进度报告源文件                       |
| [README.md](../dora-api-csharp/README.md)                                                                                                                               | C# 绑定 README                       |
| [QUICKSTART.md](../dora-api-csharp/QUICKSTART.md)                                                                                                                       | 快速开始                             |
| [BUILD.md](../dora-api-csharp/BUILD.md)                                                                                                                                 | 构建指南                             |
| [PROJECT_STRUCTURE.md](../dora-api-csharp/PROJECT_STRUCTURE.md)                                                                                                         | 项目结构                             |
| [docs/18](18-Dora%20C%23%20%E7%BB%91%E5%AE%9A%E5%BC%80%E5%8F%91%E4%B8%8E%E9%9B%86%E6%88%90%E6%8C%87%E5%8D%97.md)                                                        | C# 绑定开发与集成指南                |
| [docs/29](29-%E6%94%B6%E5%B0%BE%E5%8A%9F%E8%83%BD%E6%A8%A1%E5%9D%97%E4%B9%8BC%23%E6%A8%A1%E6%9D%BF%E4%BA%A7%E5%93%81%E5%8C%96%E5%BC%80%E5%8F%91%E6%80%BB%E7%BB%93.md)   | C# 模板产品化开发总结                |
| [docs/33](33-DoraMate%E9%A1%B9%E7%9B%AE%E6%88%AA%E6%AD%A2%E5%88%B02026%E5%B9%B45%E6%9C%8829%E6%97%A5%E7%9A%84%E5%AE%8C%E6%95%B4%E5%B7%A5%E4%BD%9C%E8%AF%84%E4%BC%B0.md) | 完整工作评估（含 .NET SDK 版本确认） |
| [docs/36](36-Dora%20C%23%E7%BB%91%E5%AE%9A%E6%80%A7%E8%83%BD%E5%9F%BA%E5%87%86%E6%B5%8B%E8%AF%95%E6%8A%A5%E5%91%8A.md) | C# 绑定性能基准测试报告              |
