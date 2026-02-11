# 09 - DORA 本地集成方案

> **核心内容**: YAML 生成、DORA CLI 集成、实时监控、进程管理
> **技术栈**: Rust (Axum + Tokio) + Leptos WebAssembly

---

## 🔄 9.1 YAML 生成与解析

### 前端 → 后端 → DORA

```rust
// 前端数据结构 (共享类型)
// doramate-frontend/src/types.rs

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DataflowGraph {
    pub nodes: Vec<GraphNode>,
    pub edges: Vec<GraphEdge>,
    pub metadata: GraphMetadata,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GraphNode {
    pub id: String,
    pub path: String,
    pub build: Option<String>,
    pub inputs: Vec<NodeInput>,
    pub outputs: Vec<String>,
    pub env: Option<std::collections::HashMap<String, String>>,
    pub position: Option<Position>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct NodeInput {
    pub id: String,
    pub mapping: InputMapping,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum InputMapping {
    User { source: String, output: Option<String> },
    Timer { interval_ms: u64 },
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct GraphEdge {
    pub from: String,
    pub to: String,
    pub from_port: String,
    pub to_port: String,
}
```

### YAML 生成逻辑 (Rust)

```rust
// doramate-frontend/src/yaml_generator.rs

use serde_yaml;
use std::collections::HashMap;

pub fn generate_yaml(graph: &DataflowGraph) -> Result<String, String> {
    // 将前端数据结构转换为 DORA YAML 格式
    let dora_nodes: Vec<DoraNode> = graph.nodes.iter().map(|node| {
        let mut inputs = HashMap::new();

        for input in &node.inputs {
            let mapping = match &input.mapping {
                InputMapping::User { source, output } => {
                    format!("{}", output.as_ref().unwrap_or(&source.clone()))
                }
                InputMapping::Timer { interval_ms } => {
                    format!("dora/timer/millis/{}", interval_ms)
                }
            };
            inputs.insert(input.id.clone(), mapping);
        }

        DoraNode {
            id: node.id.clone(),
            path: node.path.clone(),
            build: node.build.clone(),
            inputs,
            outputs: node.outputs.clone(),
            env: node.env.clone().unwrap_or_default(),
        }
    }).collect();

    let descriptor = DoraDescriptor { nodes: dora_nodes };

    // 序列化为 YAML
    serde_yaml::to_string(&descriptor)
        .map_err(|e| format!("YAML 序列化失败: {}", e))
}

#[derive(Debug, serde::Serialize)]
struct DoraDescriptor {
    nodes: Vec<DoraNode>,
}

#[derive(Debug, serde::Serialize)]
struct DoraNode {
    id: String,
    path: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    build: Option<String>,
    inputs: HashMap<String, String>,
    outputs: Vec<String>,
    #[serde(skip_serializing_if = "HashMap::is_empty")]
    env: HashMap<String, String>,
}
```

### YAML 解析逻辑

```rust
// doramate-frontend/src/yaml_parser.rs

use serde_yaml;

pub fn parse_yaml(yaml_content: &str) -> Result<DataflowGraph, String> {
    // 解析 YAML
    let descriptor: DoraDescriptor = serde_yaml::from_str(yaml_content)
        .map_err(|e| format!("YAML 解析失败: {}", e))?;

    // 转换为前端数据结构
    let nodes: Vec<GraphNode> = descriptor.nodes.into_iter().map(|dora_node| {
        let inputs: Vec<NodeInput> = dora_node.inputs.iter().map(|(id, mapping)| {
            let parsed_mapping = if mapping.starts_with("dora/timer/") {
                let interval = mapping.split('/')
                    .last()
                    .and_then(|s| s.parse::<u64>().ok())
                    .unwrap_or(1000);
                InputMapping::Timer { interval_ms: interval }
            } else {
                let parts: Vec<&str> = mapping.split('/').collect();
                InputMapping::User {
                    source: parts.get(0).unwrap_or(&"").to_string(),
                    output: parts.get(1).map(|s| s.to_string())
                }
            };

            NodeInput {
                id: id.clone(),
                mapping: parsed_mapping,
            }
        }).collect();

        GraphNode {
            id: dora_node.id,
            path: dora_node.path,
            build: dora_node.build,
            inputs,
            outputs: dora_node.outputs,
            env: if dora_node.env.is_empty() { None } else { Some(dora_node.env) },
            position: None, // 由布局算法计算
        }
    }).collect();

    // 自动生成边
    let edges = generate_edges_from_nodes(&nodes);

    Ok(DataflowGraph {
        nodes,
        edges,
        metadata: GraphMetadata {
            name: "Imported Dataflow".to_string(),
            description: None,
            version: "1.0".to_string(),
        }
    })
}

fn generate_edges_from_nodes(nodes: &[GraphNode]) -> Vec<GraphEdge> {
    let mut edges = Vec::new();
    let node_map: HashMap<String, &GraphNode> = nodes.iter()
        .map(|n| (n.id.clone(), n))
        .collect();

    for node in nodes {
        for input in &node.inputs {
            if let InputMapping::User { source, output } = &input.mapping {
                if let Some(source_node) = node_map.get(source) {
                    edges.push(GraphEdge {
                        from: source.clone(),
                        to: node.id.clone(),
                        from_port: output.clone().unwrap_or_else(|| "output".to_string()),
                        to_port: input.id.clone(),
                    });
                }
            }
        }
    }

    edges
}
```

---

## 🖥️ 9.2 DORA CLI 集成

### 本地代理 API (Rust)

```rust
// doramate-localagent/src/main.rs

use axum::{
    extract::State,
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use std::process::Stdio;
use std::sync::{Arc, Mutex};
use tokio::process::Child;
use uuid::Uuid;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let app_state = Arc::new(AppState::new());

    let app = Router::new()
        .route("/api/run", post(run_dataflow))
        .route("/api/stop", post(stop_dataflow))
        .route("/api/validate", post(validate_dataflow))
        .with_state(app_state);

    let listener = tokio::net::TcpListener::bind("127.0.0.1:52100").await?;
    axum::serve(listener, app).await?;

    Ok(())
}

#[derive(Clone)]
struct AppState {
    processes: Arc<Mutex<HashMap<String, DoraProcess>>>,
}

#[derive(Clone, Debug)]
struct DoraProcess {
    id: String,
    yaml_path: String,
    child: Arc<Mutex<Option<Child>>>,
}

/// 运行数据流请求
#[derive(Deserialize, Debug)]
pub struct RunDataflowRequest {
    pub dataflow_yaml: String,
    pub working_dir: Option<String>,
}

/// 运行数据流响应
#[derive(Serialize)]
pub struct RunDataflowResponse {
    pub success: bool,
    pub message: String,
    pub process_id: Option<String>,
}
```

### 运行数据流实现

```rust
/// 运行数据流 API
async fn run_dataflow(
    State(state): State<Arc<AppState>>,
    Json(req): Json<RunDataflowRequest>,
) -> Json<RunDataflowResponse> {
    // 生成进程 ID
    let process_id = Uuid::new_v4().to_string();

    // 保存 YAML 到临时文件
    let temp_dir = std::env::temp_dir();
    let yaml_path = temp_dir.join(format!("doramate_{}.yml", process_id));
    let yaml_path_str = yaml_path.to_string_lossy().to_string();

    if let Err(e) = tokio::fs::write(&yaml_path, &req.dataflow_yaml).await {
        return Json(RunDataflowResponse {
            success: false,
            message: format!("Failed to write YAML: {}", e),
            process_id: None,
        });
    }

    // 验证 DORA 是否已安装
    if !check_dora_installed() {
        return Json(RunDataflowResponse {
            success: false,
            message: "DORA is not installed. Please install dora-cli first.".to_string(),
            process_id: None,
        });
    }

    // 启动 dora 进程
    let mut cmd = tokio::process::Command::new("dora");
    cmd.arg("start")
        .arg(&yaml_path_str)
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .kill_on_drop(true);

    // 设置工作目录
    if let Some(dir) = &req.working_dir {
        cmd.current_dir(dir);
    }

    match cmd.spawn() {
        Ok(child) => {
            // 保存进程信息
            let dora_process = DoraProcess {
                id: process_id.clone(),
                yaml_path: yaml_path_str.clone(),
                child: Arc::new(Mutex::new(Some(child))),
            };

            state.processes.lock().unwrap().insert(process_id.clone(), dora_process);

            Json(RunDataflowResponse {
                success: true,
                message: "Dataflow started successfully".to_string(),
                process_id: Some(process_id),
            })
        }
        Err(e) => {
            Json(RunDataflowResponse {
                success: false,
                message: format!("Failed to start dora: {}", e),
                process_id: None,
            })
        }
    }
}

/// 检查 DORA 是否已安装
fn check_dora_installed() -> bool {
    std::process::Command::new("dora")
        .arg("--version")
        .output()
        .map(|_| true)
        .unwrap_or(false)
}
```

### 验证数据流

```rust
/// 验证数据流请求
#[derive(Deserialize, Debug)]
pub struct ValidateDataflowRequest {
    pub dataflow_yaml: String,
}

/// 验证数据流响应
#[derive(Serialize)]
pub struct ValidateDataflowResponse {
    pub is_valid: bool,
    pub errors: Vec<String>,
    pub warnings: Vec<String>,
}

/// 验证数据流 API
async fn validate_dataflow(
    Json(req): Json<ValidateDataflowRequest>,
) -> Json<ValidateDataflowResponse> {
    // 尝试解析 YAML
    let parse_result: Result<serde_yaml::Value, _> = serde_yaml::from_str(&req.dataflow_yaml);

    match parse_result {
        Ok(value) => {
            // 基本语法验证通过，进行深度验证
            let mut errors = Vec::new();
            let mut warnings = Vec::new();

            // 验证必需字段
            if let Some(nodes) = value.get("nodes") {
                if let Some(nodes_array) = nodes.as_sequence() {
                    for (i, node) in nodes_array.iter().enumerate() {
                        // 验证节点 ID
                        if node.get("id").is_none() {
                            errors.push(format!("Node at index {} missing 'id' field", i));
                        }

                        // 验证节点路径
                        if node.get("path").is_none() {
                            errors.push(format!("Node at index {} missing 'path' field", i));
                        }

                        // 验证输入输出
                        if node.get("inputs").is_none() && node.get("outputs").is_none() {
                            warnings.push(format!("Node at index {} has no inputs or outputs", i));
                        }
                    }
                } else {
                    errors.push("'nodes' must be an array".to_string());
                }
            } else {
                errors.push("Missing 'nodes' field".to_string());
            }

            let is_valid = errors.is_empty();

            Json(ValidateDataflowResponse {
                is_valid,
                errors,
                warnings,
            })
        }
        Err(e) => {
            Json(ValidateDataflowResponse {
                is_valid: false,
                errors: vec![format!("YAML parsing error: {}", e)],
                warnings: Vec::new(),
            })
        }
    }
}
```

### 停止数据流

```rust
/// 停止数据流请求
#[derive(Deserialize, Debug)]
pub struct StopDataflowRequest {
    pub process_id: String,
}

/// 停止数据流响应
#[derive(Serialize)]
pub struct StopDataflowResponse {
    pub success: bool,
    pub message: String,
}

/// 停止数据流 API
async fn stop_dataflow(
    State(state): State<Arc<AppState>>,
    Json(req): Json<StopDataflowRequest>,
) -> Json<StopDataflowResponse> {
    let mut processes = state.processes.lock().unwrap();

    if let Some(dora_process) = processes.remove(&req.process_id) {
        // 尝试停止进程
        if let Some(mut child) = dora_process.child.lock().unwrap().take() {
            match child.start_kill() {
                Ok(_) => {
                    Json(StopDataflowResponse {
                        success: true,
                        message: "Dataflow stopped successfully".to_string(),
                    })
                }
                Err(e) => {
                    Json(StopDataflowResponse {
                        success: false,
                        message: format!("Failed to stop process: {}", e),
                    })
                }
            }
        } else {
            Json(StopDataflowResponse {
                success: false,
                message: "Process not found".to_string(),
            })
        }
    } else {
        Json(StopDataflowResponse {
            success: false,
            message: format!("Process {} not found", req.process_id),
        })
    }
}
```

---

## 📡 9.3 节点元数据获取

```rust
// doramate-frontend/src/node_registry.rs

use reqwest::Client;
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DoraHubNode {
    pub id: String,
    pub name: String,
    pub description: String,
    pub category: String,
    pub language: String,
    pub repository: Option<String>,
    pub inputs: std::collections::HashMap<String, String>,
    pub outputs: std::collections::HashMap<String, String>,
}

pub async fn fetch_nodes_from_hub() -> Result<Vec<DoraHubNode>, String> {
    let client = Client::new();

    let response = client
        .get("https://raw.githubusercontent.com/dora-rs/dora-hub/main/node-hub/registry.json")
        .send()
        .await
        .map_err(|e| format!("Failed to fetch nodes: {}", e))?;

    let hub_nodes: Vec<DoraHubNode> = response
        .json()
        .await
        .map_err(|e| format!("Failed to parse nodes: {}", e))?;

    Ok(hub_nodes)
}

pub async fn fetch_nodes_with_cache() -> Result<Vec<DoraHubNode>, String> {
    // 检查本地缓存
    let cache_path = dirs::cache_dir()
        .unwrap()
        .join("doramate")
        .join("nodes.json");

    // 如果缓存存在且未过期（24小时），直接返回
    if let Ok(metadata) = tokio::fs::metadata(&cache_path).await {
        let modified = metadata.modified().unwrap();
        let elapsed = modified.elapsed().unwrap();

        if elapsed.as_secs() < 86400 { // 24小时
            if let Ok(content) = tokio::fs::read_to_string(&cache_path).await {
                if let Ok(cached) = serde_json::from_str::<Vec<DoraHubNode>>(&content) {
                    return Ok(cached);
                }
            }
        }
    }

    // 从远程获取
    let nodes = fetch_nodes_from_hub().await?;

    // 保存到缓存
    if let Some(parent) = cache_path.parent() {
        tokio::fs::create_dir_all(parent).await.ok();
    }

    let json = serde_json::to_string_pretty(&nodes).unwrap();
    tokio::fs::write(&cache_path, json).await.ok();

    Ok(nodes)
}
```

---

## 🔔 9.4 实时日志与监控

### WebSocket 服务端 (Rust)

```rust
// doramate-localagent/src/websocket.rs

use axum::{
    extract::{
        State,
        ws::{Message, WebSocket, WebSocketUpgrade},
    },
    response::IntoResponse,
};
use futures::{sink::SinkExt, stream::StreamExt};
use std::sync::Arc;
use tokio::sync::broadcast;

pub fn websocket_router() -> Router<Arc<AppState>> {
    Router::new()
        .route("/ws/logs", get(websocket_logs_handler))
}

pub async fn websocket_logs_handler(
    ws: WebSocketUpgrade,
    State(state): State<Arc<AppState>>,
) -> impl IntoResponse {
    ws.on_upgrade(move |socket| handle_logs_socket(socket, state))
}

async fn handle_logs_socket(socket: WebSocket, state: Arc<AppState>) {
    let (mut sender, mut receiver) = socket.split();

    // 订阅日志广播频道
    let mut log_rx = state.log_tx.subscribe();

    // 发送日志
    let mut send_task = tokio::spawn(async move {
        while let Ok(log) = log_rx.recv().await {
            if sender.send(Message::Text(log)).await.is_err() {
                break;
            }
        }
    });

    // 接收客户端消息（保持连接）
    let recv_task = tokio::spawn(async move {
        while let Some(Ok(_)) = receiver.next().await {
            // 保持连接
        }
    });

    // 等待任一任务完成
    tokio::select! {
        _ = send_task => {},
        _ = recv_task => {},
    }
}
```

### 日志收集器

```rust
// doramate-localagent/src/logger.rs

use tokio::sync::broadcast;
use tokio::process::Child;
use std::sync::Arc;
use tokio::io::{AsyncBufReadExt, BufReader};

pub struct LogCollector {
    log_tx: broadcast::Sender<String>,
}

impl LogCollector {
    pub fn new(log_tx: broadcast::Sender<String>) -> Self {
        Self { log_tx }
    }

    pub async fn collect_logs_from_process(&self, mut child: Child) -> anyhow::Result<()> {
        // 读取标准输出
        if let Some(stdout) = child.stdout.take() {
            let tx = self.log_tx.clone();
            tokio::spawn(async move {
                let reader = BufReader::new(stdout);
                let mut lines = reader.lines();

                while let Ok(Some(line)) = lines.next_line().await {
                    let _ = tx.send(format!("[STDOUT] {}", line));
                }
            });
        }

        // 读取标准错误
        if let Some(stderr) = child.stderr.take() {
            let tx = self.log_tx.clone();
            tokio::spawn(async move {
                let reader = BufReader::new(stderr);
                let mut lines = reader.lines();

                while let Ok(Some(line)) = lines.next_line().await {
                    let _ = tx.send(format!("[STDERR] {}", line));
                }
            });
        }

        Ok(())
    }
}
```

### WebSocket 客户端 (Leptos)

```rust
// doramate-frontend/src/components/log_viewer.rs

use leptos::*;
use gloo_net::websocket::WebSocket;
use wasm_bindgen_futures::spawn_local;

#[component]
pub fn LogViewer(process_id: String) -> impl IntoView {
    let (logs, set_logs) = create_signal(Vec::new());

    // 连接 WebSocket
    let ws = WebSocket::open("ws://localhost:52100/ws/logs").unwrap();

    // 设置消息处理器
    let on_message = {
        let set_logs = set_logs.clone();
        move |msg: String| {
            set_logs.update(|logs| {
                logs.push(msg);
                // 限制日志条数
                if logs.len() > 1000 {
                    logs.remove(0);
                }
            });
        }
    };

    ws.set_binary_handler(|_| {});
    ws.set_json_handler(|_| {});

    // 接收消息
    {
        let ws = ws.clone();
        spawn_local(async move {
            while let Some(msg) = ws.recv().await {
                if let Ok(text) = msg {
                    on_message(text);
                }
            }
        });
    }

    // 订阅进程日志
    {
        let ws = ws.clone();
        spawn_local(async move {
            ws.send(format!("{{\"subscribe\": \"{}\"}}", process_id)).await;
        });
    }

    view! {
        <div class="log-viewer">
            <h3>"实时日志"</h3>
            <div class="log-container">
                <For
                    each=move || logs.get().clone()
                    key=|log| log.clone()
                    view=|log| {
                        view! {
                            <div class="log-entry">{log}</div>
                        }
                    }
                />
            </div>
        </div>
    }
}
```

---

## 🎯 9.5 集成架构

### 整体架构图

```
┌─────────────────────────────────────────────────────┐
│            DoraMate 前端应用                         │
│              (Leptos WebAssembly)                   │
├─────────────────────────────────────────────────────┤
│                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  │  节点面板    │  │  画布区域    │  │  属性面板    │
│  │              │  │              │  │              │
│  │ - 拖拽节点   │  │ - 编辑连接   │  │ - 编辑配置   │
│  │ - 节点库     │  │ - 自动布局   │  │ - 验证输入   │
│  └──────────────┘  └──────────────┘  └──────────────┘
│                                                      │
│  ┌────────────────────────────────────────────────┐ │
│  │  工具栏                                         │ │
│  │  [打开] [保存] [导入YAML] [导出] [本地运行⭐]    │ │
│  └────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                  ↕ HTTP/WebSocket (localhost:52100)
┌─────────────────────────────────────────────────────┐
│        DoraMate LocalAgent (本地代理)               │
│              (Axum 1.0 + Tokio)                     │
├─────────────────────────────────────────────────────┤
│                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  │  HTTP API    │  │  WebSocket   │  │  日志收集器   │
│  │              │  │              │  │              │
│  │ - /api/run   │  │ - /ws/logs   │  │ - stdout     │
│  │ - /api/stop  │  │ - 实时推送   │  │ - stderr     │
│  │ - /api/validate│              │  │ - 广播频道   │
│  └──────────────┘  └──────────────┘  └──────────────┘
│                                                      │
│  ┌────────────────────────────────────────────────┐ │
│  │  进程管理器                                     │ │
│  │  - tokio::process::Command                     │ │
│  │  - 生命周期管理                                 │ │
│  │  - 状态追踪                                     │ │
│  └────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                  ↕ tokio::process::Command
┌─────────────────────────────────────────────────────┐
│              DORA 本地环境                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  │ dora-daemon  │  │ dora-runtime │  │  节点进程    │
│  │ (守护进程)   │  │ (运行时)     │  │              │
│  │ - 共享内存   │  │ - 节点管理   │  │ ├─ camera    │
│  │ - 数据协调   │  │ - 进程隔离   │  │ ├─ yolo      │
│  └──────────────┘  └──────────────┘  │ └─ sam2      │
│                                       └──────────────┘
└─────────────────────────────────────────────────────┘
                  ↕ 硬件访问
┌─────────────────────────────────────────────────────┐
│              本地硬件资源                            │
│  [/dev/video0] [/dev/audio] [GPU] [串口/USB]        │
└─────────────────────────────────────────────────────┘
```

### 数据流图

```
┌──────────────────────────────────────────────────────┐
│  用户操作流程                                        │
└──────────────────────────────────────────────────────┘

1. 创建数据流
   用户拖拽节点
   → 前端生成 DataflowGraph
   → 实时验证连接

2. 保存数据流
   前端调用 generate_yaml()
   → 生成 YAML 内容
   → 通过 File System API 保存
   → 更新 recent.json

3. 运行数据流 ⭐
   前端调用 POST /api/run
   → 本地代理接收请求
   → 保存临时 YAML 文件
   → tokio::process::Command::new("dora")
   → 启动节点进程
   → 返回 process_id

4. 监控日志 ⭐
   前端连接 WebSocket /ws/logs
   → 订阅 process_id 日志
   → 实时接收 stdout/stderr
   → 显示在 LogViewer 组件

5. 停止数据流
   前端调用 POST /api/stop
   → 本地代理查找进程
   → child.start_kill()
   → 清理临时文件
```

---

## 🚀 9.6 完整集成示例

### 前端使用示例

```rust
// doramate-frontend/src/components/dataflow_runner.rs

use leptos::*;
use gloo_net::http::Request;

#[component]
pub fn DataflowRunner() -> impl IntoView {
    let (running, set_running) = create_signal(false);
    let (process_id, set_process_id) = create_signal(None);
    let (logs, set_logs) = create_signal(Vec::new());

    // 运行数据流
    let run_dataflow = create_action(|dataflow: &DataflowGraph| {
        let dataflow = dataflow.clone();
        async move {
            // 生成 YAML
            let yaml = generate_yaml(&dataflow).unwrap();

            // 调用本地代理 API
            let response = Request::post("http://localhost:52100/api/run")
                .json(&serde_json::json!({
                    "dataflow_yaml": yaml,
                    "working_dir": None::<String>
                }))
                .send()
                .await;

            if let Ok(resp) = response {
                if let Ok(result) = resp.json::<RunDataflowResponse>().await {
                    if result.success {
                        set_running.set(true);
                        set_process_id.set(Some(result.process_id.unwrap()));
                    } else {
                        // 显示错误
                    }
                }
            }
        }
    });

    // 停止数据流
    let stop_dataflow = create_action(move |_: &()| {
        let pid = process_id.get().unwrap();
        async move {
            let response = Request::post("http://localhost:52100/api/stop")
                .json(&serde_json::json!({
                    "process_id": pid
                }))
                .send()
                .await;

            if let Ok(_) = response {
                set_running.set(false);
                set_process_id.set(None);
            }
        }
    });

    view! {
        <div class="dataflow-runner">
            <button
                on:click=move |_| {
                    let dataflow = /* 获取当前数据流 */;
                    run_dataflow.dispatch(dataflow);
                }
                disabled=running
            >
                "运行数据流"
            </button>

            <button
                on:click=move |_| stop_dataflow.dispatch(())
                disabled=move || !running()
            >
                "停止"
            </button>

            {move || {
                if let Some(pid) = process_id.get() {
                    view! {
                        <div class="status">
                            "正在运行: " {pid}
                        </div>
                        <LogViewer process_id=pid />
                    }
                } else {
                    view! { <div>"未运行"</div> }
                }
            }}
        </div>
    }
}
```

---

## 📊 9.7 性能与稳定性

### 性能优化策略

**1. 进程启动优化**
```rust
// 预热 DORA daemon
async fn preheat_dora_daemon() -> Result<(), String> {
    let output = tokio::process::Command::new("dora")
        .arg("daemon")
        .arg("--version")
        .output()
        .await
        .map_err(|e| format!("Failed to check dora-daemon: {}", e))?;

    if output.status.success() {
        Ok(())
    } else {
        Err("dora-daemon not available".to_string())
    }
}
```

**2. 日志流控制**
```rust
// 限制日志频率
use tokio::time::{interval, Duration};

async fn throttle_logs(rx: broadcast::Receiver<String>) {
    let mut interval = interval(Duration::from_millis(100));
    let mut buffer = Vec::new();

    loop {
        tokio::select! {
            _ = interval.tick() => {
                // 批量发送日志
                if !buffer.is_empty() {
                    // 发送到前端
                    buffer.clear();
                }
            }
            Ok(log) = rx.recv() => {
                buffer.push(log);
                if buffer.len() > 100 {
                    // 立即发送
                    buffer.clear();
                }
            }
        }
    }
}
```

**3. 内存管理**
```rust
// 限制日志缓存大小
const MAX_LOG_ENTRIES: usize = 1000;

struct LogBuffer {
    entries: Vec<String>,
}

impl LogBuffer {
    fn push(&mut self, log: String) {
        self.entries.push(log);
        if self.entries.len() > MAX_LOG_ENTRIES {
            self.entries.remove(0);
        }
    }
}
```

### 稳定性保障

**1. 进程健康检查**
```rust
// 定期检查进程状态
async fn monitor_process_health(child: Arc<Mutex<Option<Child>>>) {
    let mut interval = interval(Duration::from_secs(5));

    loop {
        interval.tick().await;

        let mut guard = child.lock().unwrap();
        if let Some(child) = guard.as_mut() {
            // 检查进程是否还在运行
            match child.try_wait() {
                Ok(Some(status)) => {
                    // 进程已退出
                    error!("Process exited: {}", status);
                    *guard = None;
                }
                Ok(None) => {
                    // 进程仍在运行
                }
                Err(e) => {
                    error!("Failed to check process: {}", e);
                }
            }
        }
    }
}
```

**2. 自动重启机制**
```rust
// 数据流自动重启
async fn auto_restart_on_failure(
    yaml_path: String,
    max_retries: u32,
) -> Result<(), String> {
    let mut retries = 0;

    loop {
        if retries >= max_retries {
            return Err("Max retries exceeded".to_string());
        }

        match spawn_dora_process(&yaml_path).await {
            Ok(mut child) => {
                // 等待进程退出
                let status = child.wait().await.unwrap();

                if !status.success() {
                    error!("Process failed: {:?}, retrying...", status);
                    retries += 1;
                    tokio::time::sleep(Duration::from_secs(5)).await;
                } else {
                    return Ok(());
                }
            }
            Err(e) => {
                return Err(format!("Failed to spawn: {}", e));
            }
        }
    }
}
```

**3. 资源清理**
```rust
// 确保资源释放
struct ProcessGuard {
    child: Option<Child>,
    yaml_path: String,
}

impl Drop for ProcessGuard {
    fn drop(&mut self) {
        // 停止进程
        if let Some(mut child) = self.child.take() {
            let _ = child.start_kill();
        }

        // 删除临时文件
        let _ = std::fs::remove_file(&self.yaml_path);
    }
}
```

---

## 🛠️ 9.8 故障排查

### 常见问题

**问题 1: DORA 命令找不到**
```rust
// 解决方案：环境变量检查
pub async fn check_dora_environment() -> DoraEnvironmentCheck {
    let checks = vec![
        check_command("dora"),
        check_command("dora-daemon"),
        check_python_version(),
        check_required_crates(),
    ];

    let results = futures::future::join_all(checks).await;

    DoraEnvironmentCheck { results }
}
```

**问题 2: YAML 验证失败**
```rust
// 详细错误提示
pub fn validate_yaml_with_details(yaml: &str) -> ValidationResult {
    let mut errors = Vec::new();
    let mut warnings = Vec::new();

    // 1. 语法验证
    match serde_yaml::from_str::<serde_yaml::Value>(yaml) {
        Ok(value) => {
            // 2. 结构验证
            validate_structure(&value, &mut errors, &mut warnings);

            // 3. 语义验证
            validate_semantics(&value, &mut errors, &mut warnings);
        }
        Err(e) => {
            errors.push(format!("YAML 语法错误: {}", e));
        }
    }

    ValidationResult { errors, warnings }
}
```

**问题 3: 进程启动失败**
```rust
// 详细错误信息
pub async fn spawn_with_diagnostics(yaml_path: &str) -> Result<Child, SpawnError> {
    let mut cmd = tokio::process::Command::new("dora");
    cmd.arg("start").arg(yaml_path);

    // 捕获输出用于诊断
    cmd.stdout(Stdio::piped());
    cmd.stderr(Stdio::piped());

    match cmd.spawn() {
        Ok(child) => Ok(child),
        Err(e) => {
            let error = SpawnError {
                error: e.to_string(),
                suggestion: get_suggestion(&e),
                diagnostic_info: collect_diagnostic_info().await,
            };
            Err(error)
        }
    }
}

fn get_suggestion(error: &io::Error) -> String {
    if error.kind() == io::ErrorKind::NotFound {
        "请确认 DORA CLI 已安装并在 PATH 中".to_string()
    } else {
        "请检查 YAML 文件格式和 DORA 配置".to_string()
    }
}
```

---

## 📚 9.9 相关文档

**继续阅读**:
- 📖 [06 - Axum 后端架构](./06-Axum 后端架构.md) - 后端实现细节
- 📖 [10 - YAML 可视化功能](./10-YAML可视化功能.md) - YAML 解析实现
- 📖 [02 - Dora 架构分析](./02-Dora架构分析.md) - DORA 核心概念

**开发指南**:
- 🛠️ [DORA 官方文档](https://dora.carsmos.ai/docs)
- 🛠️ [Axum 示例](https://github.com/tokio-rs/axum)
- 🛠️ [Leptos 指南](https://leptos.dev)

---

## 🎯 总结

### 核心集成要点

1. **YAML 生成与解析**
   - 使用 `serde_yaml` 实现类型安全的序列化
   - 前后端共享数据类型定义
   - 支持完整的 DORA YAML 特性

2. **DORA CLI 集成**
   - 通过 `tokio::process` 调用 `dora` 命令
   - 进程生命周期管理
   - 实时日志收集与推送

3. **实时监控**
   - WebSocket 实时推送日志
   - 节点状态追踪
   - 错误通知

4. **性能与稳定性**
   - 进程健康检查
   - 自动重启机制
   - 资源清理保障

### 技术优势

| 特性 | Rust 实现优势 |
|-----|-------------|
| **类型安全** | 编译时检查，零运行时类型错误 |
| **性能** | 零成本抽象，异步 I/O 高性能 |
| **稳定性** | 内存安全，无泄漏，无 GC 停顿 |
| **代码复用** | 前后端共享类型定义 |

### 下一步

- ✅ 完善错误处理和恢复机制
- ✅ 添加更多节点验证规则
- ✅ 优化日志推送性能
- ✅ 支持分布式数据流（Zenoh）

---

**文档作者**: Claude Code
**最后更新**: 2026-01-30
**版本**: v1.0 (Rust 全栈实现)
**基于**: D:\rust-dora-main\DoraMate技术实现路径分析-完整版\09-Dora后端集成.md
