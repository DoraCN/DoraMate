use crate::types::Dataflow;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

pub const LAYOUT_SIDECAR_SUFFIX: &str = ".layout.json";
const LAYOUT_SIDECAR_VERSION: u32 = 1;
const DEFAULT_YAML_FILE_NAME: &str = "dataflow.yml";

#[derive(Clone, Debug, Serialize, Deserialize)]
struct LayoutSidecarFile {
    #[serde(default = "default_sidecar_version")]
    version: u32,
    #[serde(default)]
    nodes: HashMap<String, LayoutSidecarNode>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
struct LayoutSidecarNode {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    x: Option<f64>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    y: Option<f64>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    label: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    scale: Option<f64>,
}

fn default_sidecar_version() -> u32 {
    LAYOUT_SIDECAR_VERSION
}

pub fn sidecar_path_for_yaml_path(yaml_path: &str) -> String {
    format!("{}{}", yaml_path.trim(), LAYOUT_SIDECAR_SUFFIX)
}

pub fn sidecar_file_name_for_yaml_file_name(yaml_file_name: &str) -> String {
    let trimmed = yaml_file_name.trim();
    if trimmed.is_empty() {
        return format!("{}{}", DEFAULT_YAML_FILE_NAME, LAYOUT_SIDECAR_SUFFIX);
    }
    format!("{}{}", trimmed, LAYOUT_SIDECAR_SUFFIX)
}

pub fn dataflow_to_layout_sidecar_json(dataflow: &Dataflow) -> Result<String, String> {
    let mut nodes = HashMap::with_capacity(dataflow.nodes.len());
    for node in &dataflow.nodes {
        nodes.insert(
            node.id.clone(),
            LayoutSidecarNode {
                x: Some(node.x),
                y: Some(node.y),
                label: Some(node.label.clone()),
                scale: node.scale,
            },
        );
    }

    let payload = LayoutSidecarFile {
        version: LAYOUT_SIDECAR_VERSION,
        nodes,
    };

    serde_json::to_string_pretty(&payload)
        .map_err(|e| format!("serialize layout sidecar failed: {}", e))
}

pub fn apply_layout_sidecar_json(dataflow: &Dataflow, sidecar_json: &str) -> Result<Dataflow, String> {
    let parsed: LayoutSidecarFile = serde_json::from_str(sidecar_json)
        .map_err(|e| format!("parse layout sidecar failed: {}", e))?;

    let mut merged = dataflow.clone();
    for node in &mut merged.nodes {
        let Some(layout) = parsed.nodes.get(&node.id) else {
            continue;
        };

        if let (Some(x), Some(y)) = (layout.x, layout.y) {
            node.x = x;
            node.y = y;
        }
        if let Some(label) = &layout.label {
            node.label = label.clone();
        }
        if let Some(scale) = layout.scale {
            node.scale = Some(scale);
        }
    }

    Ok(merged)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::types::{Connection, Dataflow, Node};

    fn sample_node(id: &str, x: f64, y: f64, label: &str) -> Node {
        Node {
            id: id.to_string(),
            x,
            y,
            label: label.to_string(),
            node_type: "mock".to_string(),
            path: None,
            env: None,
            config: None,
            outputs: None,
            inputs: None,
            scale: Some(1.0),
        }
    }

    fn sample_dataflow() -> Dataflow {
        Dataflow {
            nodes: vec![
                sample_node("camera", 100.0, 200.0, "Camera"),
                sample_node("detector", 420.0, 220.0, "Detector"),
            ],
            connections: vec![Connection {
                from: "camera".to_string(),
                to: "detector".to_string(),
                from_port: None,
                to_port: None,
            }],
        }
    }

    #[test]
    fn test_sidecar_path_for_yaml_path() {
        assert_eq!(
            sidecar_path_for_yaml_path("C:\\tmp\\dataflow.yml"),
            "C:\\tmp\\dataflow.yml.layout.json"
        );
    }

    #[test]
    fn test_sidecar_file_name_for_yaml_file_name() {
        assert_eq!(
            sidecar_file_name_for_yaml_file_name("pipeline.yaml"),
            "pipeline.yaml.layout.json"
        );
        assert_eq!(
            sidecar_file_name_for_yaml_file_name(""),
            "dataflow.yml.layout.json"
        );
    }

    #[test]
    fn test_dataflow_to_layout_sidecar_json_contains_node_positions() {
        let dataflow = sample_dataflow();
        let json = dataflow_to_layout_sidecar_json(&dataflow).expect("serialize sidecar");
        assert!(json.contains("\"version\": 1"));
        assert!(json.contains("\"camera\""));
        assert!(json.contains("\"x\": 100.0"));
        assert!(json.contains("\"y\": 200.0"));
    }

    #[test]
    fn test_apply_layout_sidecar_json_overrides_matching_nodes() {
        let dataflow = sample_dataflow();
        let sidecar = r#"
{
  "version": 1,
  "nodes": {
    "camera": {
      "x": 12.0,
      "y": 34.0,
      "label": "Camera New",
      "scale": 1.25
    }
  }
}
"#;

        let merged = apply_layout_sidecar_json(&dataflow, sidecar).expect("apply sidecar");
        let camera = merged
            .nodes
            .iter()
            .find(|n| n.id == "camera")
            .expect("camera node exists");
        let detector = merged
            .nodes
            .iter()
            .find(|n| n.id == "detector")
            .expect("detector node exists");

        assert_eq!(camera.x, 12.0);
        assert_eq!(camera.y, 34.0);
        assert_eq!(camera.label, "Camera New");
        assert_eq!(camera.scale, Some(1.25));

        assert_eq!(detector.x, 420.0);
        assert_eq!(detector.y, 220.0);
    }

    #[test]
    fn test_apply_layout_sidecar_json_ignores_unknown_nodes() {
        let dataflow = sample_dataflow();
        let sidecar = r#"
{
  "version": 1,
  "nodes": {
    "missing": {
      "x": 1.0,
      "y": 2.0
    }
  }
}
"#;
        let merged = apply_layout_sidecar_json(&dataflow, sidecar).expect("apply sidecar");
        assert_eq!(merged.nodes[0].x, 100.0);
        assert_eq!(merged.nodes[0].y, 200.0);
    }

    #[test]
    fn test_apply_layout_sidecar_json_returns_error_for_invalid_json() {
        let dataflow = sample_dataflow();
        let result = apply_layout_sidecar_json(&dataflow, "{not-json");
        assert!(result.is_err());
    }
}
