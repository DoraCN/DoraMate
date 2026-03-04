use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize};
use wasm_bindgen::prelude::*;
use wasm_bindgen::JsCast;
use wasm_bindgen_futures::JsFuture;
use web_sys::{
    CloseEvent, ErrorEvent, Event, Headers, MessageEvent, RequestInit, RequestMode, Response,
    WebSocket,
};

/// LocalAgent API base URL.
const API_BASE: &str = "http://127.0.0.1:52100/api";

/// WebSocket URL base for logs.
const WS_BASE: &str = "ws://127.0.0.1:52100/api";

#[derive(Serialize, Deserialize, Debug)]
pub struct RunDataflowRequest {
    pub dataflow_yaml: String,
    pub working_dir: Option<String>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct RunDataflowResponse {
    pub success: bool,
    pub message: String,
    pub process_id: Option<String>,
    #[serde(default)]
    pub error_code: Option<String>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct StopDataflowRequest {
    pub process_id: String,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct StopDataflowResponse {
    pub success: bool,
    pub message: String,
    #[serde(default)]
    pub error_code: Option<String>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct HealthResponse {
    pub status: String,
    pub version: String,
    pub dora_installed: bool,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct SelectDirectoryResponse {
    pub success: bool,
    pub cancelled: bool,
    pub path: Option<String>,
    pub message: String,
    #[serde(default)]
    pub error_code: Option<String>,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct OpenDataflowFileResponse {
    pub success: bool,
    pub cancelled: bool,
    pub file_path: Option<String>,
    pub file_name: Option<String>,
    pub working_dir: Option<String>,
    pub content: Option<String>,
    pub message: String,
    #[serde(default)]
    pub error_code: Option<String>,
}

#[derive(Serialize, Deserialize, Debug)]
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

#[derive(Serialize, Deserialize, Debug)]
pub struct SaveNodeTemplatesConfigRequest {
    #[serde(default)]
    pub templates: Vec<NodeTemplateConfigEntry>,
}

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct NodeTemplatesConfigResponse {
    pub success: bool,
    #[serde(default)]
    pub templates: Vec<NodeTemplateConfigEntry>,
    pub config_path: Option<String>,
    pub message: String,
    #[serde(default)]
    pub error_code: Option<String>,
}

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct DataflowStatusResponse {
    pub process_id: String,
    pub status: String,
    pub uptime_seconds: u64,
    pub total_nodes: usize,
    pub running_nodes: usize,
    pub error_nodes: usize,
    pub node_details: Vec<NodeDetail>,
}

#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct NodeDetail {
    pub id: String,
    pub node_type: String,
    pub is_running: bool,
}

fn normalize_js_error(err: &JsValue) -> String {
    err.as_string().unwrap_or_else(|| format!("{:?}", err))
}

fn map_fetch_error(action: &str, err: JsValue) -> String {
    let msg = normalize_js_error(&err);
    if msg.contains("Failed to fetch") || msg.contains("NetworkError") {
        format!(
            "{} failed: cannot connect to LocalAgent at {}. Start doramate-localagent first.",
            action, API_BASE
        )
    } else {
        format!("{} failed: {}", action, msg)
    }
}

pub fn friendly_error_message(error_code: Option<&str>, fallback_message: &str) -> String {
    match error_code {
        Some("DORA_NOT_INSTALLED") => "DORA is not installed. Install dora-cli first.".to_string(),
        Some("DORA_RUNTIME_INIT_FAILED") => {
            "DORA runtime failed to initialize. Check coordinator/daemon and environment."
                .to_string()
        }
        Some("YAML_WRITE_FAILED") => {
            "Failed to write run YAML. Check working directory path and permissions.".to_string()
        }
        Some("DORA_START_TIMEOUT") => {
            "dora start timed out. Check YAML validity and dora runtime status.".to_string()
        }
        Some("DORA_START_WAIT_FAILED") => {
            "Failed while waiting dora start result. Check dora installation and logs.".to_string()
        }
        Some("DORA_START_FAILED") => {
            "dora start failed. Check yaml_path and process output.".to_string()
        }
        Some("DORA_START_SPAWN_FAILED") => {
            "Failed to spawn dora process. Check dora command availability.".to_string()
        }
        Some("DIRECTORY_PICKER_FAILED") => {
            "Failed to open directory picker in LocalAgent.".to_string()
        }
        Some("FILE_PICKER_FAILED") => "Failed to open file picker in LocalAgent.".to_string(),
        Some("FILE_READ_FAILED") => {
            "Failed to read file content. Check path and permissions.".to_string()
        }
        Some("FILE_PATH_EMPTY") => "File path is empty.".to_string(),
        Some("NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE") => {
            "Node templates config path is unavailable in LocalAgent.".to_string()
        }
        Some("NODE_TEMPLATES_CONFIG_READ_FAILED") => {
            "Failed to read node templates config YAML.".to_string()
        }
        Some("NODE_TEMPLATES_CONFIG_WRITE_FAILED") => {
            "Failed to write node templates config YAML.".to_string()
        }
        Some("STOP_PARTIAL_FAILURE") => {
            "Some dataflows failed to stop. Check LocalAgent logs for details.".to_string()
        }
        _ => fallback_message.to_string(),
    }
}

async fn fetch_json<T: DeserializeOwned>(
    method: &str,
    endpoint: &str,
    body: Option<String>,
    action: &str,
) -> Result<T, String> {
    let opts = RequestInit::new();
    opts.set_method(method);
    opts.set_mode(RequestMode::Cors);

    if let Some(body_text) = body {
        opts.set_body(&JsValue::from_str(&body_text));
        let headers = Headers::new().map_err(|e| format!("{} failed: {:?}", action, e))?;
        headers
            .set("Content-Type", "application/json")
            .map_err(|e| format!("{} failed: {:?}", action, e))?;
        opts.set_headers(&headers);
    }

    let url = format!("{}/{}", API_BASE, endpoint);
    let req = web_sys::Request::new_with_str_and_init(&url, &opts)
        .map_err(|e| format!("{} failed: request build error {:?}", action, e))?;

    let window = web_sys::window().ok_or_else(|| "browser window unavailable".to_string())?;
    let resp_value = JsFuture::from(window.fetch_with_request(&req))
        .await
        .map_err(|e| map_fetch_error(action, e))?;

    let resp: Response = resp_value
        .dyn_into()
        .map_err(|e| format!("{} failed: response cast error {:?}", action, e))?;

    let status = resp.status();
    let json_value = JsFuture::from(
        resp.json()
            .map_err(|e| format!("{} failed: read response JSON error {:?}", action, e))?,
    )
    .await
    .map_err(|e| format!("{} failed: await response JSON error {:?}", action, e))?;

    let parsed: T = serde_wasm_bindgen::from_value(json_value)
        .map_err(|e| format!("{} failed: invalid response payload {}", action, e))?;

    if !(200..300).contains(&status) {
        return Err(format!("{} failed: HTTP {}", action, status));
    }

    Ok(parsed)
}

pub async fn run_dataflow(
    yaml_content: &str,
    working_dir: Option<String>,
) -> Result<RunDataflowResponse, String> {
    let request = RunDataflowRequest {
        dataflow_yaml: yaml_content.to_string(),
        working_dir,
    };
    let body = serde_json::to_string(&request)
        .map_err(|e| format!("run dataflow failed: serialize request error {}", e))?;
    fetch_json("POST", "run", Some(body), "run dataflow").await
}

pub async fn stop_dataflow(process_id: &str) -> Result<StopDataflowResponse, String> {
    let request = StopDataflowRequest {
        process_id: process_id.to_string(),
    };
    let body = serde_json::to_string(&request)
        .map_err(|e| format!("stop dataflow failed: serialize request error {}", e))?;
    fetch_json("POST", "stop", Some(body), "stop dataflow").await
}

pub async fn health_check() -> Result<HealthResponse, String> {
    fetch_json("GET", "health", None, "health check").await
}

pub async fn select_directory() -> Result<SelectDirectoryResponse, String> {
    fetch_json("POST", "select-directory", None, "select directory").await
}

pub async fn open_dataflow_file() -> Result<OpenDataflowFileResponse, String> {
    fetch_json("POST", "open-dataflow-file", None, "open dataflow file").await
}

pub async fn read_dataflow_file(file_path: &str) -> Result<OpenDataflowFileResponse, String> {
    let request = ReadDataflowFileRequest {
        file_path: file_path.to_string(),
    };
    let body = serde_json::to_string(&request)
        .map_err(|e| format!("read dataflow file failed: serialize request error {}", e))?;
    fetch_json(
        "POST",
        "read-dataflow-file",
        Some(body),
        "read dataflow file",
    )
    .await
}

pub async fn load_node_templates_config() -> Result<NodeTemplatesConfigResponse, String> {
    fetch_json(
        "GET",
        "node-templates-config",
        None,
        "load node templates config",
    )
    .await
}

pub async fn save_node_templates_config(
    templates: &[NodeTemplateConfigEntry],
) -> Result<NodeTemplatesConfigResponse, String> {
    let request = SaveNodeTemplatesConfigRequest {
        templates: templates.to_vec(),
    };
    let body = serde_json::to_string(&request).map_err(|e| {
        format!(
            "save node templates config failed: serialize request error {}",
            e
        )
    })?;
    fetch_json(
        "POST",
        "node-templates-config",
        Some(body),
        "save node templates config",
    )
    .await
}

pub async fn get_dataflow_status(process_id: &str) -> Result<DataflowStatusResponse, String> {
    fetch_json(
        "GET",
        &format!("status/{}", process_id),
        None,
        "get dataflow status",
    )
    .await
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_api_urls() {
        assert_eq!(
            format!("{}/run", API_BASE),
            "http://127.0.0.1:52100/api/run"
        );
        assert_eq!(
            format!("{}/stop", API_BASE),
            "http://127.0.0.1:52100/api/stop"
        );
        assert_eq!(
            format!("{}/health", API_BASE),
            "http://127.0.0.1:52100/api/health"
        );
        assert_eq!(
            format!("{}/select-directory", API_BASE),
            "http://127.0.0.1:52100/api/select-directory"
        );
        assert_eq!(
            format!("{}/open-dataflow-file", API_BASE),
            "http://127.0.0.1:52100/api/open-dataflow-file"
        );
        assert_eq!(
            format!("{}/read-dataflow-file", API_BASE),
            "http://127.0.0.1:52100/api/read-dataflow-file"
        );
        assert_eq!(
            format!("{}/node-templates-config", API_BASE),
            "http://127.0.0.1:52100/api/node-templates-config"
        );
        assert_eq!(
            format!("{}/status/123", API_BASE),
            "http://127.0.0.1:52100/api/status/123"
        );
        assert_eq!(
            format!("{}/logs/123", WS_BASE),
            "ws://127.0.0.1:52100/api/logs/123"
        );
        assert_eq!(
            format!("{}/status-stream/123", WS_BASE),
            "ws://127.0.0.1:52100/api/status-stream/123"
        );
    }

    #[test]
    fn test_friendly_error_message() {
        let msg = friendly_error_message(
            Some("YAML_WRITE_FAILED"),
            "Failed to write YAML: access denied",
        );
        assert!(msg.contains("write run YAML"));

        let file_read_msg =
            friendly_error_message(Some("FILE_READ_FAILED"), "Failed to read selected file");
        assert!(file_read_msg.contains("read file content"));

        let unknown_code_msg = friendly_error_message(Some("UNKNOWN_CODE"), "raw backend message");
        assert_eq!(unknown_code_msg, "raw backend message");

        let passthrough = friendly_error_message(None, "raw backend message");
        assert_eq!(passthrough, "raw backend message");
    }

    #[test]
    fn test_friendly_error_message_for_node_templates_codes() {
        let path_msg = friendly_error_message(
            Some("NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE"),
            "raw backend message",
        );
        assert!(path_msg.contains("config path is unavailable"));

        let read_msg = friendly_error_message(
            Some("NODE_TEMPLATES_CONFIG_READ_FAILED"),
            "raw backend message",
        );
        assert!(read_msg.contains("read node templates config YAML"));

        let write_msg = friendly_error_message(
            Some("NODE_TEMPLATES_CONFIG_WRITE_FAILED"),
            "raw backend message",
        );
        assert!(write_msg.contains("write node templates config YAML"));
    }

    #[test]
    fn test_save_node_templates_config_request_serialization() {
        let req = SaveNodeTemplatesConfigRequest {
            templates: vec![NodeTemplateConfigEntry {
                node_type: "python_custom".to_string(),
                name: "Python Custom".to_string(),
                description: "Python node".to_string(),
                icon: "🐍".to_string(),
                path: Some("./process.py".to_string()),
                inputs: Some(vec!["image".to_string()]),
                outputs: Some(vec!["result".to_string()]),
            }],
        };

        let json = serde_json::to_string(&req).expect("serialize template config request");
        assert!(json.contains("\"templates\""));
        assert!(json.contains("\"node_type\":\"python_custom\""));
        assert!(json.contains("\"path\":\"./process.py\""));
        assert!(json.contains("\"inputs\":[\"image\"]"));
        assert!(json.contains("\"outputs\":[\"result\"]"));
    }

    #[cfg(target_arch = "wasm32")]
    #[test]
    fn test_map_fetch_error_for_network_and_generic_error() {
        let network_msg = map_fetch_error(
            "open dataflow file",
            JsValue::from_str("TypeError: Failed to fetch"),
        );
        assert!(network_msg.contains("cannot connect to LocalAgent"));

        let generic_msg = map_fetch_error("run dataflow", JsValue::from_str("HTTP 500"));
        assert!(generic_msg.contains("run dataflow failed"));
        assert!(generic_msg.contains("HTTP 500"));
    }
}

pub struct LogWebSocket {
    ws: Option<WebSocket>,
    on_message_callback: Option<Closure<dyn FnMut(MessageEvent)>>,
    on_error_callback: Option<Closure<dyn FnMut(ErrorEvent)>>,
    on_open_callback: Option<Closure<dyn FnMut(Event)>>,
    on_close_callback: Option<Closure<dyn FnMut(CloseEvent)>>,
}

impl LogWebSocket {
    pub fn new() -> Self {
        Self {
            ws: None,
            on_message_callback: None,
            on_error_callback: None,
            on_open_callback: None,
            on_close_callback: None,
        }
    }

    pub fn connect(&mut self, process_id: &str) -> Result<(), String> {
        self.close();
        let url = format!("{}/logs/{}", WS_BASE, process_id);
        let ws =
            WebSocket::new(&url).map_err(|e| format!("failed to create websocket: {:?}", e))?;
        self.ws = Some(ws);
        Ok(())
    }

    pub fn set_on_message<F>(&mut self, mut callback: F)
    where
        F: FnMut(String) + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |event: MessageEvent| {
                if let Some(text) = event.data().as_string() {
                    callback(text);
                } else if let Ok(js_text) = event.data().dyn_into::<js_sys::JsString>() {
                    callback(js_text.as_string().unwrap_or_default());
                }
            }) as Box<dyn FnMut(MessageEvent)>);

            ws.set_onmessage(Some(closure.as_ref().unchecked_ref()));
            self.on_message_callback = Some(closure);
        }
    }

    pub fn set_on_error<F>(&mut self, mut callback: F)
    where
        F: FnMut(String) + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |event: ErrorEvent| {
                callback(event.message());
            }) as Box<dyn FnMut(ErrorEvent)>);

            ws.set_onerror(Some(closure.as_ref().unchecked_ref()));
            self.on_error_callback = Some(closure);
        }
    }

    pub fn set_on_open<F>(&mut self, mut callback: F)
    where
        F: FnMut() + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |_event: Event| {
                callback();
            }) as Box<dyn FnMut(Event)>);

            ws.set_onopen(Some(closure.as_ref().unchecked_ref()));
            self.on_open_callback = Some(closure);
        }
    }

    pub fn set_on_close<F>(&mut self, mut callback: F)
    where
        F: FnMut() + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |_event: CloseEvent| {
                callback();
            }) as Box<dyn FnMut(CloseEvent)>);

            ws.set_onclose(Some(closure.as_ref().unchecked_ref()));
            self.on_close_callback = Some(closure);
        }
    }

    pub fn close(&mut self) {
        if let Some(ws) = &self.ws {
            ws.set_onmessage(None);
            ws.set_onerror(None);
            ws.set_onopen(None);
            ws.set_onclose(None);
            let _ = ws.close();
        }

        self.ws = None;
        self.on_message_callback = None;
        self.on_error_callback = None;
        self.on_open_callback = None;
        self.on_close_callback = None;
    }

    pub fn is_connected(&self) -> bool {
        self.ws
            .as_ref()
            .map(|ws| ws.ready_state() == WebSocket::OPEN)
            .unwrap_or(false)
    }

    pub fn ready_state(&self) -> u16 {
        self.ws
            .as_ref()
            .map(|ws| ws.ready_state())
            .unwrap_or(WebSocket::CONNECTING)
    }
}

impl Default for LogWebSocket {
    fn default() -> Self {
        Self::new()
    }
}

impl Drop for LogWebSocket {
    fn drop(&mut self) {
        self.close();
    }
}

pub struct StatusWebSocket {
    ws: Option<WebSocket>,
    on_message_callback: Option<Closure<dyn FnMut(MessageEvent)>>,
    on_error_callback: Option<Closure<dyn FnMut(ErrorEvent)>>,
    on_open_callback: Option<Closure<dyn FnMut(Event)>>,
    on_close_callback: Option<Closure<dyn FnMut(CloseEvent)>>,
}

impl StatusWebSocket {
    pub fn new() -> Self {
        Self {
            ws: None,
            on_message_callback: None,
            on_error_callback: None,
            on_open_callback: None,
            on_close_callback: None,
        }
    }

    pub fn connect(&mut self, process_id: &str) -> Result<(), String> {
        self.close();
        let url = format!("{}/status-stream/{}", WS_BASE, process_id);
        let ws =
            WebSocket::new(&url).map_err(|e| format!("failed to create websocket: {:?}", e))?;
        self.ws = Some(ws);
        Ok(())
    }

    pub fn set_on_message<F>(&mut self, mut callback: F)
    where
        F: FnMut(DataflowStatusResponse) + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |event: MessageEvent| {
                let payload = if let Some(text) = event.data().as_string() {
                    Some(text)
                } else if let Ok(js_text) = event.data().dyn_into::<js_sys::JsString>() {
                    js_text.as_string()
                } else {
                    None
                };

                if let Some(text) = payload {
                    if let Ok(status) = serde_json::from_str::<DataflowStatusResponse>(&text) {
                        callback(status);
                    }
                }
            }) as Box<dyn FnMut(MessageEvent)>);

            ws.set_onmessage(Some(closure.as_ref().unchecked_ref()));
            self.on_message_callback = Some(closure);
        }
    }

    pub fn set_on_error<F>(&mut self, mut callback: F)
    where
        F: FnMut(String) + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |event: ErrorEvent| {
                callback(event.message());
            }) as Box<dyn FnMut(ErrorEvent)>);

            ws.set_onerror(Some(closure.as_ref().unchecked_ref()));
            self.on_error_callback = Some(closure);
        }
    }

    pub fn set_on_open<F>(&mut self, mut callback: F)
    where
        F: FnMut() + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |_event: Event| {
                callback();
            }) as Box<dyn FnMut(Event)>);

            ws.set_onopen(Some(closure.as_ref().unchecked_ref()));
            self.on_open_callback = Some(closure);
        }
    }

    pub fn set_on_close<F>(&mut self, mut callback: F)
    where
        F: FnMut() + 'static,
    {
        if let Some(ws) = &self.ws {
            let closure = Closure::wrap(Box::new(move |_event: CloseEvent| {
                callback();
            }) as Box<dyn FnMut(CloseEvent)>);

            ws.set_onclose(Some(closure.as_ref().unchecked_ref()));
            self.on_close_callback = Some(closure);
        }
    }

    pub fn close(&mut self) {
        if let Some(ws) = &self.ws {
            ws.set_onmessage(None);
            ws.set_onerror(None);
            ws.set_onopen(None);
            ws.set_onclose(None);
            let _ = ws.close();
        }

        self.ws = None;
        self.on_message_callback = None;
        self.on_error_callback = None;
        self.on_open_callback = None;
        self.on_close_callback = None;
    }

    pub fn is_connected(&self) -> bool {
        self.ws
            .as_ref()
            .map(|ws| ws.ready_state() == WebSocket::OPEN)
            .unwrap_or(false)
    }

    pub fn ready_state(&self) -> u16 {
        self.ws
            .as_ref()
            .map(|ws| ws.ready_state())
            .unwrap_or(WebSocket::CONNECTING)
    }
}

impl Default for StatusWebSocket {
    fn default() -> Self {
        Self::new()
    }
}

impl Drop for StatusWebSocket {
    fn drop(&mut self) {
        self.close();
    }
}
