use crate::components::connection::BezierConnection;
use crate::components::node_panel::NodeTemplate;
use crate::types::{Connection, Node, NodeState, PortType};
use crate::utils::geometry::get_port_position;
use leptos::prelude::*;
use log::info;
use std::collections::{HashMap, HashSet};
use wasm_bindgen::JsCast;
use web_sys::{DragEvent, MouseEvent, WheelEvent};

const NODE_WIDTH: f64 = 120.0;
const NODE_HEIGHT: f64 = 60.0;
const SELECTION_THRESHOLD: f64 = 4.0;

fn next_incremental_node_id(
    existing_nodes: &[Node],
    id_prefix: &str,
    also_match_prefixes: &[&str],
) -> (String, usize) {
    let mut prefixes = Vec::with_capacity(1 + also_match_prefixes.len());
    prefixes.push(format!("{}_", id_prefix));
    for legacy in also_match_prefixes {
        if !legacy.is_empty() && *legacy != id_prefix {
            prefixes.push(format!("{}_", legacy));
        }
    }

    let mut max_suffix = 0usize;

    for node in existing_nodes {
        for prefix in &prefixes {
            if let Some(rest) = node.id.strip_prefix(prefix) {
                if let Ok(value) = rest.parse::<usize>() {
                    max_suffix = max_suffix.max(value);
                    break;
                }
            }
        }
    }

    let used_ids: HashSet<&str> = existing_nodes.iter().map(|node| node.id.as_str()).collect();
    let mut next_suffix = max_suffix.saturating_add(1);

    loop {
        let candidate = format!("{}_{}", id_prefix, next_suffix);
        if !used_ids.contains(candidate.as_str()) {
            return (candidate, next_suffix);
        }
        next_suffix = next_suffix.saturating_add(1);
    }
}

fn parse_input_port_name(input: &str) -> Option<String> {
    let value = input.trim();
    if value.is_empty() {
        return None;
    }

    if let Some((port_name, _)) = value.split_once(':') {
        let normalized = port_name.trim();
        if !normalized.is_empty() {
            return Some(normalized.to_string());
        }
    }

    if let Some((_, port_name)) = value.rsplit_once('/') {
        let normalized = port_name.trim();
        if !normalized.is_empty() {
            return Some(normalized.to_string());
        }
    }

    Some(value.to_string())
}

fn input_has_bound_source(input: &str) -> bool {
    let value = input.trim();
    if value.is_empty() {
        return false;
    }
    if let Some((_, source)) = value.split_once(':') {
        return !source.trim().is_empty();
    }
    value.contains('/')
}

fn select_target_input_port(target_node: &Node, preferred_port: &str) -> (String, Option<usize>) {
    let preferred = preferred_port.trim();

    if let Some(inputs) = target_node.inputs.as_ref() {
        if !preferred.is_empty() {
            if let Some((idx, port_name)) = inputs
                .iter()
                .enumerate()
                .filter_map(|(idx, input)| parse_input_port_name(input).map(|name| (idx, name)))
                .find(|(_, name)| name == preferred)
            {
                return (port_name, Some(idx));
            }
        }

        if let Some((idx, port_name)) = inputs
            .iter()
            .enumerate()
            .filter(|(_, input)| !input_has_bound_source(input))
            .filter_map(|(idx, input)| parse_input_port_name(input).map(|name| (idx, name)))
            .next()
        {
            return (port_name, Some(idx));
        }

        if !preferred.is_empty() {
            // No matching/empty slot: create a new mapping for the preferred input port
            // instead of overwriting the first existing bound input.
            return (preferred.to_string(), None);
        }

        if let Some((idx, port_name)) = inputs
            .iter()
            .enumerate()
            .filter_map(|(idx, input)| parse_input_port_name(input).map(|name| (idx, name)))
            .next()
        {
            return (port_name, Some(idx));
        }
    }

    if preferred.is_empty() {
        ("in".to_string(), None)
    } else {
        (preferred.to_string(), None)
    }
}

#[derive(Clone, Debug)]
struct ConnectionDrag {
    from_node: String,
    from_port_type: PortType,
}

#[derive(Clone, Debug)]
struct NodeDrag {
    target_node_ids: Vec<String>,
    last_x: f64,
    last_y: f64,
}

#[derive(Clone, Debug)]
struct CanvasPan {
    last_x: f64,
    last_y: f64,
}

#[derive(Clone, Debug)]
struct MarqueeSelection {
    start_x: f64,
    start_y: f64,
    current_x: f64,
    current_y: f64,
    additive: bool,
}

#[component]
pub fn Canvas(
    nodes: Signal<Vec<Node>>,
    set_nodes: WriteSignal<Vec<Node>>,
    connections: Signal<Vec<Connection>>,
    set_connections: WriteSignal<Vec<Connection>>,
    on_selection_change: Callback<(Vec<String>, Option<String>)>,
    #[prop(optional)] on_delete_connection: Option<Callback<Connection>>,
    selected_node_ids: Signal<Vec<String>>,
    node_runtime_states: Signal<HashMap<String, NodeState>>,
    layout_focus_targets: Signal<Vec<String>>,
    layout_focus_serial: Signal<u64>,
    #[prop(optional)] is_running: Signal<bool>,
) -> impl IntoView {
    let (dragging, set_dragging) = signal(None::<NodeDrag>);
    let (panning, set_panning) = signal(None::<CanvasPan>);
    let (connecting, set_connecting) = signal(None::<ConnectionDrag>);
    let (mouse_pos, set_mouse_pos) = signal((0.0, 0.0));
    let (canvas_scale, set_canvas_scale) = signal(1.0f64);
    let (canvas_offset, set_canvas_offset) = signal((0.0f64, 0.0f64));
    let (marquee, set_marquee) = signal(None::<MarqueeSelection>);
    let (suppress_click_selection, set_suppress_click_selection) = signal(false);

    let get_svg_coords = move |local_x: f64, local_y: f64| -> (f64, f64) {
        let scale = canvas_scale.get_untracked();
        let (offset_x, offset_y) = canvas_offset.get_untracked();
        ((local_x - offset_x) / scale, (local_y - offset_y) / scale)
    };

    Effect::new(move |_| {
        let serial = layout_focus_serial.get();
        if serial == 0 {
            return;
        }

        let target_ids = layout_focus_targets.get();
        if target_ids.is_empty() {
            return;
        }

        let target_id_set: HashSet<&str> = target_ids.iter().map(|id| id.as_str()).collect();
        let focused_nodes: Vec<Node> = nodes
            .get_untracked()
            .into_iter()
            .filter(|node| target_id_set.contains(node.id.as_str()))
            .collect();
        if focused_nodes.is_empty() {
            return;
        }

        let min_x = focused_nodes
            .iter()
            .map(|node| node.x)
            .fold(f64::INFINITY, f64::min);
        let min_y = focused_nodes
            .iter()
            .map(|node| node.y)
            .fold(f64::INFINITY, f64::min);
        let max_x = focused_nodes
            .iter()
            .map(|node| node.x + NODE_WIDTH)
            .fold(f64::NEG_INFINITY, f64::max);
        let max_y = focused_nodes
            .iter()
            .map(|node| node.y + NODE_HEIGHT)
            .fold(f64::NEG_INFINITY, f64::max);

        if !min_x.is_finite() || !min_y.is_finite() || !max_x.is_finite() || !max_y.is_finite() {
            return;
        }

        let fallback_w = 1200.0;
        let fallback_h = 700.0;
        let (view_w, view_h) = web_sys::window()
            .and_then(|w| w.document())
            .and_then(|d| d.get_element_by_id("dataflow-canvas-svg"))
            .and_then(|el| el.dyn_into::<web_sys::HtmlElement>().ok())
            .map(|el| {
                (
                    (el.client_width() as f64).max(320.0),
                    (el.client_height() as f64).max(240.0),
                )
            })
            .unwrap_or((fallback_w, fallback_h));

        let content_w = (max_x - min_x).max(1.0);
        let content_h = (max_y - min_y).max(1.0);
        let target_scale = ((view_w * 0.8) / content_w)
            .min((view_h * 0.8) / content_h)
            .clamp(0.2, 2.5);

        let center_x = (min_x + max_x) / 2.0;
        let center_y = (min_y + max_y) / 2.0;
        set_canvas_scale.set(target_scale);
        set_canvas_offset.set((
            (view_w / 2.0) - center_x * target_scale,
            (view_h / 2.0) - center_y * target_scale,
        ));
    });

    let on_mouse_move = move |e: MouseEvent| {
        let client_x = e.client_x() as f64;
        let client_y = e.client_y() as f64;
        let (mouse_x, mouse_y) = get_svg_coords(e.offset_x() as f64, e.offset_y() as f64);
        set_mouse_pos.set((mouse_x, mouse_y));

        if let Some(pan) = panning.get() {
            let dx = client_x - pan.last_x;
            let dy = client_y - pan.last_y;
            if dx.abs() > 0.0 || dy.abs() > 0.0 {
                set_canvas_offset.update(|offset| {
                    offset.0 += dx;
                    offset.1 += dy;
                });
            }
            set_panning.set(Some(CanvasPan {
                last_x: client_x,
                last_y: client_y,
            }));
            return;
        }

        if let Some(drag) = dragging.get() {
            let dx = mouse_x - drag.last_x;
            let dy = mouse_y - drag.last_y;

            if dx.abs() > 0.0 || dy.abs() > 0.0 {
                set_suppress_click_selection.set(true);

                let target_ids: HashSet<&str> =
                    drag.target_node_ids.iter().map(|id| id.as_str()).collect();
                set_nodes.update(|all_nodes| {
                    for node in all_nodes.iter_mut() {
                        if target_ids.contains(node.id.as_str()) {
                            node.x += dx;
                            node.y += dy;
                        }
                    }
                });
            }

            set_dragging.set(Some(NodeDrag {
                target_node_ids: drag.target_node_ids,
                last_x: mouse_x,
                last_y: mouse_y,
            }));
            return;
        }

        if let Some(selection) = marquee.get() {
            let (x, y) = get_svg_coords(e.offset_x() as f64, e.offset_y() as f64);
            set_marquee.set(Some(MarqueeSelection {
                start_x: selection.start_x,
                start_y: selection.start_y,
                current_x: x,
                current_y: y,
                additive: selection.additive,
            }));
        }
    };

    let on_mouse_up = move |_| {
        if panning.get().is_some() {
            set_panning.set(None);
            return;
        }
        set_dragging.set(None);

        if let Some(selection) = marquee.get() {
            let dx = (selection.current_x - selection.start_x).abs();
            let dy = (selection.current_y - selection.start_y).abs();

            if dx < SELECTION_THRESHOLD && dy < SELECTION_THRESHOLD {
                if !selection.additive {
                    on_selection_change.run((Vec::new(), None));
                }
                set_marquee.set(None);
                return;
            }

            let min_x = selection.start_x.min(selection.current_x);
            let max_x = selection.start_x.max(selection.current_x);
            let min_y = selection.start_y.min(selection.current_y);
            let max_y = selection.start_y.max(selection.current_y);

            let mut in_box: Vec<String> = nodes
                .get_untracked()
                .iter()
                .filter(|node| {
                    node.x >= min_x
                        && node.y >= min_y
                        && node.x + NODE_WIDTH <= max_x
                        && node.y + NODE_HEIGHT <= max_y
                })
                .map(|node| node.id.clone())
                .collect();

            if selection.additive {
                let mut merged = selected_node_ids.get_untracked();
                for id in in_box.drain(..) {
                    if !merged.iter().any(|existing| existing == &id) {
                        merged.push(id);
                    }
                }
                let primary = merged.last().cloned();
                on_selection_change.run((merged, primary));
            } else {
                let primary = in_box.last().cloned();
                on_selection_change.run((in_box, primary));
            }
        }

        set_marquee.set(None);
    };

    let on_wheel = move |e: WheelEvent| {
        e.prevent_default();
        let local_x = e.offset_x() as f64;
        let local_y = e.offset_y() as f64;

        let old_scale = canvas_scale.get();
        let (offset_x, offset_y) = canvas_offset.get();
        let world_x = (local_x - offset_x) / old_scale;
        let world_y = (local_y - offset_y) / old_scale;

        let delta = e.delta_y() as f64;
        let scale_factor = if delta > 0.0 { 0.9 } else { 1.1 };
        let new_scale = (old_scale * scale_factor).max(0.1).min(5.0);

        let new_offset_x = local_x - world_x * new_scale;
        let new_offset_y = local_y - world_y * new_scale;
        set_canvas_offset.set((new_offset_x, new_offset_y));
        set_canvas_scale.set(new_scale);
        info!("canvas zoom: {:.2}x", new_scale);
    };

    let on_canvas_mouse_down = move |e: MouseEvent| {
        if e.button() == 1 || (e.button() == 0 && e.alt_key()) {
            e.prevent_default();
            set_panning.set(Some(CanvasPan {
                last_x: e.client_x() as f64,
                last_y: e.client_y() as f64,
            }));
            set_dragging.set(None);
            set_marquee.set(None);
            return;
        }

        if e.button() != 0 {
            return;
        }
        let (x, y) = get_svg_coords(e.offset_x() as f64, e.offset_y() as f64);
        set_marquee.set(Some(MarqueeSelection {
            start_x: x,
            start_y: y,
            current_x: x,
            current_y: y,
            additive: e.shift_key(),
        }));
        set_suppress_click_selection.set(false);
    };

    let on_drag_over = move |e: DragEvent| {
        e.prevent_default();
        if let Some(data_transfer) = e.data_transfer() {
            let _ = data_transfer.set_drop_effect("copy");
        }
    };

    let on_drag_leave = move |e: DragEvent| {
        e.prevent_default();
    };

    let on_drop = {
        let set_nodes = set_nodes.clone();
        let on_selection_change = on_selection_change.clone();
        move |e: DragEvent| {
            e.prevent_default();
            e.stop_propagation();

            if let Some(data_transfer) = e.data_transfer() {
                if let Ok(json) = data_transfer.get_data("application/json") {
                    if let Ok(template) = serde_json::from_str::<NodeTemplate>(&json) {
                        let (drop_x, drop_y) =
                            get_svg_coords(e.offset_x() as f64, e.offset_y() as f64);

                        let current_nodes = nodes.get();
                        let mut normalized_prefix = template.id.as_str();
                        let mut legacy_prefixes: Vec<&str> = Vec::new();
                        for removable_prefix in ["yaml_", "cfg_"] {
                            if let Some(stripped) = normalized_prefix.strip_prefix(removable_prefix)
                            {
                                if !stripped.is_empty() {
                                    legacy_prefixes.push(normalized_prefix);
                                    normalized_prefix = stripped;
                                }
                            }
                        }
                        let (node_id, instance_number) = next_incremental_node_id(
                            &current_nodes,
                            normalized_prefix,
                            &legacy_prefixes,
                        );

                        let node_label = format!("{} #{}", template.name, instance_number);

                        let new_node = Node {
                            id: node_id.clone(),
                            x: drop_x - NODE_WIDTH / 2.0,
                            y: drop_y - NODE_HEIGHT / 2.0,
                            label: node_label,
                            node_type: template.node_type.clone(),
                            path: template.path.clone(),
                            env: None,
                            config: None,
                            outputs: template.outputs.clone(),
                            inputs: template.inputs.clone(),
                            scale: Some(1.0),
                        };

                        set_nodes.update(|all_nodes| all_nodes.push(new_node));
                        on_selection_change.run((vec![node_id.clone()], Some(node_id.clone())));
                        info!("node added by drop: {}", node_id);
                    }
                }
            }
        }
    };

    view! {
        <svg
            id="dataflow-canvas-svg"
            width="4000"
            height="4000"
            on:mousedown=on_canvas_mouse_down
            on:mousemove=on_mouse_move
            on:mouseup=on_mouse_up
            on:mouseleave=on_mouse_up
            on:wheel=on_wheel
            on:dragover=on_drag_over
            on:dragleave=on_drag_leave
            on:drop=on_drop
            on:contextmenu=move |e| e.prevent_default()
            style=move || {
                let cursor = if panning.get().is_some() {
                    "grabbing"
                } else if dragging.get().is_some() {
                    "move"
                } else {
                    "grab"
                };
                format!(
                    "background-color: #1e1e1e; cursor: {}; display: block; user-select: none;",
                    cursor
                )
            }
        >
            <g
                transform=move || format!(
                    "translate({} {}) scale({})",
                    canvas_offset.get().0,
                    canvas_offset.get().1,
                    canvas_scale.get()
                )
            >
                {move || {
                    let connections_vec = connections.get().clone();
                    let nodes_vec = nodes.get().clone();
                    let running = is_running.get();

                    connections_vec
                        .iter()
                        .filter_map(|conn| {
                            let conn = conn.clone();
                            if let (Some(from_node), Some(to_node)) = (
                                nodes_vec.iter().find(|n| n.id == conn.from),
                                nodes_vec.iter().find(|n| n.id == conn.to),
                            ) {
                                let (x1, y1) = get_port_position(from_node, PortType::Output);
                                let (x2, y2) = get_port_position(to_node, PortType::Input);

                                let stroke_class = if running {
                                    "connection-flow running".to_string()
                                } else {
                                    "connection-flow".to_string()
                                };

                                Some(view! {
                                    <g>
                                        <BezierConnection x1=x1 y1=y1 x2=x2 y2=y2 class=stroke_class />
                                        <circle
                                            cx={(x1 + x2) / 2.0}
                                            cy={(y1 + y2) / 2.0}
                                            r="8"
                                            fill="white"
                                            stroke="#ff4444"
                                            stroke-width="2"
                                            style="cursor: pointer;"
                                            on:mousedown=move |e| {
                                                e.stop_propagation();
                                                let conn_clone = conn.clone();
                                                if let Some(ref callback) = on_delete_connection {
                                                    callback.run(conn_clone);
                                                } else {
                                                    set_connections.update(|all| {
                                                        all.retain(|c| c != &conn_clone);
                                                    });
                                                }
                                            }
                                        />
                                        <text
                                            x={(x1 + x2) / 2.0}
                                            y={(y1 + y2) / 2.0 + 3.0}
                                            text-anchor="middle"
                                            font-size="12"
                                            fill="#ff4444"
                                            style="cursor: pointer; pointer-events: none;"
                                        >
                                            "x"
                                        </text>
                                    </g>
                                })
                            } else {
                                None
                            }
                        })
                        .collect::<Vec<_>>()
                }}

                {move || {
                    if let Some(conn) = connecting.get() {
                        let nodes_vec = nodes.get();
                        if let Some(from_node) = nodes_vec.iter().find(|n| n.id == conn.from_node) {
                            let (x1, y1) = get_port_position(from_node, conn.from_port_type);
                            let (x2, y2) = mouse_pos.get();

                            Some(view! {
                                <BezierConnection
                                    x1=x1
                                    y1=y1
                                    x2=x2
                                    y2=y2
                                    stroke="#00ff88".to_string()
                                    stroke_dasharray="5,5".to_string()
                                />
                            })
                        } else {
                            None
                        }
                    } else {
                        None
                    }
                }}

                {move || {
                    marquee.get().map(|selection| {
                        let x = selection.start_x.min(selection.current_x);
                        let y = selection.start_y.min(selection.current_y);
                        let width = (selection.current_x - selection.start_x).abs();
                        let height = (selection.current_y - selection.start_y).abs();
                        view! {
                            <rect
                                x=x
                                y=y
                                width=width
                                height=height
                                fill="rgba(33, 150, 243, 0.15)"
                                stroke="#2196F3"
                                stroke-width="1.5"
                                stroke-dasharray="6,4"
                                style="pointer-events: none;"
                            />
                        }
                    })
                }}

                {move || {
                    nodes.get()
                        .iter()
                        .map(|node| {
                            let node_id = node.id.clone();
                            let node_id_for_fill = node_id.clone();
                            let node_id_for_stroke = node_id.clone();
                            let node_id_for_stroke_width = node_id.clone();
                            let node_id_for_shadow = node_id.clone();
                            let node_id_for_status_dot = node_id.clone();

                            let create_port_handler = {
                                let node_id = node_id.clone();
                                move |port_type: PortType| {
                                    let node_id = node_id.clone();
                                    move |e: MouseEvent| {
                                        e.stop_propagation();

                                        if let Some(conn) = connecting.get() {
                                            let to_node = node_id.clone();
                                            let valid = match conn.from_port_type {
                                                PortType::Output => {
                                                    port_type == PortType::Input && conn.from_node != to_node
                                                }
                                                PortType::Input => {
                                                    port_type == PortType::Output && conn.from_node != to_node
                                                }
                                            };

                                            if valid {
                                                let (from, to) = match conn.from_port_type {
                                                    PortType::Output => {
                                                        (conn.from_node.clone(), to_node.clone())
                                                    }
                                                    PortType::Input => {
                                                        (to_node.clone(), conn.from_node.clone())
                                                    }
                                                };

                                                let connections_vec = connections.get();
                                                let exists = connections_vec
                                                    .iter()
                                                    .any(|c| c.from == from && c.to == to);
                                                if !exists {
                                                    let from_port_name = {
                                                        let nodes_vec = nodes.get();
                                                        if let Some(n) =
                                                            nodes_vec.iter().find(|n| n.id == from)
                                                        {
                                                            n.outputs
                                                                .as_ref()
                                                                .and_then(|outputs| outputs.first().cloned())
                                                                .unwrap_or_else(|| "out".to_string())
                                                        } else {
                                                            "out".to_string()
                                                        }
                                                    };

                                                    let (to_port_name, to_port_index) = {
                                                        let nodes_vec = nodes.get();
                                                        if let Some(n) = nodes_vec.iter().find(|n| n.id == to) {
                                                            select_target_input_port(
                                                                n,
                                                                &from_port_name,
                                                            )
                                                        } else {
                                                            ("in".to_string(), None)
                                                        }
                                                    };

                                                    let new_connection = Connection {
                                                        from: from.clone(),
                                                        to: to.clone(),
                                                        from_port: Some(from_port_name.clone()),
                                                        to_port: Some(to_port_name.clone()),
                                                    };
                                                    set_connections.update(|all| all.push(new_connection));

                                                    let new_input_value =
                                                        format!("{}: {}/{}", to_port_name, from, from_port_name);
                                                    set_nodes.update(|all_nodes| {
                                                        if let Some(target) =
                                                            all_nodes.iter_mut().find(|n| n.id == to)
                                                        {
                                                            if target.inputs.is_none() {
                                                                target.inputs =
                                                                    Some(vec![new_input_value.clone()]);
                                                            } else if let Some(ref mut inputs) =
                                                                target.inputs
                                                            {
                                                                if let Some(idx) = to_port_index {
                                                                    if idx < inputs.len() {
                                                                        inputs[idx] =
                                                                            new_input_value.clone();
                                                                    } else {
                                                                        inputs.push(
                                                                            new_input_value.clone(),
                                                                        );
                                                                    }
                                                                } else if let Some(idx) = inputs
                                                                    .iter()
                                                                    .position(|input| {
                                                                        parse_input_port_name(input)
                                                                            .as_deref()
                                                                            == Some(to_port_name.as_str())
                                                                    })
                                                                {
                                                                    inputs[idx] =
                                                                        new_input_value.clone();
                                                                } else {
                                                                    inputs.push(new_input_value.clone());
                                                                }
                                                            }
                                                        }
                                                    });
                                                }
                                            }

                                            set_connecting.set(None);
                                        } else {
                                            set_connecting.set(Some(ConnectionDrag {
                                                from_node: node_id.clone(),
                                                from_port_type: port_type,
                                            }));
                                        }
                                    }
                                }
                            };

                            let on_input_click = create_port_handler(PortType::Input);
                            let on_output_click = create_port_handler(PortType::Output);

                            let on_node_mouse_down = {
                                let node_id = node_id.clone();
                                move |e: MouseEvent| {
                                    if e.button() != 0 {
                                        return;
                                    }
                                    e.stop_propagation();

                                    let selected = selected_node_ids.get();
                                    let drag_targets = if selected.iter().any(|id| id == &node_id) {
                                        selected
                                    } else {
                                        vec![node_id.clone()]
                                    };
                                    let (start_x, start_y) =
                                        get_svg_coords(e.offset_x() as f64, e.offset_y() as f64);

                                    set_dragging.set(Some(NodeDrag {
                                        target_node_ids: drag_targets,
                                        last_x: start_x,
                                        last_y: start_y,
                                    }));
                                    set_suppress_click_selection.set(false);
                                }
                            };

                            let on_node_click = {
                                let node_id = node_id.clone();
                                let on_selection_change = on_selection_change.clone();
                                move |e: MouseEvent| {
                                    e.stop_propagation();
                                    if suppress_click_selection.get() {
                                        set_suppress_click_selection.set(false);
                                        return;
                                    }

                                    let mut selected = selected_node_ids.get();
                                    if e.shift_key() {
                                        if let Some(pos) =
                                            selected.iter().position(|id| id == &node_id)
                                        {
                                            selected.remove(pos);
                                        } else {
                                            selected.push(node_id.clone());
                                        }
                                    } else {
                                        selected = vec![node_id.clone()];
                                    }

                                    let primary = selected.last().cloned();
                                    on_selection_change.run((selected, primary));
                                }
                            };

                            let on_delete = {
                                let node_id = node_id.clone();
                                let on_selection_change = on_selection_change.clone();
                                move |e: MouseEvent| {
                                    e.stop_propagation();
                                    set_nodes.update(|all_nodes| {
                                        all_nodes.retain(|n| n.id != node_id);
                                    });
                                    set_connections.update(|all_connections| {
                                        all_connections
                                            .retain(|c| c.from != node_id && c.to != node_id);
                                    });

                                    let mut selected = selected_node_ids.get_untracked();
                                    selected.retain(|id| id != &node_id);
                                    let primary = selected.last().cloned();
                                    on_selection_change.run((selected, primary));
                                }
                            };

                            view! {
                                <g
                                    on:mousedown=on_node_mouse_down
                                    on:click=on_node_click
                                    style="cursor: move;"
                                >
                                    <rect
                                        x=node.x
                                        y=node.y
                                        width=NODE_WIDTH
                                        height=NODE_HEIGHT
                                        fill=move || {
                                            let runtime_state = node_runtime_states
                                                .get()
                                                .get(&node_id_for_fill)
                                                .cloned()
                                                .unwrap_or(NodeState::Idle);
                                            if selected_node_ids
                                                .get()
                                                .iter()
                                                .any(|id| id == &node_id_for_fill)
                                            {
                                                "#E3F2FD"
                                            } else {
                                                match runtime_state {
                                                    NodeState::Running => "#E8F5E9",
                                                    NodeState::Starting => "#FFF3E0",
                                                    NodeState::Error(_) => "#FFEBEE",
                                                    NodeState::Stopped => "#F5F5F5",
                                                    NodeState::Idle => "white",
                                                }
                                            }
                                        }
                                        stroke=move || {
                                            let runtime_state = node_runtime_states
                                                .get()
                                                .get(&node_id_for_stroke)
                                                .cloned()
                                                .unwrap_or(NodeState::Idle);
                                            if selected_node_ids
                                                .get()
                                                .iter()
                                                .any(|id| id == &node_id_for_stroke)
                                            {
                                                "#2196F3"
                                            } else {
                                                match runtime_state {
                                                    NodeState::Idle => "#333",
                                                    NodeState::Error(_) => "#f44336",
                                                    NodeState::Starting => "#FF9800",
                                                    NodeState::Running => "#4CAF50",
                                                    NodeState::Stopped => "#9E9E9E",
                                                }
                                            }
                                        }
                                        stroke-width=move || {
                                            if selected_node_ids
                                                .get()
                                                .iter()
                                                .any(|id| id == &node_id_for_stroke_width)
                                            {
                                                "3"
                                            } else {
                                                "2"
                                            }
                                        }
                                        rx="5"
                                        style=move || {
                                            let runtime_state = node_runtime_states
                                                .get()
                                                .get(&node_id_for_shadow)
                                                .cloned()
                                                .unwrap_or(NodeState::Idle);
                                            if selected_node_ids
                                                .get()
                                                .iter()
                                                .any(|id| id == &node_id_for_shadow)
                                            {
                                                "filter: drop-shadow(0 0 8px rgba(33, 150, 243, 0.6));"
                                            } else {
                                                match runtime_state {
                                                    NodeState::Running => "filter: drop-shadow(0 0 6px rgba(76, 175, 80, 0.55));",
                                                    NodeState::Starting => "filter: drop-shadow(0 0 6px rgba(255, 152, 0, 0.55));",
                                                    NodeState::Error(_) => "filter: drop-shadow(0 0 6px rgba(244, 67, 54, 0.55));",
                                                    _ => "",
                                                }
                                            }
                                        }
                                    />

                                    <circle
                                        cx={node.x + 10.0}
                                        cy={node.y + 10.0}
                                        r="4"
                                        fill=move || {
                                            let runtime_state = node_runtime_states
                                                .get()
                                                .get(&node_id_for_status_dot)
                                                .cloned()
                                                .unwrap_or(NodeState::Idle);
                                            match runtime_state {
                                                NodeState::Running => "#4CAF50",
                                                NodeState::Starting => "#FF9800",
                                                NodeState::Error(_) => "#f44336",
                                                NodeState::Stopped => "#9E9E9E",
                                                NodeState::Idle => "#607D8B",
                                            }
                                        }
                                        stroke="#1e1e1e"
                                        stroke-width="1"
                                    />

                                    <text
                                        x={node.x + NODE_WIDTH / 2.0}
                                        y={node.y + 35.0}
                                        text-anchor="middle"
                                        font-size="12"
                                        font-family="Arial, sans-serif"
                                    >
                                        {node.label.clone()}
                                    </text>

                                    <circle
                                        cx={node.x + NODE_WIDTH}
                                        cy={node.y}
                                        r="8"
                                        fill="white"
                                        stroke="#ff4444"
                                        stroke-width="2"
                                        style="cursor: pointer;"
                                        on:mousedown=on_delete
                                    />
                                    <text
                                        x={node.x + NODE_WIDTH}
                                        y={node.y + 3.0}
                                        text-anchor="middle"
                                        font-size="12"
                                        fill="#ff4444"
                                        style="cursor: pointer; pointer-events: none;"
                                    >
                                        "x"
                                    </text>

                                    <circle
                                        cx=node.x
                                        cy={node.y + NODE_HEIGHT / 2.0}
                                        r="6"
                                        fill="#4CAF50"
                                        stroke="#333"
                                        stroke-width="2"
                                        style=format!(
                                            "cursor: crosshair;{}",
                                            if connecting.get().is_some() { " opacity: 0.6;" } else { "" }
                                        )
                                        on:mousedown=on_input_click
                                    />

                                    <circle
                                        cx={node.x + NODE_WIDTH}
                                        cy={node.y + NODE_HEIGHT / 2.0}
                                        r="6"
                                        fill="#2196F3"
                                        stroke="#333"
                                        stroke-width="2"
                                        style=format!(
                                            "cursor: crosshair;{}",
                                            if connecting.get().is_some() { " opacity: 0.6;" } else { "" }
                                        )
                                        on:mousedown=on_output_click
                                    />
                                </g>
                            }
                        })
                        .collect::<Vec<_>>()
                }}
            </g>
        </svg>

        <style>
            r#"
            .connection-flow path {
                stroke-dasharray: 10, 5;
                stroke-dashoffset: 0;
            }

            .connection-flow.running path {
                animation: flowAnimation 1s linear infinite;
            }

            @keyframes flowAnimation {
                from {
                    stroke-dashoffset: 15;
                }
                to {
                    stroke-dashoffset: 0;
                }
            }
            "#
        </style>
    }
}
