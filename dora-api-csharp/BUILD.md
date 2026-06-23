# Dora C# 绑定构建说明

本文档说明 `dora-api-csharp/` 当前的真实构建方式、输出目录和验证顺序。

最新口径是：

- `DoraNode` 已是正式托管 Node 库
- `DoraOperator` 已是正式托管 Operator 库
- 示例项目已全部纳入 `dora-api-csharp.sln`
- 统一 smoke 仍然比单纯 `dotnet build` 更有价值

## 构建目标

当前仓库主要有三类构建目标：

1. Rust 侧 C ABI 动态库
   - `dora-node-api-c`
   - `dora-operator-api-c`
2. C# 托管库
   - `src/DoraNode`
   - `src/DoraOperator`
3. 示例产物
   - 普通 C# node 可执行程序
   - NativeAOT operator 共享库

## 前置要求

- .NET SDK 8.0 或更高版本
- Rust toolchain
- Dora CLI
- Windows 上建议使用 `pwsh`
- NativeAOT 所需工具链
  - Windows: Visual Studio Build Tools / MSVC
  - Linux/macOS: 本机 C/C++ 工具链

## 推荐构建顺序

推荐按这个顺序执行：

1. 拉取 / 刷新 `third_party/dora`
2. 构建 native C ABI
3. 构建 solution
4. 定向跑示例或统一 smoke

这样做的原因：

- 先保证 native 层可用，能尽早发现 `DllNotFoundException`
- solution build 用于确认所有 C# 项目可加载、可编译
- smoke 用于确认真实运行链路，而不只是编译

## 1. 刷新上游 Dora 快照

在仓库根目录执行：

```powershell
pwsh ./scripts/bootstrap-dora.ps1
```

这个脚本会把上游 Dora 拉到：

```text
third_party/dora/
```

它使用普通目录而不是 git submodule，不会引入 `.gitmodules` 依赖。

## 2. 构建 native C ABI

推荐直接使用仓库脚本：

```powershell
pwsh ./scripts/build-native.ps1
```

该脚本会：

- 在 `third_party/dora` 下构建 `dora-node-api-c` / `dora-operator-api-c`
- 复制生成物到：

```text
artifacts/native/<rid>/
```

典型产物：

- Windows:
  - `artifacts/native/win-x64/dora_node_api_c.dll`
  - `artifacts/native/win-x64/dora_operator_api_c.dll`
- Linux:
  - `artifacts/native/linux-x64/libdora_node_api_c.so`
  - `artifacts/native/linux-x64/libdora_operator_api_c.so`
- macOS:
  - `artifacts/native/osx-x64/libdora_node_api_c.dylib`
  - `artifacts/native/osx-x64/libdora_operator_api_c.dylib`

如果你要手工构建，也可以在 `third_party/dora` 下执行：

```powershell
cargo build -p dora-node-api-c --release
cargo build -p dora-operator-api-c --release
```

## 3. 构建 C# 托管库与示例

当前主 solution 文件是：

```text
dora-api-csharp.sln
```

它现在包含：

- `src/` 下两个核心库项目
- `tests/` 下两个 regression runner
- `samples/` 下所有 C# 示例项目

统一构建命令：

```powershell
dotnet build ./dora-api-csharp.sln -c Release
```

如果只想构建核心库：

```powershell
dotnet build ./src/DoraNode/DoraNode.csproj -c Release
dotnet build ./src/DoraOperator/DoraOperator.csproj -c Release
```

## 4. NativeAOT Operator 构建

Operator 示例通常使用 `dotnet publish`，而不是 `dotnet build`：

```powershell
dotnet publish ./samples/csharp-operator-dataflow/CSharpCounterOperator.csproj -c Release
dotnet publish ./samples/csharp-node-operator-arrow-dataflow/RecordBatchForwardOperator.csproj -c Release
dotnet publish ./samples/csharp-operator-arrow-roundtrip/RecordBatchProducerOperator.csproj -c Release
dotnet publish ./samples/csharp-operator-arrow-roundtrip/RecordBatchVerifierOperator.csproj -c Release
```

如果要统一重建 C# operator 示例，使用：

```powershell
pwsh ./scripts/rebuild-csharp-operators.ps1 -Configuration Release
```

## 输出目录策略

当前仓库不使用上游 `output/` 目录，而是统一输出到：

```text
artifacts/
```

主要分层如下：

- `artifacts/native/`
  - native C ABI 动态库
- `artifacts/dotnet/`
  - `DoraNode` / `DoraOperator` / regression runner
- `artifacts/samples/`
  - 所有示例项目输出

典型示例：

```text
artifacts/dotnet/DoraNode/Release/net8.0/
artifacts/dotnet/DoraOperator/Release/net8.0/
artifacts/samples/csharp-dataflow/CSharpNode/Release/net8.0/
artifacts/samples/csharp-operator-arrow-roundtrip/RecordBatchProducerOperator/Release/net8.0/native/
```

## 推荐验证路径

### 最推荐：统一 smoke

```powershell
pwsh ./scripts/smoke-csharp-bindings.ps1
```

这是当前最有价值的验证方式，因为它覆盖：

- operator Arrow round-trip
- operator contract Arrow
- node Arrow round-trip
- node -> operator -> node
- advanced Arrow
- complex Arrow contract
- async node

### 单项验证

Node / Operator 回归：

```powershell
pwsh ./scripts/test-doranode-regression.ps1
pwsh ./scripts/test-doraoperator-regression.ps1
```

定向 smoke：

```powershell
pwsh ./scripts/smoke-doraoperator-arrow-roundtrip.ps1
pwsh ./scripts/smoke-doraoperator-contract-arrow.ps1
pwsh ./scripts/smoke-csharp-node-arrow.ps1
pwsh ./scripts/smoke-csharp-node-operator-arrow.ps1
pwsh ./scripts/smoke-csharp-advanced-arrow.ps1
pwsh ./scripts/smoke-csharp-complex-arrow-contract.ps1
```

## 常见问题

### 1. `DllNotFoundException`

优先检查：

1. 是否已经执行 `pwsh ./scripts/build-native.ps1`
2. `artifacts/native/<rid>/` 是否存在对应动态库
3. 当前进程架构与 native 库架构是否一致

### 2. `BadImageFormatException`

通常是架构不匹配，例如：

- x64 进程加载了 x86 动态库
- 或反过来

### 3. solution build 出现文件锁

如果之前手工运行过 sample 进程，可能会锁住 `artifacts/samples/.../*.dll`。

处理方式：

1. 结束残留 sample 进程
2. 再重新顺序执行：

```powershell
dotnet build ./dora-api-csharp.sln -c Release
```

### 4. NativeAOT Operator 构建失败

优先检查：

1. NativeAOT 工具链是否完整
2. 是否使用了 `dotnet publish -c Release`
3. 是否错误地把 Operator 当作普通托管插件 DLL

## 5. 模板（dotnet new）

仓库内置了两个 `dotnet new` 模板，用于快速创建 C# Dora node / operator 项目：

| 模板名         | 说明                                               |
|----------------|----------------------------------------------------|
| `dora-node`    | 最小 C# Dora node，同步读事件循环 + 输出           |
| `dora-operator`| 最小 C# NativeAOT Dora operator，演示 Init/OnInput |

模板内容在 `templates/` 目录下管理，但不参与主 solution 的编译构建（仅打包为 NuGet 模板包）。

### 构建与安装

```powershell
pwsh ./scripts/build-templates.ps1 -Force
```

该脚本会：

1. 从仓库根 `VERSION` 读取模板包版本
2. `dotnet pack` 生成 `artifacts/templates/DoraMate.Templates.*.nupkg`
3. `dotnet new install` 将模板注册到当前 .NET SDK

### 使用

安装后即可创建新项目：

```powershell
# 创建 Dora node 项目
dotnet new dora-node -n MyCustomNode
cd MyCustomNode
dotnet build

# 创建 Dora operator 项目
dotnet new dora-operator -n MyCustomOp
cd MyCustomOp
dotnet publish -c Release
```

### 本地开发 / 离线测试

模板依赖 `DoraNode` / `DoraOperator` NuGet 包。在正式发布到 nuget.org 之前，可在本地打包并测试：

```powershell
# 1. 构建核心库和模板包
pwsh ./scripts/package-nuget.ps1 -Configuration Release

# 2. 构建并安装模板
pwsh ./scripts/build-templates.ps1 -Force

# 3. 创建测试项目
dotnet new dora-node -n TestNode
cd TestNode
dotnet restore
dotnet build
```

### 发布到 nuget.org

如果需要将 SDK 和模板包正式发布到 `nuget.org`：

```powershell
$env:NUGET_API_KEY = "<your-nuget-api-key>"
pwsh ./scripts/publish-nuget.ps1 -Configuration Release
```

说明：

- `publish-nuget.ps1` 会从仓库根目录 `VERSION` 读取版本号
- 默认会先执行 `scripts/package-nuget.ps1`，确保 `artifacts/nuget/` 中的 SDK 包和模板包都是最新版本
- 发布内容包含 `DoraMate.DoraNode`、`DoraMate.DoraOperator` 和 `DoraMate.Templates`
- 推送时使用 `dotnet nuget push --skip-duplicate`，重复发布同一版本时会安全跳过
- 如果你已经确认 `artifacts/nuget/` 中的包可直接发布，可加 `-SkipPack`

GitHub Actions 也提供了手动入口：

- workflow: `dora-csharp-nuget-publish`
- secret: `NUGET_API_KEY`
- 可选输入：`skip_pack=true` 以复用现有 `artifacts/nuget/*.nupkg`

### 在线安装（nuget.org）

模板包发布后，开发者可以直接从 NuGet 安装：

```powershell
dotnet new install DoraMate.Templates
dotnet new dora-node -n MyCustomNode
dotnet new dora-operator -n MyCustomOp
```

### 卸载

```powershell
dotnet new uninstall DoraMate.Templates
```

### 模板参数

**dora-node:**

| 参数               | 默认值       | 说明                            |
|--------------------|--------------|---------------------------------|
| `--NodeName`       | `MyDoraNode` | Entry-point class / namespace   |
| `--TargetFramework`| `net8.0`     | 目标框架 (`net8.0` / `net9.0`)  |

**dora-operator:**

| 参数               | 默认值        | 说明                            |
|--------------------|---------------|---------------------------------|
| `--OperatorName`   | `MyOperator`  | Operator 类名和文件名           |
| `--TargetFramework`| `net8.0`      | 目标框架 (`net8.0` / `net9.0`)  |

## 当前推荐开发工作流

1. 修改 `src/DoraNode` / `src/DoraOperator`
2. 必要时执行 `pwsh ./scripts/build-native.ps1`
3. 执行 `dotnet build ./dora-api-csharp.sln -c Release`
4. 跑相关 smoke / regression
5. 最后跑 `pwsh ./scripts/smoke-csharp-bindings.ps1`
