use crate::types::{LogEntry, WebSocketState};
use crate::utils::api::LogWebSocket;
use leptos::prelude::*;
use std::cell::RefCell;
use std::collections::BTreeSet;
use std::rc::Rc;
use wasm_bindgen::{JsCast, JsValue};

#[component]
pub fn LogPanel(process_id: Signal<Option<String>>, visible: Signal<bool>) -> impl IntoView {
    let (logs, set_logs) = signal(Vec::<LogEntry>::new());
    let (ws_state, set_ws_state) = signal(WebSocketState::Disconnected);
    let (connected_process_id, set_connected_process_id) = signal(None::<String>);
    let (auto_scroll, set_auto_scroll) = signal(true);
    let (error_badge_count, set_error_badge_count) = signal(0u32);
    let (toast_message, set_toast_message) = signal(None::<String>);
    let (search_text, set_search_text) = signal(String::new());
    let (node_filter, set_node_filter) = signal(String::new());
    let (level_filter, set_level_filter) = signal(vec![
        "info".to_string(),
        "warn".to_string(),
        "error".to_string(),
        "debug".to_string(),
    ]);

    let ws_ref: Rc<RefCell<Option<LogWebSocket>>> = Rc::new(RefCell::new(None));

    let node_filter_options = Signal::derive(move || {
        let mut ids = BTreeSet::new();
        for log in logs.get() {
            if let Some(node_id) = log.node_id {
                if !node_id.is_empty() {
                    ids.insert(node_id);
                }
            }
        }
        ids.into_iter().collect::<Vec<_>>()
    });

    let filtered_logs = Signal::derive(move || {
        let active_levels = level_filter.get();
        let search_lower = search_text.get().to_lowercase();
        let selected_node_id = node_filter.get();
        logs.get()
            .into_iter()
            .filter(|log| {
                if !active_levels.contains(&log.level.to_lowercase()) {
                    return false;
                }
                if !selected_node_id.is_empty()
                    && log.node_id.as_deref() != Some(selected_node_id.as_str())
                {
                    return false;
                }
                if !search_lower.is_empty() {
                    let message = log.message.to_lowercase();
                    let source = log.source.to_lowercase();
                    let node_id = log.node_id.as_deref().unwrap_or_default().to_lowercase();
                    if !message.contains(&search_lower)
                        && !source.contains(&search_lower)
                        && !node_id.contains(&search_lower)
                    {
                        return false;
                    }
                }
                true
            })
            .collect::<Vec<_>>()
    });
    let is_error_only = Signal::derive(move || {
        let levels = level_filter.get();
        levels.len() == 1 && levels[0] == "error"
    });

    let disconnect_ws = Rc::new({
        let ws_ref = Rc::clone(&ws_ref);
        let set_connected_process_id = set_connected_process_id;
        let set_ws_state = set_ws_state;
        move || {
            let mut guard = ws_ref.borrow_mut();
            if let Some(ws) = guard.as_mut() {
                ws.close();
            }
            *guard = None;
            set_connected_process_id.set(None);
            set_ws_state.set(WebSocketState::Disconnected);
        }
    });

    Effect::new({
        let ws_ref = Rc::clone(&ws_ref);
        let disconnect_ws = Rc::clone(&disconnect_ws);
        move |_| {
            let pid = process_id.get();
            let panel_visible = visible.get();

            if !panel_visible {
                (disconnect_ws.as_ref())();
                return;
            }

            let Some(pid) = pid else {
                (disconnect_ws.as_ref())();
                return;
            };

            let same_pid = connected_process_id.get().as_deref() == Some(pid.as_str());
            let already_connected = ws_ref.borrow().is_some();
            if same_pid && already_connected {
                return;
            }

            (disconnect_ws.as_ref())();
            set_ws_state.set(WebSocketState::Connecting);

            let mut ws = LogWebSocket::new();
            match ws.connect(&pid) {
                Ok(()) => {
                    set_connected_process_id.set(Some(pid));

                    let set_logs_on_message = set_logs;
                    let set_error_badge_count_on_message = set_error_badge_count;
                    let set_toast_message_on_message = set_toast_message;
                    ws.set_on_message(move |data| {
                        if let Ok(entry) = serde_json::from_str::<LogEntry>(&data) {
                            let is_error =
                                matches!(entry.level.to_lowercase().as_str(), "error" | "err");
                            let toast_text = if is_error {
                                Some(format!(
                                    "ERROR: {}",
                                    entry.message.chars().take(120).collect::<String>()
                                ))
                            } else {
                                None
                            };

                            set_logs_on_message.update(|items| {
                                items.push(entry);
                                if items.len() > 1000 {
                                    let drop_count = items.len() - 1000;
                                    items.drain(0..drop_count);
                                }
                            });

                            if let Some(text) = toast_text {
                                set_error_badge_count_on_message.update(|count| {
                                    *count = count.saturating_add(1);
                                });
                                set_toast_message_on_message.set(Some(text));
                                let set_toast_message_clear = set_toast_message_on_message;
                                crate::components::setTimeout(
                                    move || set_toast_message_clear.set(None),
                                    3000,
                                );
                            }
                        }
                    });

                    let set_ws_state_open = set_ws_state;
                    ws.set_on_open(move || {
                        set_ws_state_open.set(WebSocketState::Connected);
                    });

                    let set_ws_state_error = set_ws_state;
                    ws.set_on_error(move |err| {
                        log::error!("log websocket error: {}", err);
                        set_ws_state_error.set(WebSocketState::Disconnected);
                    });

                    let set_ws_state_close = set_ws_state;
                    ws.set_on_close(move || {
                        set_ws_state_close.set(WebSocketState::Disconnected);
                    });

                    *ws_ref.borrow_mut() = Some(ws);
                }
                Err(err) => {
                    log::error!("failed to connect log websocket: {}", err);
                    set_ws_state.set(WebSocketState::Disconnected);
                }
            }
        }
    });

    Effect::new(move |_| {
        let enabled = auto_scroll.get();
        let _ = filtered_logs.get();
        if !enabled || !visible.get() {
            return;
        }
        if let Some(document) = web_sys::window().and_then(|w| w.document()) {
            if let Some(el) = document.get_element_by_id("log-list") {
                let height = el.scroll_height();
                el.set_scroll_top(height);
            }
        }
    });

    let clear_logs = move |_| {
        set_logs.set(Vec::new());
        set_error_badge_count.set(0);
    };

    let export_logs = move |_| {
        let content = logs.with(|items| {
            items
                .iter()
                .map(|log| {
                    format!(
                        "[{}] [{}] [{}] {}",
                        log.timestamp, log.level, log.source, log.message
                    )
                })
                .collect::<Vec<_>>()
                .join("\n")
        });

        let Some(window) = web_sys::window() else {
            return;
        };
        let Some(document) = window.document() else {
            return;
        };

        let parts = js_sys::Array::new();
        parts.push(&JsValue::from_str(&content));
        let Ok(blob) = web_sys::Blob::new_with_str_sequence(&parts) else {
            return;
        };
        let Ok(url) = web_sys::Url::create_object_url_with_blob(&blob) else {
            return;
        };

        if let Ok(el) = document.create_element("a") {
            if let Ok(anchor) = el.dyn_into::<web_sys::HtmlAnchorElement>() {
                anchor.set_href(&url);
                anchor.set_download("logs.txt");
                anchor.click();
            }
        }

        let _ = web_sys::Url::revoke_object_url(&url);
    };

    let toggle_level = move |level: &'static str| {
        set_level_filter.update(|levels| {
            if levels.iter().any(|it| it == level) {
                levels.retain(|it| it != level);
            } else {
                levels.push(level.to_string());
            }
        });
    };

    view! {
        <Show when=move || visible.get()>
            <div class="log-panel">
                <div class="log-panel-header">
                    <span class="log-panel-title">"运行日志"</span>
                    <div
                        class="log-panel-controls"
                        style="display: flex; align-items: center; gap: 8px; flex-wrap: nowrap; white-space: nowrap; overflow-x: auto;"
                    >
                        <span
                            class="ws-status"
                            class:ws-connected=move || ws_state.get() == WebSocketState::Connected
                            class:ws-disconnected=move || ws_state.get() == WebSocketState::Disconnected
                        >
                            {move || ws_state.get().display_text()}
                        </span>
                        <Show when=move || error_badge_count.get() != 0>
                            <span class="log-error-badge" title="未读错误日志">
                                {move || format!("{} ERR", error_badge_count.get())}
                            </span>
                        </Show>
                        <button class="btn-icon" title="清空日志" on:click=clear_logs>
                            "清空"
                        </button>
                        <button class="btn-icon" title="导出日志" on:click=export_logs>
                            "导出"
                        </button>
                        <button
                            class="btn-icon"
                            title="切换自动滚动"
                            on:click=move |_| {
                                set_auto_scroll.update(|value| *value = !*value);
                            }
                        >
                            {move || if auto_scroll.get() { "自动滚动: 开" } else { "自动滚动: 关" }}
                        </button>
                    </div>
                </div>

                <div
                    class="log-panel-filters"
                    style="display: flex; align-items: center; gap: 8px; flex-wrap: nowrap; white-space: nowrap; overflow-x: auto;"
                >
                    <button
                        class="filter-btn"
                        class:active=move || is_error_only.get()
                        on:click=move |_| {
                            if is_error_only.get() {
                                set_level_filter.set(vec![
                                    "info".to_string(),
                                    "warn".to_string(),
                                    "error".to_string(),
                                    "debug".to_string(),
                                ]);
                            } else {
                                set_level_filter.set(vec!["error".to_string()]);
                            }
                        }
                        title="快速筛选仅错误日志"
                    >
                        "仅错误"
                    </button>
                    <div
                        class="log-level-filters"
                        style="display: inline-flex; align-items: center; gap: 6px; flex-wrap: nowrap; white-space: nowrap;"
                    >
                        <button
                            class="filter-btn"
                            class:active=move || level_filter.get().contains(&"info".to_string())
                            on:click=move |_| toggle_level("info")
                        >
                            "信息"
                        </button>
                        <button
                            class="filter-btn"
                            class:active=move || level_filter.get().contains(&"warn".to_string())
                            on:click=move |_| toggle_level("warn")
                        >
                            "警告"
                        </button>
                        <button
                            class="filter-btn"
                            class:active=move || level_filter.get().contains(&"error".to_string())
                            on:click=move |_| toggle_level("error")
                        >
                            "错误"
                        </button>
                        <button
                            class="filter-btn"
                            class:active=move || level_filter.get().contains(&"debug".to_string())
                            on:click=move |_| toggle_level("debug")
                        >
                            "调试"
                        </button>
                    </div>
                    <select
                        class="log-node-filter"
                        prop:value=move || node_filter.get()
                        on:change=move |ev| set_node_filter.set(event_target_value(&ev))
                    >
                        <option value="">"全部节点"</option>
                        <For
                            each=move || node_filter_options.get()
                            key=|node_id| node_id.clone()
                            children=move |node_id| {
                                view! {
                                    <option value=node_id.clone()>{node_id.clone()}</option>
                                }
                            }
                        />
                    </select>
                    <input
                        type="text"
                        class="log-search"
                        placeholder="搜索日志..."
                        prop:value=move || search_text.get()
                        on:input=move |ev| set_search_text.set(event_target_value(&ev))
                    />
                </div>

                <div class="log-panel-content">
                    <div class="log-list" id="log-list">
                        <Show
                            when=move || !filtered_logs.get().is_empty()
                            fallback=|| view! { <div class="log-empty">"暂无日志"</div> }
                        >
                            <For
                                each=move || filtered_logs.get()
                                key=|log| {
                                    format!(
                                        "{}:{}:{}:{}",
                                        log.timestamp,
                                        log.source,
                                        log.node_id.clone().unwrap_or_default(),
                                        log.message
                                    )
                                }
                                children=move |log| {
                                    let time = log
                                        .timestamp
                                        .split('T')
                                        .nth(1)
                                        .unwrap_or(&log.timestamp)
                                        .split('.')
                                        .next()
                                        .unwrap_or(&log.timestamp)
                                        .to_string();
                                    let class_name = format!("log-entry {}", log.level_class());
                                    view! {
                                        <div class=class_name>
                                            <span class="log-time">{time}</span>
                                            <span class="log-level-icon" style=format!("color: {}", log.level_color())>
                                                {log.level_icon()}
                                            </span>
                                            <span class="log-source">{log.source}</span>
                                            {log.node_id.clone().map(|node_id| {
                                                view! { <span class="log-node-id">{node_id}</span> }
                                            })}
                                            <span class="log-message">{log.message}</span>
                                        </div>
                                    }
                                }
                            />
                        </Show>
                    </div>
                </div>
            </div>
            <Show when=move || toast_message.get().is_some()>
                <div class="log-toast log-toast-error" role="alert">
                    {move || toast_message.get().unwrap_or_default()}
                </div>
            </Show>
        </Show>
    }
}
