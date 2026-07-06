# 37 - DoraMate 项目 0.10.0 最终收尾报告

> 日期：2026-07-06
> 范围：DoraMate 主项目发布包、LocalAgent / Frontend 版本一致性、示例 YAML 口径、C# 0.10.0 发布口径。

---

## 1. 收尾结论

DoraMate `0.10.0` 已完成本地发布收尾验证：

- 本地 release gate 通过。
- release build 通过。
- ZIP 包可解压运行。
- ZIP 内 LocalAgent 能直接托管前端页面。
- 版本号已统一到 `0.10.0`。
- C# SDK / 模板源码已同步到 `0.10.0`。

当前项目已经具备作为 `0.10.0` release artifact 的本地交付条件。公开发布层面仍需执行两个外部动作：

1. 推送 tag / GitHub Release。
2. 如需公开 C# `0.10.0` NuGet 包，设置 `NUGET_API_KEY` 后运行 NuGet publish。

---

## 2. 本轮完成事项

### 2.1 发布包启动体验

LocalAgent 已支持托管 release ZIP 中的 `frontend/` 目录：

- 找到 `frontend/index.html` 时，`http://127.0.0.1:52100/` 返回 DoraMate 前端。
- 前端 SPA fallback 指向 `index.html`。
- 找不到前端产物时，保留原 LocalAgent API index 页面。

相关修复：

- `doramate-localagent/src/main.rs`
- `scripts/package-zip.ps1`
- `scripts/package-installer.nsi`

### 2.2 版本一致性

当前源码版本统一为 `0.10.0`：

| 组件 | 文件 | 当前版本 |
| ---- | ---- | -------- |
| 版本号源 | `VERSION` | `0.10.0` |
| LocalAgent | `doramate-localagent/Cargo.toml` | `0.10.0` |
| Frontend | `doramate-frontend/Cargo.toml` | `0.10.0` |
| DoraNode SDK | `dora-api-csharp/src/DoraNode/DoraNode.csproj` | `0.10.0` |
| DoraOperator SDK | `dora-api-csharp/src/DoraOperator/DoraOperator.csproj` | `0.10.0` |
| DoraMate.Templates | `dora-api-csharp/templates/DoraMate.Templates.csproj` | `0.10.0` |

同时修复了 `scripts/build-release.ps1` 的 Cargo version 解析逻辑，避免 PowerShell `Select-String` 对 `-Raw` 字符串解析不稳定导致误判为空版本。

### 2.3 README 与示例 YAML 口径

已移除 `xydataflow.yml` 口径，改为当前真实文件：

- 前端打开：`doramate-examples/test.yml`
- 布局 sidecar：`doramate-examples/test.yml.layout.json`
- 命令行运行：`doramate-examples/dataflow.yml`

相关修复：

- `README.md`
- `doramate-examples/README.md`

### 2.4 示例构建链路

`doramate-examples/dataflow.yml` 已补齐 `build` 字段：

- Rust 节点通过 `cargo build --release -p ...` 构建。
- C# summary 节点通过 `dotnet build csharp_detection_summary/CSharpDetectionSummary.csproj -c Release -p:NuGetAudit=false` 构建。

前端转换器已保留导入 Dora YAML 中的 `build` 字段，避免前端打开、保存、运行后临时 YAML 丢失构建命令。

相关修复：

- `doramate-examples/dataflow.yml`
- `doramate-frontend/src/types.rs`
- `doramate-frontend/src/utils/converter.rs`

---

## 3. 验收记录

### 3.1 本地 release gate

命令：

```powershell
pwsh ./scripts/release-gate-local-runtime-standard.ps1 -Rounds 20
```

结果：

| 指标 | 结果 |
| ---- | ---- |
| LocalAgent tests | 52 / 52 passed |
| Frontend tests | 48 / 48 passed |
| Smoke rounds | 20 / 20 completed |
| Run success rate | 100% |
| Status confirmation rate | 100% |
| Stop success rate | 100% |
| Residual failures | 0 |
| Overall | passed |

证据文件：

- `out/release-gates/release-gate-local-runtime-20260706-095927.json`
- `out/release-gates/local-runtime-smoke-20260706-100015.json`

### 3.2 Release build

命令：

```powershell
pwsh ./scripts/build-release.ps1 -SkipTests
```

结果：通过。

产物：

- `doramate-localagent/target/release/doramate-localagent.exe`
- `doramate-frontend/dist/`
- `dora-api-csharp/third_party/dora/target/release/dora.exe`
- `out/dist/doramate-0.10.0-win-x64.zip`

说明：

- 本轮先单独执行 release gate，因此 release build 使用 `-SkipTests` 避免重复执行 20 轮门禁。
- `trunk build --release` 在当前 PowerShell 环境中会受 `NO_COLOR=1` 影响，脚本已在调用 Trunk 前临时移除该环境变量并在结束后恢复。

### 3.3 ZIP 解压烟测

解压目录：

```text
out/zip-smoke-20260706-101524
```

验证内容：

| 检查项 | 结果 |
| ------ | ---- |
| `bin/doramate-localagent.exe` 存在 | passed |
| `bin/dora.exe` 存在 | passed |
| `frontend/index.html` 存在 | passed |
| `/api/health` | `status=ok`, `version=0.10.0` |
| `/` | HTTP 200，返回 DoraMate 前端 HTML |
| 前端 JS asset | HTTP 200 |
| 烟测后进程残留 | 无 `doramate-localagent` / `dora` 残留 |

---

## 4. C# 0.10.0 发布口径

当前应采用以下明确口径：

| 项目 | 状态 |
| ---- | ---- |
| `DoraMate.DoraNode` v0.9.0 | 已公开发布到 nuget.org |
| `DoraMate.DoraOperator` v0.9.0 | 已公开发布到 nuget.org |
| `DoraMate.DoraNode` v0.10.0 | 源码已同步，待公开发布 |
| `DoraMate.DoraOperator` v0.10.0 | 源码已同步，待公开发布 |
| `DoraMate.Templates` v0.10.0 | 源码已同步，待公开发布 |

不要在文档中表述为 “0.10.0 已公开发布”，除非已经实际执行 NuGet publish 并确认 nuget.org 可安装。

推荐发布命令：

```powershell
cd dora-api-csharp
pwsh ./scripts/package-nuget.ps1
pwsh ./scripts/publish-nuget.ps1
```

前置条件：

- 已设置 `NUGET_API_KEY`。
- 已确认本地 pack 产物可用。
- 已确认版本号仍为 `0.10.0`。

---

## 5. 剩余事项

本轮代码和发布包收尾已完成。剩余事项是发布执行与仓库整理：

1. 清理或忽略本地构建产物，例如 `doramate-examples/csharp_detection_summary/bin/`、`out/zip-smoke-*`、`doramate-frontend/dist/`。
2. 决定是否将 release gate 结果 JSON 纳入发布留档。
3. 创建 `v0.10.0` tag 并触发 GitHub Release。
4. 如需公开 C# SDK / 模板，执行 NuGet publish。

---

## 6. 最终判断

从本地工程验收角度看，DoraMate `0.10.0` 已经完成收尾：

- 功能主链路可运行。
- 发布脚本可构建。
- ZIP 可解压烟测。
- C# SDK 版本口径明确。
- 文档入口已对齐当前文件与发布状态。

后续不建议继续追加功能，应进入冻结、提交、打 tag 和发布阶段。
