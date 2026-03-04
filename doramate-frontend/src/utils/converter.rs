use crate::types::{Dataflow, DoraDataflow, DoraMateMeta, DoraNode, LayoutInfo};
use std::collections::HashMap;

/// 将 Dataflow 转换为 YAML 字符串 (仅包含 DORA 兼容的字段)
pub fn dataflow_to_yaml(dataflow: &Dataflow) -> Result<String, String> {
    let dora_dataflow: DoraDataflow = dataflow.into();

    // 创建仅包含 nodes 的简化结构,去除 __doramate__ 元数据
    #[derive(serde::Serialize)]
    struct DoraCompatibleYaml {
        nodes: Vec<DoraNode>,
    }

    let compatible = DoraCompatibleYaml {
        nodes: dora_dataflow.nodes,
    };

    serde_yaml::to_string(&compatible).map_err(|e| e.to_string())
}

/// 从 YAML 字符串解析为 Dataflow
pub fn yaml_to_dataflow(yaml: &str) -> Result<Dataflow, String> {
    let dora_dataflow: DoraDataflow = serde_yaml::from_str(yaml).map_err(|e| e.to_string())?;
    Ok((&dora_dataflow).into())
}

/// 节点类型到 DORA 路径的映射
fn get_node_path(node_type: &str) -> Option<String> {
    match node_type {
        "camera" => Some("opencv-video-capture".to_string()),
        "yolo" => Some("dora-yolo".to_string()),
        "plot" => Some("dora-rerun".to_string()),
        "timer" => None, // timer 不需要 path，直接使用 dora/timer
        _ => Some(format!("custom-{}", node_type)),
    }
}

/// 获取节点的默认输出端口名
/// 根据节点类型返回合理的输出端口名称
fn get_default_output_port(node_type: &str) -> &'static str {
    match node_type {
        "camera" | "webcam" => "frame",
        "yolo" | "object_detection" => "detections",
        "plot" | "viewer" => "output",
        "timer" => "tick",
        _ => "output",
    }
}

/// 获取节点的默认输入端口名
/// 根据节点类型返回合理的输入端口名称
fn get_default_input_port(node_type: &str) -> &'static str {
    match node_type {
        "yolo" | "object_detection" => "frame",
        "plot" | "viewer" => "input",
        _ => "input",
    }
}

/// 节点类型到 DORA build 命令的映射
fn get_node_build(node_type: &str) -> Option<String> {
    match node_type {
        "camera" => Some("pip install \"git+https://github.com/dora-rs/dora-hub.git#egg=opencv-video-capture&subdirectory=node-hub/opencv-video-capture\"".to_string()),
        "yolo" => Some("pip install \"git+https://github.com/dora-rs/dora-hub.git#egg=dora-yolo&subdirectory=node-hub/dora-yolo\"".to_string()),
        "plot" => Some("pip install \"git+https://github.com/dora-rs/dora-hub.git#egg=dora-rerun&subdirectory=node-hub/dora-rerun\"".to_string()),
        _ => None,
    }
}

/// 从 DORA 路径推断节点类型
fn infer_node_type(path: &Option<String>) -> String {
    if let Some(p) = path {
        if p.contains("opencv") || p.contains("camera") {
            "camera".to_string()
        } else if p.contains("yolo") {
            "yolo".to_string()
        } else if p.contains("rerun") || p.contains("plot") {
            "plot".to_string()
        } else {
            // 从路径中提取最后一段
            p.split('/').last().unwrap_or("custom").to_string()
        }
    } else {
        "custom".to_string()
    }
}

/// 从 Dataflow (DoraMate 内部格式) 转换为 DoraDataflow (DORA 运行时格式)
impl From<&Dataflow> for DoraDataflow {
    fn from(dataflow: &Dataflow) -> Self {
        let mut dora_nodes = Vec::new();
        let mut layout = HashMap::new();

        // 构建节点 ID 到节点类型的映射，用于后续查找
        let mut node_types: HashMap<String, String> = HashMap::new();
        for node in &dataflow.nodes {
            node_types.insert(node.id.clone(), node.node_type.clone());
        }

        // 构建节点 ID 到输出连接的映射
        let mut outputs_from_node: HashMap<String, Vec<String>> = HashMap::new();
        for conn in &dataflow.connections {
            outputs_from_node
                .entry(conn.from.clone())
                .or_insert_with(Vec::new)
                .push(conn.to.clone());
        }

        // 转换每个节点
        for node in &dataflow.nodes {
            // 保存布局信息到元数据
            layout.insert(
                node.id.clone(),
                LayoutInfo {
                    x: node.x,
                    y: node.y,
                    label: Some(node.label.clone()),
                },
            );

            // 构建输入映射
            let mut inputs = HashMap::new();

            // 优先使用节点上定义的输入端口列表
            if let Some(ref node_inputs) = node.inputs {
                // 如果节点定义了输入端口，检查是否包含完整映射
                for input_def in node_inputs {
                    // 检查是否是完整映射格式（包含 ":"）
                    if input_def.contains(':') {
                        // 完整映射格式: "tick: dora/timer/millis/100"
                        if let Some((port_name, source)) = input_def.split_once(':') {
                            let port_name = port_name.trim();
                            let source = source.trim();
                            inputs.insert(port_name.to_string(), source.to_string());
                        }
                    } else {
                        // 简单端口名格式: "tick"
                        // 需要从连接中推断数据源
                        // 找到对应的连接
                        for conn in &dataflow.connections {
                            if conn.to == node.id {
                                // 获取源节点的类型
                                let source_node_type = node_types
                                    .get(&conn.from)
                                    .map(|s| s.as_str())
                                    .unwrap_or("custom");

                                // 获取源节点的默认输出端口名
                                let output_port = get_default_output_port(source_node_type);

                                // 格式: "input_name": "source_node/output_port"
                                inputs.insert(
                                    input_def.clone(),
                                    format!("{}/{}", conn.from, output_port),
                                );
                                break; // 找到第一个匹配的连接后就停止
                            }
                        }
                    }
                }
            } else {
                // 如果节点没有定义输入端口，使用智能推断
                for conn in &dataflow.connections {
                    if conn.to == node.id {
                        // 获取源节点的类型
                        let source_node_type = node_types
                            .get(&conn.from)
                            .map(|s| s.as_str())
                            .unwrap_or("custom");

                        // 获取源节点的默认输出端口名
                        let output_port = get_default_output_port(source_node_type);

                        // 获取当前节点的默认输入端口名
                        let input_port = get_default_input_port(&node.node_type);

                        // 格式: "input_name": "source_node/output_port"
                        inputs.insert(
                            input_port.to_string(),
                            format!("{}/{}", conn.from, output_port),
                        );
                    }
                }
            }

            // 构建输出列表
            // 优先使用节点上定义的输出端口列表
            let outputs = if let Some(ref node_outputs) = node.outputs {
                if !node_outputs.is_empty() {
                    Some(node_outputs.clone())
                } else {
                    None
                }
            } else {
                // 如果节点没有定义输出端口，使用智能推断
                if outputs_from_node.contains_key(&node.id) {
                    let default_output_port = get_default_output_port(&node.node_type);
                    Some(vec![default_output_port.to_string()])
                } else {
                    None
                }
            };

            // 创建 DORA 节点
            // 优先使用节点已有的 path,如果没有则根据 node_type 推断
            let node_path = if node.path.is_some() {
                node.path.clone()
            } else {
                get_node_path(&node.node_type)
            };

            let dora_node = DoraNode {
                id: node.id.clone(),
                path: node_path,
                build: get_node_build(&node.node_type),
                inputs: if inputs.is_empty() {
                    None
                } else {
                    Some(inputs)
                },
                outputs,
                env: node.env.clone(), // 保留环境变量
                operators: None,
            };

            dora_nodes.push(dora_node);
        }

        DoraDataflow {
            __doramate__: Some(DoraMateMeta {
                layout: Some(layout),
                name: Some("My Dataflow".to_string()),
                description: None,
                tags: None,
            }),
            nodes: dora_nodes,
        }
    }
}

/// 从 DoraDataflow (DORA 运行时格式) 转换为 Dataflow (DoraMate 内部格式)
impl From<&DoraDataflow> for Dataflow {
    fn from(dora_dataflow: &DoraDataflow) -> Self {
        let mut nodes = Vec::new();
        let mut connections = Vec::new();
        let node_ids: std::collections::HashSet<&str> =
            dora_dataflow.nodes.iter().map(|n| n.id.as_str()).collect();

        // 提取布局信息
        let layout = dora_dataflow
            .__doramate__
            .as_ref()
            .and_then(|meta| meta.layout.as_ref());

        // 转换每个 DORA 节点
        for dora_node in &dora_dataflow.nodes {
            // 获取布局信息
            let layout_info = layout.and_then(|l| l.get(&dora_node.id));

            // 创建 DoraMate 节点
            // 处理 inputs：将完整映射转换为字符串格式
            // 例如：{"frame": "webcam/frame"} 转换为 ["frame: webcam/frame"]
            let inputs = dora_node.inputs.as_ref().map(|m| {
                m.iter()
                    .map(|(port_name, source)| {
                        // 检查是否是完整的 source 格式（包含 "/" 表示 "node/port"）
                        // 或者是特殊格式（如 "dora/timer/millis/100"）
                        if source.contains('/') || source.contains("dora/") {
                            format!("{}: {}", port_name, source)
                        } else {
                            // 简单格式，只保存 port_name
                            port_name.clone()
                        }
                    })
                    .collect()
            });

            let node = crate::types::Node {
                id: dora_node.id.clone(),
                x: layout_info.map(|l| l.x).unwrap_or(0.0),
                y: layout_info.map(|l| l.y).unwrap_or(0.0),
                label: layout_info
                    .and_then(|l| l.label.clone())
                    .unwrap_or_else(|| dora_node.id.clone()),
                node_type: infer_node_type(&dora_node.path),
                path: dora_node.path.clone(), // 保留路径
                env: dora_node.env.clone(),   // 保留环境变量
                config: None,
                inputs,
                outputs: dora_node.outputs.clone(),
                scale: Some(1.0),
            };
            nodes.push(node);

            // 从 inputs 配置中提取连接关系
            if let Some(inputs) = &dora_node.inputs {
                for (_input_name, source) in inputs {
                    // 解析 "source_node/output" 格式
                    let parts: Vec<&str> = source.split('/').collect();
                    if parts.len() >= 2 {
                        let source_id = parts[0];

                        // 系统内置源（如 dora/timer/...）或外部映射不应转成普通节点连接。
                        if source_id == "dora" || !node_ids.contains(source_id) {
                            continue;
                        }

                        connections.push(crate::types::Connection {
                            from: source_id.to_string(),
                            to: dora_node.id.clone(),
                            from_port: None,
                            to_port: None,
                        });
                    }
                }
            }
        }

        Dataflow { nodes, connections }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::types::{Connection, Node};

    #[test]
    fn test_convert_to_dora_format() {
        let mut env = std::collections::HashMap::new();
        env.insert("CAMERA_ID".to_string(), "0".to_string());

        let dataflow = Dataflow {
            nodes: vec![
                Node {
                    id: "camera".to_string(),
                    x: 100.0,
                    y: 200.0,
                    label: "Camera".to_string(),
                    node_type: "camera".to_string(),
                    path: None,
                    env: Some(env.clone()),
                    config: None,
                    inputs: None,
                    outputs: Some(vec!["image".to_string()]),
                    scale: None,
                },
                Node {
                    id: "yolo".to_string(),
                    x: 400.0,
                    y: 200.0,
                    label: "YOLO".to_string(),
                    node_type: "yolo".to_string(),
                    path: None,
                    env: None,
                    config: None,
                    inputs: Some(vec!["image".to_string()]),
                    outputs: Some(vec!["bbox".to_string()]),
                    scale: None,
                },
            ],
            connections: vec![Connection {
                from: "camera".to_string(),
                to: "yolo".to_string(),
                from_port: None,
                to_port: None,
            }],
        };

        let dora_dataflow: DoraDataflow = (&dataflow).into();

        // 验证节点数量
        assert_eq!(dora_dataflow.nodes.len(), 2);

        // 验证节点存在
        assert_eq!(dora_dataflow.nodes[0].id, "camera");
        assert_eq!(dora_dataflow.nodes[1].id, "yolo");

        // 验证路径
        assert_eq!(
            dora_dataflow.nodes[0].path,
            Some("opencv-video-capture".to_string())
        );
        assert_eq!(dora_dataflow.nodes[1].path, Some("dora-yolo".to_string()));

        // 验证 build 命令
        assert!(dora_dataflow.nodes[0].build.is_some());
        assert!(dora_dataflow.nodes[1].build.is_some());

        // 验证环境变量
        assert!(dora_dataflow.nodes[0].env.is_some());
        assert_eq!(
            dora_dataflow.nodes[0]
                .env
                .as_ref()
                .unwrap()
                .get("CAMERA_ID"),
            Some(&"0".to_string())
        );

        // 验证布局信息
        assert!(dora_dataflow.__doramate__.is_some());
        let layout = &dora_dataflow.__doramate__.as_ref().unwrap().layout;
        assert!(layout.is_some());
        let camera_layout = layout.as_ref().unwrap().get("camera").unwrap();
        assert_eq!(camera_layout.x, 100.0);
        assert_eq!(camera_layout.y, 200.0);
        assert_eq!(camera_layout.label, Some("Camera".to_string()));

        // 验证输入输出
        assert!(dora_dataflow.nodes[0].outputs.is_some()); // camera 有输出
        assert!(dora_dataflow.nodes[1].inputs.is_some()); // yolo 有输入
    }

    #[test]
    fn test_convert_from_dora_format() {
        let dora_nodes = vec![
            DoraNode {
                id: "camera".to_string(),
                path: Some("opencv-video-capture".to_string()),
                build: Some("pip install opencv-video-capture".to_string()),
                inputs: None,
                outputs: Some(vec!["image".to_string()]),
                env: None,
                operators: None,
            },
            DoraNode {
                id: "yolo".to_string(),
                path: Some("dora-yolo".to_string()),
                build: Some("pip install dora-yolo".to_string()),
                inputs: {
                    let mut inputs = HashMap::new();
                    inputs.insert("image".to_string(), "camera/image".to_string());
                    Some(inputs)
                },
                outputs: Some(vec!["bbox".to_string()]),
                env: None,
                operators: None,
            },
        ];

        let mut layout = HashMap::new();
        layout.insert(
            "camera".to_string(),
            LayoutInfo {
                x: 100.0,
                y: 200.0,
                label: Some("Camera".to_string()),
            },
        );
        layout.insert(
            "yolo".to_string(),
            LayoutInfo {
                x: 400.0,
                y: 200.0,
                label: Some("YOLO".to_string()),
            },
        );

        let dora_dataflow = DoraDataflow {
            __doramate__: Some(DoraMateMeta {
                layout: Some(layout),
                name: Some("Test Flow".to_string()),
                description: None,
                tags: None,
            }),
            nodes: dora_nodes,
        };

        let dataflow: Dataflow = (&dora_dataflow).into();

        // 验证节点
        assert_eq!(dataflow.nodes.len(), 2);
        assert_eq!(dataflow.nodes[0].id, "camera");
        assert_eq!(dataflow.nodes[1].id, "yolo");

        // 验证布局
        assert_eq!(dataflow.nodes[0].x, 100.0);
        assert_eq!(dataflow.nodes[0].y, 200.0);
        assert_eq!(dataflow.nodes[0].label, "Camera");

        // 验证连接
        assert_eq!(dataflow.connections.len(), 1);
        assert_eq!(dataflow.connections[0].from, "camera");
        assert_eq!(dataflow.connections[0].to, "yolo");
    }

    #[test]
    fn test_convert_from_dora_skips_builtin_timer_source_connection() {
        let dora_nodes = vec![
            DoraNode {
                id: "camera".to_string(),
                path: Some("opencv-video-capture".to_string()),
                build: None,
                inputs: None,
                outputs: Some(vec!["image".to_string()]),
                env: None,
                operators: None,
            },
            DoraNode {
                id: "viewer".to_string(),
                path: Some("viewer".to_string()),
                build: None,
                inputs: {
                    let mut inputs = HashMap::new();
                    inputs.insert("tick".to_string(), "dora/timer/millis/30".to_string());
                    inputs.insert("image".to_string(), "camera/image".to_string());
                    Some(inputs)
                },
                outputs: None,
                env: None,
                operators: None,
            },
        ];

        let dora_dataflow = DoraDataflow {
            __doramate__: None,
            nodes: dora_nodes,
        };

        let dataflow: Dataflow = (&dora_dataflow).into();

        assert_eq!(dataflow.connections.len(), 1);
        assert_eq!(dataflow.connections[0].from, "camera");
        assert_eq!(dataflow.connections[0].to, "viewer");
    }
}
