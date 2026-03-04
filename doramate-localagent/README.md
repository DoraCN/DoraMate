# DoraMate LocalAgent

基于 Axum 和 Tokio 构建的本地代理服务，作为 DoraMate 前端与 DORA 运行时之间的桥梁。提供 HTTP API 和 WebSocket 服务，负责管理 DORA 数据流的生命周期、文件操作和日志推送。

## 2026-03-03 今日更新

- `run_dataflow` 启动链路增强：`dora start` 失败时有界重试（最多 2 次）
- 失败诊断增强：超时/失败响应附带输出摘要与 runtime 端口快照
- 节点模板配置 API 完整化：读写 + 标准化去重 + 路径解析 + 单元测试
- 错误分支与可测试行为覆盖增强，测试总数更新为 23

## 项目结构

```
doramate-localagent/
├── src/
│   └── main.rs              # 主程序入口（所有功能模块）
├── Cargo.toml               # Rust 依赖配置
└── README.md                # 本文件
```

## 核心功能

### 1. DORA 运行时管理
- **自动启动** - 自动检测并启动 DORA Coordinator 和 Daemon
- **启动重试** - `dora start` 可恢复失败时自动重试一次（总尝试 2 次）
- **进程管理** - 跟踪和管理所有 DORA 数据流进程
- **状态查询** - 实时查询节点运行状态
- **进程清理** - 停止后清理残留节点进程

### 2. 文件操作
- **目录选择** - 原生目录选择对话框（使用 RFD 库）
- **文件打开** - YAML 文件选择与读取
- **路径读取** - 按路径读取文件内容
- **模板配置** - 节点模板配置的读写

### 3. WebSocket 推送
- **日志推送** - 实时推送 DORA 运行日志（1000 条回溯）
- **状态流** - 实时推送节点运行状态（800ms 间隔）

### 4. 错误处理
- **统一错误码** - 标准化的错误码体系
- **友好诊断** - 失败消息中包含输出摘要和端口快照
- **测试支持** - 完整的测试用例覆盖

## 技术栈

| 技术 | 用途 |
|------|------|
| **Axum 0.7** | Web 框架（HTTP + WebSocket） |
| **Tokio** | 异步运行时 |
| **Serde / serde_yaml** | 序列化/反序列化 YAML |
| **RFD 0.15** | 原生文件对话框 |
| **Tower-http** | 中间件（CORS） |
| **tracing** | 日志记录 |
| **UUID** | 进程 ID 生成 |
| **futures-util** | 异步工具流处理 |
| **tokio-tungstenite** | WebSocket 支持 |

## 安装与运行

### 环境要求

- Rust stable (1.70+)
- DORA CLI (`dora` 命令)

### 安装/验证 DORA CLI

先验证：

```bash
dora --version
```

若未安装，可使用 Cargo 方式安装：

```bash
cargo install --locked dora-cli
dora --version
```

如需其他安装方式，请参考 DORA 官方文档。

### 开发模式

```bash
cd doramate-localagent
cargo run
```

启动后默认监听：`http://127.0.0.1:52100`

### 发布构建

```bash
cd doramate-localagent
cargo build --release
```

产物位置：
- Windows: `target/release/doramate-localagent.exe`
- Linux/macOS: `target/release/doramate-localagent`

## API 端点

| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/health` | GET | 健康检查 |
| `/api/run` | POST | 运行数据流 |
| `/api/stop` | POST | 停止数据流 |
| `/api/select-directory` | POST | 选择目录 |
| `/api/open-dataflow-file` | POST | 打开文件 |
| `/api/read-dataflow-file` | POST | 读取文件 |
| `/api/node-templates-config` | GET/POST | 节点模板配置 |
| `/api/status/:process_id` | GET | 查询状态 |
| `/api/status-stream/:process_id` | GET | 状态流 WebSocket |
| `/api/logs/:process_id` | GET | 日志 WebSocket |

## API 使用示例

### 健康检查

```bash
curl http://127.0.0.1:52100/api/health
```

响应示例：
```json
{
  "status": "ok",
  "version": "0.1.0",
  "dora_installed": true,
  "dora_coordinator_running": true,
  "dora_daemon_running": true
}
```

### 运行数据流

```bash
curl -X POST http://127.0.0.1:52100/api/run \
  -H "Content-Type: application/json" \
  -d '{
    "dataflow_yaml": "nodes:\\n  - id: camera\\n    path: opencv-camera\\n",
    "working_dir": "C:\\\\path\\\\to\\\\workdir"
  }'
```

响应示例：
```json
{
  "success": true,
  "message": "Dataflow started successfully",
  "process_id": "c9be2e5d-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "error_code": null
}
```

运行行为说明（当前版本）：

- `dora start` 超时时间：20 秒
- 最大尝试次数：2
- 重试间隔：800ms
- 仅对可恢复错误重试（例如无进程输出、连接被拒绝、runtime 未就绪）

### 停止数据流

```bash
curl -X POST http://127.0.0.1:52100/api/stop \
  -H "Content-Type: application/json" \
  -d '{"process_id": "c9be2e5d-xxxx-xxxx-xxxx-xxxxxxxxxxxx"}'
```

### 选择目录

```bash
curl -X POST http://127.0.0.1:52100/api/select-directory
```

### 读取节点模板配置

```bash
curl http://127.0.0.1:52100/api/node-templates-config
```

## WebSocket 连接

### 日志 WebSocket

```javascript
const ws = new WebSocket('ws://127.0.0.1:52100/api/logs/{process_id}');
ws.onmessage = (event) => {
  const log = JSON.parse(event.data);
  console.log(`[${log.level}] ${log.source}: ${log.message}`);
};
```

### 状态流 WebSocket

```javascript
const ws = new WebSocket('ws://127.0.0.1:52100/api/status-stream/{process_id}');
ws.onmessage = (event) => {
  const status = JSON.parse(event.data);
  console.log(`状态：${status.status}`);
  console.log(`运行节点：${status.running_nodes}/${status.total_nodes}`);
};
```

## 错误码说明

| 错误码 | 说明 |
|--------|------|
| `DIRECTORY_SELECTION_CANCELLED` | 目录选择被取消 |
| `DIRECTORY_PICKER_FAILED` | 目录选择器失败 |
| `FILE_SELECTION_CANCELLED` | 文件选择被取消 |
| `FILE_PICKER_FAILED` | 文件选择器失败 |
| `FILE_PATH_EMPTY` | 文件路径为空 |
| `FILE_READ_FAILED` | 文件读取失败 |
| `YAML_WRITE_FAILED` | YAML 写入失败 |
| `DORA_NOT_INSTALLED` | DORA 未安装 |
| `DORA_RUNTIME_INIT_FAILED` | DORA 运行时初始化失败 |
| `DORA_START_WAIT_FAILED` | DORA 启动等待失败 |
| `DORA_START_TIMEOUT` | DORA 启动超时 |
| `DORA_START_FAILED` | DORA 启动失败 |
| `DORA_START_SPAWN_FAILED` | DORA 进程创建失败 |
| `STOP_PARTIAL_FAILURE` | 部分停止失败 |
| `NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE` | 节点模板配置路径不可用 |
| `NODE_TEMPLATES_CONFIG_READ_FAILED` | 节点模板配置读取失败 |
| `NODE_TEMPLATES_CONFIG_WRITE_FAILED` | 节点模板配置写入失败 |

## 配置常量

```rust
// DORA 端口配置
const DORA_COORDINATOR_PORT: u16 = 54500;   // Coordinator 端口
const DORA_CONTROL_PORT: u16 = 6012;        // 控制端口
const DORA_DAEMON_LOCAL_PORT: u16 = 54501;  // Daemon 本地端口

// 日志回溯限制
const LOG_BACKLOG_LIMIT: usize = 1000;

// dora start 重试配置
const DORA_START_TIMEOUT_SECS: u64 = 20;
const DORA_START_MAX_ATTEMPTS: usize = 2;
const DORA_START_RETRY_DELAY_MS: u64 = 800;
```

## 节点模板配置路径

配置文件存储路径：
- **Windows**: `%APPDATA%\DoraMate\node_templates.yml`
- **备选**: `%USERPROFILE%\AppData\Roaming\DoraMate\node_templates.yml`
- **Linux/macOS**: `$XDG_CONFIG_HOME/doramate/node_templates.yml`
- **备选**: `~/.config/doramate/node_templates.yml`

## 测试

运行测试：

```bash
cargo test
```

测试覆盖：
- 进程名标准化逻辑
- 进程清理逻辑
- 运行数据流错误处理
- 停止数据流错误处理
- 状态查询功能
- 文件读取功能
- 节点模板配置标准化（端口与条目去重）
- dora start 可重试/不可重试错误判定

当前验证结果（2026-03-03）：

- `cargo test --locked` 通过
- 23 passed, 0 failed

## 日志

日志输出示例：
```
2024-01-01T12:00:00.000  INFO 🚀 DoraMate LocalAgent starting...
2024-01-01T12:00:00.000  INFO 📡 Server listening on http://127.0.0.1:52100
2024-01-01T12:00:01.000  INFO run.request process_id=xxx working_dir=xxx yaml_path=xxx
2024-01-01T12:00:01.000  INFO Dora daemon started with PID: 12345
```

## 安全说明

- 服务仅绑定 `127.0.0.1:52100`（本地回环）
- CORS 允许任意来源（`allow_origin(Any)`）
- **注意**: 不要直接将此端口暴露到公网
- 如需远程访问，请在上层网关添加鉴权和访问控制

## 故障排查

### 端口已被占用

**错误**: `Address already in use`

**解决**:
```bash
# Windows - 查找占用进程
netstat -ano | findstr :52100
# 终止进程
taskkill /PID <pid> /F
```

### DORA 未安装

**错误**: `DORA is not installed`

**解决**:
```bash
cargo install --locked dora-cli
dora --version
```

## 贡献指南

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 创建 Pull Request

## 许可证

本项目采用 MIT 许可证。

## 相关链接

- [DoraMate 项目主页](../README.md)
- [DoraMate Frontend](../doramate-frontend/README.md)
- [DORA 官方文档](https://dora-rs.github.io/)
- [Axum 文档](https://docs.rs/axum/)
- [Tokio 文档](https://tokio.rs/)
