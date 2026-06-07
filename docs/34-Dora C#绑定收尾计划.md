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

**完成度：85-90%（v1.0 级别）**

Dora C# 绑定层已经过多轮开发与迭代，核心功能覆盖了常规节点（Node）和 Operator 开发的绝大部分场景。当前状态可以支撑实际用户的 C# 数据流节点开发，但距离正式公开发布（nuget.org）还有几项必须完成的工作。

### 1.2 仓库结构

| 目录 / 项目 | 说明 |
|-------------|------|
| `dora-api-csharp/src/DoraNode/` | DoraNode SDK 源码（25 个原生 P/Invoke 绑定） |
| `dora-api-csharp/src/DoraOperator/` | DoraOperator SDK 源码（15+ 个原生 P/Invoke 绑定） |
| `dora-api-csharp/tests/` | DoraNode / DoraOperator 回归测试 |
| `dora-api-csharp/templates/` | dotnet new 模板（dora-node / dora-operator） |
| `dora-api-csharp/scripts/` | 构建、打包、冒烟脚本 |
| `dora-api-csharp/samples/` | 10 个示例项目 |

---

## 2. 总体完成度

### 2.1 ✅ 已完成

| 模块 | 完成度 | 说明 |
|------|--------|------|
| **DoraNode SDK** | ~95% | 节点生命周期、同步/异步事件读取、多种输出发送、OpenTelemetry 上下文、诊断信息、错误码 |
| **DoraOperator SDK** | ~90% | `DoraOperatorBase` 抽象基类（Init/OnEvent/OnInput/OnStop）、`OperatorEntrypoint` 泛型桥接、类型化输出 |
| **Arrow 集成层** | ~85% | Arrow C ABI 托管包装（Array/Schema/Payload）、IPC 序列化、Schema 校验、类型化映射（Contract/Projector）、断言辅助 |
| **P/Invoke 绑定** | 100% | 自定义 `DllImportResolver` 多目录搜索，Node 端 25 个、Operator 端 15+ 个原生函数 |
| **示例项目** | 10 个 | 覆盖最小节点、多节点、Operator、Arrow 往返、异步、契约等场景 |
| **回归测试** | 15 个用例 | DoraNode 5 个 + DoraOperator 10 个（含 Decimal128/256） |
| **基础设施** | ✅ | 解决方案 `.sln`（28 项目）、`bootstrap-dora.ps1` + `build-native.ps1`、NuGet 打包、dotnet new 模板 |
| **CI 集成** | 部分 | 3 个 workflow 有 C# 构建步骤，但未跑完整冒烟套件 |

### 2.2 ⏳ 未完成 / 待改进

| 项目 | 当前状态 | 目标 |
|------|---------|------|
| 异步深度支持 | 后台线程泵模式 | 真正原生异步推送 |
| macOS / Linux 验证 | 加载逻辑已支持但未充分测试 | 三平台全部验证通过 |
| NuGet 公开发布 | 本地打包就绪 | 发布到 nuget.org |
| OpenTelemetry 集成 | 读取了 OTel 上下文 | 接入 .NET OTel SDK |
| 高级 Arrow 类型 | Union / FixedSizeBinary / Duration / Interval 未覆盖 | 完整覆盖 Arrow 类型体系 |
| 单元测试框架 | 自定义 Expect 类 | xUnit / NUnit |
| CI 冒烟覆盖 | 只构建了一个示例 | 跑完整冒烟套件 |

---

## 3. 收尾工作项

### 3.1 🔴 高优先级（v1.0 发布前必须完成）

| ID | 事项 | 说明 | 工作量 |
|----|------|------|--------|
| **C1** | CI 完整冒烟套件 | 在 CI workflow 中运行所有 10 个示例的构建 + 冒烟测试（当前只构建了 `csharp-dataflow` 一个）；需要先在 CI 中构建 native C 库并确保 artifacts 目录存在 | 1-2 天 |
| **C2** | NuGet 包发布到 nuget.org | 注册 nuget.org 账号（如尚未注册）、生成 API Key、配置发布 CI 或手动发布步骤；当前 `DoraMate.DoraNode` 和 `DoraMate.DoraOperator` 的 v0.9.0 包已本地就绪 | 0.5 天 |
| **C3** | 跨平台验证 | 在 macOS 和 Linux CI runner 上验证 C# 绑定能正常编译和运行（当前 P/Invoke 加载逻辑有多平台分支但未实测）；需要添加 Linux/macOS CI workflow 或扩展现有 workflow 的 matrix | 1-2 天 |
| **C4** | .NET SDK 版本确认 | ✅ 已完成（详见 [docs/33](33-DoraMate项目截止到2026年5月29日的完整工作评估.md)）— CI 用 .NET 8、所有 csproj target net8.0、无 .NET 10 特有 API | 0 |

### 3.2 🟡 中优先级（v1.0 建议包含）

| ID | 事项 | 说明 | 工作量 |
|----|------|------|--------|
| **C5** | 单元测试框架迁移 | 将自定义 `Expect` 类替换为 xUnit 或 NUnit，提高测试可读性和社区熟悉度；现有 15 个回归测试用例需同步迁移 | 1 天 |
| **C6** | 高级 Arrow 类型覆盖 | 补充 Union / FixedSizeBinary / Duration / Interval 类型的 C# 包装和示例 | 1-2 天 |
| **C7** | 异步深度支持重构 | 从后台线程泵模式改为真正的原生异步推送（`ValueTask` / `PipeReader` 模式），提升高性能场景表现 | 2-3 天 |

### 3.3 🟢 低优先级（v1.0 后可迭代）

| ID | 事项 | 说明 |
|----|------|------|
| **C8** | OpenTelemetry 集成 | 将当前读取的 OTel 上下文接入 .NET OpenTelemetry SDK（`Activity` / `ActivitySource`），实现端到端追踪 |
| **C9** | API 文档完善 | 补充 XML Doc 注释缺失部分（当前约 100+ 个 CS1591 warning），生成 API 文档站点 |
| **C10** | 模板在线安装 | 将 dotnet new 模板发布到 NuGet，支持 `dotnet new install DoraMate.Templates` 一键安装 |
| **C11** | 基准测试 | 对比 C# 绑定与原生 Rust 节点的吞吐量，识别性能瓶颈 |

---

## 4. 工作量估算与排期

### 4.1 v1.0 发布必须完成项（🔴）

```
C1  CI 完整冒烟套件      ████████████████  1-2 天
C2  NuGet 公开发布       ██████            0.5 天
C3  跨平台验证           ████████████████  1-2 天
                       ━━━━━━━━━━━━━━━━
累计                     2.5-4.5 天
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
- 第 1-2 天：C1 CI 冒烟 + C3 Linux/macOS 验证
- 第 3 天：C2 NuGet 发布
- **总计：2-3 天**

**方案 B：标准 v1.0（必须项 + 建议项）**
- 第 1-2 天：C1 CI 冒烟 + C3 跨平台 + C5 测试迁移
- 第 3-4 天：C6 高级 Arrow + C2 NuGet 发布
- **总计：3-4 天**

**方案 C：完整 v1.0（所有项）**
- 第 1-2 天：C1 CI 冒烟 + C3 跨平台
- 第 3-4 天：C5 测试迁移 + C6 高级 Arrow
- 第 5-7 天：C7 异步重构 + C2 NuGet 发布
- **总计：6-7 天**

---

## 5. 最终发布检查清单

| 检查项 | 状态 | 说明 |
|--------|------|------|
| CI 完整冒烟套件通过 | 🔲 | 所有 10 个示例构建 + 冒烟测试 |
| macOS / Linux CI 通过 | 🔲 | 至少基础构建和运行 |
| NuGet 包可正常安装使用 | ✅ | v0.9.0 本地已验证 |
| NuGet 包发布到 nuget.org | 🔲 | 可供全球用户 `dotnet add package` |
| API 文档无 CS1591 警告 | 🔲 | 当前 ~100+ 个缺少 XML 注释的警告 |
| 版本号一致性 | ✅ | 全部组件同步到 v0.9.0 |
| dotnet new 模板可用 | ✅ | 已验证 `dora-node` / `dora-operator` |

---

## 6. 相关文档索引

| 文档 | 说明 |
|------|------|
| [dora-csharp-progress-report.txt](../dora-csharp-progress-report.txt) | 进度报告源文件 |
| [README.md](../dora-api-csharp/README.md) | C# 绑定 README |
| [QUICKSTART.md](../dora-api-csharp/QUICKSTART.md) | 快速开始 |
| [BUILD.md](../dora-api-csharp/BUILD.md) | 构建指南 |
| [PROJECT_STRUCTURE.md](../dora-api-csharp/PROJECT_STRUCTURE.md) | 项目结构 |
| [docs/18](18-Dora%20C%23%20%E7%BB%91%E5%AE%9A%E5%BC%80%E5%8F%91%E4%B8%8E%E9%9B%86%E6%88%90%E6%8C%87%E5%8D%97.md) | C# 绑定开发与集成指南 |
| [docs/29](29-%E6%94%B6%E5%B0%BE%E5%8A%9F%E8%83%BD%E6%A8%A1%E5%9D%97%E4%B9%8BC%23%E6%A8%A1%E6%9D%BF%E4%BA%A7%E5%93%81%E5%8C%96%E5%BC%80%E5%8F%91%E6%80%BB%E7%BB%93.md) | C# 模板产品化开发总结 |
| [docs/33](33-DoraMate%E9%A1%B9%E7%9B%AE%E6%88%AA%E6%AD%A2%E5%88%B02026%E5%B9%B45%E6%9C%8829%E6%97%A5%E7%9A%84%E5%AE%8C%E6%95%B4%E5%B7%A5%E4%BD%9C%E8%AF%84%E4%BC%B0.md) | 完整工作评估（含 .NET SDK 版本确认） |
