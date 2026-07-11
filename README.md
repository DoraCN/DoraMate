# DoraMate

DoraMate 是一个面向 [Dora](https://github.com/dora-rs/dora) 数据流的本地可视化编辑与运行工具链。当前仓库主要由四个子项目组成：

1. `doramate-frontend`：基于 Leptos + WASM 的前端可视化编辑器
2. `doramate-localagent`：基于 Axum 的本地代理服务，负责文件操作、运行 Dora、状态与日志推送
3. `doramate-examples`：Rust 示例数据流（摄像头采集 + 目标检测 + 可视化 + 录制）
4. `dora-api-csharp`：Dora C# 语言绑定工作区，提供 Node / Operator SDK、样例与回归脚本

## 仓库结构

```text
DoraMate/
├── doramate-frontend/     # 前端编辑器（Web）
├── doramate-localagent/   # 本地服务（HTTP + WebSocket）
├── doramate-examples/     # 示例数据流与节点
├── dora-api-csharp/       # Dora C# 绑定、样例与验证脚本
├── docs/                  # 设计与架构文档
└── README.md
```

## 组件关系

运行链路如下：

1. 用户在 `doramate-frontend` 中打开/编辑 YAML（包含布局元数据）
2. 前端调用 `doramate-localagent` 的 `http://127.0.0.1:52100/api/*`
3. `localagent` 负责写入运行 YAML、调用 `dora start`、并通过 WebSocket 回传日志与状态
4. 数据流示例通常来自 `doramate-examples`

## 快速开始（推荐路径）

### 1. 环境准备

- Rust stable（建议 1.70+）
- Dora CLI（`dora --version` 可用）
- Trunk（用于前端开发：`trunk --version`）
- Windows 下运行 `doramate-examples` 还需要 OpenCV/vcpkg（默认路径为 `E:\vcpkg\installed\x64-windows`）

可选安装命令：

```powershell
cargo install --locked dora-cli
cargo install --locked trunk
```

### 2. 启动 LocalAgent

```powershell
cd doramate-localagent
cargo run
```

默认监听：`http://127.0.0.1:52100`

### 3. 启动前端

新开一个终端：

```powershell
cd doramate-frontend
trunk serve --open
```

### 4. 在前端打开示例 YAML

在网页里选择“打开/导入 YAML”，加载：

`doramate-examples/test.yml`

如果同目录下存在 `test.yml.layout.json`，前端会自动加载布局 sidecar。随后可在画布中查看与编辑节点布局、连线和参数，并通过前端触发运行。

## YAML 文件约定（非常重要）

`doramate-examples` 中当前主要有两类文件：

1. `test.yml` + `test.yml.layout.json`
   - 给 DoraMate 前端使用
   - `test.yml` 是标准 Dora YAML
   - `test.yml.layout.json` 保存 DoraMate 画布布局
   - 命令行运行推荐使用完整的 `dataflow.yml`
2. `dataflow.yml`
   - Dora CLI 可直接运行的标准描述
   - 命令行运行应使用它

前端保存布局时会额外写入同名 `.layout.json` sidecar；命令行运行时只需要使用标准 Dora YAML。

正确方式：

```powershell
dora start dataflow.yml
```

## 各子项目说明

### doramate-frontend

- 技术栈：Leptos 0.7 + WebAssembly
- 主要能力：
  - 可视化编辑节点与连线
  - YAML 导入/导出与格式转换
  - 调用 localagent 运行/停止数据流
  - 通过 WebSocket 展示日志和状态
- 关键代码：
  - `src/utils/api.rs`：API 基地址、HTTP 调用、WebSocket 客户端
  - `src/utils/converter.rs`：DoraMate YAML 与 Dora 兼容 YAML 转换

### doramate-localagent

- 技术栈：Axum 0.7 + Tokio
- 默认地址：`127.0.0.1:52100`
- 主要 API：
  - `GET /api/health`
  - `POST /api/run`
  - `POST /api/stop`
  - `POST /api/select-directory`
  - `POST /api/open-dataflow-file`
  - `POST /api/read-dataflow-file`
  - `GET/POST /api/node-templates-config`
  - `GET /api/status/:process_id`
  - `GET /api/status-stream/:process_id`（WebSocket）
  - `GET /api/logs/:process_id`（WebSocket）
- 运行策略：
  - 启动 Dora 失败会做有界重试
  - 运行错误返回标准错误码，便于前端做友好提示

### doramate-examples

- Rust workspace，包含 4 个节点：
  - `webcam`
  - `object_detection`
  - `viewer`
  - `recorder`
- 模型文件默认路径：
  - `object_detection/models/yolov8n.safetensors`
- 命令行运行示例：

```powershell
cd doramate-examples
cargo build --release
dora start dataflow.yml
```

### dora-api-csharp

- 技术栈：.NET 8 + C# + NativeAOT + Dora C ABI
- 主要能力：
  - 提供 `DoraNode` 托管 API，用于编写独立 C# node
  - 提供 `DoraOperator` 托管 API，用于编写可被 Dora runtime 加载的 NativeAOT operator
  - 支持 bytes、UTF-8 string 和 Arrow `RecordBatch` 数据收发
  - 提供 samples、smoke 脚本和 regression runner 验证 C# 绑定运行链路
- 关键代码：
  - `src/DoraNode/DoraNode.cs`：Node 生命周期、`Next()` / `NextAsync(...)`、输出发送主入口
  - `src/DoraOperator/DoraOperatorBase.cs`：Operator 继承基类与事件处理模型
  - `src/DoraOperator/OperatorEntrypoint.cs`：NativeAOT operator 的 init / on_event / drop 导出桥接
  - `scripts/smoke-csharp-bindings.ps1`：统一 smoke 入口，串联 native 构建、sample 运行与回归验证

## 常见问题

### 1. 前端提示无法连接 LocalAgent

- 先确认 `doramate-localagent` 已启动
- 确认 `http://127.0.0.1:52100/api/health` 可访问

### 2. `unknown field '__doramate__'`

- 原因：把包含 DoraMate 内嵌布局元数据的旧格式 YAML 直接给了 Dora CLI
- 处理：改用 `dataflow.yml` 运行

### 3. `dora` 命令不可用

- 安装 `dora-cli` 并确认 `dora --version`

### 4. 示例运行时报 OpenCV 或模型相关错误

- 检查 `E:\vcpkg\installed\x64-windows\bin` 是否在 PATH 中
- 检查 `object_detection/models/yolov8n.safetensors` 是否存在

## 相关文档

- [doramate-frontend/README.md](./doramate-frontend/README.md)
- [doramate-localagent/README.md](./doramate-localagent/README.md)
- [doramate-examples/README.md](./doramate-examples/README.md)
- [dora-api-csharp/README.md](./dora-api-csharp/README.md)
- [docs/](./docs/)
