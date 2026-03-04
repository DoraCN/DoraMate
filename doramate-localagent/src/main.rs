use axum::{
    extract::{
        ws::{Message, WebSocket, WebSocketUpgrade},
        Path, State,
    },
    response::Html,
    routing::{get, post},
    Json, Router,
};
use futures_util::{SinkExt, StreamExt};
use serde::{Deserialize, Serialize};
use serde_yaml::Value;
use std::collections::{HashMap, HashSet, VecDeque};
use std::net::{SocketAddr, TcpStream};
use std::process::Stdio;
use std::sync::{Arc, Mutex};
use std::time::Duration;
use tokio::process::Command;
use tokio::sync::broadcast;
use tower_http::cors::{Any, CorsLayer};
use tracing::{error, info, warn};
use uuid::Uuid;

const LOG_BACKLOG_LIMIT: usize = 1000;

const ERR_DIRECTORY_SELECTION_CANCELLED: &str = "DIRECTORY_SELECTION_CANCELLED";
const ERR_DIRECTORY_PICKER_FAILED: &str = "DIRECTORY_PICKER_FAILED";
const ERR_FILE_SELECTION_CANCELLED: &str = "FILE_SELECTION_CANCELLED";
const ERR_FILE_PICKER_FAILED: &str = "FILE_PICKER_FAILED";
const ERR_FILE_PATH_EMPTY: &str = "FILE_PATH_EMPTY";
const ERR_FILE_READ_FAILED: &str = "FILE_READ_FAILED";
const ERR_NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE: &str = "NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE";
const ERR_NODE_TEMPLATES_CONFIG_READ_FAILED: &str = "NODE_TEMPLATES_CONFIG_READ_FAILED";
const ERR_NODE_TEMPLATES_CONFIG_WRITE_FAILED: &str = "NODE_TEMPLATES_CONFIG_WRITE_FAILED";
const ERR_YAML_WRITE_FAILED: &str = "YAML_WRITE_FAILED";
const ERR_DORA_NOT_INSTALLED: &str = "DORA_NOT_INSTALLED";
const ERR_DORA_RUNTIME_INIT_FAILED: &str = "DORA_RUNTIME_INIT_FAILED";
const ERR_DORA_START_WAIT_FAILED: &str = "DORA_START_WAIT_FAILED";
const ERR_DORA_START_TIMEOUT: &str = "DORA_START_TIMEOUT";
const ERR_DORA_START_FAILED: &str = "DORA_START_FAILED";
const ERR_DORA_START_SPAWN_FAILED: &str = "DORA_START_SPAWN_FAILED";
const ERR_STOP_PARTIAL_FAILURE: &str = "STOP_PARTIAL_FAILURE";
const DORA_START_TIMEOUT_SECS: u64 = 20;
const DORA_START_MAX_ATTEMPTS: usize = 2;
const DORA_START_RETRY_DELAY_MS: u64 = 800;

/// 从 DoraMate YAML 中提取纯净的 DORA YAML（移除 __doramate__ 元数据）
fn extract_clean_dora_yaml(yaml: &str) -> String {
    if let Some(doramate_pos) = yaml.find("__doramate__:") {
        if let Some(newline_pos) = yaml[doramate_pos..].find('\n') {
            return yaml[newline_pos..].to_string();
        }
    }
    yaml.to_string()
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::INFO)
        .init();

    info!("🚀 DoraMate LocalAgent starting...");

    let app_state = Arc::new(AppState::new());

    // 构建路由
    let app = Router::new()
        .route("/api/health", get(health_check))
        .route("/api/run", post(run_dataflow))
        .route("/api/stop", post(stop_dataflow))
        .route("/api/select-directory", post(select_directory))
        .route("/api/open-dataflow-file", post(open_dataflow_file))
        .route("/api/read-dataflow-file", post(read_dataflow_file))
        .route(
            "/api/node-templates-config",
            get(read_node_templates_config).post(write_node_templates_config),
        )
        .route("/api/status/:process_id", get(get_dataflow_status))
        .route("/api/status-stream/:process_id", get(status_stream_handler))
        .route("/api/logs/:process_id", get(logs_handler))
        .route("/", get(index))
        .layer(
            CorsLayer::new()
                .allow_origin(Any)
                .allow_methods(Any)
                .allow_headers(Any),
        )
        .with_state(app_state);

    let addr = "127.0.0.1:52100";
    info!("📡 Server listening on http://{}", addr);

    let listener = tokio::net::TcpListener::bind(addr).await?;
    axum::serve(listener, app).await?;

    Ok(())
}

/// 首页
async fn index() -> Html<&'static str> {
    Html(
        r#"
    <!DOCTYPE html>
    <html>
    <head>
        <title>DoraMate LocalAgent</title>
    </head>
    <body>
        <h1>DoraMate LocalAgent API</h1>
        <p>Local agent is running!</p>
        <h2>API Endpoints:</h2>
        <ul>
            <li>GET /api/health - Health check</li>
            <li>POST /api/run - Run dataflow</li>
            <li>POST /api/stop - Stop dataflow</li>
            <li>POST /api/open-dataflow-file - Open file picker and read YAML</li>
            <li>POST /api/read-dataflow-file - Read YAML by file path</li>
            <li>GET /api/node-templates-config - Load node templates config YAML</li>
            <li>POST /api/node-templates-config - Save node templates config YAML</li>
            <li>GET /api/status-stream/:process_id - WebSocket status stream</li>
            <li>GET /api/logs/:process_id - WebSocket logs</li>
        </ul>
    </body>
    </html>
    "#,
    )
}

/// 应用状态
#[derive(Clone)]
struct AppState {
    processes: Arc<Mutex<HashMap<String, DoraProcess>>>,
    #[cfg(test)]
    test_behavior: Arc<Mutex<TestBehavior>>,
}

impl AppState {
    fn new() -> Self {
        Self {
            processes: Arc::new(Mutex::new(HashMap::new())),
            #[cfg(test)]
            test_behavior: Arc::new(Mutex::new(TestBehavior::default())),
        }
    }
}

#[cfg(test)]
#[derive(Clone, Debug, Default)]
struct TestBehavior {
    force_dora_installed: Option<bool>,
    force_runtime_ready_error: Option<String>,
    force_run_outcome: Option<ForcedRunOutcome>,
    force_stop_error: Option<String>,
}

#[cfg(test)]
#[derive(Clone, Debug)]
enum ForcedRunOutcome {
    StartWaitFailed(String),
    StartTimeout,
    StartFailed(String),
    StartSpawnFailed(String),
}

#[cfg(test)]
fn get_forced_dora_installed(state: &AppState) -> Option<bool> {
    state
        .test_behavior
        .lock()
        .expect("lock test behavior")
        .force_dora_installed
}

#[cfg(test)]
fn get_forced_runtime_ready_error(state: &AppState) -> Option<String> {
    state
        .test_behavior
        .lock()
        .expect("lock test behavior")
        .force_runtime_ready_error
        .clone()
}

#[cfg(test)]
fn get_forced_run_outcome(state: &AppState) -> Option<ForcedRunOutcome> {
    state
        .test_behavior
        .lock()
        .expect("lock test behavior")
        .force_run_outcome
        .clone()
}

#[cfg(test)]
fn get_forced_stop_error(state: &AppState) -> Option<String> {
    state
        .test_behavior
        .lock()
        .expect("lock test behavior")
        .force_stop_error
        .clone()
}

/// 日志条目
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct LogEntry {
    pub timestamp: String,
    pub level: String,
    pub source: String,
    pub message: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub node_id: Option<String>,
    pub process_id: Option<String>,
}

impl LogEntry {
    fn new(level: &str, source: &str, message: String, process_id: Option<String>) -> Self {
        let now = chrono::Local::now();
        let node_id = extract_node_id(&message);
        Self {
            timestamp: now.format("%Y-%m-%dT%H:%M:%S%.3f").to_string(),
            level: level.to_string(),
            source: source.to_string(),
            message,
            node_id,
            process_id,
        }
    }

    fn info(message: String, process_id: Option<String>) -> Self {
        Self::new("info", "system", message, process_id)
    }

    fn stdout(message: String, process_id: Option<String>) -> Self {
        Self::new("info", "stdout", message, process_id)
    }

    fn stderr(message: String, process_id: Option<String>) -> Self {
        Self::new("error", "stderr", message, process_id)
    }
}

fn is_probable_node_id(candidate: &str) -> bool {
    if candidate.len() < 2 || candidate.len() > 128 {
        return false;
    }

    if !candidate
        .chars()
        .all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-' || c == '.')
    {
        return false;
    }

    if !candidate.chars().any(|c| c.is_ascii_alphabetic()) {
        return false;
    }

    let lowered = candidate.to_ascii_lowercase();
    let blacklist = [
        "info",
        "warn",
        "warning",
        "error",
        "debug",
        "trace",
        "stdout",
        "stderr",
        "system",
        "dora",
        "node",
        "process",
        "pid",
        "connected",
        "disconnected",
        "running",
        "stopped",
    ];
    !blacklist.contains(&lowered.as_str())
}

fn clean_node_token(raw: &str) -> &str {
    raw.trim_matches(|c: char| {
        c == '['
            || c == ']'
            || c == '('
            || c == ')'
            || c == '{'
            || c == '}'
            || c == '"'
            || c == '\''
            || c == ','
            || c == ';'
    })
}

fn extract_node_id(message: &str) -> Option<String> {
    let prefixes = [
        "node_id=", "node_id:", "node-id=", "node-id:", "node=", "node:",
    ];

    let tokens: Vec<&str> = message
        .split_whitespace()
        .map(clean_node_token)
        .filter(|t| !t.is_empty())
        .collect();

    for (idx, token) in tokens.iter().enumerate() {
        let token_lower = token.to_ascii_lowercase();

        for prefix in prefixes {
            if token_lower.starts_with(prefix) {
                let value = clean_node_token(&token[prefix.len()..]);
                if !value.is_empty() && is_probable_node_id(value) {
                    return Some(value.to_string());
                }

                if let Some(next) = tokens.get(idx + 1) {
                    let next_value = clean_node_token(next);
                    if is_probable_node_id(next_value) {
                        return Some(next_value.to_string());
                    }
                }
            }
        }

        if (token_lower == "node" || token_lower == "node_id" || token_lower == "node-id")
            && tokens.get(idx + 1).is_some()
        {
            let next = clean_node_token(tokens[idx + 1]);
            if is_probable_node_id(next) {
                return Some(next.to_string());
            }
        }
    }

    for segment in message.split('[').skip(1) {
        if let Some(end) = segment.find(']') {
            let candidate = clean_node_token(&segment[..end]);
            if is_probable_node_id(candidate) {
                return Some(candidate.to_string());
            }
        }
    }

    if let Some(first) = tokens.first() {
        let candidate = clean_node_token(first.trim_end_matches(':'));
        if is_probable_node_id(candidate) {
            return Some(candidate.to_string());
        }
    }

    None
}

/// DORA 进程信息
#[derive(Clone, Debug)]
struct DoraProcess {
    _id: String,
    yaml_path: String,
    started_at: std::time::Instant,
    dataflow_uuid: Option<String>,
    log_tx: broadcast::Sender<LogEntry>,
    log_backlog: Arc<Mutex<VecDeque<LogEntry>>>,
}

fn publish_log(
    log_tx: &broadcast::Sender<LogEntry>,
    log_backlog: &Arc<Mutex<VecDeque<LogEntry>>>,
    entry: LogEntry,
) {
    {
        let mut backlog = log_backlog.lock().unwrap();
        backlog.push_back(entry.clone());
        if backlog.len() > LOG_BACKLOG_LIMIT {
            let excess = backlog.len() - LOG_BACKLOG_LIMIT;
            for _ in 0..excess {
                backlog.pop_front();
            }
        }
    }

    let _ = log_tx.send(entry);
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
    pub error_code: Option<String>,
}

/// 停止数据流请求
#[derive(Deserialize, Debug)]
pub struct StopDataflowRequest {
    #[serde(default)]
    pub process_id: Option<String>,
}

/// 停止数据流响应
#[derive(Serialize)]
pub struct StopDataflowResponse {
    pub success: bool,
    pub message: String,
    pub error_code: Option<String>,
}

/// 健康检查响应
#[derive(Serialize)]
pub struct HealthResponse {
    pub status: String,
    pub version: String,
    pub dora_installed: bool,
    pub dora_coordinator_running: bool,
    pub dora_daemon_running: bool,
}

/// 选择目录响应
#[derive(Serialize)]
pub struct SelectDirectoryResponse {
    pub success: bool,
    pub cancelled: bool,
    pub path: Option<String>,
    pub message: String,
    pub error_code: Option<String>,
}

/// 打开数据流文件响应
#[derive(Serialize)]
pub struct OpenDataflowFileResponse {
    pub success: bool,
    pub cancelled: bool,
    pub file_path: Option<String>,
    pub file_name: Option<String>,
    pub working_dir: Option<String>,
    pub content: Option<String>,
    pub message: String,
    pub error_code: Option<String>,
}

/// 按路径读取数据流文件请求
#[derive(Deserialize, Debug)]
pub struct ReadDataflowFileRequest {
    pub file_path: String,
}

#[derive(Serialize, Deserialize, Debug, Clone, PartialEq, Eq)]
pub struct NodeTemplateConfigEntry {
    pub node_type: String,
    pub name: String,
    pub description: String,
    pub icon: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub path: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub inputs: Option<Vec<String>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub outputs: Option<Vec<String>>,
}

#[derive(Serialize, Deserialize, Debug, Default)]
struct NodeTemplatesConfigFile {
    #[serde(default)]
    templates: Vec<NodeTemplateConfigEntry>,
}

#[derive(Deserialize, Debug)]
pub struct SaveNodeTemplatesConfigRequest {
    #[serde(default)]
    pub templates: Vec<NodeTemplateConfigEntry>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct NodeTemplatesConfigResponse {
    pub success: bool,
    #[serde(default)]
    pub templates: Vec<NodeTemplateConfigEntry>,
    pub config_path: Option<String>,
    pub message: String,
    pub error_code: Option<String>,
}

/// 数据流状态响应
#[derive(Serialize)]
pub struct DataflowStatusResponse {
    pub process_id: String,
    pub status: String,
    pub uptime_seconds: u64,
    pub total_nodes: usize,
    pub running_nodes: usize,
    pub error_nodes: usize,
    pub node_details: Vec<NodeDetail>,
}

/// 节点详细信息
#[derive(Serialize, Clone, Debug)]
pub struct NodeDetail {
    pub id: String,
    pub node_type: String,
    pub is_running: bool,
}

/// WebSocket 日志处理器
async fn logs_handler(
    ws: WebSocketUpgrade,
    Path(process_id): Path<String>,
    State(state): State<Arc<AppState>>,
) -> impl axum::response::IntoResponse {
    ws.on_upgrade(move |socket| handle_websocket(socket, process_id, state))
}

async fn status_stream_handler(
    ws: WebSocketUpgrade,
    Path(process_id): Path<String>,
    State(state): State<Arc<AppState>>,
) -> impl axum::response::IntoResponse {
    ws.on_upgrade(move |socket| handle_status_stream_websocket(socket, process_id, state))
}

async fn handle_status_stream_websocket(
    socket: WebSocket,
    process_id: String,
    state: Arc<AppState>,
) {
    info!(
        "Status stream connection request for process: {}",
        process_id
    );
    let (mut sender, mut receiver) = socket.split();

    let process_id_for_send = process_id.clone();
    let state_for_send = Arc::clone(&state);
    let send_task = tokio::spawn(async move {
        let mut ticker = tokio::time::interval(Duration::from_millis(800));
        ticker.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);

        loop {
            ticker.tick().await;
            let status = build_dataflow_status_response(&state_for_send, &process_id_for_send);
            let payload = serde_json::to_string(&status).unwrap_or_else(|_| {
                "{\"process_id\":\"\",\"status\":\"error\",\"uptime_seconds\":0,\"total_nodes\":0,\"running_nodes\":0,\"error_nodes\":0,\"node_details\":[]}".to_string()
            });

            if sender.send(Message::Text(payload)).await.is_err() {
                break;
            }
            if matches!(status.status.as_str(), "stopped" | "not_found") {
                break;
            }
        }
    });

    let recv_task = tokio::spawn(async move {
        while let Some(msg) = receiver.next().await {
            match msg {
                Ok(Message::Close(_)) | Ok(Message::Ping(_)) => break,
                Err(_) => break,
                _ => {}
            }
        }
    });

    tokio::select! {
        _ = send_task => {},
        _ = recv_task => {},
    }
    info!(
        "Status stream connection closed for process: {}",
        process_id
    );
}

async fn handle_websocket(socket: WebSocket, process_id: String, state: Arc<AppState>) {
    info!("WebSocket connection request for process: {}", process_id);

    let process_id_for_sender = process_id.clone();

    // 检查进程是否存在，然后立即释放锁
    let (log_rx, backlog_snapshot) = {
        let processes = state.processes.lock().unwrap();
        if let Some(dora_process) = processes.get(&process_id) {
            let log_rx = dora_process.log_tx.subscribe();
            let backlog_snapshot = dora_process
                .log_backlog
                .lock()
                .unwrap()
                .iter()
                .cloned()
                .collect::<Vec<_>>();
            (log_rx, backlog_snapshot)
        } else {
            info!(
                "WebSocket connection rejected: process {} not found",
                process_id
            );
            return;
        }
    };

    let (mut sender, mut receiver) = socket.split();

    let send_task = tokio::spawn(async move {
        let connect_msg = LogEntry::info(
            format!(
                "Log stream connected for process: {}",
                process_id_for_sender
            ),
            Some(process_id_for_sender),
        );
        let _ = sender
            .send(Message::Text(
                serde_json::to_string(&connect_msg).unwrap_or_default(),
            ))
            .await;

        for log_entry in backlog_snapshot {
            let json = serde_json::to_string(&log_entry).unwrap_or_default();
            if sender.send(Message::Text(json)).await.is_err() {
                return;
            }
        }

        let mut log_rx = log_rx;
        loop {
            match log_rx.recv().await {
                Ok(log_entry) => {
                    let json = serde_json::to_string(&log_entry).unwrap_or_default();
                    if sender.send(Message::Text(json)).await.is_err() {
                        break;
                    }
                }
                Err(tokio::sync::broadcast::error::RecvError::Lagged(_)) => continue,
                Err(tokio::sync::broadcast::error::RecvError::Closed) => break,
            }
        }
    });

    let recv_task = tokio::spawn(async move {
        while let Some(msg) = receiver.next().await {
            match msg {
                Ok(Message::Close(_)) | Ok(Message::Ping(_)) => break,
                Err(_) => break,
                _ => {}
            }
        }
    });

    tokio::select! {
        _ = send_task => {},
        _ = recv_task => {},
    }

    info!("WebSocket connection closed for process: {}", process_id);
}

/// 节点信息 (用于内部处理)
#[derive(Debug, Clone)]
struct NodeInfo {
    id: String,
    path: Option<String>,
    node_type: String,
}

/// 从 YAML 文件解析节点信息
fn parse_yaml_nodes(yaml_path: &str) -> Result<Vec<NodeInfo>, Box<dyn std::error::Error>> {
    let yaml_content = std::fs::read_to_string(yaml_path)?;
    let yaml: Value = serde_yaml::from_str(&yaml_content)?;

    let mut nodes = Vec::new();

    if let Some(node_array) = yaml.get("nodes").and_then(|v| v.as_sequence()) {
        for node_value in node_array {
            let id = node_value
                .get("id")
                .and_then(|v| v.as_str())
                .unwrap_or("unknown")
                .to_string();

            let path = node_value
                .get("path")
                .and_then(|v| v.as_str())
                .map(|s| s.to_string());

            let node_type = if let Some(ref p) = path {
                infer_node_type_from_path(p)
            } else {
                "custom".to_string()
            };

            nodes.push(NodeInfo {
                id,
                path,
                node_type,
            });
        }
    }

    Ok(nodes)
}

/// 从路径推断节点类型
fn infer_node_type_from_path(path: &str) -> String {
    let path_lower = path.to_lowercase();

    if path_lower.contains("opencv") || path_lower.contains("camera") {
        "opencv-video-capture".to_string()
    } else if path_lower.contains("yolo") {
        "dora-yolo".to_string()
    } else if path_lower.contains("rerun") || path_lower.contains("plot") {
        "dora-rerun".to_string()
    } else {
        path.split('/')
            .last()
            .and_then(|s| s.split('\\').last())
            .unwrap_or("custom")
            .to_string()
    }
}

/// 检查单个节点进程是否正在运行
fn check_node_process(node_info: &NodeInfo) -> bool {
    use std::process::Command;

    let search_terms = generate_search_terms(&node_info.node_type);

    #[cfg(target_os = "windows")]
    {
        for term in search_terms {
            let output = Command::new("powershell")
                .args(&[
                    "-Command",
                    &format!("Get-Process | Where-Object {{$_.ProcessName -like '*{}*'}} | Select-Object -First 1", term)
                ])
                .output();

            if let Ok(out) = output {
                if out.status.success() && !out.stdout.is_empty() {
                    return true;
                }
            }
        }
        false
    }

    #[cfg(not(target_os = "windows"))]
    {
        for term in search_terms {
            let output = Command::new("pgrep").args(&["-f", term]).output();

            if let Ok(out) = output {
                if out.status.success() && !out.stdout.is_empty() {
                    return true;
                }
            }
        }
        false
    }
}

/// 生成进程搜索关键词
fn generate_search_terms(node_type: &str) -> Vec<String> {
    match node_type {
        "opencv-video-capture" => vec!["opencv".to_string(), "camera".to_string()],
        "dora-yolo" => vec!["yolo".to_string(), "dora-yolo".to_string()],
        "dora-rerun" => vec!["rerun".to_string(), "dora-rerun".to_string()],
        _ => vec![node_type.to_string()],
    }
}

fn normalize_process_name_candidate(candidate: &str) -> Option<String> {
    let normalized = candidate.trim().trim_end_matches(".exe").trim().to_string();

    if normalized.len() < 2 || normalized.len() > 64 {
        return None;
    }

    if !normalized
        .chars()
        .all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-')
    {
        return None;
    }

    let lowered = normalized.to_ascii_lowercase();
    let blocked = [
        "dora",
        "python",
        "python3",
        "node",
        "cmd",
        "powershell",
        "bash",
        "sh",
        "zsh",
        "cargo",
        "rustc",
        "explorer",
        "system",
    ];

    if blocked.contains(&lowered.as_str()) {
        return None;
    }

    Some(normalized)
}

fn process_name_from_node_path(path: &str) -> Option<String> {
    let file_name = std::path::Path::new(path).file_name()?.to_str()?;
    let stem = std::path::Path::new(file_name).file_stem()?.to_str()?;
    normalize_process_name_candidate(stem)
}

fn collect_cleanup_process_names(yaml_path: &str) -> Result<Vec<String>, String> {
    let nodes = parse_yaml_nodes(yaml_path)
        .map_err(|e| format!("Failed to parse YAML for cleanup: {}", e))?;

    let mut names = HashSet::new();

    for node in nodes {
        if let Some(path) = node.path.as_deref() {
            if let Some(name) = process_name_from_node_path(path) {
                names.insert(name);
            }
        }

        if let Some(name) = normalize_process_name_candidate(&node.id) {
            names.insert(name);
        }
    }

    Ok(names.into_iter().collect())
}

#[cfg(target_os = "windows")]
fn kill_process_by_name(process_name: &str) -> Result<bool, String> {
    let image_name = format!("{}.exe", process_name.trim_end_matches(".exe"));

    let tasklist_output = std::process::Command::new("tasklist")
        .args(["/FI", &format!("IMAGENAME eq {}", image_name)])
        .output()
        .map_err(|e| format!("Failed to check process {}: {}", image_name, e))?;

    let tasklist_stdout = String::from_utf8_lossy(&tasklist_output.stdout).to_ascii_lowercase();
    if !tasklist_stdout.contains(&image_name.to_ascii_lowercase()) {
        return Ok(false);
    }

    let output = std::process::Command::new("taskkill")
        .args(["/F", "/T", "/IM", &image_name])
        .output()
        .map_err(|e| format!("Failed to run taskkill for {}: {}", image_name, e))?;

    if output.status.success() {
        Ok(true)
    } else {
        let stderr = String::from_utf8_lossy(&output.stderr);
        Err(format!("taskkill failed for {}: {}", image_name, stderr))
    }
}

#[cfg(not(target_os = "windows"))]
fn kill_process_by_name(process_name: &str) -> Result<bool, String> {
    let check = std::process::Command::new("pgrep")
        .args(["-x", process_name])
        .output()
        .map_err(|e| format!("Failed to check process {}: {}", process_name, e))?;

    if !check.status.success() || check.stdout.is_empty() {
        return Ok(false);
    }

    let output = std::process::Command::new("pkill")
        .args(["-x", process_name])
        .output()
        .map_err(|e| format!("Failed to run pkill for {}: {}", process_name, e))?;

    if output.status.success() {
        Ok(true)
    } else {
        let stderr = String::from_utf8_lossy(&output.stderr);
        Err(format!("pkill failed for {}: {}", process_name, stderr))
    }
}

fn cleanup_stale_node_processes(yaml_path: &str) -> Vec<String> {
    let process_names = match collect_cleanup_process_names(yaml_path) {
        Ok(names) => names,
        Err(e) => {
            warn!("{}", e);
            return Vec::new();
        }
    };

    let mut killed = Vec::new();
    for process_name in process_names {
        match kill_process_by_name(&process_name) {
            Ok(true) => killed.push(process_name),
            Ok(false) => {}
            Err(e) => warn!("{}", e),
        }
    }

    killed
}

/// 检查所有节点的状态
fn check_all_nodes_status(yaml_path: &str) -> (usize, usize, usize, Vec<NodeDetail>) {
    let nodes = match parse_yaml_nodes(yaml_path) {
        Ok(n) => n,
        Err(e) => {
            error!("Failed to parse YAML: {}", e);
            return (0, 0, 0, vec![]);
        }
    };

    let total_nodes = nodes.len();
    let mut running_nodes = 0;
    let mut error_nodes = 0;
    let mut node_details = Vec::new();

    for node in nodes {
        let is_running = check_node_process(&node);

        if is_running {
            running_nodes += 1;
        } else {
            error_nodes += 1;
        }

        node_details.push(NodeDetail {
            id: node.id.clone(),
            node_type: node.node_type.clone(),
            is_running,
        });

        info!(
            "Node {} ({}): {}",
            node.id,
            node.node_type,
            if is_running { "running" } else { "stopped" }
        );
    }

    info!("Total: {}/{}/{}", running_nodes, total_nodes, error_nodes);

    (total_nodes, running_nodes, error_nodes, node_details)
}

/// 健康检查 API
async fn health_check() -> Json<HealthResponse> {
    let dora_installed = check_dora_installed();

    let dora_coordinator_running = if dora_installed {
        check_dora_coordinator_running()
    } else {
        false
    };

    let dora_daemon_running = if dora_installed {
        check_dora_daemon_running()
    } else {
        false
    };

    let response = HealthResponse {
        status: "ok".to_string(),
        version: env!("CARGO_PKG_VERSION").to_string(),
        dora_installed,
        dora_coordinator_running,
        dora_daemon_running,
    };

    info!(
        "Health check: dora_installed={}, coordinator={}, daemon={}",
        dora_installed, dora_coordinator_running, dora_daemon_running
    );
    Json(response)
}

/// 选择目录 API
async fn select_directory() -> Json<SelectDirectoryResponse> {
    let picked = tokio::task::spawn_blocking(|| {
        rfd::FileDialog::new()
            .set_title("Select DoraMate Working Directory")
            .pick_folder()
    })
    .await;

    match picked {
        Ok(Some(path)) => {
            let path_str = path.to_string_lossy().to_string();
            info!("Directory selected: {}", path_str);
            Json(SelectDirectoryResponse {
                success: true,
                cancelled: false,
                path: Some(path_str),
                message: "Directory selected".to_string(),
                error_code: None,
            })
        }
        Ok(None) => Json(SelectDirectoryResponse {
            success: false,
            cancelled: true,
            path: None,
            message: "Directory selection cancelled".to_string(),
            error_code: Some(ERR_DIRECTORY_SELECTION_CANCELLED.to_string()),
        }),
        Err(e) => {
            error!("Failed to open directory picker: {}", e);
            Json(SelectDirectoryResponse {
                success: false,
                cancelled: false,
                path: None,
                message: format!("Failed to open directory picker: {}", e),
                error_code: Some(ERR_DIRECTORY_PICKER_FAILED.to_string()),
            })
        }
    }
}

/// 打开数据流文件 API
async fn open_dataflow_file() -> Json<OpenDataflowFileResponse> {
    let picked = tokio::task::spawn_blocking(|| {
        rfd::FileDialog::new()
            .set_title("Open DoraMate Dataflow")
            .add_filter("YAML", &["yml", "yaml"])
            .pick_file()
    })
    .await;

    let selected_path = match picked {
        Ok(Some(path)) => path,
        Ok(None) => {
            return Json(OpenDataflowFileResponse {
                success: false,
                cancelled: true,
                file_path: None,
                file_name: None,
                working_dir: None,
                content: None,
                message: "File selection cancelled".to_string(),
                error_code: Some(ERR_FILE_SELECTION_CANCELLED.to_string()),
            });
        }
        Err(e) => {
            error!("Failed to open file picker: {}", e);
            return Json(OpenDataflowFileResponse {
                success: false,
                cancelled: false,
                file_path: None,
                file_name: None,
                working_dir: None,
                content: None,
                message: format!("Failed to open file picker: {}", e),
                error_code: Some(ERR_FILE_PICKER_FAILED.to_string()),
            });
        }
    };

    let file_path = selected_path.to_string_lossy().to_string();
    let file_name = selected_path
        .file_name()
        .map(|name| name.to_string_lossy().to_string());
    let working_dir = selected_path
        .parent()
        .map(|parent| parent.to_string_lossy().to_string());

    let content_path = selected_path.clone();
    let file_content =
        tokio::task::spawn_blocking(move || std::fs::read_to_string(content_path)).await;

    match file_content {
        Ok(Ok(content)) => {
            info!("Dataflow file opened: {}", file_path);
            Json(OpenDataflowFileResponse {
                success: true,
                cancelled: false,
                file_path: Some(file_path),
                file_name,
                working_dir,
                content: Some(content),
                message: "File opened".to_string(),
                error_code: None,
            })
        }
        Ok(Err(e)) => {
            error!("Failed to read selected file: {}", e);
            Json(OpenDataflowFileResponse {
                success: false,
                cancelled: false,
                file_path: Some(file_path),
                file_name,
                working_dir,
                content: None,
                message: format!("Failed to read selected file: {}", e),
                error_code: Some(ERR_FILE_READ_FAILED.to_string()),
            })
        }
        Err(e) => {
            error!("Failed to read file in blocking task: {}", e);
            Json(OpenDataflowFileResponse {
                success: false,
                cancelled: false,
                file_path: Some(file_path),
                file_name,
                working_dir,
                content: None,
                message: format!("Failed to read selected file: {}", e),
                error_code: Some(ERR_FILE_READ_FAILED.to_string()),
            })
        }
    }
}

/// 按路径读取数据流文件 API
async fn read_dataflow_file(
    Json(req): Json<ReadDataflowFileRequest>,
) -> Json<OpenDataflowFileResponse> {
    let file_path = req.file_path.trim().to_string();

    if file_path.is_empty() {
        return Json(OpenDataflowFileResponse {
            success: false,
            cancelled: false,
            file_path: None,
            file_name: None,
            working_dir: None,
            content: None,
            message: "File path is empty".to_string(),
            error_code: Some(ERR_FILE_PATH_EMPTY.to_string()),
        });
    }

    let path_buf = std::path::PathBuf::from(&file_path);
    let file_name = path_buf
        .file_name()
        .map(|name| name.to_string_lossy().to_string());
    let working_dir = path_buf
        .parent()
        .map(|parent| parent.to_string_lossy().to_string());

    let file_content = tokio::task::spawn_blocking(move || std::fs::read_to_string(path_buf)).await;

    match file_content {
        Ok(Ok(content)) => {
            info!("Dataflow file read by path: {}", file_path);
            Json(OpenDataflowFileResponse {
                success: true,
                cancelled: false,
                file_path: Some(file_path),
                file_name,
                working_dir,
                content: Some(content),
                message: "File opened".to_string(),
                error_code: None,
            })
        }
        Ok(Err(e)) => {
            error!("Failed to read file by path: {}", e);
            Json(OpenDataflowFileResponse {
                success: false,
                cancelled: false,
                file_path: Some(file_path),
                file_name,
                working_dir,
                content: None,
                message: format!("Failed to read selected file: {}", e),
                error_code: Some(ERR_FILE_READ_FAILED.to_string()),
            })
        }
        Err(e) => {
            error!("Failed to read file by path in blocking task: {}", e);
            Json(OpenDataflowFileResponse {
                success: false,
                cancelled: false,
                file_path: Some(file_path),
                file_name,
                working_dir,
                content: None,
                message: format!("Failed to read selected file: {}", e),
                error_code: Some(ERR_FILE_READ_FAILED.to_string()),
            })
        }
    }
}

/// 运行数据流 API
fn normalize_node_template_ports(ports: Option<Vec<String>>) -> Option<Vec<String>> {
    let Some(ports) = ports else {
        return None;
    };

    let mut seen = HashSet::<String>::new();
    let mut normalized = Vec::<String>::new();
    for raw in ports {
        let port = raw.trim();
        if port.is_empty() {
            continue;
        }
        let lowered = port.to_ascii_lowercase();
        if seen.insert(lowered) {
            normalized.push(port.to_string());
        }
    }

    if normalized.is_empty() {
        None
    } else {
        Some(normalized)
    }
}

fn normalize_node_template_entries(
    entries: Vec<NodeTemplateConfigEntry>,
) -> Vec<NodeTemplateConfigEntry> {
    let mut by_node_type = HashMap::<String, NodeTemplateConfigEntry>::new();

    for mut entry in entries {
        let node_type = entry.node_type.trim().to_string();
        if node_type.is_empty() {
            continue;
        }

        entry.node_type = node_type.clone();
        entry.name = if entry.name.trim().is_empty() {
            node_type.clone()
        } else {
            entry.name.trim().to_string()
        };
        entry.description = entry.description.trim().to_string();
        entry.icon = if entry.icon.trim().is_empty() {
            "🔧".to_string()
        } else {
            entry.icon.trim().to_string()
        };
        entry.path = entry.path.and_then(|path| {
            let trimmed = path.trim();
            if trimmed.is_empty() {
                None
            } else {
                Some(trimmed.to_string())
            }
        });
        entry.inputs = normalize_node_template_ports(entry.inputs);
        entry.outputs = normalize_node_template_ports(entry.outputs);

        by_node_type.insert(node_type, entry);
    }

    let mut normalized: Vec<NodeTemplateConfigEntry> = by_node_type.into_values().collect();
    normalized.sort_by(|a, b| a.node_type.cmp(&b.node_type));
    normalized
}

fn resolve_node_templates_config_path() -> Result<std::path::PathBuf, String> {
    #[cfg(target_os = "windows")]
    {
        if let Ok(appdata) = std::env::var("APPDATA") {
            let trimmed = appdata.trim();
            if !trimmed.is_empty() {
                return Ok(std::path::PathBuf::from(trimmed)
                    .join("DoraMate")
                    .join("node_templates.yml"));
            }
        }

        if let Ok(profile) = std::env::var("USERPROFILE") {
            let trimmed = profile.trim();
            if !trimmed.is_empty() {
                return Ok(std::path::PathBuf::from(trimmed)
                    .join("AppData")
                    .join("Roaming")
                    .join("DoraMate")
                    .join("node_templates.yml"));
            }
        }
    }

    if let Ok(xdg_config_home) = std::env::var("XDG_CONFIG_HOME") {
        let trimmed = xdg_config_home.trim();
        if !trimmed.is_empty() {
            return Ok(std::path::PathBuf::from(trimmed)
                .join("doramate")
                .join("node_templates.yml"));
        }
    }

    if let Ok(home) = std::env::var("HOME") {
        let trimmed = home.trim();
        if !trimmed.is_empty() {
            return Ok(std::path::PathBuf::from(trimmed)
                .join(".config")
                .join("doramate")
                .join("node_templates.yml"));
        }
    }

    Err("Failed to resolve node templates config path".to_string())
}

/// Load node templates config YAML.
async fn read_node_templates_config() -> Json<NodeTemplatesConfigResponse> {
    let config_path = match resolve_node_templates_config_path() {
        Ok(path) => path,
        Err(err) => {
            return Json(NodeTemplatesConfigResponse {
                success: false,
                templates: Vec::new(),
                config_path: None,
                message: err,
                error_code: Some(ERR_NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE.to_string()),
            });
        }
    };
    let config_path_str = config_path.to_string_lossy().to_string();

    let read_result = tokio::task::spawn_blocking({
        let config_path = config_path.clone();
        move || -> Result<Vec<NodeTemplateConfigEntry>, String> {
            if !config_path.exists() {
                return Ok(Vec::new());
            }

            let raw = std::fs::read_to_string(&config_path)
                .map_err(|e| format!("Failed to read node templates config: {}", e))?;
            if raw.trim().is_empty() {
                return Ok(Vec::new());
            }

            if let Ok(wrapper) = serde_yaml::from_str::<NodeTemplatesConfigFile>(&raw) {
                return Ok(normalize_node_template_entries(wrapper.templates));
            }

            if let Ok(entries) = serde_yaml::from_str::<Vec<NodeTemplateConfigEntry>>(&raw) {
                return Ok(normalize_node_template_entries(entries));
            }

            Err("Failed to parse node templates config YAML".to_string())
        }
    })
    .await;

    match read_result {
        Ok(Ok(templates)) => Json(NodeTemplatesConfigResponse {
            success: true,
            templates,
            config_path: Some(config_path_str),
            message: "Node templates config loaded".to_string(),
            error_code: None,
        }),
        Ok(Err(err)) => {
            error!("Failed to load node templates config: {}", err);
            Json(NodeTemplatesConfigResponse {
                success: false,
                templates: Vec::new(),
                config_path: Some(config_path_str),
                message: err,
                error_code: Some(ERR_NODE_TEMPLATES_CONFIG_READ_FAILED.to_string()),
            })
        }
        Err(err) => {
            error!(
                "Failed to load node templates config in blocking task: {}",
                err
            );
            Json(NodeTemplatesConfigResponse {
                success: false,
                templates: Vec::new(),
                config_path: Some(config_path_str),
                message: format!("Failed to load node templates config: {}", err),
                error_code: Some(ERR_NODE_TEMPLATES_CONFIG_READ_FAILED.to_string()),
            })
        }
    }
}

/// Save node templates config YAML.
async fn write_node_templates_config(
    Json(req): Json<SaveNodeTemplatesConfigRequest>,
) -> Json<NodeTemplatesConfigResponse> {
    let config_path = match resolve_node_templates_config_path() {
        Ok(path) => path,
        Err(err) => {
            return Json(NodeTemplatesConfigResponse {
                success: false,
                templates: Vec::new(),
                config_path: None,
                message: err,
                error_code: Some(ERR_NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE.to_string()),
            });
        }
    };
    let config_path_str = config_path.to_string_lossy().to_string();

    let templates = normalize_node_template_entries(req.templates);

    let write_result = tokio::task::spawn_blocking({
        let config_path = config_path.clone();
        let templates = templates.clone();
        move || -> Result<(), String> {
            if let Some(parent) = config_path.parent() {
                std::fs::create_dir_all(parent)
                    .map_err(|e| format!("Failed to create config directory: {}", e))?;
            }

            let payload = NodeTemplatesConfigFile { templates };
            let yaml = serde_yaml::to_string(&payload)
                .map_err(|e| format!("Failed to serialize node templates config: {}", e))?;
            std::fs::write(&config_path, yaml)
                .map_err(|e| format!("Failed to write node templates config: {}", e))
        }
    })
    .await;

    match write_result {
        Ok(Ok(())) => Json(NodeTemplatesConfigResponse {
            success: true,
            templates,
            config_path: Some(config_path_str),
            message: "Node templates config saved".to_string(),
            error_code: None,
        }),
        Ok(Err(err)) => {
            error!("Failed to save node templates config: {}", err);
            Json(NodeTemplatesConfigResponse {
                success: false,
                templates: Vec::new(),
                config_path: Some(config_path_str),
                message: err,
                error_code: Some(ERR_NODE_TEMPLATES_CONFIG_WRITE_FAILED.to_string()),
            })
        }
        Err(err) => {
            error!(
                "Failed to save node templates config in blocking task: {}",
                err
            );
            Json(NodeTemplatesConfigResponse {
                success: false,
                templates: Vec::new(),
                config_path: Some(config_path_str),
                message: format!("Failed to save node templates config: {}", err),
                error_code: Some(ERR_NODE_TEMPLATES_CONFIG_WRITE_FAILED.to_string()),
            })
        }
    }
}

struct DoraStartSuccess {
    stdout: String,
    stderr: String,
    dataflow_uuid: Option<String>,
}

enum DoraStartError {
    Spawn(String),
    Wait(String),
    Timeout,
    FailedExit { status: String, details: String },
}

fn parse_dataflow_uuid_from_output(combined_output: &str) -> Option<String> {
    for raw_line in combined_output.lines() {
        let line = raw_line.trim();
        if line.is_empty() {
            continue;
        }

        if line.contains("dataflow start triggered:") {
            if let Some(uuid_part) = line.split(':').last() {
                let uuid = uuid_part.trim().to_string();
                if !uuid.is_empty() {
                    return Some(uuid);
                }
            }
        }

        if line.len() == 36 && line.matches('-').count() == 4 {
            let parts: Vec<&str> = line.split('-').collect();
            if parts.len() == 5
                && parts[0].len() == 8
                && parts[1].len() == 4
                && parts[2].len() == 4
                && parts[3].len() == 4
                && parts[4].len() == 12
            {
                return Some(line.to_string());
            }
        }
    }

    None
}

fn dora_runtime_port_snapshot() -> String {
    format!(
        "coord_port_open={} control_port_open={} daemon_port_open={}",
        is_local_port_open(DORA_COORDINATOR_PORT),
        is_local_port_open(DORA_CONTROL_PORT),
        is_local_port_open(DORA_DAEMON_LOCAL_PORT)
    )
}

fn should_retry_dora_start_failed_exit(details: &str) -> bool {
    let lowered = details.to_ascii_lowercase();
    if lowered.is_empty() {
        return true;
    }

    let likely_permanent = lowered.contains("yaml")
        || lowered.contains("parse")
        || lowered.contains("invalid")
        || lowered.contains("unknown node")
        || lowered.contains("no such file")
        || lowered.contains("not found");
    if likely_permanent {
        return false;
    }

    lowered.contains("no process output")
        || lowered.contains("timed out")
        || lowered.contains("timeout")
        || lowered.contains("connection refused")
        || lowered.contains("coordinator")
        || lowered.contains("daemon")
        || lowered.contains("broken pipe")
        || lowered.contains("temporarily unavailable")
}

async fn run_dora_start_once(
    working_dir: &str,
    yaml_path_str: &str,
) -> Result<DoraStartSuccess, DoraStartError> {
    let mut cmd = Command::new("dora");
    cmd.current_dir(working_dir);
    cmd.arg("start")
        .arg("--detach")
        .arg("--coordinator-port")
        .arg(&DORA_CONTROL_PORT.to_string())
        .arg(yaml_path_str);
    cmd.stdout(Stdio::piped()).stderr(Stdio::piped());

    let child = cmd
        .spawn()
        .map_err(|e| DoraStartError::Spawn(e.to_string()))?;

    let output = match tokio::time::timeout(
        Duration::from_secs(DORA_START_TIMEOUT_SECS),
        child.wait_with_output(),
    )
    .await
    {
        Ok(Ok(out)) => out,
        Ok(Err(e)) => return Err(DoraStartError::Wait(e.to_string())),
        Err(_) => return Err(DoraStartError::Timeout),
    };

    let stdout = String::from_utf8_lossy(&output.stdout).to_string();
    let stderr = String::from_utf8_lossy(&output.stderr).to_string();

    if !output.status.success() {
        return Err(DoraStartError::FailedExit {
            status: output.status.to_string(),
            details: summarize_process_output(&output.stdout, &output.stderr),
        });
    }

    let combined_output = format!("{}\n{}", stdout, stderr);
    let dataflow_uuid = parse_dataflow_uuid_from_output(&combined_output);
    Ok(DoraStartSuccess {
        stdout,
        stderr,
        dataflow_uuid,
    })
}

async fn run_dataflow(
    State(state): State<Arc<AppState>>,
    Json(req): Json<RunDataflowRequest>,
) -> Json<RunDataflowResponse> {
    let process_id = Uuid::new_v4().to_string();

    let working_dir = req
        .working_dir
        .clone()
        .unwrap_or_else(|| {
            std::env::current_dir()
                .unwrap()
                .to_string_lossy()
                .to_string()
        })
        .trim()
        .to_string();

    let clean_yaml = if req.dataflow_yaml.contains("__doramate__") {
        extract_clean_dora_yaml(&req.dataflow_yaml)
    } else {
        req.dataflow_yaml.clone()
    };

    let yaml_to_run = clean_yaml;
    let yaml_path = std::path::Path::new(&working_dir).join(format!("doramate_{}.yml", process_id));
    let yaml_path_str = yaml_path.to_string_lossy().to_string();

    info!(
        "run.request process_id={} working_dir={} yaml_path={} yaml_len={}",
        process_id,
        working_dir,
        yaml_path_str,
        req.dataflow_yaml.len()
    );

    if let Err(e) = std::fs::write(&yaml_path, &yaml_to_run) {
        error!(
            "run.write_failed process_id={} working_dir={} yaml_path={} err={}",
            process_id, working_dir, yaml_path_str, e
        );
        return Json(RunDataflowResponse {
            success: false,
            message: format!("Failed to write YAML: {}", e),
            process_id: None,
            error_code: Some(ERR_YAML_WRITE_FAILED.to_string()),
        });
    }

    info!(
        "run.yaml_saved process_id={} working_dir={} yaml_path={}",
        process_id, working_dir, yaml_path_str
    );

    #[cfg(test)]
    let dora_installed = get_forced_dora_installed(&state).unwrap_or_else(check_dora_installed);
    #[cfg(not(test))]
    let dora_installed = check_dora_installed();

    if !dora_installed {
        error!(
            "run.dora_not_installed process_id={} working_dir={} yaml_path={}",
            process_id, working_dir, yaml_path_str
        );
        return Json(RunDataflowResponse {
            success: false,
            message: "DORA is not installed. Please install dora-cli first.".to_string(),
            process_id: None,
            error_code: Some(ERR_DORA_NOT_INSTALLED.to_string()),
        });
    }

    info!(
        "run.check_runtime process_id={} working_dir={} yaml_path={}",
        process_id, working_dir, yaml_path_str
    );
    #[cfg(test)]
    let forced_run_outcome = get_forced_run_outcome(&state);
    #[cfg(test)]
    let runtime_ready = if forced_run_outcome.is_some() {
        Ok(())
    } else if let Some(forced_err) = get_forced_runtime_ready_error(&state) {
        Err(forced_err)
    } else {
        ensure_dora_runtime_ready().await
    };
    #[cfg(not(test))]
    let runtime_ready = ensure_dora_runtime_ready().await;

    if let Err(e) = runtime_ready {
        error!(
            "run.runtime_init_failed process_id={} working_dir={} yaml_path={} err={}",
            process_id, working_dir, yaml_path_str, e
        );
        return Json(RunDataflowResponse {
            success: false,
            message: format!("Failed to initialize dora runtime: {}", e),
            process_id: None,
            error_code: Some(ERR_DORA_RUNTIME_INIT_FAILED.to_string()),
        });
    }

    #[cfg(test)]
    if let Some(forced_outcome) = forced_run_outcome {
        let resp = match forced_outcome {
            ForcedRunOutcome::StartWaitFailed(err) => RunDataflowResponse {
                success: false,
                message: format!("Failed to wait for dora start: {}", err),
                process_id: None,
                error_code: Some(ERR_DORA_START_WAIT_FAILED.to_string()),
            },
            ForcedRunOutcome::StartTimeout => RunDataflowResponse {
                success: false,
                message: "dora start timed out after 20s. Check dataflow YAML validity and dora runtime status.".to_string(),
                process_id: None,
                error_code: Some(ERR_DORA_START_TIMEOUT.to_string()),
            },
            ForcedRunOutcome::StartFailed(details) => RunDataflowResponse {
                success: false,
                message: format!(
                    "dora start failed (status mocked): {}. yaml_path={}",
                    details, yaml_path_str
                ),
                process_id: None,
                error_code: Some(ERR_DORA_START_FAILED.to_string()),
            },
            ForcedRunOutcome::StartSpawnFailed(err) => RunDataflowResponse {
                success: false,
                message: format!("Failed to start dora: {}", err),
                process_id: None,
                error_code: Some(ERR_DORA_START_SPAWN_FAILED.to_string()),
            },
        };
        return Json(resp);
    }

    // 创建日志广播通道
    let (log_tx, _log_rx) = broadcast::channel::<LogEntry>(100);
    let log_backlog = Arc::new(Mutex::new(VecDeque::<LogEntry>::new()));

    for attempt in 1..=DORA_START_MAX_ATTEMPTS {
        let runtime_snapshot = dora_runtime_port_snapshot();
        info!(
            "run.start_attempt process_id={} attempt={}/{} working_dir={} yaml_path={} runtime_snapshot={}",
            process_id,
            attempt,
            DORA_START_MAX_ATTEMPTS,
            working_dir,
            yaml_path_str,
            runtime_snapshot
        );

        let start_result = run_dora_start_once(&working_dir, &yaml_path_str).await;
        match start_result {
            Ok(started) => {
                if !started.stdout.trim().is_empty() {
                    publish_log(
                        &log_tx,
                        &log_backlog,
                        LogEntry::stdout(started.stdout.clone(), Some(process_id.clone())),
                    );
                }
                if !started.stderr.trim().is_empty() {
                    publish_log(
                        &log_tx,
                        &log_backlog,
                        LogEntry::stderr(started.stderr.clone(), Some(process_id.clone())),
                    );
                }

                let dora_process = DoraProcess {
                    _id: process_id.clone(),
                    yaml_path: yaml_path_str.clone(),
                    started_at: std::time::Instant::now(),
                    dataflow_uuid: started.dataflow_uuid.clone(),
                    log_tx,
                    log_backlog,
                };

                state
                    .processes
                    .lock()
                    .unwrap()
                    .insert(process_id.clone(), dora_process);

                info!(
                    "run.started process_id={} working_dir={} yaml_path={} uuid={:?} attempt={}/{}",
                    process_id,
                    working_dir,
                    yaml_path_str,
                    started.dataflow_uuid,
                    attempt,
                    DORA_START_MAX_ATTEMPTS
                );

                return Json(RunDataflowResponse {
                    success: true,
                    message: format!(
                        "Dataflow started successfully (UUID: {:?})",
                        started.dataflow_uuid
                    ),
                    process_id: Some(process_id),
                    error_code: None,
                });
            }
            Err(err) => {
                let (error_code, message, retryable) = match err {
                    DoraStartError::Wait(e) => {
                        let msg = format!(
                            "Failed to wait for dora start: {}. yaml_path={}. runtime_snapshot={}",
                            e, yaml_path_str, runtime_snapshot
                        );
                        (ERR_DORA_START_WAIT_FAILED, msg, true)
                    }
                    DoraStartError::Timeout => {
                        let msg = format!(
                            "dora start timed out after {}s. Check dora runtime and YAML validity. yaml_path={}. runtime_snapshot={}",
                            DORA_START_TIMEOUT_SECS, yaml_path_str, runtime_snapshot
                        );
                        (ERR_DORA_START_TIMEOUT, msg, true)
                    }
                    DoraStartError::FailedExit { status, details } => {
                        let msg = format!(
                            "dora start failed (status {}): {}. yaml_path={}. runtime_snapshot={}",
                            status, details, yaml_path_str, runtime_snapshot
                        );
                        (
                            ERR_DORA_START_FAILED,
                            msg,
                            should_retry_dora_start_failed_exit(&details),
                        )
                    }
                    DoraStartError::Spawn(e) => {
                        let msg = format!(
                            "Failed to start dora: {}. yaml_path={}. runtime_snapshot={}",
                            e, yaml_path_str, runtime_snapshot
                        );
                        (ERR_DORA_START_SPAWN_FAILED, msg, false)
                    }
                };

                error!(
                    "run.start_attempt_failed process_id={} attempt={}/{} code={} message={}",
                    process_id, attempt, DORA_START_MAX_ATTEMPTS, error_code, message
                );

                if retryable && attempt < DORA_START_MAX_ATTEMPTS {
                    warn!(
                        "run.start_retry process_id={} next_attempt={} reason={}",
                        process_id,
                        attempt + 1,
                        error_code
                    );
                    publish_log(
                        &log_tx,
                        &log_backlog,
                        LogEntry::info(
                            format!(
                                "dora start attempt {}/{} failed ({}), retrying once...",
                                attempt, DORA_START_MAX_ATTEMPTS, error_code
                            ),
                            Some(process_id.clone()),
                        ),
                    );

                    if let Err(recover_err) = ensure_dora_runtime_ready().await {
                        warn!(
                            "run.start_retry_runtime_recovery_failed process_id={} err={}",
                            process_id, recover_err
                        );
                    }
                    tokio::time::sleep(Duration::from_millis(DORA_START_RETRY_DELAY_MS)).await;
                    continue;
                }

                return Json(RunDataflowResponse {
                    success: false,
                    message,
                    process_id: None,
                    error_code: Some(error_code.to_string()),
                });
            }
        }
    }

    Json(RunDataflowResponse {
        success: false,
        message: format!(
            "dora start failed after retries. yaml_path={}. runtime_snapshot={}",
            yaml_path_str,
            dora_runtime_port_snapshot()
        ),
        process_id: None,
        error_code: Some(ERR_DORA_START_FAILED.to_string()),
    })
}

/// 停止数据流 API
async fn stop_dataflow(
    State(state): State<Arc<AppState>>,
    Json(req): Json<StopDataflowRequest>,
) -> Json<StopDataflowResponse> {
    info!("stop.request process_id={:?}", req.process_id);
    let stop_info: Vec<(String, Option<String>, String)> = {
        let processes = state.processes.lock().unwrap();

        if let Some(process_id) = &req.process_id {
            if let Some(dora_process) = processes.get(process_id) {
                vec![(
                    process_id.clone(),
                    dora_process.dataflow_uuid.clone(),
                    dora_process.yaml_path.clone(),
                )]
            } else {
                vec![]
            }
        } else {
            processes
                .iter()
                .map(|(id, proc)| {
                    (
                        id.clone(),
                        proc.dataflow_uuid.clone(),
                        proc.yaml_path.clone(),
                    )
                })
                .collect()
        }
    };

    let mut stopped_count = 0;
    let mut error_count = 0;
    let mut to_remove = Vec::new();

    for (process_id, dataflow_uuid, yaml_path) in stop_info {
        info!(
            "stop.attempt process_id={} yaml_path={} uuid={:?}",
            process_id, yaml_path, dataflow_uuid
        );
        #[cfg(test)]
        let forced_stop_error = get_forced_stop_error(&state);
        #[cfg(not(test))]
        let forced_stop_error: Option<String> = None;

        let stop_result = if let Some(err) = forced_stop_error {
            Err(err)
        } else if let Some(uuid) = &dataflow_uuid {
            stop_dataflow_by_uuid(uuid).await
        } else {
            stop_all_dataflows().await
        };

        match stop_result {
            Ok(_msg) => {
                stopped_count += 1;
                to_remove.push(process_id);

                let killed_processes = cleanup_stale_node_processes(&yaml_path);
                if !killed_processes.is_empty() {
                    info!("Cleaned up stale node processes: {:?}", killed_processes);
                }
            }
            Err(e) => {
                error!(
                    "stop.failed process_id={} yaml_path={} err={}",
                    process_id, yaml_path, e
                );
                error_count += 1;
            }
        }
    }

    let mut processes = state.processes.lock().unwrap();
    for process_id in to_remove {
        processes.remove(&process_id);
    }

    Json(StopDataflowResponse {
        success: true,
        message: format!(
            "Stopped {} dataflow(s) with {} errors",
            stopped_count, error_count
        ),
        error_code: if error_count > 0 {
            Some(ERR_STOP_PARTIAL_FAILURE.to_string())
        } else {
            None
        },
    })
}

#[cfg(test)]
mod process_cleanup_tests {
    use super::*;

    #[test]
    fn test_normalize_process_name_candidate() {
        assert_eq!(
            normalize_process_name_candidate("viewer.exe"),
            Some("viewer".to_string())
        );
        assert_eq!(normalize_process_name_candidate("node"), None);
        assert_eq!(normalize_process_name_candidate("bad name"), None);
    }

    #[test]
    fn test_process_name_from_node_path() {
        assert_eq!(
            process_name_from_node_path("target/release/viewer"),
            Some("viewer".to_string())
        );
        assert_eq!(
            process_name_from_node_path("C:\\bin\\viewer.exe"),
            Some("viewer".to_string())
        );
    }

    #[test]
    fn test_summarize_process_output_prefers_available_streams() {
        let only_stdout = summarize_process_output(b"ok", b"");
        assert!(only_stdout.contains("stdout: ok"));
        assert!(!only_stdout.contains("stderr:"));

        let only_stderr = summarize_process_output(b"", b"err");
        assert!(only_stderr.contains("stderr: err"));
        assert!(!only_stderr.contains("stdout:"));
    }

    #[test]
    fn test_summarize_process_output_empty() {
        let summary = summarize_process_output(b"", b"");
        assert_eq!(summary, "no process output");
    }

    #[test]
    fn test_parse_dataflow_uuid_from_output_supports_trigger_line_and_raw_uuid() {
        let by_trigger = parse_dataflow_uuid_from_output(
            "info: dataflow start triggered: 11111111-2222-3333-4444-555555555555",
        );
        assert_eq!(
            by_trigger.as_deref(),
            Some("11111111-2222-3333-4444-555555555555")
        );

        let by_raw = parse_dataflow_uuid_from_output(
            "random line\n66666666-7777-8888-9999-000000000000\nother line",
        );
        assert_eq!(
            by_raw.as_deref(),
            Some("66666666-7777-8888-9999-000000000000")
        );
    }

    #[test]
    fn test_should_retry_dora_start_failed_exit_for_transient_and_permanent_errors() {
        assert!(should_retry_dora_start_failed_exit("no process output"));
        assert!(should_retry_dora_start_failed_exit(
            "connection refused from coordinator"
        ));
        assert!(should_retry_dora_start_failed_exit(
            "daemon temporarily unavailable"
        ));

        assert!(!should_retry_dora_start_failed_exit(
            "invalid yaml: mapping values are not allowed"
        ));
        assert!(!should_retry_dora_start_failed_exit(
            "unknown node type custom_x"
        ));
    }
}

/// 使用 dora stop 命令停止指定 UUID 的数据流
#[cfg(test)]
mod api_path_tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn missing_working_dir() -> String {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time before unix epoch")
            .as_nanos();
        std::env::temp_dir()
            .join(format!(
                "doramate_missing_dir_{}_{}",
                std::process::id(),
                nanos
            ))
            .join("nested")
            .to_string_lossy()
            .to_string()
    }

    fn writable_working_dir() -> String {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time before unix epoch")
            .as_nanos();
        let dir = std::env::temp_dir().join(format!(
            "doramate_working_dir_{}_{}",
            std::process::id(),
            nanos
        ));
        std::fs::create_dir_all(&dir).expect("create temp working dir");
        dir.to_string_lossy().to_string()
    }

    fn make_state_with_behavior(behavior: TestBehavior) -> Arc<AppState> {
        let state = AppState::new();
        *state
            .test_behavior
            .lock()
            .expect("lock test behavior for setup") = behavior;
        Arc::new(state)
    }

    fn insert_mock_process(state: &Arc<AppState>, process_id: &str, uuid: Option<&str>) {
        let (log_tx, _log_rx) = broadcast::channel::<LogEntry>(16);
        let process = DoraProcess {
            _id: process_id.to_string(),
            yaml_path: "mock_dataflow.yml".to_string(),
            started_at: std::time::Instant::now(),
            dataflow_uuid: uuid.map(|s| s.to_string()),
            log_tx,
            log_backlog: Arc::new(Mutex::new(VecDeque::new())),
        };
        state
            .processes
            .lock()
            .expect("lock process map for setup")
            .insert(process_id.to_string(), process);
    }

    fn template_entry(node_type: &str) -> NodeTemplateConfigEntry {
        NodeTemplateConfigEntry {
            node_type: node_type.to_string(),
            name: "template".to_string(),
            description: "desc".to_string(),
            icon: "🔧".to_string(),
            path: None,
            inputs: None,
            outputs: None,
        }
    }

    #[test]
    fn test_normalize_node_template_ports_trims_and_deduplicates_case_insensitive() {
        assert_eq!(normalize_node_template_ports(None), None);
        assert_eq!(
            normalize_node_template_ports(Some(vec![
                "  ".to_string(),
                "\n".to_string(),
                "\t".to_string()
            ])),
            None
        );

        let normalized = normalize_node_template_ports(Some(vec![
            " in ".to_string(),
            "IN".to_string(),
            "out".to_string(),
            " out ".to_string(),
            "Out".to_string(),
        ]))
        .expect("normalized ports");

        assert_eq!(normalized, vec!["in".to_string(), "out".to_string()]);
    }

    #[test]
    fn test_normalize_node_template_entries_deduplicates_and_applies_defaults() {
        let mut python_old = template_entry("  python_custom ");
        python_old.name = "   ".to_string();
        python_old.description = "  old desc ".to_string();
        python_old.icon = " ".to_string();
        python_old.path = Some("  ./old.py ".to_string());
        python_old.inputs = Some(vec![" in ".to_string(), "IN".to_string()]);
        python_old.outputs = Some(vec![" out ".to_string()]);

        let mut python_new = template_entry("python_custom");
        python_new.name = " Python Custom ".to_string();
        python_new.description = " latest ".to_string();
        python_new.icon = " 🐍 ".to_string();
        python_new.path = Some("  ".to_string());
        python_new.inputs = Some(vec!["events".to_string(), "EVENTS".to_string()]);
        python_new.outputs = None;

        let mut rust_entry = template_entry("rust_custom");
        rust_entry.name = " ".to_string();
        rust_entry.description = " rust desc ".to_string();
        rust_entry.icon = " ".to_string();
        rust_entry.path = Some(" ./node.so ".to_string());
        rust_entry.inputs = Some(vec![" input ".to_string()]);
        rust_entry.outputs = Some(vec![" output ".to_string(), "OUTPUT".to_string()]);

        let mut empty_node_type = template_entry("   ");
        empty_node_type.name = "should be dropped".to_string();

        let normalized = normalize_node_template_entries(vec![
            python_old,
            python_new,
            rust_entry,
            empty_node_type,
        ]);

        assert_eq!(normalized.len(), 2);
        assert_eq!(normalized[0].node_type, "python_custom");
        assert_eq!(normalized[1].node_type, "rust_custom");

        let python = &normalized[0];
        assert_eq!(python.name, "Python Custom");
        assert_eq!(python.description, "latest");
        assert_eq!(python.icon, "🐍");
        assert_eq!(python.path, None);
        assert_eq!(python.inputs, Some(vec!["events".to_string()]));
        assert_eq!(python.outputs, None);

        let rust = &normalized[1];
        assert_eq!(rust.name, "rust_custom");
        assert_eq!(rust.description, "rust desc");
        assert_eq!(rust.icon, "🔧");
        assert_eq!(rust.path.as_deref(), Some("./node.so"));
        assert_eq!(rust.inputs, Some(vec!["input".to_string()]));
        assert_eq!(rust.outputs, Some(vec!["output".to_string()]));
    }

    #[tokio::test]
    async fn test_run_dataflow_returns_write_error_for_missing_working_dir() {
        let state = Arc::new(AppState::new());
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(missing_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;
        assert!(!resp.success);
        assert!(resp.process_id.is_none());
        assert!(resp.message.contains("Failed to write YAML"));
        assert_eq!(resp.error_code.as_deref(), Some(ERR_YAML_WRITE_FAILED));
    }

    #[tokio::test]
    async fn test_run_dataflow_returns_dora_not_installed_error_code() {
        let state = make_state_with_behavior(TestBehavior {
            force_dora_installed: Some(false),
            ..Default::default()
        });
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(writable_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;

        assert!(!resp.success);
        assert_eq!(resp.error_code.as_deref(), Some(ERR_DORA_NOT_INSTALLED));
    }

    #[tokio::test]
    async fn test_run_dataflow_returns_runtime_init_failed_error_code() {
        let state = make_state_with_behavior(TestBehavior {
            force_dora_installed: Some(true),
            force_runtime_ready_error: Some("mock runtime boot failure".to_string()),
            ..Default::default()
        });
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(writable_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;

        assert!(!resp.success);
        assert!(resp.message.contains("mock runtime boot failure"));
        assert_eq!(
            resp.error_code.as_deref(),
            Some(ERR_DORA_RUNTIME_INIT_FAILED)
        );
    }

    #[tokio::test]
    async fn test_run_dataflow_returns_start_timeout_error_code() {
        let state = make_state_with_behavior(TestBehavior {
            force_dora_installed: Some(true),
            force_run_outcome: Some(ForcedRunOutcome::StartTimeout),
            ..Default::default()
        });
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(writable_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;

        assert!(!resp.success);
        assert_eq!(resp.error_code.as_deref(), Some(ERR_DORA_START_TIMEOUT));
    }

    #[tokio::test]
    async fn test_run_dataflow_returns_start_failed_error_code() {
        let state = make_state_with_behavior(TestBehavior {
            force_dora_installed: Some(true),
            force_run_outcome: Some(ForcedRunOutcome::StartFailed(
                "mocked stderr: invalid node config".to_string(),
            )),
            ..Default::default()
        });
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(writable_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;

        assert!(!resp.success);
        assert!(resp.message.contains("invalid node config"));
        assert_eq!(resp.error_code.as_deref(), Some(ERR_DORA_START_FAILED));
    }

    #[tokio::test]
    async fn test_run_dataflow_returns_start_wait_failed_error_code() {
        let state = make_state_with_behavior(TestBehavior {
            force_dora_installed: Some(true),
            force_run_outcome: Some(ForcedRunOutcome::StartWaitFailed(
                "mock wait interrupted".to_string(),
            )),
            ..Default::default()
        });
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(writable_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;

        assert!(!resp.success);
        assert!(resp.message.contains("mock wait interrupted"));
        assert_eq!(resp.error_code.as_deref(), Some(ERR_DORA_START_WAIT_FAILED));
    }

    #[tokio::test]
    async fn test_run_dataflow_returns_start_spawn_failed_error_code() {
        let state = make_state_with_behavior(TestBehavior {
            force_dora_installed: Some(true),
            force_run_outcome: Some(ForcedRunOutcome::StartSpawnFailed(
                "mock spawn access denied".to_string(),
            )),
            ..Default::default()
        });
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(writable_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;

        assert!(!resp.success);
        assert!(resp.message.contains("mock spawn access denied"));
        assert_eq!(
            resp.error_code.as_deref(),
            Some(ERR_DORA_START_SPAWN_FAILED)
        );
    }

    #[tokio::test]
    async fn test_run_dataflow_forced_run_outcome_skips_runtime_ready_check() {
        let state = make_state_with_behavior(TestBehavior {
            force_dora_installed: Some(true),
            force_runtime_ready_error: Some("runtime should be skipped".to_string()),
            force_run_outcome: Some(ForcedRunOutcome::StartFailed(
                "mocked stderr: invalid node config".to_string(),
            )),
            ..Default::default()
        });
        let req = RunDataflowRequest {
            dataflow_yaml: "nodes: []\n".to_string(),
            working_dir: Some(writable_working_dir()),
        };

        let Json(resp) = run_dataflow(State(state), Json(req)).await;

        assert!(!resp.success);
        assert!(resp.message.contains("invalid node config"));
        assert_eq!(resp.error_code.as_deref(), Some(ERR_DORA_START_FAILED));
    }

    #[tokio::test]
    async fn test_status_returns_not_found_for_unknown_process() {
        let state = Arc::new(AppState::new());
        let Json(resp) = get_dataflow_status(State(state), Path("unknown".to_string())).await;

        assert_eq!(resp.process_id, "unknown");
        assert_eq!(resp.status, "not_found");
        assert_eq!(resp.total_nodes, 0);
        assert_eq!(resp.running_nodes, 0);
        assert_eq!(resp.error_nodes, 0);
    }

    #[tokio::test]
    async fn test_status_returns_stopped_for_registered_process_without_yaml() {
        let state = Arc::new(AppState::new());
        let process_id = "process_for_status_test".to_string();
        let missing_yaml = std::env::temp_dir()
            .join("doramate_status_missing_dataflow.yml")
            .to_string_lossy()
            .to_string();
        let (log_tx, _log_rx) = broadcast::channel::<LogEntry>(16);
        let dora_process = DoraProcess {
            _id: process_id.clone(),
            yaml_path: missing_yaml,
            started_at: std::time::Instant::now(),
            dataflow_uuid: None,
            log_tx,
            log_backlog: Arc::new(Mutex::new(VecDeque::new())),
        };
        state
            .processes
            .lock()
            .expect("lock process map")
            .insert(process_id.clone(), dora_process);

        let Json(resp) = get_dataflow_status(State(state), Path(process_id.clone())).await;

        assert_eq!(resp.process_id, process_id);
        assert_eq!(resp.status, "stopped");
        assert_eq!(resp.total_nodes, 0);
        assert_eq!(resp.running_nodes, 0);
        assert_eq!(resp.error_nodes, 0);
    }

    #[tokio::test]
    async fn test_stop_with_unknown_process_id_is_noop_success() {
        let state = Arc::new(AppState::new());
        let req = StopDataflowRequest {
            process_id: Some("unknown".to_string()),
        };

        let Json(resp) = stop_dataflow(State(state), Json(req)).await;

        assert!(resp.success);
        assert!(resp.message.contains("Stopped 0 dataflow(s) with 0 errors"));
        assert!(resp.error_code.is_none());
    }

    #[tokio::test]
    async fn test_stop_without_process_id_with_empty_registry_is_noop_success() {
        let state = Arc::new(AppState::new());
        let req = StopDataflowRequest { process_id: None };

        let Json(resp) = stop_dataflow(State(state), Json(req)).await;

        assert!(resp.success);
        assert!(resp.message.contains("Stopped 0 dataflow(s) with 0 errors"));
        assert!(resp.error_code.is_none());
    }

    #[tokio::test]
    async fn test_stop_returns_partial_failure_error_code_when_backend_stop_fails() {
        let state = make_state_with_behavior(TestBehavior {
            force_stop_error: Some("mock stop failed".to_string()),
            ..Default::default()
        });
        insert_mock_process(&state, "p-stop-fail", Some("uuid-1"));
        let req = StopDataflowRequest { process_id: None };

        let Json(resp) = stop_dataflow(State(state.clone()), Json(req)).await;

        assert!(resp.success);
        assert!(resp.message.contains("Stopped 0 dataflow(s) with 1 errors"));
        assert_eq!(resp.error_code.as_deref(), Some(ERR_STOP_PARTIAL_FAILURE));
        assert!(state
            .processes
            .lock()
            .expect("lock process map after stop")
            .contains_key("p-stop-fail"));
    }

    #[tokio::test]
    async fn test_read_dataflow_file_returns_file_path_empty_error_code() {
        let req = ReadDataflowFileRequest {
            file_path: "   ".to_string(),
        };

        let Json(resp) = read_dataflow_file(Json(req)).await;

        assert!(!resp.success);
        assert!(resp.file_path.is_none());
        assert_eq!(resp.error_code.as_deref(), Some(ERR_FILE_PATH_EMPTY));
    }

    #[tokio::test]
    async fn test_read_dataflow_file_returns_file_read_failed_for_missing_path() {
        let missing_path = std::env::temp_dir()
            .join(format!(
                "doramate_missing_file_{}_{}.yml",
                std::process::id(),
                SystemTime::now()
                    .duration_since(UNIX_EPOCH)
                    .expect("system time before unix epoch")
                    .as_nanos()
            ))
            .to_string_lossy()
            .to_string();
        let req = ReadDataflowFileRequest {
            file_path: missing_path.clone(),
        };

        let Json(resp) = read_dataflow_file(Json(req)).await;

        assert!(!resp.success);
        assert_eq!(resp.file_path.as_deref(), Some(missing_path.as_str()));
        assert_eq!(resp.error_code.as_deref(), Some(ERR_FILE_READ_FAILED));
    }
}

async fn stop_dataflow_by_uuid(uuid: &str) -> Result<String, String> {
    let output = tokio::process::Command::new("dora")
        .args(&[
            "stop",
            "--coordinator-port",
            &DORA_CONTROL_PORT.to_string(),
            uuid,
        ])
        .output()
        .await
        .map_err(|e| format!("Failed to execute dora stop: {}", e))?;

    let stdout = String::from_utf8_lossy(&output.stdout);
    let stderr = String::from_utf8_lossy(&output.stderr);

    if output.status.success() {
        Ok(stdout.trim().to_string())
    } else {
        Err(format!("dora stop failed: {}", stderr))
    }
}

/// 使用 dora stop 停止所有数据流
async fn stop_all_dataflows() -> Result<String, String> {
    let output = tokio::process::Command::new("dora")
        .args(&[
            "stop",
            "--all",
            "--coordinator-port",
            &DORA_CONTROL_PORT.to_string(),
        ])
        .output()
        .await
        .map_err(|e| format!("Failed to execute dora stop --all: {}", e))?;

    let stdout = String::from_utf8_lossy(&output.stdout);

    if output.status.success() {
        Ok(stdout.trim().to_string())
    } else {
        let stderr = String::from_utf8_lossy(&output.stderr);
        Err(format!("dora stop --all failed: {}", stderr))
    }
}

fn build_dataflow_status_response(
    state: &Arc<AppState>,
    process_id: &str,
) -> DataflowStatusResponse {
    let process_snapshot = {
        let processes = state.processes.lock().unwrap();
        processes.get(process_id).map(|dora_process| {
            (
                dora_process.started_at.elapsed().as_secs(),
                dora_process.yaml_path.clone(),
            )
        })
    };

    if let Some((uptime, yaml_path)) = process_snapshot {
        let (total_nodes, running_nodes, error_nodes, node_details) =
            check_all_nodes_status(&yaml_path);
        let status = if running_nodes > 0 {
            "running"
        } else {
            "stopped"
        };

        DataflowStatusResponse {
            process_id: process_id.to_string(),
            status: status.to_string(),
            uptime_seconds: uptime,
            total_nodes,
            running_nodes,
            error_nodes,
            node_details,
        }
    } else {
        DataflowStatusResponse {
            process_id: process_id.to_string(),
            status: "not_found".to_string(),
            uptime_seconds: 0,
            total_nodes: 0,
            running_nodes: 0,
            error_nodes: 0,
            node_details: vec![],
        }
    }
}

/// 查询数据流状态 API
async fn get_dataflow_status(
    State(state): State<Arc<AppState>>,
    Path(process_id): Path<String>,
) -> Json<DataflowStatusResponse> {
    Json(build_dataflow_status_response(&state, &process_id))
}

/// 检查 dora 是否安装
fn check_dora_installed() -> bool {
    std::process::Command::new("dora")
        .arg("--version")
        .output()
        .map(|_| true)
        .unwrap_or(false)
}

/// Dora coordinator 配置
const DORA_COORDINATOR_PORT: u16 = 54500;
const DORA_CONTROL_PORT: u16 = 6012;
const DORA_DAEMON_LOCAL_PORT: u16 = 54501;

fn is_local_port_open(port: u16) -> bool {
    let addr = SocketAddr::from(([127, 0, 0, 1], port));
    TcpStream::connect_timeout(&addr, Duration::from_millis(300)).is_ok()
}

fn summarize_process_output(stdout: &[u8], stderr: &[u8]) -> String {
    let stdout = String::from_utf8_lossy(stdout).trim().to_string();
    let stderr = String::from_utf8_lossy(stderr).trim().to_string();

    let mut parts = Vec::new();
    if !stdout.is_empty() {
        parts.push(format!("stdout: {}", stdout));
    }
    if !stderr.is_empty() {
        parts.push(format!("stderr: {}", stderr));
    }

    if parts.is_empty() {
        "no process output".to_string()
    } else {
        parts.join(" | ")
    }
}

/// 检查 dora coordinator 是否正在运行
fn check_dora_coordinator_running() -> bool {
    if is_local_port_open(DORA_COORDINATOR_PORT) {
        return true;
    }

    let output = std::process::Command::new("dora").args(&["list"]).output();

    if let Ok(out) = output {
        if out.status.success() {
            return true;
        }
    }

    false
}

/// 检查 dora daemon 是否正在运行
fn check_dora_daemon_running() -> bool {
    if is_local_port_open(DORA_DAEMON_LOCAL_PORT) {
        return true;
    }

    #[cfg(not(target_os = "windows"))]
    {
        let output = std::process::Command::new("pgrep")
            .args(&["-f", "dora"])
            .output();

        if let Ok(out) = output {
            if out.status.success() && !out.stdout.is_empty() {
                return true;
            }
        }
    }

    false
}

/// 启动 dora daemon（后台运行）
async fn start_dora_daemon() -> Result<(), String> {
    let mut cmd = tokio::process::Command::new("dora");
    cmd.args(&[
        "daemon",
        "--coordinator-port",
        &DORA_COORDINATOR_PORT.to_string(),
        "--local-listen-port",
        &DORA_DAEMON_LOCAL_PORT.to_string(),
    ])
    .stdout(Stdio::piped())
    .stderr(Stdio::piped())
    .kill_on_drop(false);

    let mut child = cmd
        .spawn()
        .map_err(|e| format!("Failed to spawn dora daemon: {}", e))?;

    let pid = child.id();
    info!("Dora daemon started with PID: {:?}", pid);

    tokio::time::sleep(Duration::from_millis(1200)).await;

    match child.try_wait() {
        Ok(Some(status)) => {
            let output = child
                .wait_with_output()
                .await
                .map_err(|e| format!("Failed reading dora daemon output: {}", e))?;
            return Err(format!(
                "Dora daemon exited early (status {}): {}",
                status,
                summarize_process_output(&output.stdout, &output.stderr)
            ));
        }
        Ok(None) => {}
        Err(e) => {
            return Err(format!("Failed to query dora daemon status: {}", e));
        }
    }

    for _ in 0..6 {
        if check_dora_daemon_running() {
            info!("Dora daemon is now running");
            return Ok(());
        }
        tokio::time::sleep(Duration::from_millis(500)).await;
    }

    Err(format!(
        "Dora daemon did not open local port {} in time",
        DORA_DAEMON_LOCAL_PORT
    ))
}

/// 启动 dora coordinator（后台运行）
async fn start_dora_coordinator() -> Result<(), String> {
    let mut cmd = tokio::process::Command::new("dora");
    cmd.args(&[
        "coordinator",
        "--port",
        &DORA_COORDINATOR_PORT.to_string(),
        "--control-port",
        &DORA_CONTROL_PORT.to_string(),
    ])
    .stdout(Stdio::piped())
    .stderr(Stdio::piped())
    .kill_on_drop(false);

    let mut child = cmd
        .spawn()
        .map_err(|e| format!("Failed to spawn dora coordinator: {}", e))?;

    let pid = child.id();
    info!("Dora coordinator started with PID: {:?}", pid);

    tokio::time::sleep(Duration::from_millis(1200)).await;

    match child.try_wait() {
        Ok(Some(status)) => {
            let output = child
                .wait_with_output()
                .await
                .map_err(|e| format!("Failed reading dora coordinator output: {}", e))?;
            return Err(format!(
                "Dora coordinator exited early (status {}): {}",
                status,
                summarize_process_output(&output.stdout, &output.stderr)
            ));
        }
        Ok(None) => {}
        Err(e) => {
            return Err(format!("Failed to query dora coordinator status: {}", e));
        }
    }

    let mut retries = 0;
    let max_retries = 5;

    while retries < max_retries {
        if check_dora_coordinator_running() {
            info!("Dora coordinator is now running");
            return Ok(());
        }

        retries += 1;
        tokio::time::sleep(Duration::from_secs(1)).await;
    }

    Err("Dora coordinator failed to start within timeout".to_string())
}

/// 确保 dora coordinator 和 daemon 都在运行
async fn ensure_dora_runtime_ready() -> Result<(), String> {
    let coordinator_running = check_dora_coordinator_running();
    let daemon_running = check_dora_daemon_running();

    if coordinator_running && daemon_running {
        info!("Dora runtime is already ready");
        return Ok(());
    }

    if !coordinator_running {
        warn!("Dora coordinator not running, starting...");
        start_dora_coordinator().await?;
    }

    if !daemon_running {
        warn!("Dora daemon not running, starting...");
        start_dora_daemon().await?;
    }

    tokio::time::sleep(Duration::from_millis(500)).await;

    if check_dora_coordinator_running() && check_dora_daemon_running() {
        info!("Dora runtime is now ready");
        Ok(())
    } else {
        Err("Dora runtime failed to initialize properly".to_string())
    }
}
