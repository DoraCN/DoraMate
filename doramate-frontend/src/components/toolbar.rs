use crate::utils::recent_files::{format_timestamp, RecentFileEntry};
use crate::utils::shortcuts::{KeyEventSpec, ShortcutAction, ShortcutConfig};
use leptos::ev::KeyboardEvent;
use leptos::prelude::*;
use wasm_bindgen::prelude::*;
use wasm_bindgen::JsCast;

fn is_editable_target(event: &KeyboardEvent) -> bool {
    let Some(target) = event.target() else {
        return false;
    };
    let Ok(element) = target.dyn_into::<web_sys::Element>() else {
        return false;
    };

    let tag = element.tag_name().to_lowercase();
    if matches!(tag.as_str(), "input" | "textarea" | "select") {
        return true;
    }

    element
        .get_attribute("contenteditable")
        .map(|v| v != "false")
        .unwrap_or(false)
}

#[component]
pub fn Toolbar(
    on_new: Callback<()>,
    on_open: Callback<()>,
    on_open_recent: Callback<String>,
    on_save: Callback<()>,
    on_export: Callback<()>,
    on_validate: Callback<()>,
    on_auto_layout: Callback<()>,
    on_run: Callback<()>,
    on_stop: Callback<()>,
    on_undo: Callback<()>,
    on_redo: Callback<()>,
    on_copy: Callback<()>,
    on_cut: Callback<()>,
    on_duplicate: Callback<()>,
    on_paste: Callback<()>,
    on_delete_selected: Callback<()>,
    on_select_all: Callback<()>,
    on_clear: Callback<()>,
    on_toggle_logs: Callback<()>,
    on_open_shortcuts: Callback<()>,
    shortcut_config: Signal<ShortcutConfig>,
    has_unsaved_changes: Signal<bool>,
    is_running: Signal<bool>,
    can_undo: Signal<bool>,
    can_redo: Signal<bool>,
    can_copy: Signal<bool>,
    can_delete_selected: Signal<bool>,
    can_select_all: Signal<bool>,
    can_paste: Signal<bool>,
    can_auto_layout: Signal<bool>,
    loading: Signal<bool>,
    show_log_panel: Signal<bool>,
    recent_files: Signal<Vec<RecentFileEntry>>,
) -> impl IntoView {
    let shortcut_new = Signal::derive({
        let shortcut_config = shortcut_config;
        move || shortcut_config.get().primary_hint(ShortcutAction::NewFile)
    });
    let shortcut_open = Signal::derive({
        let shortcut_config = shortcut_config;
        move || shortcut_config.get().primary_hint(ShortcutAction::OpenFile)
    });
    let shortcut_save = Signal::derive({
        let shortcut_config = shortcut_config;
        move || shortcut_config.get().primary_hint(ShortcutAction::SaveFile)
    });
    let shortcut_export = Signal::derive({
        let shortcut_config = shortcut_config;
        move || {
            shortcut_config
                .get()
                .primary_hint(ShortcutAction::ExportYaml)
        }
    });
    let shortcut_auto_layout = Signal::derive({
        let shortcut_config = shortcut_config;
        move || {
            shortcut_config
                .get()
                .primary_hint(ShortcutAction::AutoLayout)
        }
    });
    let shortcut_undo = Signal::derive({
        let shortcut_config = shortcut_config;
        move || shortcut_config.get().primary_hint(ShortcutAction::Undo)
    });
    let shortcut_redo = Signal::derive({
        let shortcut_config = shortcut_config;
        move || shortcut_config.get().primary_hint(ShortcutAction::Redo)
    });
    let shortcut_copy = Signal::derive({
        let shortcut_config = shortcut_config;
        move || shortcut_config.get().primary_hint(ShortcutAction::Copy)
    });
    let shortcut_paste = Signal::derive({
        let shortcut_config = shortcut_config;
        move || shortcut_config.get().primary_hint(ShortcutAction::Paste)
    });
    let shortcut_delete = Signal::derive({
        let shortcut_config = shortcut_config;
        move || {
            shortcut_config
                .get()
                .primary_hint(ShortcutAction::DeleteSelected)
        }
    });
    let shortcut_clear = Signal::derive({
        let shortcut_config = shortcut_config;
        move || {
            shortcut_config
                .get()
                .primary_hint(ShortcutAction::ClearCanvas)
        }
    });
    let shortcut_logs = Signal::derive({
        let shortcut_config = shortcut_config;
        move || {
            shortcut_config
                .get()
                .primary_hint(ShortcutAction::ToggleLogs)
        }
    });
    let shortcut_run_stop = Signal::derive({
        let shortcut_config = shortcut_config;
        move || {
            shortcut_config
                .get()
                .primary_hint(ShortcutAction::RunStopToggle)
        }
    });
    let shortcut_none = Signal::derive(String::new);

    let on_save_key = on_save.clone();
    let on_open_key = on_open.clone();
    let on_new_key = on_new.clone();
    let on_export_key = on_export.clone();
    let on_auto_layout_key = on_auto_layout.clone();
    let on_run_key = on_run.clone();
    let on_stop_key = on_stop.clone();
    let on_undo_key = on_undo.clone();
    let on_redo_key = on_redo.clone();
    let on_copy_key = on_copy.clone();
    let on_cut_key = on_cut.clone();
    let on_duplicate_key = on_duplicate.clone();
    let on_paste_key = on_paste.clone();
    let on_delete_selected_key = on_delete_selected.clone();
    let on_select_all_key = on_select_all.clone();
    let on_clear_key = on_clear.clone();
    let on_toggle_logs_key = on_toggle_logs.clone();
    let is_running_key = is_running;
    let can_undo_key = can_undo;
    let can_redo_key = can_redo;
    let can_copy_key = can_copy;
    let can_delete_selected_key = can_delete_selected;
    let can_select_all_key = can_select_all;
    let can_paste_key = can_paste;
    let can_auto_layout_key = can_auto_layout;
    let shortcut_config_key = shortcut_config;

    Effect::new(move |_| {
        let shortcut_config_for_listener = shortcut_config_key;
        let listener = Closure::wrap(Box::new(move |e: KeyboardEvent| {
            if is_editable_target(&e) {
                return;
            }

            let key_event = KeyEventSpec::from_keyboard_event(&e);
            let action = shortcut_config_for_listener
                .get()
                .action_for_event(&key_event);
            match action {
                Some(ShortcutAction::SaveFile) => {
                    e.prevent_default();
                    on_save_key.run(());
                }
                Some(ShortcutAction::OpenFile) => {
                    e.prevent_default();
                    on_open_key.run(());
                }
                Some(ShortcutAction::NewFile) => {
                    e.prevent_default();
                    on_new_key.run(());
                }
                Some(ShortcutAction::ExportYaml) => {
                    e.prevent_default();
                    on_export_key.run(());
                }
                Some(ShortcutAction::AutoLayout) => {
                    if can_auto_layout_key.get() {
                        e.prevent_default();
                        on_auto_layout_key.run(());
                    }
                }
                Some(ShortcutAction::SelectAll) => {
                    if can_select_all_key.get() {
                        e.prevent_default();
                        on_select_all_key.run(());
                    }
                }
                Some(ShortcutAction::RunStopToggle) => {
                    e.prevent_default();
                    if is_running_key.get() {
                        on_stop_key.run(());
                    } else {
                        on_run_key.run(());
                    }
                }
                Some(ShortcutAction::ToggleLogs) => {
                    e.prevent_default();
                    on_toggle_logs_key.run(());
                }
                Some(ShortcutAction::Undo) => {
                    if can_undo_key.get() {
                        e.prevent_default();
                        on_undo_key.run(());
                    }
                }
                Some(ShortcutAction::Redo) => {
                    if can_redo_key.get() {
                        e.prevent_default();
                        on_redo_key.run(());
                    }
                }
                Some(ShortcutAction::Copy) => {
                    if can_copy_key.get() {
                        e.prevent_default();
                        on_copy_key.run(());
                    }
                }
                Some(ShortcutAction::Cut) => {
                    if can_copy_key.get() {
                        e.prevent_default();
                        on_cut_key.run(());
                    }
                }
                Some(ShortcutAction::Duplicate) => {
                    if can_copy_key.get() {
                        e.prevent_default();
                        on_duplicate_key.run(());
                    }
                }
                Some(ShortcutAction::Paste) => {
                    if can_paste_key.get() {
                        e.prevent_default();
                        on_paste_key.run(());
                    }
                }
                Some(ShortcutAction::DeleteSelected) => {
                    if can_delete_selected_key.get() {
                        e.prevent_default();
                        on_delete_selected_key.run(());
                    }
                }
                Some(ShortcutAction::ClearCanvas) => {
                    e.prevent_default();
                    on_clear_key.run(());
                }
                None => {}
            }
        }) as Box<dyn FnMut(_)>);

        window()
            .add_event_listener_with_callback("keydown", listener.as_ref().unchecked_ref())
            .unwrap();
        listener.forget();
    });

    let undo_disabled = Signal::derive({
        let loading = loading;
        let can_undo = can_undo;
        move || loading.get() || !can_undo.get()
    });
    let redo_disabled = Signal::derive({
        let loading = loading;
        let can_redo = can_redo;
        move || loading.get() || !can_redo.get()
    });
    let copy_disabled = Signal::derive({
        let loading = loading;
        let can_copy = can_copy;
        move || loading.get() || !can_copy.get()
    });
    let delete_selected_disabled = Signal::derive({
        let loading = loading;
        let can_delete_selected = can_delete_selected;
        move || loading.get() || !can_delete_selected.get()
    });
    let paste_disabled = Signal::derive({
        let loading = loading;
        let can_paste = can_paste;
        move || loading.get() || !can_paste.get()
    });
    let auto_layout_disabled = Signal::derive({
        let loading = loading;
        let can_auto_layout = can_auto_layout;
        move || loading.get() || !can_auto_layout.get()
    });

    view! {
        <div class="toolbar">
            <ToolbarButton icon="+" label="新建" shortcut=shortcut_new on_click=on_new disabled=loading />
            <ToolbarButton icon="O" label="打开" shortcut=shortcut_open on_click=on_open disabled=loading />
            <RecentFilesDropdown recent_files=recent_files on_open_recent=on_open_recent />
            <ToolbarButton
                icon="S"
                label="保存"
                shortcut=shortcut_save
                on_click=on_save
                disabled=loading
                show_unsaved=has_unsaved_changes
            />

            <div class="toolbar-separator"></div>

            <ToolbarButton icon="E" label="导出 YAML" shortcut=shortcut_export on_click=on_export disabled=loading />
            <ToolbarButton icon="V" label="校验" shortcut=shortcut_none on_click=on_validate disabled=loading />
            <ToolbarButton icon="A" label="自动布局" shortcut=shortcut_auto_layout on_click=on_auto_layout disabled=auto_layout_disabled />
            <ToolbarButton icon="U" label="撤销" shortcut=shortcut_undo on_click=on_undo disabled=undo_disabled />
            <ToolbarButton icon="R" label="重做" shortcut=shortcut_redo on_click=on_redo disabled=redo_disabled />
            <ToolbarButton icon="C" label="复制" shortcut=shortcut_copy on_click=on_copy disabled=copy_disabled />
            <ToolbarButton icon="P" label="粘贴" shortcut=shortcut_paste on_click=on_paste disabled=paste_disabled />
            <ToolbarButton icon="D" label="删除" shortcut=shortcut_delete on_click=on_delete_selected disabled=delete_selected_disabled />
            <ToolbarButton
                icon="X"
                label="清空"
                shortcut=shortcut_clear
                on_click=on_clear
                disabled=loading
                confirm=true
                _confirm_message="清空所有节点和连接"
            />
            <ToolbarButton icon="L" label="日志" shortcut=shortcut_logs on_click=on_toggle_logs disabled=loading toggled=show_log_panel />
            <ToolbarButton icon="K" label="快捷键" shortcut=shortcut_none on_click=on_open_shortcuts disabled=loading />

            <div class="toolbar-spacer"></div>

            {move || {
                if is_running.get() {
                    view! { <ToolbarButton icon="[]" label="停止" shortcut=shortcut_run_stop on_click=on_stop variant="danger" disabled=false.into() /> }
                } else {
                    view! { <ToolbarButton icon=">" label="运行" shortcut=shortcut_run_stop on_click=on_run variant="success" disabled=loading /> }
                }
            }}

            <ToolbarStatus is_running=is_running has_unsaved_changes=has_unsaved_changes loading=loading />
        </div>
        <style>{include_str!("toolbar.css")}</style>
    }
}

#[component]
fn RecentFilesDropdown(
    recent_files: Signal<Vec<RecentFileEntry>>,
    on_open_recent: Callback<String>,
) -> impl IntoView {
    let (show_recent_menu, set_show_recent_menu) = signal(false);

    view! {
        <div class="recent-files-dropdown" style="position: relative; z-index: 12000;">
            <button
                r#type="button"
                class="recent-files-button"
                style="display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; cursor: pointer;"
                title="最近文件"
                on:click=move |_| {
                    if !show_recent_menu.get() {
                        set_show_recent_menu.set(true);
                    }
                }
            >
                <span class="button-icon">{"R"}</span>
                <span class="button-label">
                    {move || {
                        if show_recent_menu.get() {
                            format!("最近({})", recent_files.get().len())
                        } else {
                            format!("最近({})", recent_files.get().len())
                        }
                    }}
                </span>
            </button>

            <Show when=move || show_recent_menu.get()>
                <div
                    class="recent-files-menu"
                    style="position: absolute; top: calc(100% + 6px); left: 0; min-width: 320px; max-width: 460px; max-height: 320px; overflow-y: auto; padding: 6px; background: #1f1f20; border: 1px solid #3e3e42; border-radius: 6px; box-shadow: 0 10px 24px rgba(0, 0, 0, 0.45); z-index: 12001;"
                >
                    <div
                        class="recent-files-header"
                        style="padding: 4px 8px 8px; font-size: 12px; color: #9a9a9a; display: flex; justify-content: space-between; align-items: center;"
                    >
                        <span>"最近文件"</span>
                        <button
                            r#type="button"
                            style="padding: 2px 6px; background: transparent; border: 1px solid #4a4a4a; border-radius: 4px; color: #c8c8c8; font-size: 11px; cursor: pointer;"
                            on:click=move |_| {
                                set_show_recent_menu.set(false);
                            }
                        >
                            "关闭"
                        </button>
                    </div>

                    <Show
                        when=move || !recent_files.get().is_empty()
                        fallback=|| view! {
                            <div class="recent-files-empty" style="padding: 10px 8px; font-size: 12px; color: #8a8a8a;">
                                "暂无最近文件"
                            </div>
                        }
                    >
                        <div class="recent-files-list" style="display: flex; flex-direction: column; gap: 4px;">
                            {move || {
                                recent_files
                                    .get()
                                    .into_iter()
                                    .map(|entry| {
                                        let path = entry.path.clone();
                                        let name = entry.name.clone();
                                        let title = entry.path;
                                        let time = format_timestamp(entry.last_modified);
                                        let on_open_recent = on_open_recent.clone();
                                        let set_show_recent_menu = set_show_recent_menu.clone();

                                        view! {
                                            <button
                                                r#type="button"
                                                class="recent-file-item"
                                                style="width: 100%; display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px; background: transparent; border: 1px solid transparent; border-radius: 4px; color: #d6d6d6; cursor: pointer; text-align: left;"
                                                title=title
                                                on:click=move |_| {
                                                    set_show_recent_menu.set(false);
                                                    on_open_recent.run(path.clone());
                                                }
                                            >
                                                <span class="recent-file-name" style="flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px;">
                                                    {name}
                                                </span>
                                                <span class="recent-file-time" style="flex-shrink: 0; font-size: 11px; color: #8f8f94;">
                                                    {time}
                                                </span>
                                            </button>
                                        }
                                    })
                                    .collect_view()
                            }}
                        </div>
                    </Show>
                </div>
            </Show>
        </div>
    }
}

#[component]
fn ToolbarButton(
    icon: &'static str,
    label: &'static str,
    on_click: Callback<()>,
    #[prop(default = false.into())] disabled: Signal<bool>,
    shortcut: Signal<String>,
    #[prop(default = "default")] variant: &'static str,
    #[prop(default = false.into())] show_unsaved: Signal<bool>,
    #[prop(default = false)] confirm: bool,
    #[prop(default = "")] _confirm_message: &'static str,
    #[prop(default = false.into())] toggled: Signal<bool>,
) -> impl IntoView {
    let (show_confirm, set_show_confirm) = signal(false);
    let has_shortcut = Signal::derive({
        let shortcut = shortcut;
        move || !shortcut.get().is_empty()
    });

    let handle_click = move |_| {
        if confirm && !show_confirm.get() {
            set_show_confirm.set(true);
            setTimeout(move || set_show_confirm.set(false), 3000);
        } else {
            on_click.run(());
            set_show_confirm.set(false);
        }
    };

    view! {
        <div
            class="toolbar-button-wrapper"
            class:disabled=move || disabled.get()
            class:confirm-pending=move || show_confirm.get()
            class:has-unsaved=move || show_unsaved.get()
            class:toggled=move || toggled.get()
        >
            <button
                r#type="button"
                class="toolbar-button"
                class:variant=move || variant != "default"
                class:danger=move || variant == "danger"
                class:success=move || variant == "success"
                style="display: inline-flex; align-items: center; gap: 6px; white-space: nowrap;"
                on:click=handle_click
                disabled=move || disabled.get()
                title={move || {
                    let mut title = label.to_string();
                    let shortcut_text = shortcut.get();
                    if !shortcut_text.is_empty() {
                        title.push_str(" (");
                        title.push_str(&shortcut_text);
                        title.push(')');
                    }
                    title
                }}
            >
                <span class="button-icon">{icon}</span>
                <span class="button-label" style="white-space: nowrap;">{label}</span>

                <span class="unsaved-indicator" style=move || if show_unsaved.get() { "" } else { "display: none;" }>
                    {"*"}
                </span>

                <span class="confirm-hint" style=move || if confirm && show_confirm.get() { "" } else { "display: none;" }>
                    "再次点击确认"
                </span>
            </button>

            <span class="shortcut-hint" style=move || if has_shortcut.get() { "" } else { "display: none;" }>
                {move || shortcut.get()}
            </span>
        </div>
    }
}

#[component]
fn ToolbarStatus(
    is_running: Signal<bool>,
    has_unsaved_changes: Signal<bool>,
    loading: Signal<bool>,
) -> impl IntoView {
    view! {
        <div class="toolbar-status">
            <div class="status-indicator running" title="数据流运行中" style=move || if is_running.get() { "" } else { "display: none;" }>
                <span class="status-dot">{"●"}</span>
                "运行中"
            </div>
            <div class="status-indicator stopped" title="数据流已停止" style=move || if is_running.get() { "display: none;" } else { "" }>
                <span class="status-dot">{"○"}</span>
                "已停止"
            </div>
            <div class="status-indicator unsaved" title="有未保存修改" style=move || if has_unsaved_changes.get() { "" } else { "display: none;" }>
                <span class="status-dot">{"●"}</span>
                "未保存"
            </div>
            <div class="status-indicator loading" style=move || if loading.get() { "" } else { "display: none;" }>
                <span class="loading-spinner">{"~"}</span>
                "加载中..."
            </div>
        </div>
    }
}

#[allow(non_snake_case)]
pub fn setTimeout(callback: impl FnOnce() + 'static, duration: i32) {
    let closure = Closure::once(callback);
    window()
        .set_timeout_with_callback_and_timeout_and_arguments_0(
            closure.as_ref().unchecked_ref(),
            duration,
        )
        .unwrap();
    closure.forget();
}
