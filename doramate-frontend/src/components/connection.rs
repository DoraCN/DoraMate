use crate::utils::geometry::calculate_bezier_control_points;
use leptos::prelude::*;

/// 贝塞尔曲线连线组件
#[component]
pub fn BezierConnection(
    /// 起点 X
    x1: f64,
    /// 起点 Y
    y1: f64,
    /// 终点 X
    x2: f64,
    /// 终点 Y
    y2: f64,
    /// 连线颜色
    #[prop(default = "#00d4ff".into())]
    stroke: String,
    /// 线条宽度
    #[prop(default = 2)]
    stroke_width: u32,
    /// 虚线样式（可选）
    #[prop(optional)]
    stroke_dasharray: Option<String>,
    /// CSS 类名（可选）
    #[prop(optional)]
    class: Option<String>,
) -> impl IntoView {
    // 计算控制点
    let (cp1x, cp1y, cp2x, cp2y) = calculate_bezier_control_points(x1, y1, x2, y2);

    // 生成路径数据
    // M x1 y1 - 移动到起点
    // C cp1x cp1y cp2x cp2y x2 y2 - 三次贝塞尔曲线到终点
    let path_data = format!(
        "M {} {} C {} {} {} {} {} {}",
        x1, y1, cp1x, cp1y, cp2x, cp2y, x2, y2
    );

    view! {
        <path
            d=path_data
            fill="none"
            stroke=stroke.clone()
            stroke-width=stroke_width
            stroke-dasharray=stroke_dasharray.clone()
            class=class.clone()
        />
    }
}
