use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

const SHORTCUTS_STORAGE_KEY: &str = "doramate_shortcuts_v1";

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ShortcutAction {
    NewFile,
    OpenFile,
    SaveFile,
    ExportYaml,
    RunStopToggle,
    ToggleLogs,
    Undo,
    Redo,
    Copy,
    Cut,
    Duplicate,
    Paste,
    DeleteSelected,
    SelectAll,
    AutoLayout,
    ClearCanvas,
}

impl ShortcutAction {
    pub const fn all() -> &'static [ShortcutAction] {
        &[
            ShortcutAction::NewFile,
            ShortcutAction::OpenFile,
            ShortcutAction::SaveFile,
            ShortcutAction::ExportYaml,
            ShortcutAction::RunStopToggle,
            ShortcutAction::ToggleLogs,
            ShortcutAction::Undo,
            ShortcutAction::Redo,
            ShortcutAction::Copy,
            ShortcutAction::Cut,
            ShortcutAction::Duplicate,
            ShortcutAction::Paste,
            ShortcutAction::DeleteSelected,
            ShortcutAction::SelectAll,
            ShortcutAction::AutoLayout,
            ShortcutAction::ClearCanvas,
        ]
    }

    pub fn storage_key(self) -> &'static str {
        match self {
            ShortcutAction::NewFile => "new_file",
            ShortcutAction::OpenFile => "open_file",
            ShortcutAction::SaveFile => "save_file",
            ShortcutAction::ExportYaml => "export_yaml",
            ShortcutAction::RunStopToggle => "run_stop_toggle",
            ShortcutAction::ToggleLogs => "toggle_logs",
            ShortcutAction::Undo => "undo",
            ShortcutAction::Redo => "redo",
            ShortcutAction::Copy => "copy",
            ShortcutAction::Cut => "cut",
            ShortcutAction::Duplicate => "duplicate",
            ShortcutAction::Paste => "paste",
            ShortcutAction::DeleteSelected => "delete_selected",
            ShortcutAction::SelectAll => "select_all",
            ShortcutAction::AutoLayout => "auto_layout",
            ShortcutAction::ClearCanvas => "clear_canvas",
        }
    }

    pub fn display_name(self) -> &'static str {
        match self {
            ShortcutAction::NewFile => "新建文件",
            ShortcutAction::OpenFile => "打开文件",
            ShortcutAction::SaveFile => "保存文件",
            ShortcutAction::ExportYaml => "导出 YAML",
            ShortcutAction::RunStopToggle => "运行/停止",
            ShortcutAction::ToggleLogs => "切换日志面板",
            ShortcutAction::Undo => "撤销",
            ShortcutAction::Redo => "重做",
            ShortcutAction::Copy => "复制",
            ShortcutAction::Cut => "剪切",
            ShortcutAction::Duplicate => "复制副本",
            ShortcutAction::Paste => "粘贴",
            ShortcutAction::DeleteSelected => "删除选中",
            ShortcutAction::SelectAll => "全选",
            ShortcutAction::AutoLayout => "自动布局",
            ShortcutAction::ClearCanvas => "清空画布",
        }
    }

    pub fn from_storage_key(key: &str) -> Option<Self> {
        match key {
            "new_file" => Some(ShortcutAction::NewFile),
            "open_file" => Some(ShortcutAction::OpenFile),
            "save_file" => Some(ShortcutAction::SaveFile),
            "export_yaml" => Some(ShortcutAction::ExportYaml),
            "run_stop_toggle" => Some(ShortcutAction::RunStopToggle),
            "toggle_logs" => Some(ShortcutAction::ToggleLogs),
            "undo" => Some(ShortcutAction::Undo),
            "redo" => Some(ShortcutAction::Redo),
            "copy" => Some(ShortcutAction::Copy),
            "cut" => Some(ShortcutAction::Cut),
            "duplicate" => Some(ShortcutAction::Duplicate),
            "paste" => Some(ShortcutAction::Paste),
            "delete_selected" => Some(ShortcutAction::DeleteSelected),
            "select_all" => Some(ShortcutAction::SelectAll),
            "auto_layout" => Some(ShortcutAction::AutoLayout),
            "clear_canvas" => Some(ShortcutAction::ClearCanvas),
            _ => None,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct ShortcutBinding {
    pub key: String,
    #[serde(default)]
    pub ctrl_or_meta: bool,
    #[serde(default)]
    pub shift: bool,
    #[serde(default)]
    pub alt: bool,
}

impl ShortcutBinding {
    pub fn new(key: &str, ctrl_or_meta: bool, shift: bool, alt: bool) -> Self {
        Self {
            key: normalize_key(key),
            ctrl_or_meta,
            shift,
            alt,
        }
    }

    fn matches(&self, event: &KeyEventSpec) -> bool {
        self.key == normalize_key(&event.key)
            && self.ctrl_or_meta == (event.ctrl || event.meta)
            && self.shift == event.shift
            && self.alt == event.alt
    }

    pub fn display_text(&self) -> String {
        let mut parts: Vec<String> = Vec::new();
        if self.ctrl_or_meta {
            parts.push("Ctrl".to_string());
        }
        if self.shift {
            parts.push("Shift".to_string());
        }
        if self.alt {
            parts.push("Alt".to_string());
        }
        let key = display_key(&self.key);
        if !key.is_empty() {
            parts.push(key);
        }
        parts.join("+")
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct KeyEventSpec {
    pub key: String,
    pub ctrl: bool,
    pub meta: bool,
    pub shift: bool,
    pub alt: bool,
}

impl KeyEventSpec {
    pub fn new(key: &str, ctrl: bool, meta: bool, shift: bool, alt: bool) -> Self {
        Self {
            key: normalize_key(key),
            ctrl,
            meta,
            shift,
            alt,
        }
    }

    pub fn from_keyboard_event(event: &web_sys::KeyboardEvent) -> Self {
        Self {
            key: normalize_key(&event.key()),
            ctrl: event.ctrl_key(),
            meta: event.meta_key(),
            shift: event.shift_key(),
            alt: event.alt_key(),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ShortcutConflict {
    pub binding: ShortcutBinding,
    pub actions: Vec<ShortcutAction>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ShortcutConfig {
    bindings: BTreeMap<ShortcutAction, Vec<ShortcutBinding>>,
}

impl ShortcutConfig {
    pub fn bindings_for(&self, action: ShortcutAction) -> &[ShortcutBinding] {
        self.bindings.get(&action).map(Vec::as_slice).unwrap_or(&[])
    }

    pub fn primary_binding(&self, action: ShortcutAction) -> Option<ShortcutBinding> {
        self.bindings_for(action).first().cloned()
    }

    pub fn set_primary_binding(&mut self, action: ShortcutAction, binding: ShortcutBinding) {
        let normalized = ShortcutBinding::new(
            &binding.key,
            binding.ctrl_or_meta,
            binding.shift,
            binding.alt,
        );
        let entry = self.bindings.entry(action).or_default();
        if entry.is_empty() {
            entry.push(normalized);
        } else {
            entry[0] = normalized;
        }
    }

    pub fn find_conflicts(&self) -> Vec<ShortcutConflict> {
        let mut by_binding = BTreeMap::<(String, bool, bool, bool), Vec<ShortcutAction>>::new();
        for action in ShortcutAction::all() {
            let Some(bindings) = self.bindings.get(action) else {
                continue;
            };
            for binding in bindings {
                if binding.key.is_empty() {
                    continue;
                }
                by_binding
                    .entry((
                        binding.key.clone(),
                        binding.ctrl_or_meta,
                        binding.shift,
                        binding.alt,
                    ))
                    .or_default()
                    .push(*action);
            }
        }

        let mut conflicts = Vec::new();
        for ((key, ctrl_or_meta, shift, alt), actions) in by_binding {
            if actions.len() <= 1 {
                continue;
            }
            conflicts.push(ShortcutConflict {
                binding: ShortcutBinding::new(&key, ctrl_or_meta, shift, alt),
                actions,
            });
        }
        conflicts
    }

    pub fn action_for_event(&self, event: &KeyEventSpec) -> Option<ShortcutAction> {
        for action in ShortcutAction::all() {
            let Some(candidates) = self.bindings.get(action) else {
                continue;
            };
            if candidates.iter().any(|binding| binding.matches(event)) {
                return Some(*action);
            }
        }
        None
    }

    pub fn primary_hint(&self, action: ShortcutAction) -> String {
        self.bindings
            .get(&action)
            .and_then(|list| list.first())
            .map(|binding| binding.display_text())
            .unwrap_or_default()
    }

    fn merge_override(&mut self, action: ShortcutAction, list: Vec<ShortcutBinding>) {
        if list.is_empty() {
            return;
        }
        self.bindings.insert(action, list);
    }

    fn to_payload(&self) -> ShortcutConfigPayload {
        let mut bindings = BTreeMap::<String, Vec<ShortcutBinding>>::new();
        for (action, value) in &self.bindings {
            bindings.insert(action.storage_key().to_string(), value.clone());
        }
        ShortcutConfigPayload { bindings }
    }

    fn apply_payload(&mut self, payload: ShortcutConfigPayload) {
        for (action_key, list) in payload.bindings {
            if let Some(action) = ShortcutAction::from_storage_key(&action_key) {
                self.merge_override(action, list);
            }
        }
    }
}

impl Default for ShortcutConfig {
    fn default() -> Self {
        let mut bindings = BTreeMap::<ShortcutAction, Vec<ShortcutBinding>>::new();
        bindings.insert(
            ShortcutAction::NewFile,
            vec![ShortcutBinding::new("n", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::OpenFile,
            vec![ShortcutBinding::new("o", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::SaveFile,
            vec![ShortcutBinding::new("s", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::ExportYaml,
            vec![ShortcutBinding::new("e", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::RunStopToggle,
            vec![ShortcutBinding::new("r", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::ToggleLogs,
            vec![ShortcutBinding::new("l", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::Undo,
            vec![ShortcutBinding::new("z", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::Redo,
            vec![
                ShortcutBinding::new("y", true, false, false),
                ShortcutBinding::new("z", true, true, false),
            ],
        );
        bindings.insert(
            ShortcutAction::Copy,
            vec![ShortcutBinding::new("c", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::Cut,
            vec![ShortcutBinding::new("x", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::Duplicate,
            vec![ShortcutBinding::new("d", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::Paste,
            vec![ShortcutBinding::new("v", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::DeleteSelected,
            vec![ShortcutBinding::new("delete", false, false, false)],
        );
        bindings.insert(
            ShortcutAction::SelectAll,
            vec![ShortcutBinding::new("a", true, false, false)],
        );
        bindings.insert(
            ShortcutAction::AutoLayout,
            vec![ShortcutBinding::new("a", true, true, false)],
        );
        bindings.insert(
            ShortcutAction::ClearCanvas,
            vec![ShortcutBinding::new("delete", true, false, false)],
        );
        Self { bindings }
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
struct ShortcutConfigPayload {
    #[serde(default)]
    bindings: BTreeMap<String, Vec<ShortcutBinding>>,
}

pub fn load_shortcut_config() -> ShortcutConfig {
    let mut config = ShortcutConfig::default();
    let Some(window) = web_sys::window() else {
        return config;
    };
    let Ok(Some(storage)) = window.local_storage() else {
        return config;
    };
    let Ok(Some(raw)) = storage.get_item(SHORTCUTS_STORAGE_KEY) else {
        return config;
    };
    let Ok(payload) = serde_json::from_str::<ShortcutConfigPayload>(&raw) else {
        return config;
    };
    config.apply_payload(payload);
    config
}

pub fn save_shortcut_config(config: &ShortcutConfig) -> Result<(), String> {
    let Some(window) = web_sys::window() else {
        return Ok(());
    };
    let Some(storage) = window
        .local_storage()
        .map_err(|err| format!("failed to access local storage: {:?}", err))?
    else {
        return Ok(());
    };

    let payload = config.to_payload();
    let raw = serde_json::to_string(&payload)
        .map_err(|err| format!("failed to serialize shortcut config: {}", err))?;
    storage
        .set_item(SHORTCUTS_STORAGE_KEY, &raw)
        .map_err(|err| format!("failed to persist shortcut config: {:?}", err))
}

pub fn reset_shortcut_config() -> Result<(), String> {
    let Some(window) = web_sys::window() else {
        return Ok(());
    };
    let Some(storage) = window
        .local_storage()
        .map_err(|err| format!("failed to access local storage: {:?}", err))?
    else {
        return Ok(());
    };
    storage
        .remove_item(SHORTCUTS_STORAGE_KEY)
        .map_err(|err| format!("failed to reset shortcut config: {:?}", err))
}

fn normalize_key(key: &str) -> String {
    let lowered = key.trim().to_lowercase();
    match lowered.as_str() {
        "del" => "delete".to_string(),
        "esc" => "escape".to_string(),
        _ => lowered,
    }
}

fn display_key(normalized_key: &str) -> String {
    match normalized_key {
        " " => "Space".to_string(),
        key if key.len() == 1 => key.to_ascii_uppercase(),
        "delete" => "Delete".to_string(),
        "escape" => "Esc".to_string(),
        other => other.to_string(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_config_resolves_core_actions() {
        let cfg = ShortcutConfig::default();

        assert_eq!(
            cfg.action_for_event(&KeyEventSpec::new("s", true, false, false, false)),
            Some(ShortcutAction::SaveFile)
        );
        assert_eq!(
            cfg.action_for_event(&KeyEventSpec::new("a", true, false, true, false)),
            Some(ShortcutAction::AutoLayout)
        );
        assert_eq!(
            cfg.action_for_event(&KeyEventSpec::new("delete", false, false, false, false)),
            Some(ShortcutAction::DeleteSelected)
        );
    }

    #[test]
    fn test_default_config_supports_secondary_redo_binding() {
        let cfg = ShortcutConfig::default();
        assert_eq!(
            cfg.action_for_event(&KeyEventSpec::new("z", true, false, true, false)),
            Some(ShortcutAction::Redo)
        );
    }

    #[test]
    fn test_payload_override_replaces_binding() {
        let mut cfg = ShortcutConfig::default();
        cfg.apply_payload(ShortcutConfigPayload {
            bindings: BTreeMap::from([(
                "save_file".to_string(),
                vec![ShortcutBinding::new("k", true, false, false)],
            )]),
        });

        assert_eq!(
            cfg.action_for_event(&KeyEventSpec::new("k", true, false, false, false)),
            Some(ShortcutAction::SaveFile)
        );
        assert_ne!(
            cfg.action_for_event(&KeyEventSpec::new("s", true, false, false, false)),
            Some(ShortcutAction::SaveFile)
        );
        assert_eq!(cfg.primary_hint(ShortcutAction::SaveFile), "Ctrl+K");
    }

    #[test]
    fn test_set_primary_binding_keeps_secondary_binding() {
        let mut cfg = ShortcutConfig::default();
        cfg.set_primary_binding(
            ShortcutAction::Redo,
            ShortcutBinding::new("k", true, false, false),
        );

        let redo_bindings = cfg.bindings_for(ShortcutAction::Redo);
        assert_eq!(redo_bindings.len(), 2);
        assert_eq!(
            redo_bindings[0],
            ShortcutBinding::new("k", true, false, false)
        );
        assert_eq!(
            redo_bindings[1],
            ShortcutBinding::new("z", true, true, false)
        );
    }

    #[test]
    fn test_find_conflicts_detects_duplicate_binding() {
        let mut cfg = ShortcutConfig::default();
        cfg.set_primary_binding(
            ShortcutAction::OpenFile,
            ShortcutBinding::new("k", true, false, false),
        );
        cfg.set_primary_binding(
            ShortcutAction::SaveFile,
            ShortcutBinding::new("k", true, false, false),
        );

        let conflicts = cfg.find_conflicts();
        assert_eq!(conflicts.len(), 1);
        let conflict = &conflicts[0];
        assert_eq!(
            conflict.binding,
            ShortcutBinding::new("k", true, false, false)
        );
        assert!(conflict.actions.contains(&ShortcutAction::OpenFile));
        assert!(conflict.actions.contains(&ShortcutAction::SaveFile));
    }
}
