use gloo_storage::{LocalStorage, Storage};
use serde::{Deserialize, Serialize};
use wasm_bindgen::JsValue;

/// 最近文件条目
#[derive(Clone, Serialize, Deserialize, Debug)]
pub struct RecentFileEntry {
    pub name: String,
    pub path: String,
    pub last_modified: i64, // Unix 时间戳
}

/// 最近文件管理器
const MAX_RECENT_FILES: usize = 10;
const RECENT_FILES_KEY: &str = "doramate_recent_files";

/// 添加到最近文件列表
pub fn add_recent_file(name: String, path: String) {
    let mut recent_files = get_recent_files();
    let timestamp = js_sys::Date::now() as i64;

    // 移除旧条目（如果存在）
    recent_files.retain(|entry| entry.path != path);

    // 添加新条目到开头
    recent_files.insert(
        0,
        RecentFileEntry {
            name,
            path,
            last_modified: timestamp,
        },
    );

    // 限制数量
    recent_files.truncate(MAX_RECENT_FILES);

    // 保存到本地存储
    let _ = LocalStorage::set(RECENT_FILES_KEY, recent_files);
}

/// 获取最近文件列表
pub fn get_recent_files() -> Vec<RecentFileEntry> {
    LocalStorage::get(RECENT_FILES_KEY).unwrap_or_default()
}

/// 清除最近文件列表
pub fn clear_recent_files() {
    LocalStorage::delete(RECENT_FILES_KEY);
}

/// 从最近文件列表中移除
pub fn remove_recent_file(path: &str) {
    let mut recent_files = get_recent_files();
    recent_files.retain(|entry| entry.path != path);
    let _ = LocalStorage::set(RECENT_FILES_KEY, recent_files);
}

/// 格式化时间戳
pub fn format_timestamp(timestamp: i64) -> String {
    let date = js_sys::Date::new(&JsValue::from_f64(timestamp as f64));
    let now_ms = js_sys::Date::now();
    let now = js_sys::Date::new(&JsValue::from_f64(now_ms));
    let diff_ms = now.get_time() - date.get_time();
    let diff_hours = diff_ms / (1000.0 * 60.0 * 60.0);
    let diff_days = diff_hours / 24.0;

    if diff_days < 1.0 {
        if diff_hours < 1.0 {
            "刚刚".to_string()
        } else {
            format!("{}小时前", diff_hours.floor())
        }
    } else if diff_days < 7.0 {
        format!("{}天前", diff_days.floor())
    } else {
        // 格式化为日期
        let year = date.get_full_year();
        let month = date.get_month() + 1; // 0-indexed
        let day = date.get_date();
        format!("{}-{:02}-{:02}", year, month, day)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_max_recent_files() {
        assert_eq!(MAX_RECENT_FILES, 10);
    }

    #[test]
    fn test_storage_key() {
        assert_eq!(RECENT_FILES_KEY, "doramate_recent_files");
    }
}
