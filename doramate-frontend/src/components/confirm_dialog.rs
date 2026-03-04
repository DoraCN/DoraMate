// 确认对话框组件

use leptos::ev::MouseEvent;
use leptos::prelude::*;

/// 确认对话框类型
#[derive(Clone, Copy, PartialEq, Debug)]
pub enum ConfirmType {
    /// 警告类型（黄色）
    Warning,
    /// 危险类型（红色）
    Danger,
    /// 信息类型（蓝色）
    Info,
}

/// 确认对话框配置
#[derive(Clone, PartialEq)]
pub struct ConfirmConfig {
    /// 对话框标题
    pub title: String,
    /// 确认消息
    pub message: String,
    /// 确认按钮文本
    pub confirm_text: String,
    /// 取消按钮文本
    pub cancel_text: String,
    /// 对话框类型
    pub confirm_type: ConfirmType,
}

impl Default for ConfirmConfig {
    fn default() -> Self {
        Self {
            title: "确认操作".to_string(),
            message: "您确定要执行此操作吗？".to_string(),
            confirm_text: "确认".to_string(),
            cancel_text: "取消".to_string(),
            confirm_type: ConfirmType::Info,
        }
    }
}

impl ConfirmConfig {
    /// 创建警告对话框
    pub fn warning(title: &str, message: &str) -> Self {
        Self {
            title: title.to_string(),
            message: message.to_string(),
            confirm_text: "继续".to_string(),
            cancel_text: "取消".to_string(),
            confirm_type: ConfirmType::Warning,
        }
    }

    /// 创建危险对话框
    pub fn danger(title: &str, message: &str) -> Self {
        Self {
            title: title.to_string(),
            message: message.to_string(),
            confirm_text: "确认删除".to_string(),
            cancel_text: "取消".to_string(),
            confirm_type: ConfirmType::Danger,
        }
    }

    /// 创建信息对话框
    pub fn info(title: &str, message: &str) -> Self {
        Self {
            title: title.to_string(),
            message: message.to_string(),
            confirm_text: "确认".to_string(),
            cancel_text: "取消".to_string(),
            confirm_type: ConfirmType::Info,
        }
    }
}

/// 确认对话框状态
#[derive(Clone, PartialEq)]
pub enum ConfirmState {
    Closed,
    Open(ConfirmConfig),
}

/// 确认对话框组件
#[component]
pub fn ConfirmDialog(
    /// 对话框状态
    state: Signal<ConfirmState>,
    /// 设置对话框状态
    set_state: WriteSignal<ConfirmState>,
    /// 确认回调
    on_confirm: Callback<()>,
    /// 取消回调
    on_cancel: Callback<()>,
) -> impl IntoView {
    // 关闭对话框
    let close_dialog = move |_| {
        set_state.set(ConfirmState::Closed);
    };

    // 确认操作
    let confirm = {
        let on_confirm = on_confirm.clone();
        move |e: MouseEvent| {
            e.stop_propagation();
            on_confirm.run(());
            set_state.set(ConfirmState::Closed);
        }
    };

    // 取消操作
    let cancel = {
        let on_cancel = on_cancel.clone();
        move |e: MouseEvent| {
            e.stop_propagation();
            on_cancel.run(());
            set_state.set(ConfirmState::Closed);
        }
    };

    // 获取当前配置
    let config = Signal::derive(move || match state.get() {
        ConfirmState::Open(ref cfg) => Some(cfg.clone()),
        _ => None,
    });

    // 获取图标和样式
    let get_icon_and_style = move |confirm_type: ConfirmType| -> (&'static str, &'static str) {
        match confirm_type {
            ConfirmType::Warning => ("⚠️", "#FF9800"),
            ConfirmType::Danger => ("⚠️", "#f44336"),
            ConfirmType::Info => ("ℹ️", "#2196F3"),
        }
    };

    view! {
        <div class="confirm-dialog-overlay"
             style:display=move || match state.get() {
                 ConfirmState::Open(_) => "flex",
                 _ => "none"
             }
        >
            <div class="confirm-dialog">
                // 标题栏
                <div class="dialog-header">
                    <div class="dialog-title-row">
                        <span class="dialog-icon" style:color=move || {
                            config.get().as_ref()
                                .map(|c| get_icon_and_style(c.confirm_type).1)
                                .unwrap_or("")
                        }>
                            {move || config.get().map(|c| get_icon_and_style(c.confirm_type).0)}
                        </span>
                        <h2>{move || config.get().map(|c| c.title)}</h2>
                    </div>
                    <button class="close-btn" on:click=close_dialog>"×"</button>
                </div>

                // 对话框内容
                <div class="dialog-content">
                    <p class="confirm-message">
                        {move || config.get().map(|c| c.message)}
                    </p>
                </div>

                // 对话框底部按钮
                <div class="dialog-footer">
                    <button class="btn btn-secondary" on:click=cancel>
                        {move || config.get().map(|c| c.cancel_text)}
                    </button>
                    <button
                        class=move || {
                            let btn_type = config.get()
                                .as_ref()
                                .map(|c| c.confirm_type);
                            match btn_type {
                                Some(ConfirmType::Warning) => "btn btn-warning",
                                Some(ConfirmType::Danger) => "btn btn-danger",
                                Some(ConfirmType::Info) => "btn btn-primary",
                                None => "btn btn-primary",
                            }
                        }
                        on:click=confirm
                    >
                        {move || config.get().map(|c| c.confirm_text)}
                    </button>
                </div>
            </div>
        </div>

        <style>
            r#"
            .confirm-dialog-overlay {
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0, 0, 0, 0.6);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 10000;
                backdrop-filter: blur(2px);
            }

            .confirm-dialog {
                background: #2d2d2d;
                border-radius: 8px;
                box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
                min-width: 400px;
                max-width: 500px;
                animation: dialog-slide-in 0.2s ease-out;
            }

            @keyframes dialog-slide-in {
                from {
                    opacity: 0;
                    transform: translateY(-20px) scale(0.95);
                }
                to {
                    opacity: 1;
                    transform: translateY(0) scale(1);
                }
            }

            .dialog-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                padding: 20px 24px;
                border-bottom: 1px solid #444;
            }

            .dialog-title-row {
                display: flex;
                align-items: center;
                gap: 12px;
            }

            .dialog-icon {
                font-size: 24px;
            }

            .dialog-header h2 {
                margin: 0;
                font-size: 18px;
                font-weight: 600;
                color: #e0e0e0;
            }

            .close-btn {
                background: none;
                border: none;
                color: #999;
                font-size: 24px;
                cursor: pointer;
                padding: 0;
                width: 32px;
                height: 32px;
                display: flex;
                align-items: center;
                justify-content: center;
                border-radius: 4px;
                transition: all 0.2s;
            }

            .close-btn:hover {
                background: #3a3a3a;
                color: #e0e0e0;
            }

            .dialog-content {
                padding: 24px;
            }

            .confirm-message {
                margin: 0;
                font-size: 15px;
                line-height: 1.6;
                color: #b0b0b0;
            }

            .dialog-footer {
                display: flex;
                gap: 12px;
                justify-content: flex-end;
                padding: 16px 24px;
                border-top: 1px solid #444;
            }

            .btn {
                padding: 8px 24px;
                border: none;
                border-radius: 4px;
                font-size: 14px;
                font-weight: 500;
                cursor: pointer;
                transition: all 0.2s;
            }

            .btn-secondary {
                background: #3a3a3a;
                color: #e0e0e0;
            }

            .btn-secondary:hover {
                background: #4a4a4a;
            }

            .btn-primary {
                background: #2196F3;
                color: white;
            }

            .btn-primary:hover {
                background: #1976D2;
            }

            .btn-warning {
                background: #FF9800;
                color: white;
            }

            .btn-warning:hover {
                background: #F57C00;
            }

            .btn-danger {
                background: #f44336;
                color: white;
            }

            .btn-danger:hover {
                background: #d32f2f;
            }
            "#
        </style>
    }
}
