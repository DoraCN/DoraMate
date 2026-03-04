// 文件保存对话框组件

use leptos::ev::Event;
use leptos::prelude::*;
use wasm_bindgen::JsCast;

/// 对话框状态
#[derive(Clone, PartialEq)]
pub enum DialogState {
    Closed,
    Open,
    ConfirmOverwrite(String), // 需要确认覆盖的文件名
    Saving,
    Success(String), // 保存的文件名
    Error(String),   // 错误消息
}

/// 文件保存对话框组件
#[component]
pub fn SaveFileDialog(
    /// 对话框状态
    state: Signal<DialogState>,
    /// 设置对话框状态
    set_state: WriteSignal<DialogState>,
    /// YAML内容
    yaml_content: Signal<String>,
    /// 保存成功回调（返回文件名）
    on_save_success: Callback<String>,
    /// 已保存的文件列表（用于检测覆盖）
    #[prop(optional)]
    saved_files: Signal<Vec<String>>,
) -> impl IntoView {
    // 文件名输入
    let (filename, set_filename) = signal("dataflow.yml".to_string());
    let (error_msg, set_error_msg) = signal(String::new());

    // 验证文件名
    let validate_filename = move |name: String| -> Result<(), String> {
        if name.trim().is_empty() {
            return Err("文件名不能为空".to_string());
        }

        if !name.ends_with(".yml") && !name.ends_with(".yaml") {
            return Err("文件名必须以 .yml 或 .yaml 结尾".to_string());
        }

        // 检查非法字符
        let invalid_chars = ['<', '>', ':', '"', '|', '?', '*'];
        for &ch in &invalid_chars {
            if name.contains(ch) {
                return Err(format!("文件名包含非法字符: {}", ch));
            }
        }

        Ok(())
    };

    // 执行保存（先检查是否需要确认覆盖）
    let save_file = {
        let yaml_content = yaml_content.clone();
        let saved_files = saved_files.clone();
        move |_| {
            let current_filename = filename.get();

            // 验证文件名
            match validate_filename(current_filename.clone()) {
                Ok(_) => {
                    set_error_msg.set(String::new());

                    // 检查文件是否已存在
                    let exists = saved_files.get().contains(&current_filename);
                    if exists {
                        // 显示覆盖确认对话框
                        set_state.set(DialogState::ConfirmOverwrite(current_filename.clone()));
                    } else {
                        // 直接保存
                        execute_save(
                            yaml_content.clone(),
                            current_filename,
                            set_state.clone(),
                            on_save_success.clone(),
                        );
                    }
                }
                Err(e) => {
                    set_error_msg.set(e);
                }
            }
        }
    };

    // 确认覆盖并保存
    let confirm_overwrite = {
        let yaml_content = yaml_content.clone();
        move |filename_to_save: String| {
            execute_save(
                yaml_content.clone(),
                filename_to_save,
                set_state.clone(),
                on_save_success.clone(),
            );
        }
    };

    // 执行保存（使用 trigger_download）
    fn execute_save(
        yaml_content: Signal<String>,
        filename: String,
        set_state: WriteSignal<DialogState>,
        on_save_success: Callback<String>,
    ) {
        set_state.set(DialogState::Saving);

        let yaml = yaml_content.get();
        trigger_download(&yaml, &filename);
        set_state.set(DialogState::Success(filename.clone()));
        on_save_success.run(filename);
    }

    // 触发浏览器下载
    fn trigger_download(yaml: &str, filename: &str) {
        use web_sys::{Blob, BlobPropertyBag, Url};

        // 创建 Blob
        let array = js_sys::Array::new();
        array.push(&wasm_bindgen::JsValue::from_str(yaml));

        let blob_options = BlobPropertyBag::new();
        blob_options.set_type("text/yaml");

        let blob = Blob::new_with_str_sequence_and_options(&array, &blob_options).unwrap();

        // 创建下载链接
        let url = Url::create_object_url_with_blob(&blob).unwrap();

        // 创建临时 <a> 元素并触发点击
        let window = web_sys::window().unwrap();
        let document = window.document().unwrap();
        let a = document.create_element("a").unwrap();
        let anchor = a.dyn_ref::<web_sys::HtmlAnchorElement>().unwrap();

        anchor.set_href(&url);
        anchor.set_download(filename);
        anchor.click();

        // 清理
        Url::revoke_object_url(&url).unwrap();
    }

    view! {
        // 主保存对话框
        <div class="save-dialog-overlay" style=move || {
            if state.get() == DialogState::Open {
                "display: flex; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); align-items: center; justify-content: center; z-index: 1000;"
            } else {
                "display: none;"
            }
        }>
            <div class="save-dialog" style="background: #2d2d2d; border: 1px solid #3e3e3e; border-radius: 8px; padding: 24px; min-width: 400px; box-shadow: 0 4px 20px rgba(0,0,0,0.5);">
                <h2 style="margin: 0 0 16px 0; font-size: 18px; font-weight: 600; color: #e0e0e0;">
                    "保存文件"
                </h2>

                <div style="margin-bottom: 16px;">
                    <label style="display: block; margin-bottom: 8px; font-size: 14px; color: #b0b0b0;">
                        "文件名："
                    </label>
                    <input
                        type="text"
                        id="save-filename-input"
                        value=move || filename.get()
                        on:change=move |e: Event| {
                            let input = e.target().unwrap().unchecked_into::<web_sys::HtmlInputElement>();
                            set_filename.set(input.value());
                        }
                        on:input=move |e: Event| {
                            let input = e.target().unwrap().unchecked_into::<web_sys::HtmlInputElement>();
                            set_filename.set(input.value());
                            // 清除错误消息
                            let _ = validate_filename(input.value()).map(|_| set_error_msg.set(String::new()));
                        }
                        style="width: 100%; padding: 8px 12px; background: #1e1e1e; border: 1px solid #3e3e3e; border-radius: 4px; color: #e0e0e0; font-size: 14px; box-sizing: border-box;"
                        placeholder="dataflow.yml"
                    />
                </div>

                // 错误消息
                <div style=move || {
                    if error_msg.get().is_empty() {
                        "display: none;"
                    } else {
                        "margin-bottom: 16px; padding: 8px 12px; background: rgba(255, 87, 34, 0.1); border: 1px solid rgba(255, 87, 34, 0.3); border-radius: 4px; color: #ff5722; font-size: 13px;"
                    }
                }>
                    {move || error_msg.get()}
                </div>

                // 按钮
                <div style="display: flex; gap: 12px; justify-content: flex-end;">
                    <button
                        on:click=move |_| set_state.set(DialogState::Closed)
                        style="padding: 8px 16px; background: #3e3e3e; border: none; border-radius: 4px; color: #e0e0e0; font-size: 14px; cursor: pointer; transition: background 0.2s;"
                    >
                        "取消"
                    </button>
                    <button
                        on:click=save_file
                        style="padding: 8px 16px; background: #1976d2; border: none; border-radius: 4px; color: #ffffff; font-size: 14px; cursor: pointer; transition: background 0.2s;"
                    >
                        "保存"
                    </button>
                </div>
            </div>
        </div>

        // 覆盖确认对话框
        <div class="confirm-overwrite-dialog-overlay" style=move || {
            if let DialogState::ConfirmOverwrite(_) = state.get() {
                "display: flex; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); align-items: center; justify-content: center; z-index: 1001;"
            } else {
                "display: none;"
            }
        }>
            <div class="confirm-overwrite-dialog" style="background: #2d2d2d; border: 1px solid #ff5722; border-radius: 8px; padding: 24px; min-width: 400px; box-shadow: 0 4px 20px rgba(0,0,0,0.5);">
                <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                    // 警告图标
                    <div style="width: 32px; height: 32px; background: rgba(255, 87, 34, 0.2); border-radius: 50%; display: flex; align-items: center; justify-content: center; flex-shrink: 0;">
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#ff5722" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <circle cx="12" cy="12" r="10"></circle>
                            <line x1="12" y1="8" x2="12" y2="12"></line>
                            <line x1="12" y1="16" x2="12.01" y2="16"></line>
                        </svg>
                    </div>

                    <h2 style="margin: 0; font-size: 18px; font-weight: 600; color: #ff5722;">
                        "文件已存在"
                    </h2>
                </div>

                <p style="margin: 0 0 24px 0; font-size: 14px; color: #b0b0b0; line-height: 1.5;">
                    {move || {
                        if let DialogState::ConfirmOverwrite(ref filename) = state.get() {
                            format!("文件 <span style=\"color: #ff5722; font-family: monospace;\">{}</span> 已存在，是否要覆盖？", filename)
                        } else {
                            String::new()
                        }
                    }}
                </p>

                <div style="display: flex; gap: 12px; justify-content: flex-end;">
                    <button
                        on:click=move |_| set_state.set(DialogState::Open)
                        style="padding: 8px 16px; background: #3e3e3e; border: none; border-radius: 4px; color: #e0e0e0; font-size: 14px; cursor: pointer; transition: background 0.2s;"
                    >
                        "不覆盖"
                    </button>
                    <button
                        on:click=move |_| {
                            if let DialogState::ConfirmOverwrite(filename) = state.get() {
                                confirm_overwrite(filename);
                            }
                        }
                        style="padding: 8px 16px; background: #d32f2f; border: none; border-radius: 4px; color: #ffffff; font-size: 14px; cursor: pointer; transition: background 0.2s;"
                    >
                        "覆盖"
                    </button>
                </div>
            </div>
        </div>

        // 保存中对话框
        <div class="saving-dialog-overlay" style=move || {
            if state.get() == DialogState::Saving {
                "display: flex; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); align-items: center; justify-content: center; z-index: 1000;"
            } else {
                "display: none;"
            }
        }>
            <div class="saving-dialog" style="background: #2d2d2d; border: 1px solid #3e3e3e; border-radius: 8px; padding: 24px; min-width: 300px; box-shadow: 0 4px 20px rgba(0,0,0,0.5); text-align: center;">
                <div style="margin-bottom: 16px; color: #1976d2;">
                    <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="animate-spin">
                        <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                    </svg>
                </div>
                <p style="margin: 0; font-size: 16px; font-weight: 500; color: #e0e0e0;">
                    "正在保存..."
                </p>
            </div>
        </div>

        // 保存成功对话框
        <div class="save-success-dialog-overlay" style=move || {
            if let DialogState::Success(_) = state.get() {
                "display: flex; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); align-items: center; justify-content: center; z-index: 1000;"
            } else {
                "display: none;"
            }
        }>
            <div class="save-success-dialog" style="background: #2d2d2d; border: 1px solid #4caf50; border-radius: 8px; padding: 24px; min-width: 300px; box-shadow: 0 4px 20px rgba(0,0,0,0.5); text-align: center;">
                <div style="margin-bottom: 16px; color: #4caf50;">
                    <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                        <polyline points="22 4 12 14.01 9 11.01"></polyline>
                    </svg>
                </div>
                <p style="margin: 0 0 16px 0; font-size: 16px; font-weight: 500; color: #e0e0e0;">
                    "文件已保存"
                </p>
                <p style="margin: 0 0 24px 0; font-size: 14px; color: #b0b0b0;">
                    {move || {
                        if let DialogState::Success(ref _filename) = state.get() {
                            format!("文件已保存到浏览器的下载文件夹")
                        } else {
                            String::new()
                        }
                    }}
                </p>
                <button
                    on:click=move |_| set_state.set(DialogState::Closed)
                    style="padding: 8px 24px; background: #4caf50; border: none; border-radius: 4px; color: #ffffff; font-size: 14px; cursor: pointer; transition: background 0.2s;"
                >
                    "确定"
                </button>
            </div>
        </div>

        // 错误对话框
        <div class="error-dialog-overlay" style=move || {
            if let DialogState::Error(_) = state.get() {
                "display: flex; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); align-items: center; justify-content: center; z-index: 1000;"
            } else {
                "display: none;"
            }
        }>
            <div class="error-dialog" style="background: #2d2d2d; border: 1px solid #f44336; border-radius: 8px; padding: 24px; min-width: 400px; box-shadow: 0 4px 20px rgba(0,0,0,0.5);">
                <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 16px;">
                    // 错误图标
                    <div style="width: 32px; height: 32px; background: rgba(244, 67, 54, 0.2); border-radius: 50%; display: flex; align-items: center; justify-content: center; flex-shrink: 0;">
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#f44336" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <circle cx="12" cy="12" r="10"></circle>
                            <line x1="15" y1="9" x2="9" y2="15"></line>
                            <line x1="9" y1="9" x2="15" y2="15"></line>
                        </svg>
                    </div>

                    <h2 style="margin: 0; font-size: 18px; font-weight: 600; color: #f44336;">
                        "保存失败"
                    </h2>
                </div>

                <p style="margin: 0 0 24px 0; font-size: 14px; color: #b0b0b0; line-height: 1.5;">
                    {move || {
                        if let DialogState::Error(ref msg) = state.get() {
                            msg.clone()
                        } else {
                            String::new()
                        }
                    }}
                </p>

                <div style="display: flex; justify-content: flex-end;">
                    <button
                        on:click=move |_| set_state.set(DialogState::Closed)
                        style="padding: 8px 16px; background: #3e3e3e; border: none; border-radius: 4px; color: #e0e0e0; font-size: 14px; cursor: pointer; transition: background 0.2s;"
                    >
                        "关闭"
                    </button>
                </div>
            </div>
        </div>
    }
}
