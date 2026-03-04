use crate::types::{Node, PortType};

/// 计算节点端口位置
pub fn get_port_position(node: &Node, port_type: PortType) -> (f64, f64) {
    match port_type {
        PortType::Input => (node.x, node.y + 30.0), // 左侧端口
        PortType::Output => (node.x + 120.0, node.y + 30.0), // 右侧端口
    }
}

/// 检测点是否在节点内
pub fn is_point_in_node(x: f64, y: f64, node: &Node) -> bool {
    x >= node.x && x <= node.x + 120.0 && y >= node.y && y <= node.y + 60.0
}

/// 计算贝塞尔曲线控制点
pub fn calculate_bezier_control_points(x1: f64, y1: f64, x2: f64, y2: f64) -> (f64, f64, f64, f64) {
    let dx = (x2 - x1).abs() / 2.0;
    let cp1x = x1 + dx;
    let cp1y = y1;
    let cp2x = x2 - dx;
    let cp2y = y2;
    (cp1x, cp1y, cp2x, cp2y)
}
