use crate::types::{Dataflow, DoraDataflow};
use leptos::prelude::*;
use wasm_bindgen::prelude::*;
use web_sys::{Blob, BlobPropertyBag, File, FileReader, Url};

/// 从 YAML 文本解析数据流，支持 DORA 与 DoraMate 两种格式。
pub fn parse_yaml_text(text: &str) -> Result<Dataflow, String> {
    match serde_yaml::from_str::<DoraDataflow>(text) {
        Ok(dora_dataflow) => {
            let dataflow: Dataflow = (&dora_dataflow).into();
            log::info!("按 DORA 格式解析成功，节点数: {}", dataflow.nodes.len());
            Ok(dataflow)
        }
        Err(dora_err) => {
            log::warn!("按 DORA 格式解析失败: {}", dora_err);
            match serde_yaml::from_str::<Dataflow>(text) {
                Ok(dataflow) => {
                    log::info!("按 DoraMate 格式解析成功，节点数: {}", dataflow.nodes.len());
                    Ok(dataflow)
                }
                Err(dm_err) => {
                    log::warn!("按 DoraMate 格式解析失败: {}", dm_err);
                    Err("无法识别 YAML 格式，请检查文件内容".to_string())
                }
            }
        }
    }
}

/// 读取用户选择的 YAML 文件并转换为 Dataflow。
pub async fn read_yaml_file(file: File) -> Result<Dataflow, String> {
    log::info!("开始读取文件: {}", file.name());

    let reader = FileReader::new().map_err(|e| format!("创建 FileReader 失败: {:?}", e))?;

    let promise = js_sys::Promise::new(&mut |resolve, _reject| {
        let reader_clone = reader.clone();
        let onload = Closure::once_into_js(move |_: JsValue| {
            let result = reader_clone.result().unwrap();
            let text = result.as_string().unwrap();
            resolve
                .call1(&JsValue::NULL, &JsValue::from_str(&text))
                .unwrap();
        });

        reader.set_onload(Some(onload.as_ref().unchecked_ref()));
        reader.read_as_text(&file).unwrap();
    });

    use wasm_bindgen_futures::JsFuture;
    let text = JsFuture::from(promise)
        .await
        .map_err(|e| format!("读取文件失败: {:?}", e))?
        .as_string()
        .ok_or_else(|| "读取结果不是字符串".to_string())?;

    parse_yaml_text(&text)
}

/// 以浏览器下载方式导出 YAML 文件（DORA 格式）。
pub fn save_yaml_file(dataflow: &Dataflow, filename: &str) {
    let dora_dataflow: DoraDataflow = dataflow.into();
    let yaml = serde_yaml::to_string(&dora_dataflow)
        .unwrap_or_else(|_| "Error: Failed to serialize".to_string());

    let array = js_sys::Array::new();
    array.push(&JsValue::from_str(&yaml));

    let blob_options = BlobPropertyBag::new();
    blob_options.set_type("text/yaml");

    let blob = Blob::new_with_str_sequence_and_options(&array, &blob_options).unwrap();
    let url = Url::create_object_url_with_blob(&blob).unwrap();

    let window = web_sys::window().unwrap();
    let document = window.document().unwrap();
    let a = document.create_element("a").unwrap();
    let anchor = a.dyn_ref::<web_sys::HtmlAnchorElement>().unwrap();

    anchor.set_href(&url);
    anchor.set_download(filename);
    anchor.click();

    web_sys::Url::revoke_object_url(&url).unwrap();
}
