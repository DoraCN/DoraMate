use crate::utils::shortcuts::{ShortcutAction, ShortcutBinding, ShortcutConfig};
use leptos::prelude::*;
use wasm_bindgen::JsCast;

fn checked_from_event(event: &web_sys::Event) -> bool {
    event
        .target()
        .and_then(|target| target.dyn_into::<web_sys::HtmlInputElement>().ok())
        .map(|input| input.checked())
        .unwrap_or(false)
}

fn update_binding(
    set_draft_config: WriteSignal<ShortcutConfig>,
    action: ShortcutAction,
    mutator: impl FnOnce(ShortcutBinding) -> ShortcutBinding,
) {
    set_draft_config.update(move |cfg| {
        let current = cfg
            .primary_binding(action)
            .unwrap_or_else(|| ShortcutBinding::new("", false, false, false));
        cfg.set_primary_binding(action, mutator(current));
    });
}

#[component]
pub fn ShortcutSettingsDialog(
    show: Signal<bool>,
    shortcut_config: Signal<ShortcutConfig>,
    error_message: Signal<Option<String>>,
    on_close: Callback<()>,
    on_save: Callback<ShortcutConfig>,
    on_reset: Callback<()>,
) -> impl IntoView {
    let (draft_config, set_draft_config) = signal(shortcut_config.get_untracked());
    let (validation_error, set_validation_error) = signal(None::<String>);

    Effect::new(move |_| {
        if show.get() {
            set_draft_config.set(shortcut_config.get());
            set_validation_error.set(None);
        }
    });

    let conflicts = Signal::derive(move || draft_config.get().find_conflicts());

    let save_draft = {
        let draft_config = draft_config;
        let conflicts = conflicts;
        move |_| {
            let draft = draft_config.get();

            let missing_actions = ShortcutAction::all()
                .iter()
                .copied()
                .filter(|action| {
                    draft
                        .primary_binding(*action)
                        .map(|binding| binding.key.trim().is_empty())
                        .unwrap_or(true)
                })
                .map(|action| action.display_name().to_string())
                .collect::<Vec<_>>();
            if !missing_actions.is_empty() {
                set_validation_error.set(Some(format!(
                    "以下操作未设置按键：{}",
                    missing_actions.join(", ")
                )));
                return;
            }

            if !conflicts.get().is_empty() {
                set_validation_error.set(Some("保存前请先解决快捷键冲突。".to_string()));
                return;
            }

            set_validation_error.set(None);
            on_save.run(draft);
        }
    };

    view! {
        <Show when=move || show.get()>
            <div
                class="shortcut-settings-overlay"
                on:click=move |_| {
                    on_close.run(());
                }
            >
                <div
                    class="shortcut-settings-modal"
                    on:click=move |ev| {
                        ev.stop_propagation();
                    }
                >
                    <div class="shortcut-settings-header">
                        <h3>"快捷键设置"</h3>
                        <button
                            class="shortcut-close-btn"
                            on:click=move |_| {
                                on_close.run(());
                            }
                        >
                            "关闭"
                        </button>
                    </div>

                    <p class="shortcut-settings-help">
                        "提示：在 Windows/Linux 上使用 Ctrl，在 macOS 上会自动映射为 Command。"
                    </p>

                    <div class="shortcut-settings-grid">
                        <div class="shortcut-settings-table">
                            <div class="shortcut-settings-row shortcut-settings-head">
                                <span>"操作"</span>
                                <span>"按键"</span>
                                <span>"Ctrl键"</span>
                                <span>"Shift键"</span>
                                <span>"Alt键"</span>
                                <span>"预览"</span>
                            </div>
                            <For
                                each=move || ShortcutAction::all().to_vec()
                                key=|action| *action
                                children=move |action| {
                                    let set_draft_config_key = set_draft_config;
                                    let set_draft_config_ctrl = set_draft_config;
                                    let set_draft_config_shift = set_draft_config;
                                    let set_draft_config_alt = set_draft_config;
                                    let set_validation_error_key = set_validation_error;
                                    let set_validation_error_ctrl = set_validation_error;
                                    let set_validation_error_shift = set_validation_error;
                                    let set_validation_error_alt = set_validation_error;

                                    view! {
                                        <div class="shortcut-settings-row">
                                            <span class="shortcut-action-name">{action.display_name()}</span>
                                            <span>
                                                <input
                                                    class="shortcut-key-input"
                                                    type="text"
                                                    placeholder="例如：字母 s / delete / f5"
                                                    prop:value=move || {
                                                        draft_config
                                                            .get()
                                                            .primary_binding(action)
                                                            .map(|binding| binding.key)
                                                            .unwrap_or_default()
                                                    }
                                                    on:input=move |ev| {
                                                        let key = event_target_value(&ev);
                                                        update_binding(
                                                            set_draft_config_key,
                                                            action,
                                                            move |current| {
                                                                ShortcutBinding::new(
                                                                    &key,
                                                                    current.ctrl_or_meta,
                                                                    current.shift,
                                                                    current.alt,
                                                                )
                                                            },
                                                        );
                                                        set_validation_error_key.set(None);
                                                    }
                                                />
                                            </span>
                                            <span>
                                                <input
                                                    type="checkbox"
                                                    prop:checked=move || {
                                                        draft_config
                                                            .get()
                                                            .primary_binding(action)
                                                            .map(|binding| binding.ctrl_or_meta)
                                                            .unwrap_or(false)
                                                    }
                                                    on:change=move |ev: web_sys::Event| {
                                                        let checked = checked_from_event(&ev);
                                                        update_binding(
                                                            set_draft_config_ctrl,
                                                            action,
                                                            move |current| {
                                                                ShortcutBinding::new(
                                                                    &current.key,
                                                                    checked,
                                                                    current.shift,
                                                                    current.alt,
                                                                )
                                                            },
                                                        );
                                                        set_validation_error_ctrl.set(None);
                                                    }
                                                />
                                            </span>
                                            <span>
                                                <input
                                                    type="checkbox"
                                                    prop:checked=move || {
                                                        draft_config
                                                            .get()
                                                            .primary_binding(action)
                                                            .map(|binding| binding.shift)
                                                            .unwrap_or(false)
                                                    }
                                                    on:change=move |ev: web_sys::Event| {
                                                        let checked = checked_from_event(&ev);
                                                        update_binding(
                                                            set_draft_config_shift,
                                                            action,
                                                            move |current| {
                                                                ShortcutBinding::new(
                                                                    &current.key,
                                                                    current.ctrl_or_meta,
                                                                    checked,
                                                                    current.alt,
                                                                )
                                                            },
                                                        );
                                                        set_validation_error_shift.set(None);
                                                    }
                                                />
                                            </span>
                                            <span>
                                                <input
                                                    type="checkbox"
                                                    prop:checked=move || {
                                                        draft_config
                                                            .get()
                                                            .primary_binding(action)
                                                            .map(|binding| binding.alt)
                                                            .unwrap_or(false)
                                                    }
                                                    on:change=move |ev: web_sys::Event| {
                                                        let checked = checked_from_event(&ev);
                                                        update_binding(
                                                            set_draft_config_alt,
                                                            action,
                                                            move |current| {
                                                                ShortcutBinding::new(
                                                                    &current.key,
                                                                    current.ctrl_or_meta,
                                                                    current.shift,
                                                                    checked,
                                                                )
                                                            },
                                                        );
                                                        set_validation_error_alt.set(None);
                                                    }
                                                />
                                            </span>
                                            <span class="shortcut-preview">
                                                {move || {
                                                    draft_config
                                                        .get()
                                                        .primary_binding(action)
                                                        .map(|binding| binding.display_text())
                                                        .unwrap_or_else(|| "-".to_string())
                                                }}
                                            </span>
                                        </div>
                                    }
                                }
                            />
                        </div>
                    </div>

                    <Show when=move || !conflicts.get().is_empty()>
                        <div class="shortcut-conflict-box">
                            <div class="shortcut-conflict-title">"检测到快捷键冲突"</div>
                            <ul>
                                {move || {
                                    conflicts
                                        .get()
                                        .into_iter()
                                        .map(|conflict| {
                                            let actions = conflict
                                                .actions
                                                .into_iter()
                                                .map(|action| action.display_name().to_string())
                                                .collect::<Vec<_>>()
                                                .join(", ");
                                            view! {
                                                <li>
                                                    {format!("{} -> {}", conflict.binding.display_text(), actions)}
                                                </li>
                                            }
                                        })
                                        .collect_view()
                                }}
                            </ul>
                        </div>
                    </Show>

                    <Show when=move || validation_error.get().is_some() || error_message.get().is_some()>
                        <div class="shortcut-error-box">
                            {move || {
                                validation_error
                                    .get()
                                    .or_else(|| {
                                        error_message.get().map(|msg| format!("保存失败：{}", msg))
                                    })
                                    .unwrap_or_else(|| "发生未知错误".to_string())
                            }}
                        </div>
                    </Show>

                    <div class="shortcut-settings-footer">
                        <button
                            class="shortcut-btn secondary"
                            on:click=move |_| {
                                on_close.run(());
                            }
                        >
                            "取消"
                        </button>
                        <button
                            class="shortcut-btn warning"
                            on:click=move |_| {
                                set_validation_error.set(None);
                                on_reset.run(());
                            }
                        >
                            "恢复默认"
                        </button>
                        <button class="shortcut-btn primary" on:click=save_draft>
                            "保存"
                        </button>
                    </div>
                </div>
            </div>
            <style>{include_str!("shortcut_settings.css")}</style>
        </Show>
    }
}
