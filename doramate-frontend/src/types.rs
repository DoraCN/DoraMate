use serde::{Deserialize, Serialize};
use std::collections::HashMap;

/// 数据流图 (DoraMate 内部格式 - 用于可视化编辑器)
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
pub struct Dataflow {
    pub nodes: Vec<Node>,
    pub connections: Vec<Connection>,
}

/// DORA 数据流图 (运行时格式 - 用于 dora-runtime)
#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct DoraDataflow {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub __doramate__: Option<DoraMateMeta>,
    pub nodes: Vec<DoraNode>,
}

/// DoraMate 元数据 (存储可视化信息)
#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct DoraMateMeta {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub layout: Option<HashMap<String, LayoutInfo>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub tags: Option<Vec<String>>,
}

/// 布局信息
#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct LayoutInfo {
    pub x: f64,
    pub y: f64,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub label: Option<String>,
}

/// DORA 节点 (运行时格式)
#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct DoraNode {
    pub id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub path: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub build: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub inputs: Option<HashMap<String, String>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub outputs: Option<Vec<String>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub env: Option<HashMap<String, String>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub operators: Option<Vec<DoraOperator>>,
}

/// DORA Operator (用于 runtime-node)
#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct DoraOperator {
    pub id: String,
    pub shared_library: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub inputs: Option<HashMap<String, String>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub outputs: Option<Vec<String>>,
}

/// 节点 (DoraMate 可视化编辑器格式)
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
pub struct Node {
    /// 节点唯一标识符
    pub id: String,
    /// X 坐标 (可视化位置)
    pub x: f64,
    /// Y 坐标 (可视化位置)
    pub y: f64,
    /// 显示标签
    pub label: String,
    /// 节点类型 (用于推断 DORA path 和 build)
    #[serde(rename = "type")]
    pub node_type: String,
    /// 节点路径 (可选，用于自定义 DORA node 路径)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub path: Option<String>,
    /// 环境变量 (可选)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub env: Option<HashMap<String, String>>,
    /// 自定义配置 (可选)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub config: Option<serde_yaml::Value>,
    /// 输出端口列表 (可选，用于可视化)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub outputs: Option<Vec<String>>,
    /// 输入端口列表 (可选，用于可视化)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub inputs: Option<Vec<String>>,
    /// 节点缩放比例 (可选，用于可视化，默认 1.0)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub scale: Option<f64>,
}

/// 连线
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
pub struct Connection {
    pub from: String,
    pub to: String,
    /// 输出端口名称 (可选，默认为 "out")
    #[serde(skip_serializing_if = "Option::is_none")]
    pub from_port: Option<String>,
    /// 输入端口名称 (可选，默认为 "in")
    #[serde(skip_serializing_if = "Option::is_none")]
    pub to_port: Option<String>,
}

/// 端口类型
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PortType {
    Input,
    Output,
}

/// 节点运行状态
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum NodeState {
    /// 空闲状态
    Idle,
    /// 启动中
    Starting,
    /// 运行中
    Running,
    /// 已停止
    Stopped,
    /// 错误状态
    Error(String),
}

impl NodeState {
    /// 获取状态对应的CSS类名
    pub fn css_class(&self) -> &'static str {
        match self {
            NodeState::Idle => "node-idle",
            NodeState::Starting => "node-starting",
            NodeState::Running => "node-running",
            NodeState::Stopped => "node-stopped",
            NodeState::Error(_) => "node-error",
        }
    }

    /// 获取状态对应的边框颜色
    pub fn border_color(&self) -> &'static str {
        match self {
            NodeState::Idle => "#2196F3",     // 蓝色
            NodeState::Starting => "#FF9800", // 橙色
            NodeState::Running => "#4CAF50",  // 绿色
            NodeState::Stopped => "#9E9E9E",  // 灰色
            NodeState::Error(_) => "#f44336", // 红色
        }
    }

    /// 获取状态显示文本
    pub fn display_text(&self) -> &'static str {
        match self {
            NodeState::Idle => "空闲",
            NodeState::Starting => "启动中",
            NodeState::Running => "运行中",
            NodeState::Stopped => "已停止",
            NodeState::Error(_) => "错误",
        }
    }
}

impl Default for NodeState {
    fn default() -> Self {
        NodeState::Idle
    }
}

/// 日志条目级别
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum LogLevel {
    Info,
    Warn,
    Error,
    Debug,
}

impl LogLevel {
    pub fn from_str(s: &str) -> Self {
        match s.to_lowercase().as_str() {
            "error" | "err" => LogLevel::Error,
            "warn" | "warning" => LogLevel::Warn,
            "debug" | "dbg" => LogLevel::Debug,
            _ => LogLevel::Info,
        }
    }

    pub fn css_color(&self) -> &'static str {
        match self {
            LogLevel::Info => "#2196F3",
            LogLevel::Warn => "#FF9800",
            LogLevel::Error => "#f44336",
            LogLevel::Debug => "#9E9E9E",
        }
    }

    pub fn css_class(&self) -> &'static str {
        match self {
            LogLevel::Info => "log-info",
            LogLevel::Warn => "log-warn",
            LogLevel::Error => "log-error",
            LogLevel::Debug => "log-debug",
        }
    }

    pub fn icon(&self) -> &'static str {
        match self {
            LogLevel::Info => "ℹ",
            LogLevel::Warn => "⚠",
            LogLevel::Error => "✖",
            LogLevel::Debug => "🐛",
        }
    }
}

/// 日志来源
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum LogSource {
    Stdout,
    Stderr,
    Dora,
    System,
}

impl LogSource {
    pub fn from_str(s: &str) -> Self {
        match s.to_lowercase().as_str() {
            "stdout" | "out" => LogSource::Stdout,
            "stderr" | "err" => LogSource::Stderr,
            "dora" => LogSource::Dora,
            _ => LogSource::System,
        }
    }

    pub fn css_class(&self) -> &'static str {
        match self {
            LogSource::Stdout => "log-source-stdout",
            LogSource::Stderr => "log-source-stderr",
            LogSource::Dora => "log-source-dora",
            LogSource::System => "log-source-system",
        }
    }
}

/// 日志条目
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct LogEntry {
    /// 时间戳 (ISO 8601 格式)
    pub timestamp: String,
    /// 日志级别 (字符串格式，兼容后端)
    pub level: String,
    /// 日志来源 (字符串格式，兼容后端)
    pub source: String,
    /// 日志消息
    pub message: String,
    /// 关联的节点 ID (可选)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub node_id: Option<String>,
    /// 进程 ID (可选)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub process_id: Option<String>,
}

impl LogEntry {
    pub fn new(level: String, source: String, message: String, process_id: Option<String>) -> Self {
        let now = js_sys::Date::new_0();
        let timestamp = now
            .to_iso_string()
            .as_string()
            .unwrap_or_else(|| now.to_string().as_string().unwrap_or_default());

        Self {
            timestamp,
            level,
            source,
            message,
            node_id: None,
            process_id,
        }
    }

    pub fn info(message: String, process_id: Option<String>) -> Self {
        Self::new(
            "info".to_string(),
            "system".to_string(),
            message,
            process_id,
        )
    }

    pub fn error(message: String, process_id: Option<String>) -> Self {
        Self::new(
            "error".to_string(),
            "system".to_string(),
            message,
            process_id,
        )
    }

    pub fn warn(message: String, process_id: Option<String>) -> Self {
        Self::new(
            "warn".to_string(),
            "system".to_string(),
            message,
            process_id,
        )
    }

    /// 获取日志级别的图标
    pub fn level_icon(&self) -> &'static str {
        match self.level.to_lowercase().as_str() {
            "error" | "err" => "✖",
            "warn" | "warning" => "⚠",
            "debug" | "dbg" => "🐛",
            _ => "ℹ",
        }
    }

    /// 获取日志级别的颜色
    pub fn level_color(&self) -> &'static str {
        match self.level.to_lowercase().as_str() {
            "error" | "err" => "#f44336",
            "warn" | "warning" => "#FF9800",
            "debug" | "dbg" => "#9E9E9E",
            _ => "#2196F3",
        }
    }

    /// 获取日志级别的 CSS 类
    pub fn level_class(&self) -> &'static str {
        match self.level.to_lowercase().as_str() {
            "error" | "err" => "log-error",
            "warn" | "warning" => "log-warn",
            "debug" | "dbg" => "log-debug",
            _ => "log-info",
        }
    }
}

/// WebSocket 连接状态
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum WebSocketState {
    Connecting,
    Connected,
    Disconnecting,
    Disconnected,
    Reconnecting(u32), // 重连次数
}

impl WebSocketState {
    pub fn css_class(&self) -> &'static str {
        match self {
            WebSocketState::Connecting => "ws-connecting",
            WebSocketState::Connected => "ws-connected",
            WebSocketState::Disconnecting => "ws-disconnecting",
            WebSocketState::Disconnected => "ws-disconnected",
            WebSocketState::Reconnecting(_) => "ws-reconnecting",
        }
    }

    pub fn display_text(&self) -> String {
        match self {
            WebSocketState::Connecting => "连接中...".to_string(),
            WebSocketState::Connected => "已连接".to_string(),
            WebSocketState::Disconnecting => "断开中...".to_string(),
            WebSocketState::Disconnected => "已断开".to_string(),
            WebSocketState::Reconnecting(count) => {
                format!("重连中... ({}/{})", count, MAX_RECONNECT_ATTEMPTS)
            }
        }
    }
}

impl Default for WebSocketState {
    fn default() -> Self {
        WebSocketState::Disconnected
    }
}

pub const MAX_RECONNECT_ATTEMPTS: u32 = 5;
