use leptos::prelude::*;

#[component]
pub fn StatusPanel(
    is_running: Signal<bool>,
    uptime: Signal<u64>,
    total_nodes: Signal<usize>,
    running_nodes: Signal<usize>,
    error_nodes: Signal<usize>,
    process_id: Signal<Option<String>>,
    working_dir: Signal<Option<String>>,
    on_set_working_dir: Callback<()>,
) -> impl IntoView {
    let formatted_uptime = Signal::derive(move || {
        let seconds = uptime.get();
        if seconds == 0 {
            return "0s".to_string();
        }

        let hours = seconds / 3600;
        let minutes = (seconds % 3600) / 60;
        let secs = seconds % 60;

        if hours > 0 {
            format!("{}h {}m {}s", hours, minutes, secs)
        } else if minutes > 0 {
            format!("{}m {}s", minutes, secs)
        } else {
            format!("{}s", secs)
        }
    });

    let status_class = Signal::derive(move || {
        if is_running.get() {
            "status-indicator running"
        } else {
            "status-indicator"
        }
    });

    let status_text = Signal::derive(move || {
        if is_running.get() {
            "运行中"
        } else {
            "已停止"
        }
    });

    view! {
        <div class="status-panel">
            <div class="status-header">
                <div class={move || status_class.get()}>
                    <span class="status-dot"></span>
                    <span class="status-text">{move || status_text.get()}</span>
                </div>

                {move || {
                    process_id.get().map(|id| {
                        view! {
                            <div class="process-id" title="进程 ID">
                                "PID: " {id.chars().take(8).collect::<String>()}
                            </div>
                        }
                    })
                }}
            </div>

            <div class="working-dir-section">
                <div class="working-dir-info">
                    <span class="working-dir-label">{"\u{1F4C1} 工作目录:"}</span>
                    <span
                        class="working-dir-path"
                        title={move || working_dir.get().clone().unwrap_or_else(|| "未设置".to_string())}
                    >
                        {move || working_dir.get().clone().unwrap_or_else(|| ".".to_string())}
                    </span>
                </div>
                <button
                    class="working-dir-btn"
                    on:click=move |_| {
                        on_set_working_dir.run(());
                    }
                    title="设置工作目录"
                >
                    "设置"
                </button>
            </div>

            <div class="status-stats">
                <div class="stat-item">
                    <span class="stat-label">运行时间</span>
                    <span class="stat-value">{move || formatted_uptime.get()}</span>
                </div>

                <div class="stat-divider"></div>

                <div class="stat-item">
                    <span class="stat-label">节点</span>
                    <span class="stat-value">
                        {move || running_nodes.get()}/{move || total_nodes.get()}
                    </span>
                </div>

                {move || {
                    let error_count = error_nodes.get();
                    if error_count > 0 {
                        Some(view! {
                            <div class="stat-item stat-error">
                                <span class="stat-label">错误</span>
                                <span class="stat-value">{error_count}</span>
                            </div>
                        })
                    } else {
                        None
                    }
                }}
            </div>

            <div class="status-bar">
                <div
                    class="status-bar-fill"
                    class:running=is_running
                    style:width=move || {
                        if total_nodes.get() > 0 {
                            format!("{}%", (running_nodes.get() as f64 / total_nodes.get() as f64 * 100.0) as i32)
                        } else {
                            "0%".to_string()
                        }
                    }
                ></div>
            </div>
        </div>

        <style>
            r#"
            .status-panel {
                background: #252526;
                border-bottom: 1px solid #3e3e3e;
                padding: 8px 16px;
                display: flex;
                flex-direction: column;
                gap: 8px;
                user-select: none;
            }

            .status-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 12px;
            }

            .status-indicator {
                display: flex;
                align-items: center;
                gap: 8px;
                padding: 4px 12px;
                background: #1e1e1e;
                border-radius: 16px;
                font-size: 13px;
                font-weight: 500;
                transition: all 0.3s ease;
            }

            .status-dot {
                width: 8px;
                height: 8px;
                border-radius: 50%;
                background: #9E9E9E;
                transition: all 0.3s ease;
            }

            .status-indicator.running .status-dot {
                background: #4CAF50;
                animation: pulse 2s ease-in-out infinite;
            }

            .status-indicator.running {
                border: 1px solid #4CAF50;
            }

            .working-dir-section {
                display: flex;
                align-items: center;
                justify-content: space-between;
                padding: 6px 10px;
                background: #1e1e1e;
                border-radius: 6px;
                gap: 10px;
            }

            .working-dir-info {
                display: flex;
                align-items: center;
                gap: 8px;
                flex: 1;
                min-width: 0;
            }

            .working-dir-label {
                font-size: 12px;
                color: #9cdcfe;
                white-space: nowrap;
            }

            .working-dir-path {
                font-size: 12px;
                color: #d4d4d4;
                overflow: hidden;
                text-overflow: ellipsis;
                white-space: nowrap;
                font-family: 'Consolas', 'Monaco', monospace;
            }

            .working-dir-btn {
                padding: 4px 12px;
                background: #0e639c;
                color: white;
                border: none;
                border-radius: 4px;
                font-size: 12px;
                cursor: pointer;
                white-space: nowrap;
                transition: background 0.2s;
            }

            .working-dir-btn:hover {
                background: #1177bb;
            }

            .working-dir-btn:active {
                background: #0d5a8c;
            }

            @keyframes pulse {
                0%, 100% {
                    opacity: 1;
                    box-shadow: 0 0 0 0 rgba(76, 175, 80, 0.4);
                }
                50% {
                    opacity: 0.8;
                    box-shadow: 0 0 0 4px rgba(76, 175, 80, 0);
                }
            }

            .status-text {
                color: #e0e0e0;
            }

            .process-id {
                font-size: 11px;
                color: #888;
                font-family: 'Consolas', 'Monaco', monospace;
                padding: 2px 8px;
                background: #1e1e1e;
                border-radius: 4px;
            }

            .status-stats {
                display: flex;
                align-items: center;
                gap: 16px;
                font-size: 12px;
            }

            .stat-item {
                display: flex;
                flex-direction: column;
                gap: 2px;
            }

            .stat-label {
                color: #888;
                font-size: 11px;
                text-transform: uppercase;
                letter-spacing: 0.5px;
            }

            .stat-value {
                color: #e0e0e0;
                font-weight: 600;
                font-family: 'Consolas', 'Monaco', monospace;
            }

            .stat-error .stat-value {
                color: #f44336;
            }

            .stat-divider {
                width: 1px;
                height: 24px;
                background: #3e3e3e;
            }

            .status-bar {
                width: 100%;
                height: 3px;
                background: #1e1e1e;
                border-radius: 2px;
                overflow: hidden;
            }

            .status-bar-fill {
                height: 100%;
                background: linear-gradient(90deg, #2196F3, #4CAF50);
                width: 0%;
                transition: width 0.5s ease;
            }

            .status-bar-fill.running {
                background: linear-gradient(90deg, #4CAF50, #8BC34A);
            }
            "#
        </style>
    }
}
