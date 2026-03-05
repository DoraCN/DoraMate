// 属性面板组件 - 用于编辑选中节点的属性
use crate::components::MinimalParameterEditor;
use crate::types::{Connection, Node};
use leptos::prelude::*;
use std::collections::{HashMap, HashSet};

/// ========================================
/// 属性面板主组件
/// ========================================

#[component]
pub fn PropertyPanel(
    /// 当前选中的节点
    selected_node: Signal<Option<Node>>,
    /// 节点列表
    _nodes: Signal<Vec<Node>>,
    /// 设置节点列表
    set_nodes: WriteSignal<Vec<Node>>,
    /// 连接列表
    connections: Signal<Vec<Connection>>,
    /// 设置连接列表
    set_connections: WriteSignal<Vec<Connection>>,
    /// 运行状态（运行中禁止重命名节点 ID）
    is_running: ReadSignal<bool>,
    /// 设置主选中节点 ID
    set_selected_node_id: WriteSignal<Option<String>>,
    /// 设置多选节点 ID 列表
    set_selected_node_ids: WriteSignal<Vec<String>>,
    /// 标记是否有未保存更改
    set_has_unsaved_changes: WriteSignal<bool>,
) -> impl IntoView {
    // 编辑模式：查看 vs 编辑
    let (edit_mode, set_edit_mode) = signal(false);

    // 临时编辑数据
    let (edit_data, set_edit_data) = signal(EditData::default());

    // 验证错误
    let (errors, set_errors) = signal(HashMap::new());

    // 节点 ID 重命名草稿
    let (id_draft, set_id_draft) = signal(String::new());

    // 节点 ID 重命名预览
    let (rename_preview, set_rename_preview) = signal(None::<NodeIdRenamePreview>);

    // 保存加载状态
    let (saving, _set_saving) = signal(false);

    // 记录上一次选中的节点 ID，用于检测节点切换
    let (last_selected_id, set_last_selected_id) = signal(None::<String>);

    // 当选中节点改变时，重置编辑状态
    // 注意：只在节点 ID 改变时才重置，而不是节点属性改变时
    Effect::new(move |_| {
        let current_id = selected_node.get().map(|n| n.id.clone());

        // 检查节点 ID 是否真的改变了
        if current_id != last_selected_id.get() {
            if let Some(node) = selected_node.get() {
                set_edit_data.set(EditData::from_node(&node));
                set_id_draft.set(node.id.clone());
                set_edit_mode.set(false);
                set_errors.set(HashMap::new());
                set_rename_preview.set(None);
                set_last_selected_id.set(current_id);
            } else {
                // 节点被取消选中
                set_id_draft.set(String::new());
                set_rename_preview.set(None);
                set_last_selected_id.set(None);
            }
        }
    });

    // ID 草稿变化后，若与预览目标不一致，清理预览，避免展示过期信息
    Effect::new(move |_| {
        if let Some(preview) = rename_preview.get() {
            if id_draft.get().trim() != preview.new_id {
                set_rename_preview.set(None);
            }
        }
    });

    // 保存修改
    let edit_data_for_action = edit_data.clone();
    let save_changes = Action::new(move |_: &()| {
        let node = selected_node.get().unwrap();
        let data = edit_data_for_action.get();
        let mut errors = HashMap::new();

        // 验证节点标签
        if data.label.trim().is_empty() {
            errors.insert("label".to_string(), "节点标签不能为空".to_string());
        }

        // 验证节点路径
        if data.path.trim().is_empty() {
            errors.insert("path".to_string(), "节点路径不能为空".to_string());
        }

        // 提前计算是否为空，避免移动后使用
        let has_errors = !errors.is_empty();

        if has_errors {
            set_errors.set(errors);
        }

        // ========================================
        // 检测输出端口变更并同步更新下游节点
        // ========================================
        let old_outputs = node.outputs.clone().unwrap_or_default();
        let new_outputs = data.outputs.clone();

        // 收集需要更新的端口变更 (old_name, new_name)
        let port_changes: Vec<(String, String)> = old_outputs
            .iter()
            .zip(new_outputs.iter())
            .filter(|(old, new)| old != new)
            .map(|(old, new)| (old.clone(), new.clone()))
            .collect();

        // 收集需要更新的下游节点信息: (target_node_id, to_port, new_port_name)
        // to_port 是下游节点的输入端口名，需要用新端口名更新它
        let downstream_updates: Vec<(String, String, String)> = connections
            .get()
            .iter()
            .filter(|conn| conn.from == node.id)
            .filter_map(|conn| {
                // 查找此连接的输出端口是否有变更
                let from_port = conn.from_port.clone().unwrap_or_else(|| "out".to_string());
                let to_port = conn.to_port.clone().unwrap_or_else(|| "in".to_string());
                for (old_name, new_name) in &port_changes {
                    if &from_port == old_name {
                        return Some((conn.to.clone(), to_port, new_name.clone()));
                    }
                }
                None
            })
            .collect();

        // 更新连接的 from_port
        for (old_name, new_name) in &port_changes {
            set_connections.update(|conns| {
                for conn in conns.iter_mut() {
                    if conn.from == node.id && conn.from_port.as_ref() == Some(old_name) {
                        conn.from_port = Some(new_name.clone());
                        log::info!(
                            "🔄 更新连接: {} -> {} 的端口从 {} 改为 {}",
                            node.id,
                            conn.to,
                            old_name,
                            new_name
                        );
                    }
                }
            });
        }

        // 更新下游节点的输入
        // 输入格式为 "节点ID/端口名"，需要更新为新格式
        for (target_node_id, to_port, new_port_name) in &downstream_updates {
            let new_source = format!("{}/{}", node.id, new_port_name);
            let source_prefix = format!("{}/", node.id);
            set_nodes.update(|nodes| {
                if let Some(target) = nodes.iter_mut().find(|n| n.id == *target_node_id) {
                    if let Some(ref mut inputs) = target.inputs {
                        for input in inputs.iter_mut() {
                            let trimmed = input.trim();
                            let (input_port, input_source, has_mapping) =
                                if let Some((port_name, source)) = trimmed.split_once(':') {
                                    (port_name.trim(), source.trim(), true)
                                } else {
                                    ("in", trimmed, false)
                                };

                            if input_port == to_port && input_source.starts_with(&source_prefix) {
                                let old_value = input.clone();
                                *input = if has_mapping {
                                    format!("{}: {}", input_port, new_source)
                                } else if to_port == "in" {
                                    new_source.clone()
                                } else {
                                    format!("{}: {}", to_port, new_source)
                                };
                                log::info!(
                                    "🔄 sync downstream input {}: {} -> {}",
                                    target_node_id,
                                    old_value,
                                    input
                                );
                                break;
                            }
                        }
                    }
                }
            });
        }

        // 更新当前节点
        set_nodes.update(|nodes| {
            if let Some(n) = nodes.iter_mut().find(|n| n.id == node.id) {
                n.label = data.label.clone();
                n.node_type = data.node_type.clone();
                n.path = Some(data.path.clone());

                // 更新环境变量
                if !data.env.is_empty() {
                    n.env = Some(data.env.clone());
                } else {
                    n.env = None;
                }

                // 注意：config 由 MinimalParameterEditor 管理，这里不再处理
                // 避免与 MinimalParameterEditor 的配置管理冲突

                // 更新端口
                if !data.inputs.is_empty() {
                    n.inputs = Some(data.inputs.clone());
                } else {
                    n.inputs = None;
                }

                if !data.outputs.is_empty() {
                    n.outputs = Some(data.outputs.clone());
                } else {
                    n.outputs = None;
                }
            }
        });

        // 显示同步更新提示
        if !downstream_updates.is_empty() {
            log::info!(
                "✅ 已同步更新 {} 个下游节点的输入",
                downstream_updates.len()
            );
        }

        set_has_unsaved_changes.set(true);
        set_edit_mode.set(false);

        // 提前计算结果,避免async block类型不匹配
        let success = !has_errors;
        async move { success }
    });

    // 取消编辑
    let cancel_edit = move |_| {
        if let Some(node) = selected_node.get() {
            set_edit_data.set(EditData::from_node(&node));
            set_id_draft.set(node.id.clone());
            set_edit_mode.set(false);
            set_errors.set(HashMap::new());
            set_rename_preview.set(None);
        }
    };

    let request_rename_node_id = Callback::new({
        let selected_node = selected_node;
        let nodes = _nodes;
        let connections = connections;
        let is_running = is_running;
        let id_draft = id_draft;
        let set_errors = set_errors.clone();
        let set_rename_preview = set_rename_preview.clone();
        move |()| {
            set_errors.update(|errs| {
                errs.remove("id");
            });
            set_rename_preview.set(None);

            if is_running.get_untracked() {
                set_errors.update(|errs| {
                    errs.insert(
                        "id".to_string(),
                        "运行中禁止重命名节点 ID，请先停止运行。".to_string(),
                    );
                });
                return;
            }

            let Some(node) = selected_node.get_untracked() else {
                return;
            };

            let old_id = node.id;
            let new_id = id_draft.get_untracked().trim().to_string();
            let all_nodes = nodes.get_untracked();

            if new_id == old_id {
                set_errors.update(|errs| {
                    errs.insert("id".to_string(), "节点 ID 未变化。".to_string());
                });
                return;
            }

            if let Some(err) = validate_node_id_candidate(&new_id, &old_id, &all_nodes) {
                set_errors.update(|errs| {
                    errs.insert("id".to_string(), err);
                });
                return;
            }

            let preview = build_node_id_rename_preview(
                &all_nodes,
                &connections.get_untracked(),
                &old_id,
                &new_id,
            );
            set_rename_preview.set(Some(preview));
        }
    });

    let confirm_rename_node_id = Callback::new({
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_errors = set_errors.clone();
        let set_id_draft = set_id_draft.clone();
        let rename_preview = rename_preview;
        let set_rename_preview = set_rename_preview.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        move |()| {
            let Some(preview) = rename_preview.get_untracked() else {
                return;
            };

            let old_id = preview.old_id.clone();
            let new_id = preview.new_id.clone();

            set_nodes.update(|nodes| {
                for node in nodes.iter_mut() {
                    if node.id == old_id {
                        node.id = new_id.clone();
                    }

                    if let Some(inputs) = node.inputs.as_mut() {
                        for input in inputs.iter_mut() {
                            if let Some(updated) =
                                rewrite_input_reference(input, &old_id, &new_id)
                            {
                                *input = updated;
                            }
                        }
                    }
                }
            });

            set_connections.update(|conns| {
                for conn in conns.iter_mut() {
                    if conn.from == old_id {
                        conn.from = new_id.clone();
                    }
                    if conn.to == old_id {
                        conn.to = new_id.clone();
                    }
                }
            });

            set_selected_node_id.update(|selected| {
                if selected.as_ref() == Some(&old_id) {
                    *selected = Some(new_id.clone());
                }
            });

            set_selected_node_ids.update(|selected_ids| {
                for id in selected_ids.iter_mut() {
                    if *id == old_id {
                        *id = new_id.clone();
                    }
                }
            });

            set_id_draft.set(new_id.clone());
            set_rename_preview.set(None);
            set_errors.update(|errs| {
                errs.remove("id");
            });
            set_has_unsaved_changes.set(true);

            log::info!("node id renamed: {} -> {}", old_id, new_id);
        }
    });

    let cancel_rename_preview = Callback::new({
        let set_rename_preview = set_rename_preview.clone();
        move |()| set_rename_preview.set(None)
    });

    // 重置为默认值
    let reset_to_default = Action::new(move |_: &()| {
        let node = selected_node.get().unwrap();
        let set_has_unsaved_changes = set_has_unsaved_changes;
        set_nodes.update(|nodes| {
            if let Some(n) = nodes.iter_mut().find(|n| n.id == node.id) {
                // 重置为初始状态
                n.env = None;
                n.config = None;
                n.inputs = None;
                n.outputs = None;
                n.label = n.node_type.clone();
            }
        });
        set_has_unsaved_changes.set(true);

        async move { true }
    });

    view! {
        <div class="property-panel">
            {move || {
                selected_node.get().map(|node| {
                    view! {
                        <div class="panel-header">
                            <h3>"节点属性"</h3>
                            <div class="header-actions">
                                <button
                                    class="btn-save"
                                    class:edit-mode=move || edit_mode.get()
                                    disabled=move || saving.get()
                                    on:click=move |_| { let _ = save_changes.dispatch(()); }
                                    style=move || if edit_mode.get() { "" } else { "display: none;" }
                                >
                                    {move || if saving.get() { "保存中..." } else { "保存" } }
                                </button>
                                <button
                                    class="btn-cancel"
                                    on:click=cancel_edit
                                    style=move || if edit_mode.get() { "" } else { "display: none;" }
                                >
                                    "取消"
                                </button>
                                <button
                                    class="btn-edit"
                                    on:click=move |_| set_edit_mode.set(true)
                                    style=move || if edit_mode.get() { "display: none;" } else { "" }
                                >
                                    "编辑"
                                </button>
                                <button
                                    class="btn-reset"
                                    on:click=move |_| { let _ = reset_to_default.dispatch(()); }
                                    style=move || if edit_mode.get() { "display: none;" } else { "" }
                                >
                                    "重置"
                                </button>
                            </div>
                        </div>

                        <div class="panel-content">
                            // 基本属性
                            <BasicProperties
                                node=node.clone()
                                edit_data=edit_data.clone()
                                set_edit_data
                                edit_mode=edit_mode.clone()
                                errors=errors.clone()
                                id_draft=id_draft
                                set_id_draft
                                rename_preview=rename_preview
                                is_running=is_running
                                on_request_rename_node_id=request_rename_node_id
                                on_confirm_rename_node_id=confirm_rename_node_id
                                on_cancel_rename_preview=cancel_rename_preview
                            />

                            // 环境变量
                            <EnvVariables
                                _node=node.clone()
                                edit_data=edit_data.clone()
                                set_edit_data
                                edit_mode=edit_mode.clone()
                            />

                            // 端口配置
                            <PortConfiguration
                                _node=node.clone()
                                edit_data=edit_data.clone()
                                set_edit_data
                                edit_mode=edit_mode.clone()
                            />

                            // ⭐ 高级配置 - 使用 MinimalParameterEditor
                            <MinimalParameterEditor
                                node=node
                                _nodes=_nodes
                                set_nodes=set_nodes
                            />
                        </div>
                    }
                })
            }}

            // 未选中节点时显示提示
            <div
                class="empty-state"
                style=move || if selected_node.get().is_none() { "" } else { "display: none;" }
            >
                <div class="empty-icon">{ "\u{1f4dd}" }</div>
                <p>"请选择一个节点以查看和编辑属性"</p>
            </div>
        </div>
    }
}

/// ========================================
/// 基本属性部分
/// ========================================

#[component]
fn BasicProperties(
    node: Node,
    edit_data: ReadSignal<EditData>,
    set_edit_data: WriteSignal<EditData>,
    edit_mode: ReadSignal<bool>,
    errors: ReadSignal<HashMap<String, String>>,
    id_draft: ReadSignal<String>,
    set_id_draft: WriteSignal<String>,
    rename_preview: ReadSignal<Option<NodeIdRenamePreview>>,
    is_running: ReadSignal<bool>,
    on_request_rename_node_id: Callback<()>,
    on_confirm_rename_node_id: Callback<()>,
    on_cancel_rename_preview: Callback<()>,
) -> impl IntoView {
    view! {
        <div class="property-section">
            <h4>"基本信息"</h4>

            <div class="property-row">
                <label>"节点 ID"</label>
                <div class="input-wrapper">
                    <div
                        class="id-edit-row"
                        style=move || if edit_mode.get() { "" } else { "display: none;" }
                    >
                        <input
                            type="text"
                            class:property-input=true
                            class:error=move || errors.with(|e| e.get("id").is_some())
                            value=move || id_draft.get()
                            disabled=move || is_running.get()
                            on:input=move |e| {
                                set_id_draft.set(event_target_value(&e));
                            }
                        />
                        <button
                            class="btn-rename"
                            on:click=move |_| on_request_rename_node_id.run(())
                            disabled=move || is_running.get()
                        >
                            "重命名"
                        </button>
                    </div>

                    <div
                        class="readonly"
                        style=move || if edit_mode.get() { "display: none;" } else { "" }
                    >
                        {node.id.clone()}
                    </div>

                    <div
                        class="rename-running-hint"
                        style=move || if edit_mode.get() && is_running.get() { "" } else { "display: none;" }
                    >
                        "运行中禁止重命名节点 ID，请先停止运行。"
                    </div>

                    <div
                        class="error-message"
                        style=move || if errors.with(|e| e.get("id").is_some()) { "" } else { "display: none;" }
                    >
                        {move || errors.with(|e| e.get("id").cloned().unwrap_or_default())}
                    </div>
                </div>
            </div>

            <div
                class="rename-preview"
                style=move || {
                    if edit_mode.get() && rename_preview.get().is_some() {
                        ""
                    } else {
                        "display: none;"
                    }
                }
            >
                <div class="rename-preview-title">"重命名影响预览"</div>
                <div class="rename-preview-line">
                    {move || {
                        rename_preview
                            .get()
                            .map(|p| format!("{} -> {}", p.old_id, p.new_id))
                            .unwrap_or_default()
                    }}
                </div>
                <div class="rename-preview-line">
                    {move || {
                        rename_preview
                            .get()
                            .map(|p| {
                                format!(
                                    "连接更新：源 {} 条，目标 {} 条",
                                    p.source_connection_updates, p.target_connection_updates
                                )
                            })
                            .unwrap_or_default()
                    }}
                </div>
                <div class="rename-preview-line">
                    {move || {
                        rename_preview
                            .get()
                            .map(|p| {
                                format!(
                                    "输入引用更新：{} 项（{} 个节点）",
                                    p.input_reference_updates, p.input_nodes_touched
                                )
                            })
                            .unwrap_or_default()
                    }}
                </div>
                <div class="rename-preview-actions">
                    <button
                        class="btn-save"
                        on:click=move |_| on_confirm_rename_node_id.run(())
                        disabled=move || is_running.get()
                    >
                        "确认重命名"
                    </button>
                    <button
                        class="btn-cancel"
                        on:click=move |_| on_cancel_rename_preview.run(())
                    >
                        "取消"
                    </button>
                </div>
            </div>

            <div class="property-row">
                <label>"节点类型"</label>
                {move || {
                    let node_type_val = node.node_type.clone();
                    let node_type_val_clone = node_type_val.clone();  // Clone for readonly div
                    let edit_data_val = edit_data.get().node_type.clone();
                    let edit_data_val_clone = edit_data_val.clone();  // Clone for value attribute
                    let set_edit_data_clone = set_edit_data.clone();
                    let edit_mode_clone = edit_mode.clone();

                    view! {
                        <div class="property-input-container">
                            <input
                                type="text"
                                class="property-input"
                                value=edit_data_val_clone
                                on:input=move |e| {
                                    set_edit_data_clone.update(|d| d.node_type = event_target_value(&e));
                                }
                                style=move || if edit_mode_clone.get() { "" } else { "display: none;" }
                            />
                            <div
                                class="readonly"
                                style=move || if edit_mode.get() { "display: none;" } else { "" }
                            >
                                {node_type_val_clone}
                            </div>
                        </div>
                    }
                }}
            </div>

            <div class="property-row">
                <label>"节点标签"</label>
                {move || {
                    let value = edit_data.get().label.clone();
                    let value_clone = value.clone();  // Clone for value attribute
                    let has_error = errors.get().contains_key("label");
                    let label_val = node.label.clone();
                    let label_val_clone = label_val.clone();  // Clone for readonly div
                    let set_edit_data_clone = set_edit_data.clone();
                    let errors_clone = errors.clone();
                    let edit_mode_clone = edit_mode.clone();

                    view! {
                        <div class="input-wrapper">
                            <input
                                type="text"
                                class:property-input=true
                                class:error=has_error
                                value=value_clone
                                on:input=move |e| {
                                    set_edit_data_clone.update(|d| d.label = event_target_value(&e));
                                }
                                style=move || if edit_mode_clone.get() { "" } else { "display: none;" }
                            />
                            <div
                                class="readonly"
                                style=move || if edit_mode.get() { "display: none;" } else { "" }
                            >
                                {label_val_clone}
                            </div>
                            <div
                                class="error-message"
                                style=move || if errors_clone.with(|e| e.get("label").is_some()) { "" } else { "display: none;" }
                            >
                                {move || errors_clone.with(|e| e.get("label").cloned().unwrap_or_default())}
                            </div>
                            <div
                                class="no-error"
                                style=move || if errors_clone.with(|e| e.get("label").is_some()) { "display: none;" } else { "" }
                            ></div>
                        </div>
                    }
                }}
            </div>

            <div class="property-row">
                <label>"节点路径"</label>
                {move || {
                    let value = edit_data.get().path.clone();
                    let value_clone = value.clone();  // Clone for value attribute
                    let has_error = errors.get().contains_key("path");
                    let path_val = node.path.clone();
                    let path_val_clone = path_val.clone();  // Clone for readonly div
                    let set_edit_data_clone = set_edit_data.clone();
                    let errors_clone = errors.clone();
                    let edit_mode_clone = edit_mode.clone();

                    view! {
                        <div class="input-wrapper">
                            <input
                                type="text"
                                class:property-input=true
                                class:error=has_error
                                value=value_clone
                                on:input=move |e| {
                                    set_edit_data_clone.update(|d| d.path = event_target_value(&e));
                                }
                                style=move || if edit_mode_clone.get() { "" } else { "display: none;" }
                            />
                            <div
                                class="readonly path-value"
                                style=move || if edit_mode.get() { "display: none;" } else { "" }
                            >
                                {path_val_clone}
                            </div>
                            <div
                                class="error-message"
                                style=move || if errors_clone.with(|e| e.get("path").is_some()) { "" } else { "display: none;" }
                            >
                                {move || errors_clone.with(|e| e.get("path").cloned().unwrap_or_default())}
                            </div>
                            <div
                                class="no-error-msg"
                                style=move || if errors_clone.with(|e| e.get("path").is_some()) { "display: none;" } else { "" }
                            ></div>
                        </div>
                    }
                }}
            </div>
        </div>
    }
}

/// ========================================
/// 环境变量部分
/// ========================================

#[component]
fn EnvVariables(
    _node: Node,
    edit_data: ReadSignal<EditData>,
    set_edit_data: WriteSignal<EditData>,
    edit_mode: ReadSignal<bool>,
) -> impl IntoView {
    // 添加新环境变量
    let add_env_var = move |_| {
        set_edit_data.update(|d| {
            d.env.insert("".to_string(), "".to_string());
        });
    };

    // 删除环境变量
    let remove_env_var = move |key: String| {
        set_edit_data.update(|d| {
            d.env.remove(&key);
        });
    };

    // 更新环境变量键
    let update_env_key = move |old_key: String, new_key: String| {
        set_edit_data.update(|d| {
            if let Some(value) = d.env.remove(&old_key) {
                d.env.insert(new_key, value);
            }
        });
    };

    // 更新环境变量值
    let update_env_value = move |key: String, value: String| {
        set_edit_data.update(|d| {
            d.env.insert(key, value);
        });
    };

    view! {
        <div class="property-section">
            <div class="section-header">
                <h4>"环境变量"</h4>
                <button
                    class="btn-add"
                    on:click=add_env_var
                    style=move || if edit_mode.get() { "" } else { "display: none;" }
                >
                    "+ 添加"
                </button>
            </div>

            {move || {
                let env_vars = edit_data.get().env;
                let is_empty = env_vars.is_empty();
                let is_edit = edit_mode.get();

                view! {
                    <div
                        class="empty-section"
                        style=move || if is_empty && !is_edit { "" } else { "display: none;" }
                    >
                        "未配置环境变量"
                    </div>
                    <div
                        class="env-vars-list"
                        style=move || if is_empty && !is_edit { "display: none;" } else { "" }
                    >
                        {move || {
                            edit_data.get().env.clone().into_iter()
                                .map(|(key, value)| {
                                    let key_clone = key.clone();
                                    let key_for_value = key.clone();  // For input value attribute
                                    let key2 = key.clone();  // For readonly div
                                    let key3 = key.clone();  // For on:input handler
                                    let value_clone = value.clone();
                                    let value_for_value = value.clone();  // For input value attribute

                                    let edit_mode_clone = edit_mode.clone();

                                    view! {
                                        <div class="env-var-row">
                                            <input
                                                type="text"
                                                class="env-key"
                                                placeholder="变量名"
                                                value=key_for_value
                                                on:change=move |e| {
                                                    update_env_key(key3.clone(), event_target_value(&e));
                                                }
                                                style=move || if edit_mode_clone.get() { "" } else { "display: none;" }
                                            />
                                            <div
                                                class="env-key readonly"
                                                style=move || if edit_mode.get() { "display: none;" } else { "" }
                                            >
                                                {key2}
                                            </div>

                                            "="

                                            <input
                                                type="text"
                                                class="env-value"
                                                placeholder="变量值"
                                                value=value_for_value
                                                on:change=move |e| {
                                                    update_env_value(key_clone.clone(), event_target_value(&e));
                                                }
                                                style=move || if edit_mode_clone.get() { "" } else { "display: none;" }
                                            />
                                            <div
                                                class="env-value readonly"
                                                style=move || if edit_mode.get() { "display: none;" } else { "" }
                                            >
                                                {value_clone}
                                            </div>

                                            <button
                                                class="btn-remove"
                                                on:click=move |_| remove_env_var(key.clone())
                                                style=move || if edit_mode.get() { "" } else { "display: none;" }
                                            >
                                                "×"
                                            </button>
                                            <div
                                                style=move || if edit_mode.get() { "display: none;" } else { "" }
                                            ></div>
                                        </div>
                                    }
                                })
                                .collect_view()
                        }}
                    </div>
                }
            }}
        </div>
    }
}

/// ========================================
/// 端口配置部分
/// ========================================

#[component]
fn PortConfiguration(
    _node: Node,
    edit_data: ReadSignal<EditData>,
    set_edit_data: WriteSignal<EditData>,
    edit_mode: ReadSignal<bool>,
) -> impl IntoView {
    // 添加输入端口
    let add_input_port = move |_| {
        set_edit_data.update(|d| {
            d.inputs.push("".to_string());
        });
    };

    // 删除端口
    let remove_port = move |index: usize, is_input: bool| {
        set_edit_data.update(|d| {
            if is_input {
                d.inputs.remove(index);
            } else {
                d.outputs.remove(index);
            }
        });
    };

    // 更新端口
    let update_port = move |index: usize, value: String, is_input: bool| {
        set_edit_data.update(|d| {
            if is_input {
                if let Some(port) = d.inputs.get_mut(index) {
                    *port = value;
                }
            } else {
                if let Some(port) = d.outputs.get_mut(index) {
                    *port = value;
                }
            }
        });
    };

    view! {
        <div class="property-section">
            <div class="section-header">
                <h4>"输入端口"</h4>
                <button
                    class="btn-add"
                    on:click=add_input_port
                    style=move || if edit_mode.get() { "" } else { "display: none;" }
                >
                    "+ 添加"
                </button>
                <div style=move || if edit_mode.get() { "display: none;" } else { "" }></div>
            </div>

            <div
                class="empty-section"
                style=move || if edit_data.get().inputs.is_empty() && !edit_mode.get() { "" } else { "display: none;" }
            >
                "未配置输入端口"
            </div>
            <div
                class="ports-list"
                style=move || if edit_data.get().inputs.is_empty() && !edit_mode.get() { "display: none;" } else { "" }
            >
                {move || {
                    edit_data.get().inputs.clone().into_iter()
                        .enumerate()
                        .map(|(index, port)| {
                            let port_clone = port.clone();
                            let edit_mode_clone = edit_mode.clone();
                            view! {
                                <div class="port-row input-port">
                                    <span class="port-icon">{ "\u{2192}" }</span>
                                    <input
                                        type="text"
                                        class="port-name"
                                        placeholder="端口名"
                                        value=port_clone.clone()
                                        on:input=move |e| {
                                            update_port(index, event_target_value(&e), true);
                                        }
                                        style=move || if edit_mode_clone.get() { "" } else { "display: none;" }
                                    />
                                    <div
                                        class="port-name readonly"
                                        style=move || if edit_mode.get() { "display: none;" } else { "" }
                                    >
                                        {port_clone}
                                    </div>
                                    <button
                                        class="btn-remove"
                                        on:click=move |_| remove_port(index, true)
                                        style=move || if edit_mode.get() { "" } else { "display: none;" }
                                    >
                                        "×"
                                    </button>
                                    <div style=move || if edit_mode.get() { "display: none;" } else { "" }></div>
                                </div>
                            }
                        })
                        .collect_view()
                }}
            </div>

            <div class="section-header">
                <h4>"输出端口"</h4>
                <button
                    class="btn-add"
                    on:click=move |_| set_edit_data.update(|d| d.outputs.push("".to_string()))
                    style=move || if edit_mode.get() { "" } else { "display: none;" }
                >
                    "+ 添加"
                </button>
                <div style=move || if edit_mode.get() { "display: none;" } else { "" }></div>
            </div>

            <div
                class="empty-section"
                style=move || if edit_data.get().outputs.is_empty() && !edit_mode.get() { "" } else { "display: none;" }
            >
                "未配置输出端口"
            </div>
            <div
                class="ports-list"
                style=move || if edit_data.get().outputs.is_empty() && !edit_mode.get() { "display: none;" } else { "" }
            >
                {move || {
                    edit_data.get().outputs.clone().into_iter()
                        .enumerate()
                        .map(|(index, port)| {
                            let port_clone = port.clone();
                            let edit_mode_clone = edit_mode.clone();
                            view! {
                                <div class="port-row output-port">
                                    <span class="port-icon">{ "\u{2190}" }</span>
                                    <input
                                        type="text"
                                        class="port-name"
                                        placeholder="端口名"
                                        value=port_clone.clone()
                                        on:input=move |e| {
                                            update_port(index, event_target_value(&e), false);
                                        }
                                        style=move || if edit_mode_clone.get() { "" } else { "display: none;" }
                                    />
                                    <div
                                        class="port-name readonly"
                                        style=move || if edit_mode.get() { "display: none;" } else { "" }
                                    >
                                        {port_clone}
                                    </div>
                                    <button
                                        class="btn-remove"
                                        on:click=move |_| remove_port(index, false)
                                        style=move || if edit_mode.get() { "" } else { "display: none;" }
                                    >
                                        "×"
                                    </button>
                                    <div style=move || if edit_mode.get() { "display: none;" } else { "" }></div>
                                </div>
                            }
                        })
                        .collect_view()
                }}
            </div>
        </div>
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
struct NodeIdRenamePreview {
    old_id: String,
    new_id: String,
    source_connection_updates: usize,
    target_connection_updates: usize,
    input_reference_updates: usize,
    input_nodes_touched: usize,
}

fn validate_node_id_candidate(new_id: &str, old_id: &str, nodes: &[Node]) -> Option<String> {
    if new_id.trim().is_empty() {
        return Some("节点 ID 不能为空".to_string());
    }
    if new_id.eq_ignore_ascii_case("dora") {
        return Some("节点 ID 不能为保留值 dora".to_string());
    }
    if !new_id
        .chars()
        .all(|ch| ch.is_ascii_alphanumeric() || ch == '_' || ch == '-')
    {
        return Some("节点 ID 仅支持字母、数字、_、-".to_string());
    }
    if nodes
        .iter()
        .any(|node| node.id == new_id && node.id != old_id)
    {
        return Some("节点 ID 已存在，请使用其他值".to_string());
    }
    None
}

fn replace_source_node_id(source: &str, old_id: &str, new_id: &str) -> Option<String> {
    let trimmed = source.trim();
    if trimmed == old_id {
        return Some(new_id.to_string());
    }
    let prefix = format!("{}/", old_id);
    trimmed
        .strip_prefix(prefix.as_str())
        .map(|rest| format!("{}/{}", new_id, rest))
}

fn rewrite_input_reference(input: &str, old_id: &str, new_id: &str) -> Option<String> {
    let trimmed = input.trim();
    if trimmed.is_empty() {
        return None;
    }

    if let Some((port_name, source)) = trimmed.split_once(':') {
        let normalized_port = port_name.trim();
        let normalized_source = source.trim();
        return replace_source_node_id(normalized_source, old_id, new_id)
            .map(|updated_source| format!("{}: {}", normalized_port, updated_source));
    }

    replace_source_node_id(trimmed, old_id, new_id)
}

fn build_node_id_rename_preview(
    nodes: &[Node],
    connections: &[Connection],
    old_id: &str,
    new_id: &str,
) -> NodeIdRenamePreview {
    let source_connection_updates = connections.iter().filter(|conn| conn.from == old_id).count();
    let target_connection_updates = connections.iter().filter(|conn| conn.to == old_id).count();

    let mut input_reference_updates = 0usize;
    let mut touched_nodes = HashSet::<String>::new();

    for node in nodes {
        if let Some(inputs) = node.inputs.as_ref() {
            for input in inputs {
                if rewrite_input_reference(input, old_id, new_id).is_some() {
                    input_reference_updates += 1;
                    touched_nodes.insert(node.id.clone());
                }
            }
        }
    }

    NodeIdRenamePreview {
        old_id: old_id.to_string(),
        new_id: new_id.to_string(),
        source_connection_updates,
        target_connection_updates,
        input_reference_updates,
        input_nodes_touched: touched_nodes.len(),
    }
}

/// ========================================
/// 数据结构
/// ========================================

#[derive(Debug, Clone, Default)]
struct EditData {
    node_type: String,
    label: String,
    path: String,
    env: HashMap<String, String>,
    inputs: Vec<String>,
    outputs: Vec<String>,
    // 注意：config 已移至 MinimalParameterEditor 管理
}

impl EditData {
    fn from_node(node: &Node) -> Self {
        Self {
            node_type: node.node_type.clone(),
            label: node.label.clone(),
            path: node.path.clone().unwrap_or_default(),
            env: node.env.clone().unwrap_or_default(),
            inputs: node.inputs.clone().unwrap_or_default(),
            outputs: node.outputs.clone().unwrap_or_default(),
        }
    }
}
