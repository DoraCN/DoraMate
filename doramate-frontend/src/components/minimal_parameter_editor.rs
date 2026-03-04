// 最小可工作的参数编辑器 - 基于已验证的 Leptos 0.7 模式
use crate::types::Node;
use leptos::ev::Event;
use leptos::prelude::*;
use serde_yaml::Value;
use wasm_bindgen::JsCast;

/// 最小参数编辑器组件
#[component]
pub fn MinimalParameterEditor(
    /// 当前节点
    node: Node,
    /// 节点列表
    _nodes: Signal<Vec<Node>>,
    /// 设置节点列表
    set_nodes: WriteSignal<Vec<Node>>,
) -> impl IntoView {
    // 提前克隆 node_id，避免所有权问题
    let node_id = node.id.clone();

    // 编辑模式 - ✅ 正确解构 signal
    let (edit_mode, set_edit_mode) = signal(false);

    // 错误提示 - ✅ 正确解构
    let (error_msg, set_error_msg) = signal(None::<String>);

    // 当前配置（作为字符串显示） - ✅ 正确解构
    let (config_text, set_config_text) = signal(
        node.config
            .as_ref()
            .map(|c| {
                // 尝试格式化为更友好的 YAML
                match c {
                    Value::Mapping(_map) => {
                        // 将 Mapping 转换为友好的 YAML 格式
                        let yaml_str = serde_yaml::to_string(c).unwrap_or_default();
                        yaml_str
                    }
                    _ => format!("{:?}", c),
                }
            })
            .unwrap_or_else(|| {
                // 提供示例格式
                "# 示例配置:\n# width: 640\n# height: 480\n# fps: 30\n\n".to_string()
            }),
    );

    view! {
        <div class="minimal-parameter-editor">
            // 头部
            <div class="editor-header">
                <h4>"节点配置"</h4>
                <button
                    class="btn-toggle"
                    on:click=move |_| {
                        set_edit_mode.update(|e| *e = !*e);
                    }
                >
                    {move || if edit_mode.get() { "完成" } else { "编辑" }}
                </button>
            </div>

            // 配置显示
            <div class="editor-content">
                // 只读模式
                <div
                    class="readonly-view"
                    style=move || if !edit_mode.get() { "" } else { "display: none;" }
                >
                    <div class="config-display">
                        <code>
                            {move || config_text.get()}
                        </code>
                    </div>
                </div>

                // 编辑模式
                <div
                    class="edit-view"
                    style=move || if edit_mode.get() { "" } else { "display: none;" }
                >
                    <div class="help-text" style="background: #fff3cd; padding: 8px; margin-bottom: 8px; border-radius: 4px; font-size: 11px; color: #856404; border-left: 3px solid #ffc107;">
                        <div style="display: flex; align-items: center; gap: 8px;">
                            <span style="font-weight: bold; color: #856404;">"Tip: Use YAML format without braces"</span>
                            <span style="color: #6c757d; font-size: 10px;">"(Example: width: 800, height: 600)"</span>
                        </div>
                    </div>

                    <textarea
                        class="config-textarea"
                        rows="10"
                        prop:value=move || config_text.get()
                        on:input=move |e: Event| {
                            let input = e.target().unwrap();
                            let input = input.unchecked_into::<web_sys::HtmlTextAreaElement>();
                            set_config_text.set(input.value());
                            // 清除错误信息
                            set_error_msg.set(None);
                        }
                        placeholder="输入节点配置（YAML格式）"
                        style="width: 100%; font-family: monospace; padding: 10px; border: 1px solid #ddd; border-radius: 4px;"
                    ></textarea>

                    <div class="action-buttons">
                        <button
                            class="btn-save"
                            on:click={
                                let node_id = node_id.clone();  // 在闭包外先克隆
                                move |_| {
                                let text = config_text.get();

                                // 尝试解析 YAML
                                match serde_yaml::from_str::<Value>(&text) {
                                    Ok(value) => {
                                        set_nodes.update(|nodes| {
                                            if let Some(n) = nodes.iter_mut().find(|n| n.id == node_id) {
                                                n.config = Some(value);
                                            }
                                        });
                                        set_error_msg.set(None);
                                        log::info!("✅ 配置保存成功");
                                    }
                                    Err(e) => {
                                        // 提供更友好的错误提示
                                        let error_str = e.to_string();
                                        let friendly_error = if error_str.contains("flow mapping") {
                                            "您使用了类似 JSON 的花括号格式。YAML 不需要花括号，请使用以下格式：\n\nwidth: 800\nheight: 600\nfps: 30".to_string()
                                        } else if error_str.contains("expected ',' or '}'") {
                                            "看起来您在使用 JSON 格式。在 YAML 中，不需要逗号和花括号：\n\n✓ 正确: width: 800\n✗ 错误: { width: 800 }".to_string()
                                        } else {
                                            format!("YAML 格式错误: {}", error_str)
                                        };

                                        log::error!("❌ {}", friendly_error);
                                        set_error_msg.set(Some(friendly_error));
                                    }
                                }
                            }}
                        >
                            "保存"
                        </button>

                        <button
                            class="btn-reset"
                            on:click={
                                let node_id = node_id.clone();  // 在闭包外先克隆
                                move |_| {
                                set_nodes.update(|nodes| {
                                    if let Some(n) = nodes.iter_mut().find(|n| n.id == node_id) {
                                        n.config = None;
                                    }
                                });
                                set_config_text.set("# 示例配置:\n# width: 640\n# height: 480\n# fps: 30\n\n".to_string());
                                set_error_msg.set(None);
                            }}
                        >
                            "重置"
                        </button>
                    </div>

                    // 错误提示
                    <div
                        class="error-message"
                        style=move || match error_msg.get() {
                            Some(_) => "display: block; background: #fee; border: 1px solid #f88; padding: 10px; margin-top: 10px; border-radius: 4px; color: #c00;",
                            None => "display: none;"
                        }
                    >
                        {move || error_msg.get().unwrap_or_default()}
                    </div>
                </div>
            </div>
        </div>
    }
}
