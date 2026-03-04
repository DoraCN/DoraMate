// DoraMate Frontend - Visual DORA dataflow editor

use leptos::ev::Event;
use leptos::mount::mount_to_body;
use leptos::prelude::*;
use leptos::task::spawn_local;
use std::cell::RefCell;
use std::collections::{BTreeMap, HashMap, HashSet, VecDeque};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use wasm_bindgen::prelude::*;
use wasm_bindgen::JsCast;

pub mod components;
pub mod node_registry;
pub mod types;
pub mod utils;

use components::SaveDialogState;
use components::{
    setTimeout, Canvas, ConfirmConfig, ConfirmDialog, ConfirmState, LogPanel, NodePanel,
    NodeTemplate, PropertyPanel, SaveFileDialog, ShortcutSettingsDialog, StatusPanel, Toolbar,
};
use node_registry::{NodeCategory, NODE_REGISTRY};
use types::{Connection, Dataflow, Node, NodeState};
use utils::api;
use utils::recent_files::{add_recent_file, get_recent_files, remove_recent_file, RecentFileEntry};
use utils::shortcuts::{
    load_shortcut_config, reset_shortcut_config, save_shortcut_config, ShortcutConfig,
};

#[derive(Clone)]
struct ValidationFeedback {
    is_success: bool,
    summary: String,
    details: Vec<String>,
}

const HISTORY_LIMIT: usize = 100;
const HISTORY_RECORD_INTERVAL_MS: f64 = 120.0;
const DUPLICATE_OFFSET: f64 = 48.0;
const AUTO_LAYOUT_START_X: f64 = 120.0;
const AUTO_LAYOUT_START_Y: f64 = 120.0;
const AUTO_LAYOUT_X_GAP: f64 = 280.0;
const AUTO_LAYOUT_Y_GAP: f64 = 140.0;
const AUTO_LAYOUT_GROUP_GAP: f64 = 220.0;
const AUTO_LAYOUT_ORDER_SWEEPS: usize = 2;
const CUSTOM_NODE_TYPE_ALLOWLIST: [&str; 4] =
    ["python_custom", "rust_custom", "c_custom", "csharp_custom"];

thread_local! {
    static STATUS_STREAM: RefCell<Option<api::StatusWebSocket>> = const { RefCell::new(None) };
}

#[allow(dead_code)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum AutoLayoutDirection {
    LeftToRight,
    TopToBottom,
}

#[derive(Clone, Copy, Debug)]
struct AutoLayoutOptions {
    direction: AutoLayoutDirection,
    x_gap: f64,
    y_gap: f64,
    group_disconnected: bool,
    group_gap: f64,
    focus_selection_after_layout: bool,
}

impl Default for AutoLayoutOptions {
    fn default() -> Self {
        Self {
            direction: AutoLayoutDirection::LeftToRight,
            x_gap: AUTO_LAYOUT_X_GAP,
            y_gap: AUTO_LAYOUT_Y_GAP,
            group_disconnected: true,
            group_gap: AUTO_LAYOUT_GROUP_GAP,
            focus_selection_after_layout: true,
        }
    }
}

fn push_history_snapshot(stack: &mut Vec<Dataflow>, snapshot: Dataflow) {
    if stack.last().map(|last| last == &snapshot).unwrap_or(false) {
        return;
    }
    stack.push(snapshot);
    if stack.len() > HISTORY_LIMIT {
        let overflow = stack.len() - HISTORY_LIMIT;
        stack.drain(0..overflow);
    }
}

fn collect_selected_subflow(
    nodes: &[Node],
    connections: &[Connection],
    selected_node_ids: &[String],
) -> Option<Dataflow> {
    let selected: HashSet<&str> = selected_node_ids.iter().map(|id| id.as_str()).collect();
    if selected.is_empty() {
        return None;
    }

    let selected_nodes: Vec<Node> = nodes
        .iter()
        .filter(|node| selected.contains(node.id.as_str()))
        .cloned()
        .collect();
    if selected_nodes.is_empty() {
        return None;
    }

    let selected_connections: Vec<Connection> = connections
        .iter()
        .filter(|conn| selected.contains(conn.from.as_str()) && selected.contains(conn.to.as_str()))
        .cloned()
        .collect();

    Some(Dataflow {
        nodes: selected_nodes,
        connections: selected_connections,
    })
}

fn split_numeric_suffix(id: &str) -> (&str, Option<usize>) {
    let Some((prefix, suffix)) = id.rsplit_once('_') else {
        return (id, None);
    };
    if prefix.is_empty() {
        return (id, None);
    }
    match suffix.parse::<usize>() {
        Ok(value) => (prefix, Some(value)),
        Err(_) => (id, None),
    }
}

fn split_label_number_suffix(label: &str) -> (&str, Option<usize>) {
    let Some((prefix, suffix)) = label.rsplit_once(" #") else {
        return (label, None);
    };
    if prefix.trim().is_empty() {
        return (label, None);
    }
    match suffix.parse::<usize>() {
        Ok(value) => (prefix, Some(value)),
        Err(_) => (label, None),
    }
}

fn make_unique_pasted_node_id(source_id: &str, used: &mut HashSet<String>) -> String {
    let (series_prefix, source_suffix) = split_numeric_suffix(source_id);
    let numeric_prefix = format!("{}_", series_prefix);
    let mut max_suffix = source_suffix.unwrap_or(0usize);

    for existing_id in used.iter() {
        if let Some(rest) = existing_id.strip_prefix(&numeric_prefix) {
            if let Ok(value) = rest.parse::<usize>() {
                max_suffix = max_suffix.max(value);
            }
        }
    }

    let mut next_suffix = max_suffix.saturating_add(1);
    loop {
        let candidate = format!("{}_{}", series_prefix, next_suffix);
        if !used.contains(&candidate) {
            used.insert(candidate.clone());
            return candidate;
        }
        next_suffix = next_suffix.saturating_add(1);
    }
}

fn build_pasted_dataflow(clipboard: &Dataflow, existing_nodes: &[Node], offset: f64) -> Dataflow {
    let mut used_ids: HashSet<String> = existing_nodes.iter().map(|n| n.id.clone()).collect();
    let mut id_map = HashMap::<String, String>::new();

    let mut pasted_nodes = Vec::with_capacity(clipboard.nodes.len());
    for node in &clipboard.nodes {
        let new_id = make_unique_pasted_node_id(&node.id, &mut used_ids);
        id_map.insert(node.id.clone(), new_id.clone());

        let (_, label_index) = split_numeric_suffix(&new_id);
        let (raw_label_base, _) = split_label_number_suffix(node.label.trim());
        let label_base = if raw_label_base.trim().is_empty() {
            node.node_type.as_str()
        } else {
            raw_label_base.trim()
        };

        let mut cloned = node.clone();
        cloned.id = new_id;
        cloned.x += offset;
        cloned.y += offset;
        cloned.label = match label_index {
            Some(value) => format!("{} #{}", label_base, value),
            None => label_base.to_string(),
        };
        pasted_nodes.push(cloned);
    }

    let pasted_connections = clipboard
        .connections
        .iter()
        .filter_map(|conn| {
            let from = id_map.get(&conn.from)?;
            let to = id_map.get(&conn.to)?;
            let mut cloned = conn.clone();
            cloned.from = from.clone();
            cloned.to = to.clone();
            Some(cloned)
        })
        .collect();

    Dataflow {
        nodes: pasted_nodes,
        connections: pasted_connections,
    }
}

fn remove_selected_nodes(
    nodes: &mut Vec<Node>,
    connections: &mut Vec<Connection>,
    selected_node_ids: &[String],
) -> bool {
    let selected: HashSet<&str> = selected_node_ids.iter().map(|id| id.as_str()).collect();
    if selected.is_empty() {
        return false;
    }

    let before_nodes = nodes.len();
    nodes.retain(|node| !selected.contains(node.id.as_str()));
    if nodes.len() == before_nodes {
        return false;
    }

    connections.retain(|conn| {
        !selected.contains(conn.from.as_str()) && !selected.contains(conn.to.as_str())
    });
    true
}

fn duplicate_selected_subflow(
    nodes: &mut Vec<Node>,
    connections: &mut Vec<Connection>,
    selected_node_ids: &[String],
    offset: f64,
) -> Vec<String> {
    let Some(clipboard) = collect_selected_subflow(nodes, connections, selected_node_ids) else {
        return Vec::new();
    };
    if clipboard.nodes.is_empty() {
        return Vec::new();
    }

    let pasted = build_pasted_dataflow(&clipboard, nodes, offset);
    if pasted.nodes.is_empty() {
        return Vec::new();
    }

    let selected_ids: Vec<String> = pasted.nodes.iter().map(|node| node.id.clone()).collect();
    nodes.extend(pasted.nodes);
    connections.extend(pasted.connections);
    selected_ids
}

fn select_all_nodes(nodes: &[Node]) -> (Vec<String>, Option<String>) {
    let selected: Vec<String> = nodes.iter().map(|node| node.id.clone()).collect();
    let primary = selected.last().cloned();
    (selected, primary)
}

fn collect_weak_components(node_ids: &[String], connections: &[Connection]) -> Vec<Vec<String>> {
    if node_ids.is_empty() {
        return Vec::new();
    }

    let node_set: HashSet<&str> = node_ids.iter().map(|id| id.as_str()).collect();
    let mut adjacency = HashMap::<String, Vec<String>>::new();
    for id in node_ids {
        adjacency.insert(id.clone(), Vec::new());
    }

    for conn in connections {
        if conn.from == conn.to {
            continue;
        }
        if !node_set.contains(conn.from.as_str()) || !node_set.contains(conn.to.as_str()) {
            continue;
        }
        if let Some(list) = adjacency.get_mut(&conn.from) {
            list.push(conn.to.clone());
        }
        if let Some(list) = adjacency.get_mut(&conn.to) {
            list.push(conn.from.clone());
        }
    }

    let mut visited = HashSet::<String>::new();
    let mut sorted_ids = node_ids.to_vec();
    sorted_ids.sort();
    let mut components = Vec::<Vec<String>>::new();

    for start in sorted_ids {
        if visited.contains(&start) {
            continue;
        }
        let mut queue = VecDeque::<String>::from([start.clone()]);
        visited.insert(start.clone());
        let mut component = Vec::<String>::new();

        while let Some(current) = queue.pop_front() {
            component.push(current.clone());
            if let Some(neighbors) = adjacency.get(&current) {
                let mut sorted_neighbors = neighbors.clone();
                sorted_neighbors.sort();
                for neighbor in sorted_neighbors {
                    if visited.insert(neighbor.clone()) {
                        queue.push_back(neighbor);
                    }
                }
            }
        }

        component.sort();
        components.push(component);
    }

    components.sort_by(|a, b| a.first().cmp(&b.first()));
    components
}

fn build_component_positions(
    component_ids: &[String],
    connections: &[Connection],
    node_lookup: &HashMap<String, (f64, f64)>,
    origin_x: f64,
    origin_y: f64,
    options: AutoLayoutOptions,
) -> (HashMap<String, (f64, f64)>, f64, f64) {
    if component_ids.is_empty() {
        return (HashMap::new(), 0.0, 0.0);
    }

    let component_set: HashSet<&str> = component_ids.iter().map(|id| id.as_str()).collect();
    let mut indegree = HashMap::<String, usize>::new();
    let mut incoming = HashMap::<String, Vec<String>>::new();
    let mut outgoing = HashMap::<String, Vec<String>>::new();
    for id in component_ids {
        indegree.insert(id.clone(), 0);
        incoming.insert(id.clone(), Vec::new());
        outgoing.insert(id.clone(), Vec::new());
    }

    let mut seen_edges = HashSet::<(String, String)>::new();
    for conn in connections {
        if conn.from == conn.to {
            continue;
        }
        if component_set.contains(conn.from.as_str()) && component_set.contains(conn.to.as_str()) {
            let edge_key = (conn.from.clone(), conn.to.clone());
            if !seen_edges.insert(edge_key) {
                continue;
            }
            if let Some(list) = outgoing.get_mut(&conn.from) {
                list.push(conn.to.clone());
            }
            if let Some(list) = incoming.get_mut(&conn.to) {
                list.push(conn.from.clone());
            }
            if let Some(incoming) = indegree.get_mut(&conn.to) {
                *incoming += 1;
            }
        }
    }

    let mut zero_indegree: Vec<String> = indegree
        .iter()
        .filter_map(|(id, deg)| if *deg == 0 { Some(id.clone()) } else { None })
        .collect();
    zero_indegree.sort();

    let mut queue: VecDeque<String> = zero_indegree.into_iter().collect();
    let mut layer = HashMap::<String, usize>::new();
    while let Some(id) = queue.pop_front() {
        layer.entry(id.clone()).or_insert(0);
        let current_layer = layer.get(&id).copied().unwrap_or(0);
        if let Some(targets) = outgoing.get(&id) {
            let mut sorted_targets = targets.clone();
            sorted_targets.sort();
            for target in sorted_targets {
                if let Some(next_layer) = layer.get_mut(&target) {
                    *next_layer = (*next_layer).max(current_layer + 1);
                } else {
                    layer.insert(target.clone(), current_layer + 1);
                }

                if let Some(deg) = indegree.get_mut(&target) {
                    *deg = deg.saturating_sub(1);
                    if *deg == 0 {
                        queue.push_back(target);
                    }
                }
            }
        }
    }

    let mut max_layer = layer.values().copied().max().unwrap_or(0);
    let mut remaining_ids: Vec<String> = component_ids.to_vec();
    remaining_ids.sort();
    for id in remaining_ids {
        if !layer.contains_key(&id) {
            layer.insert(id, max_layer + 1);
            max_layer += 1;
        }
    }

    let mut rows_by_layer = BTreeMap::<usize, Vec<String>>::new();
    for id in component_ids {
        let node_layer = layer.get(id).copied().unwrap_or(0);
        rows_by_layer
            .entry(node_layer)
            .or_default()
            .push(id.clone());
    }

    for ids in rows_by_layer.values_mut() {
        ids.sort_by(|a, b| {
            let (ax, ay) = node_lookup.get(a).copied().unwrap_or((0.0, 0.0));
            let (bx, by) = node_lookup.get(b).copied().unwrap_or((0.0, 0.0));
            ay.total_cmp(&by)
                .then(ax.total_cmp(&bx))
                .then_with(|| a.cmp(b))
        });
    }
    reorder_rows_by_barycenter(
        &mut rows_by_layer,
        &incoming,
        &outgoing,
        &layer,
        node_lookup,
        options.direction,
    );

    let mut positions = HashMap::<String, (f64, f64)>::new();
    let mut max_rows = 0usize;
    for (layer_idx, ids) in rows_by_layer {
        max_rows = max_rows.max(ids.len());
        for (row_idx, id) in ids.into_iter().enumerate() {
            let (x, y) = match options.direction {
                AutoLayoutDirection::LeftToRight => (
                    origin_x + (layer_idx as f64 * options.x_gap),
                    origin_y + (row_idx as f64 * options.y_gap),
                ),
                AutoLayoutDirection::TopToBottom => (
                    origin_x + (row_idx as f64 * options.x_gap),
                    origin_y + (layer_idx as f64 * options.y_gap),
                ),
            };
            positions.insert(id, (x, y));
        }
    }

    let layer_count = layer.len().max(1);
    let row_count = max_rows.max(1);
    let width = match options.direction {
        AutoLayoutDirection::LeftToRight => (layer_count as f64) * options.x_gap,
        AutoLayoutDirection::TopToBottom => (row_count as f64) * options.x_gap,
    };
    let height = match options.direction {
        AutoLayoutDirection::LeftToRight => (row_count as f64) * options.y_gap,
        AutoLayoutDirection::TopToBottom => (layer_count as f64) * options.y_gap,
    };

    (positions, width, height)
}

fn compute_layer_order_index(
    rows_by_layer: &BTreeMap<usize, Vec<String>>,
) -> HashMap<String, usize> {
    let mut order = HashMap::<String, usize>::new();
    for ids in rows_by_layer.values() {
        for (idx, node_id) in ids.iter().enumerate() {
            order.insert(node_id.clone(), idx);
        }
    }
    order
}

fn neighbor_barycenter(
    node_id: &str,
    neighbor_map: &HashMap<String, Vec<String>>,
    target_layer: usize,
    layer_of: &HashMap<String, usize>,
    order_index: &HashMap<String, usize>,
) -> Option<f64> {
    let neighbors = neighbor_map.get(node_id)?;
    let mut acc = 0.0;
    let mut count = 0usize;

    for neighbor in neighbors {
        if layer_of.get(neighbor).copied() != Some(target_layer) {
            continue;
        }
        if let Some(order) = order_index.get(neighbor) {
            acc += *order as f64;
            count += 1;
        }
    }

    if count == 0 {
        None
    } else {
        Some(acc / count as f64)
    }
}

fn axis_value(
    node_id: &str,
    node_lookup: &HashMap<String, (f64, f64)>,
    direction: AutoLayoutDirection,
) -> f64 {
    let (x, y) = node_lookup.get(node_id).copied().unwrap_or((0.0, 0.0));
    match direction {
        AutoLayoutDirection::LeftToRight => y,
        AutoLayoutDirection::TopToBottom => x,
    }
}

fn reorder_rows_by_barycenter(
    rows_by_layer: &mut BTreeMap<usize, Vec<String>>,
    incoming: &HashMap<String, Vec<String>>,
    outgoing: &HashMap<String, Vec<String>>,
    layer_of: &HashMap<String, usize>,
    node_lookup: &HashMap<String, (f64, f64)>,
    direction: AutoLayoutDirection,
) {
    if rows_by_layer.len() <= 1 {
        return;
    }

    let mut max_layer = 0usize;
    if let Some(last_key) = rows_by_layer.keys().next_back() {
        max_layer = *last_key;
    }

    let mut order_index = compute_layer_order_index(rows_by_layer);
    for _ in 0..AUTO_LAYOUT_ORDER_SWEEPS {
        for layer_idx in 1..=max_layer {
            if let Some(ids) = rows_by_layer.get_mut(&layer_idx) {
                ids.sort_by(|a, b| {
                    let a_rank = neighbor_barycenter(
                        a,
                        incoming,
                        layer_idx.saturating_sub(1),
                        layer_of,
                        &order_index,
                    )
                    .unwrap_or_else(|| order_index.get(a).copied().unwrap_or(0) as f64);
                    let b_rank = neighbor_barycenter(
                        b,
                        incoming,
                        layer_idx.saturating_sub(1),
                        layer_of,
                        &order_index,
                    )
                    .unwrap_or_else(|| order_index.get(b).copied().unwrap_or(0) as f64);

                    a_rank
                        .total_cmp(&b_rank)
                        .then(axis_value(a, node_lookup, direction).total_cmp(&axis_value(
                            b,
                            node_lookup,
                            direction,
                        )))
                        .then_with(|| a.cmp(b))
                });
            }
            order_index = compute_layer_order_index(rows_by_layer);
        }

        for layer_idx in (0..max_layer).rev() {
            if let Some(ids) = rows_by_layer.get_mut(&layer_idx) {
                ids.sort_by(|a, b| {
                    let a_rank =
                        neighbor_barycenter(a, outgoing, layer_idx + 1, layer_of, &order_index)
                            .unwrap_or_else(|| order_index.get(a).copied().unwrap_or(0) as f64);
                    let b_rank =
                        neighbor_barycenter(b, outgoing, layer_idx + 1, layer_of, &order_index)
                            .unwrap_or_else(|| order_index.get(b).copied().unwrap_or(0) as f64);

                    a_rank
                        .total_cmp(&b_rank)
                        .then(axis_value(a, node_lookup, direction).total_cmp(&axis_value(
                            b,
                            node_lookup,
                            direction,
                        )))
                        .then_with(|| a.cmp(b))
                });
            }
            order_index = compute_layer_order_index(rows_by_layer);
        }
    }
}

fn apply_auto_layout(
    nodes: &mut [Node],
    connections: &[Connection],
    options: AutoLayoutOptions,
) -> bool {
    if nodes.len() <= 1 {
        return false;
    }

    let node_lookup: HashMap<String, (f64, f64)> = nodes
        .iter()
        .map(|node| (node.id.clone(), (node.x, node.y)))
        .collect();
    let mut positions = HashMap::<String, (f64, f64)>::new();

    let mut sorted_ids: Vec<String> = nodes.iter().map(|node| node.id.clone()).collect();
    sorted_ids.sort_by(|a, b| {
        let (ax, ay) = node_lookup.get(a).copied().unwrap_or((0.0, 0.0));
        let (bx, by) = node_lookup.get(b).copied().unwrap_or((0.0, 0.0));
        ay.total_cmp(&by)
            .then(ax.total_cmp(&bx))
            .then_with(|| a.cmp(b))
    });

    let components = if options.group_disconnected {
        collect_weak_components(&sorted_ids, connections)
    } else {
        vec![sorted_ids]
    };

    let mut cursor_x = AUTO_LAYOUT_START_X;
    let mut cursor_y = AUTO_LAYOUT_START_Y;
    for component in components {
        let (component_positions, width, height) = build_component_positions(
            &component,
            connections,
            &node_lookup,
            cursor_x,
            cursor_y,
            options,
        );
        positions.extend(component_positions);

        if options.group_disconnected {
            match options.direction {
                AutoLayoutDirection::LeftToRight => {
                    cursor_y += height + options.group_gap;
                }
                AutoLayoutDirection::TopToBottom => {
                    cursor_x += width + options.group_gap;
                }
            }
        }
    }

    let mut changed = false;
    for node in nodes.iter_mut() {
        if let Some((new_x, new_y)) = positions.get(&node.id) {
            if (node.x - *new_x).abs() > f64::EPSILON || (node.y - *new_y).abs() > f64::EPSILON {
                node.x = *new_x;
                node.y = *new_y;
                changed = true;
            }
        }
    }

    changed
}

fn map_node_runtime_states(status: &api::DataflowStatusResponse) -> HashMap<String, NodeState> {
    status
        .node_details
        .iter()
        .map(|detail| {
            let state = if detail.is_running {
                NodeState::Running
            } else {
                NodeState::Stopped
            };
            (detail.id.clone(), state)
        })
        .collect()
}

fn apply_status_snapshot(
    status: &api::DataflowStatusResponse,
    set_total_nodes: WriteSignal<usize>,
    set_running_nodes: WriteSignal<usize>,
    set_error_nodes: WriteSignal<usize>,
    set_node_runtime_states: WriteSignal<HashMap<String, NodeState>>,
) {
    set_total_nodes.set(status.total_nodes);
    set_running_nodes.set(status.running_nodes);
    set_error_nodes.set(status.error_nodes);
    set_node_runtime_states.set(map_node_runtime_states(status));
}

fn disconnect_status_stream() {
    STATUS_STREAM.with(|stream_ref| {
        let mut guard = stream_ref.borrow_mut();
        if let Some(stream) = guard.as_mut() {
            stream.close();
        }
        *guard = None;
    });
}

fn connect_status_stream(
    process_id: String,
    set_is_running: WriteSignal<bool>,
    set_total_nodes: WriteSignal<usize>,
    set_running_nodes: WriteSignal<usize>,
    set_error_nodes: WriteSignal<usize>,
    set_node_runtime_states: WriteSignal<HashMap<String, NodeState>>,
) {
    disconnect_status_stream();

    let mut ws = api::StatusWebSocket::new();
    match ws.connect(&process_id) {
        Ok(()) => {
            ws.set_on_message({
                let set_total_nodes = set_total_nodes;
                let set_running_nodes = set_running_nodes;
                let set_error_nodes = set_error_nodes;
                let set_node_runtime_states = set_node_runtime_states;
                let set_is_running = set_is_running;
                move |status| {
                    apply_status_snapshot(
                        &status,
                        set_total_nodes,
                        set_running_nodes,
                        set_error_nodes,
                        set_node_runtime_states,
                    );
                    if matches!(status.status.as_str(), "stopped" | "not_found") {
                        set_is_running.set(false);
                    }
                }
            });
            ws.set_on_error(move |err| {
                log::warn!("status stream error: {}", err);
            });
            ws.set_on_close(move || {
                log::info!("status stream closed");
            });
            STATUS_STREAM.with(|stream_ref| {
                *stream_ref.borrow_mut() = Some(ws);
            });
        }
        Err(err) => {
            log::warn!("failed to connect status stream: {}", err);
        }
    }
}

fn start_status_polling(
    process_id: String,
    set_is_running: WriteSignal<bool>,
    set_total_nodes: WriteSignal<usize>,
    set_running_nodes: WriteSignal<usize>,
    set_error_nodes: WriteSignal<usize>,
    set_node_runtime_states: WriteSignal<HashMap<String, NodeState>>,
) {
    spawn_local(async move {
        loop {
            let promise = js_sys::Promise::new(&mut |resolve, _reject| {
                web_sys::window()
                    .unwrap()
                    .set_timeout_with_callback_and_timeout_and_arguments_0(&resolve, 2000)
                    .unwrap();
            });
            let _ = wasm_bindgen_futures::JsFuture::from(promise).await;

            match api::get_dataflow_status(&process_id).await {
                Ok(status) => {
                    apply_status_snapshot(
                        &status,
                        set_total_nodes,
                        set_running_nodes,
                        set_error_nodes,
                        set_node_runtime_states,
                    );

                    match status.status.as_str() {
                        "running" => {}
                        "stopped" | "not_found" => {
                            log::info!("process status: {}", status.status);
                            set_is_running.set(false);
                            break;
                        }
                        _ => {}
                    }
                }
                Err(_) => {
                    set_is_running.set(false);
                    set_node_runtime_states.set(HashMap::new());
                    break;
                }
            }
        }
    });
}

fn start_uptime_timer(
    set_uptime: WriteSignal<u64>,
    start_time: ReadSignal<Option<f64>>,
    is_running: ReadSignal<bool>,
) {
    spawn_local(async move {
        loop {
            let promise = js_sys::Promise::new(&mut |resolve, _reject| {
                web_sys::window()
                    .unwrap()
                    .set_timeout_with_callback_and_timeout_and_arguments_0(&resolve, 1000)
                    .unwrap();
            });
            let _ = wasm_bindgen_futures::JsFuture::from(promise).await;

            if is_running.get_untracked() {
                if let Some(st) = start_time.get_untracked() {
                    let now = js_sys::Date::now();
                    let elapsed = ((now - st) / 1000.0) as u64;
                    set_uptime.set(elapsed);
                }
            }
        }
    });
}

fn parent_dir_from_path(path: &str) -> Option<String> {
    path.rfind(['\\', '/'])
        .filter(|idx| *idx > 0)
        .map(|idx| path[..idx].to_string())
}

fn file_name_from_path(path: &str) -> String {
    path.rsplit(['\\', '/'])
        .next()
        .filter(|name| !name.is_empty())
        .unwrap_or(path)
        .to_string()
}

fn is_probably_absolute_path(path: &str) -> bool {
    path.starts_with('/')
        || path.starts_with("\\\\")
        || path
            .as_bytes()
            .get(1)
            .map(|ch| *ch == b':')
            .unwrap_or(false)
}

fn should_record_recent_file_for_open(file_path: Option<&str>) -> bool {
    file_path.map(is_probably_absolute_path).unwrap_or(false)
}

fn browser_fallback_recent_files_notice() -> &'static str {
    "已切换到浏览器文件选择器；该模式无法获取绝对路径，最近文件列表不会新增此文件。"
}

fn is_missing_file_message(message: &str) -> bool {
    let lowered = message.to_ascii_lowercase();
    lowered.contains("not found")
        || lowered.contains("cannot find")
        || lowered.contains("no such file")
}

fn should_remove_recent_file_on_open_recent_failure(
    message: &str,
    error_code: Option<&str>,
) -> bool {
    matches!(error_code, Some("FILE_READ_FAILED")) || is_missing_file_message(message)
}

fn sanitize_template_id(raw: &str) -> String {
    let mut out = String::new();
    for ch in raw.chars() {
        if ch.is_ascii_alphanumeric() || ch == '_' || ch == '-' {
            out.push(ch);
        } else {
            out.push('_');
        }
    }

    let collapsed = out
        .split('_')
        .filter(|part| !part.is_empty())
        .collect::<Vec<_>>()
        .join("_");
    if collapsed.is_empty() {
        "template".to_string()
    } else {
        collapsed
    }
}

fn node_type_to_display_name(node_type: &str) -> String {
    let parts: Vec<String> = node_type
        .split(|ch: char| ch == '_' || ch == '-' || ch == '/' || ch.is_whitespace())
        .filter(|part| !part.is_empty())
        .map(|part| {
            let mut chars = part.chars();
            if let Some(first) = chars.next() {
                let mut word = String::new();
                word.push(first.to_ascii_uppercase());
                word.push_str(chars.as_str());
                word
            } else {
                String::new()
            }
        })
        .collect();

    if parts.is_empty() {
        node_type.to_string()
    } else {
        parts.join(" ")
    }
}

fn icon_for_node_type(node_type: &str, path: Option<&str>) -> String {
    let lowered_node_type = node_type.to_ascii_lowercase();
    let lowered_path = path.unwrap_or_default().to_ascii_lowercase();

    if lowered_node_type.contains("python") || lowered_path.contains("python") {
        "🐍".to_string()
    } else if lowered_node_type.contains("rust") || lowered_path.contains("rust") {
        "🦀".to_string()
    } else if lowered_node_type.contains("csharp")
        || lowered_node_type.contains("c#")
        || lowered_path.contains("csharp")
    {
        "💠".to_string()
    } else if lowered_node_type == "c_custom"
        || lowered_node_type.contains("cpp")
        || lowered_node_type.contains("c++")
    {
        "⚙".to_string()
    } else {
        "🔧".to_string()
    }
}

fn normalize_template_ports(
    ports: Option<Vec<String>>,
    split_input_mapping: bool,
) -> Option<Vec<String>> {
    let Some(ports) = ports else {
        return None;
    };

    let mut seen = HashSet::<String>::new();
    let mut normalized = Vec::<String>::new();

    for raw in ports {
        let mut value = raw.trim().to_string();
        if split_input_mapping {
            if let Some((port_name, _)) = value.split_once(':') {
                value = port_name.trim().to_string();
            }
        }

        if value.is_empty() {
            continue;
        }

        let lowered = value.to_ascii_lowercase();
        if seen.insert(lowered) {
            normalized.push(value);
        }
    }

    if normalized.is_empty() {
        None
    } else {
        Some(normalized)
    }
}

fn merge_ports(base: Option<Vec<String>>, incoming: Option<Vec<String>>) -> Option<Vec<String>> {
    let mut seen = HashSet::<String>::new();
    let mut merged = Vec::<String>::new();

    for value in base
        .unwrap_or_default()
        .into_iter()
        .chain(incoming.unwrap_or_default())
    {
        let key = value.to_ascii_lowercase();
        if seen.insert(key) {
            merged.push(value);
        }
    }

    if merged.is_empty() {
        None
    } else {
        Some(merged)
    }
}

fn normalize_node_template(mut template: NodeTemplate) -> Option<NodeTemplate> {
    let node_type = template.node_type.trim().to_string();
    if node_type.is_empty() {
        return None;
    }
    template.node_type = node_type.clone();

    template.id = if template.id.trim().is_empty() {
        format!("tpl_{}", sanitize_template_id(&node_type))
    } else {
        sanitize_template_id(template.id.trim())
    };

    template.name = if template.name.trim().is_empty() {
        node_type_to_display_name(&node_type)
    } else {
        template.name.trim().to_string()
    };

    template.description = if template.description.trim().is_empty() {
        "自动收集的节点类型".to_string()
    } else {
        template.description.trim().to_string()
    };

    template.icon = if template.icon.trim().is_empty() {
        icon_for_node_type(&node_type, template.path.as_deref())
    } else {
        template.icon.trim().to_string()
    };

    template.path = template.path.and_then(|path| {
        let trimmed = path.trim();
        if trimmed.is_empty() {
            None
        } else {
            Some(trimmed.to_string())
        }
    });

    template.inputs = normalize_template_ports(template.inputs, true);
    template.outputs = normalize_template_ports(template.outputs, false);

    Some(template)
}

fn merge_node_template(existing: &NodeTemplate, incoming: &NodeTemplate) -> NodeTemplate {
    let mut merged = existing.clone();
    merged.id = incoming.id.clone();
    merged.name = incoming.name.clone();
    merged.description = incoming.description.clone();
    merged.category = incoming.category;
    merged.icon = incoming.icon.clone();
    merged.path = incoming.path.clone().or_else(|| existing.path.clone());
    merged.inputs = merge_ports(existing.inputs.clone(), incoming.inputs.clone());
    merged.outputs = merge_ports(existing.outputs.clone(), incoming.outputs.clone());
    merged
}

fn upsert_template(templates_by_type: &mut BTreeMap<String, NodeTemplate>, template: NodeTemplate) {
    let Some(normalized) = normalize_node_template(template) else {
        return;
    };

    match templates_by_type.get(&normalized.node_type).cloned() {
        Some(existing) => {
            templates_by_type.insert(
                normalized.node_type.clone(),
                merge_node_template(&existing, &normalized),
            );
        }
        None => {
            templates_by_type.insert(normalized.node_type.clone(), normalized);
        }
    }
}

fn collect_yaml_node_templates(nodes: &[Node]) -> Vec<NodeTemplate> {
    let mut templates_by_type = BTreeMap::<String, NodeTemplate>::new();

    for node in nodes {
        let node_type = node.node_type.trim().to_string();
        if node_type.is_empty() {
            continue;
        }

        let path = node.path.as_ref().map(|path| path.trim().to_string());
        let description = path
            .as_ref()
            .filter(|path| !path.is_empty())
            .map(|path| format!("来自 YAML: {}", path))
            .unwrap_or_else(|| "来自 YAML 自动收集".to_string());

        let template = NodeTemplate {
            id: format!("yaml_{}", sanitize_template_id(&node_type)),
            name: node_type_to_display_name(&node_type),
            description,
            category: NodeCategory::Custom,
            node_type: node_type.clone(),
            icon: icon_for_node_type(&node_type, path.as_deref()),
            path,
            inputs: node.inputs.clone(),
            outputs: node.outputs.clone(),
        };
        upsert_template(&mut templates_by_type, template);
    }

    templates_by_type.into_values().collect()
}

fn built_in_custom_node_templates() -> Vec<NodeTemplate> {
    let allow_set: HashSet<&str> = CUSTOM_NODE_TYPE_ALLOWLIST.iter().copied().collect();
    let mut templates = Vec::<NodeTemplate>::new();

    for def in NODE_REGISTRY.get_by_category(NodeCategory::Custom) {
        if !allow_set.contains(def.node_type.as_str()) {
            continue;
        }
        templates.push(NodeTemplate::from(def));
    }

    templates
}

fn merge_node_template_sources(
    builtin_templates: &[NodeTemplate],
    persisted_templates: &[NodeTemplate],
    current_yaml_templates: &[NodeTemplate],
) -> Vec<NodeTemplate> {
    let mut templates_by_type = BTreeMap::<String, NodeTemplate>::new();

    for template in builtin_templates {
        upsert_template(&mut templates_by_type, template.clone());
    }

    for template in persisted_templates {
        upsert_template(&mut templates_by_type, template.clone());
    }

    for template in current_yaml_templates {
        upsert_template(&mut templates_by_type, template.clone());
    }

    templates_by_type.into_values().collect()
}

fn node_templates_to_config_entries(
    templates: &[NodeTemplate],
) -> Vec<api::NodeTemplateConfigEntry> {
    templates
        .iter()
        .map(|template| api::NodeTemplateConfigEntry {
            node_type: template.node_type.clone(),
            name: template.name.clone(),
            description: template.description.clone(),
            icon: template.icon.clone(),
            path: template.path.clone(),
            inputs: template.inputs.clone(),
            outputs: template.outputs.clone(),
        })
        .collect()
}

fn config_entries_to_node_templates(
    entries: Vec<api::NodeTemplateConfigEntry>,
) -> Vec<NodeTemplate> {
    let mut templates_by_type = BTreeMap::<String, NodeTemplate>::new();

    for entry in entries {
        let template = NodeTemplate {
            id: format!("cfg_{}", sanitize_template_id(&entry.node_type)),
            name: entry.name,
            description: entry.description,
            category: NodeCategory::Custom,
            node_type: entry.node_type,
            icon: entry.icon,
            path: entry.path,
            inputs: entry.inputs,
            outputs: entry.outputs,
        };
        upsert_template(&mut templates_by_type, template);
    }

    templates_by_type.into_values().collect()
}

fn normalize_working_dir_select_error(err: &str) -> String {
    let lowered = err.to_ascii_lowercase();
    if lowered.contains("cannot connect to localagent")
        || lowered.contains("failed to fetch")
        || lowered.contains("networkerror")
    {
        "无法浏览目录：LocalAgent 未启动。请先启动 doramate-localagent，或手动输入工作目录绝对路径。"
            .to_string()
    } else {
        format!("目录选择失败: {}", err)
    }
}

fn should_fallback_to_browser_picker_after_open_result(
    result: &Result<api::OpenDataflowFileResponse, String>,
) -> bool {
    match result {
        Ok(resp) => !resp.cancelled && (!resp.success || resp.content.is_none()),
        Err(_) => true,
    }
}

fn should_open_manual_working_dir_dialog(
    result: &Result<api::SelectDirectoryResponse, String>,
) -> bool {
    match result {
        Ok(resp) => !resp.success,
        Err(_) => true,
    }
}

fn working_dir_error_message_for_select_result(
    result: &Result<api::SelectDirectoryResponse, String>,
) -> Option<String> {
    match result {
        Ok(resp) if !resp.success && !resp.cancelled => Some(api::friendly_error_message(
            resp.error_code.as_deref(),
            &normalize_working_dir_select_error(&resp.message),
        )),
        Err(err) => Some(normalize_working_dir_select_error(err)),
        _ => None,
    }
}

#[cfg(test)]
mod open_flow_state_tests {
    use super::*;
    use std::collections::HashSet;

    fn open_resp(
        success: bool,
        cancelled: bool,
        content: Option<&str>,
    ) -> api::OpenDataflowFileResponse {
        api::OpenDataflowFileResponse {
            success,
            cancelled,
            file_path: None,
            file_name: None,
            working_dir: None,
            content: content.map(|s| s.to_string()),
            message: String::new(),
            error_code: None,
        }
    }

    fn select_resp(
        success: bool,
        cancelled: bool,
        path: Option<&str>,
        message: &str,
        error_code: Option<&str>,
    ) -> api::SelectDirectoryResponse {
        api::SelectDirectoryResponse {
            success,
            cancelled,
            path: path.map(|s| s.to_string()),
            message: message.to_string(),
            error_code: error_code.map(|s| s.to_string()),
        }
    }

    fn sample_dataflow(tag: &str) -> Dataflow {
        Dataflow {
            nodes: vec![Node {
                id: format!("node_{}", tag),
                x: 1.0,
                y: 2.0,
                label: format!("Node {}", tag),
                node_type: "mock".to_string(),
                path: None,
                env: None,
                config: None,
                outputs: None,
                inputs: None,
                scale: Some(1.0),
            }],
            connections: vec![],
        }
    }

    fn sample_node(id: &str, x: f64, y: f64) -> Node {
        Node {
            id: id.to_string(),
            x,
            y,
            label: id.to_string(),
            node_type: "mock".to_string(),
            path: None,
            env: None,
            config: None,
            outputs: Some(vec!["out".to_string()]),
            inputs: Some(vec!["in".to_string()]),
            scale: Some(1.0),
        }
    }

    #[test]
    fn test_should_fallback_to_browser_picker_after_open_result() {
        let ok_with_content = Ok(open_resp(true, false, Some("nodes: []")));
        assert!(!should_fallback_to_browser_picker_after_open_result(
            &ok_with_content
        ));

        let ok_without_content = Ok(open_resp(true, false, None));
        assert!(should_fallback_to_browser_picker_after_open_result(
            &ok_without_content
        ));

        let open_failed = Ok(open_resp(false, false, None));
        assert!(should_fallback_to_browser_picker_after_open_result(
            &open_failed
        ));

        let cancelled = Ok(open_resp(false, true, None));
        assert!(!should_fallback_to_browser_picker_after_open_result(
            &cancelled
        ));

        let request_err: Result<api::OpenDataflowFileResponse, String> =
            Err("cannot connect".to_string());
        assert!(should_fallback_to_browser_picker_after_open_result(
            &request_err
        ));
    }

    #[test]
    fn test_should_open_manual_working_dir_dialog_after_select_directory() {
        let success = Ok(select_resp(true, false, Some("C:\\work"), "ok", None));
        assert!(!should_open_manual_working_dir_dialog(&success));

        let cancelled = Ok(select_resp(false, true, None, "cancelled", None));
        assert!(should_open_manual_working_dir_dialog(&cancelled));

        let failed = Ok(select_resp(false, false, None, "failed", None));
        assert!(should_open_manual_working_dir_dialog(&failed));

        let request_err: Result<api::SelectDirectoryResponse, String> =
            Err("cannot connect to LocalAgent".to_string());
        assert!(should_open_manual_working_dir_dialog(&request_err));
    }

    #[test]
    fn test_working_dir_error_message_for_select_result() {
        let cancelled = Ok(select_resp(false, true, None, "cancelled", None));
        assert!(working_dir_error_message_for_select_result(&cancelled).is_none());

        let failed = Ok(select_resp(false, false, None, "bad path", None));
        let failed_msg =
            working_dir_error_message_for_select_result(&failed).expect("failed message");
        assert!(failed_msg.contains("目录选择失败"));

        let failed_with_code = Ok(select_resp(
            false,
            false,
            None,
            "raw backend picker error",
            Some("DIRECTORY_PICKER_FAILED"),
        ));
        let failed_with_code_msg = working_dir_error_message_for_select_result(&failed_with_code)
            .expect("failed message with error code");
        assert!(failed_with_code_msg.contains("Failed to open directory picker in LocalAgent."));

        let request_err: Result<api::SelectDirectoryResponse, String> =
            Err("cannot connect to LocalAgent".to_string());
        let request_err_msg =
            working_dir_error_message_for_select_result(&request_err).expect("request err message");
        assert!(request_err_msg.contains("LocalAgent"));
    }

    #[test]
    fn test_normalize_working_dir_select_error_network_message() {
        let msg = normalize_working_dir_select_error(
            "select directory failed: cannot connect to LocalAgent at http://127.0.0.1:52100/api",
        );
        assert!(msg.contains("LocalAgent 未启动"));
    }

    #[test]
    fn test_should_remove_recent_file_on_open_recent_failure() {
        assert!(should_remove_recent_file_on_open_recent_failure(
            "No such file or directory",
            None,
        ));
        assert!(should_remove_recent_file_on_open_recent_failure(
            "cannot find the file specified",
            None,
        ));
        assert!(should_remove_recent_file_on_open_recent_failure(
            "read failed",
            Some("FILE_READ_FAILED"),
        ));
        assert!(!should_remove_recent_file_on_open_recent_failure(
            "permission denied",
            None,
        ));
        assert!(!should_remove_recent_file_on_open_recent_failure(
            "dora start timeout",
            Some("DORA_START_TIMEOUT"),
        ));
    }

    #[test]
    fn test_should_record_recent_file_for_open_path() {
        assert!(should_record_recent_file_for_open(Some(
            "C:\\Users\\Administrator\\projects\\dora-yolo-rust\\dataflow.yml"
        )));
        assert!(!should_record_recent_file_for_open(Some("dataflow.yml")));
        assert!(!should_record_recent_file_for_open(None));
    }

    #[test]
    fn test_browser_fallback_recent_files_notice_mentions_recent_list() {
        let msg = browser_fallback_recent_files_notice();
        assert!(msg.contains("最近文件"));
        assert!(msg.contains("不会新增"));
    }

    #[test]
    fn test_collect_yaml_node_templates_merges_duplicate_node_types() {
        let nodes = vec![
            Node {
                id: "n1".to_string(),
                x: 0.0,
                y: 0.0,
                label: "n1".to_string(),
                node_type: "python_custom".to_string(),
                path: Some(" ./process.py ".to_string()),
                env: None,
                config: None,
                inputs: Some(vec![
                    " image ".to_string(),
                    "IMAGE".to_string(),
                    "".to_string(),
                ]),
                outputs: Some(vec![" result ".to_string()]),
                scale: Some(1.0),
            },
            Node {
                id: "n2".to_string(),
                x: 10.0,
                y: 10.0,
                label: "n2".to_string(),
                node_type: "python_custom".to_string(),
                path: None,
                env: None,
                config: None,
                inputs: Some(vec!["meta".to_string()]),
                outputs: Some(vec!["RESULT".to_string(), "bbox".to_string()]),
                scale: Some(1.0),
            },
        ];

        let templates = collect_yaml_node_templates(&nodes);
        assert_eq!(templates.len(), 1);

        let template = &templates[0];
        assert_eq!(template.node_type, "python_custom");
        assert_eq!(template.path.as_deref(), Some("./process.py"));
        assert_eq!(
            template.inputs,
            Some(vec!["image".to_string(), "meta".to_string()])
        );
        assert_eq!(
            template.outputs,
            Some(vec!["result".to_string(), "bbox".to_string()])
        );
    }

    #[test]
    fn test_config_entries_to_node_templates_normalizes_and_upserts() {
        let entries = vec![
            api::NodeTemplateConfigEntry {
                node_type: "python_custom".to_string(),
                name: "Legacy Python".to_string(),
                description: "legacy".to_string(),
                icon: "".to_string(),
                path: Some(" ./old.py ".to_string()),
                inputs: Some(vec!["image".to_string(), "IMAGE".to_string()]),
                outputs: Some(vec!["result".to_string()]),
            },
            api::NodeTemplateConfigEntry {
                node_type: " python_custom ".to_string(),
                name: " Python Custom ".to_string(),
                description: " latest ".to_string(),
                icon: " 🐍 ".to_string(),
                path: Some("  ".to_string()),
                inputs: Some(vec!["meta".to_string()]),
                outputs: None,
            },
        ];

        let templates = config_entries_to_node_templates(entries);
        assert_eq!(templates.len(), 1);

        let template = &templates[0];
        assert_eq!(template.id, "cfg_python_custom");
        assert_eq!(template.category, NodeCategory::Custom);
        assert_eq!(template.node_type, "python_custom");
        assert_eq!(template.name, "Python Custom");
        assert_eq!(template.description, "latest");
        assert_eq!(template.icon, "🐍");
        assert_eq!(template.path.as_deref(), Some("./old.py"));
        assert_eq!(
            template.inputs,
            Some(vec!["image".to_string(), "meta".to_string()])
        );
        assert_eq!(template.outputs, Some(vec!["result".to_string()]));
    }

    #[test]
    fn test_push_history_snapshot_deduplicates_tail_entry() {
        let mut history = vec![sample_dataflow("a")];
        let duplicate = sample_dataflow("a");
        push_history_snapshot(&mut history, duplicate);
        assert_eq!(history.len(), 1);
    }

    #[test]
    fn test_push_history_snapshot_limits_history_size() {
        let mut history = Vec::new();
        for i in 0..(HISTORY_LIMIT + 5) {
            push_history_snapshot(&mut history, sample_dataflow(&i.to_string()));
        }
        assert_eq!(history.len(), HISTORY_LIMIT);
    }

    #[test]
    fn test_collect_selected_subflow_keeps_only_internal_connections() {
        let nodes = vec![
            sample_node("a", 0.0, 0.0),
            sample_node("b", 100.0, 0.0),
            sample_node("c", 200.0, 0.0),
        ];
        let connections = vec![
            Connection {
                from: "a".to_string(),
                to: "b".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
            Connection {
                from: "b".to_string(),
                to: "c".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
        ];

        let selected = vec!["a".to_string(), "b".to_string()];
        let subflow =
            collect_selected_subflow(&nodes, &connections, &selected).expect("subflow exists");

        assert_eq!(subflow.nodes.len(), 2);
        assert_eq!(subflow.connections.len(), 1);
        assert_eq!(subflow.connections[0].from, "a");
        assert_eq!(subflow.connections[0].to, "b");
    }

    #[test]
    fn test_build_pasted_dataflow_remaps_ids_and_offsets_nodes() {
        let clipboard = Dataflow {
            nodes: vec![
                sample_node("node_a", 10.0, 20.0),
                sample_node("node_b", 40.0, 80.0),
            ],
            connections: vec![Connection {
                from: "node_a".to_string(),
                to: "node_b".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            }],
        };
        let existing_nodes = vec![
            sample_node("node_a_1", 0.0, 0.0),
            sample_node("node_b_1", 0.0, 0.0),
        ];

        let pasted = build_pasted_dataflow(&clipboard, &existing_nodes, 32.0);

        assert_eq!(pasted.nodes.len(), 2);
        assert_eq!(pasted.connections.len(), 1);

        let pasted_ids: HashSet<String> = pasted.nodes.iter().map(|n| n.id.clone()).collect();
        assert_eq!(pasted_ids.len(), 2);
        assert!(!pasted_ids.contains("node_a"));
        assert!(!pasted_ids.contains("node_b"));
        assert!(pasted_ids.contains("node_a_2"));
        assert!(pasted_ids.contains("node_b_2"));

        let pasted_a = pasted
            .nodes
            .iter()
            .find(|n| n.label == "node_a #2")
            .expect("copied node_a");
        assert_eq!(pasted_a.x, 42.0);
        assert_eq!(pasted_a.y, 52.0);

        let pasted_conn = &pasted.connections[0];
        assert!(pasted_ids.contains(&pasted_conn.from));
        assert!(pasted_ids.contains(&pasted_conn.to));
    }

    #[test]
    fn test_remove_selected_nodes_prunes_related_connections() {
        let mut nodes = vec![
            sample_node("a", 0.0, 0.0),
            sample_node("b", 100.0, 0.0),
            sample_node("c", 200.0, 0.0),
        ];
        let mut connections = vec![
            Connection {
                from: "a".to_string(),
                to: "b".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
            Connection {
                from: "b".to_string(),
                to: "c".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
        ];
        let selected = vec!["b".to_string()];

        let changed = remove_selected_nodes(&mut nodes, &mut connections, &selected);

        assert!(changed);
        assert_eq!(nodes.len(), 2);
        assert!(nodes.iter().all(|n| n.id != "b"));
        assert!(connections.is_empty());
    }

    #[test]
    fn test_duplicate_selected_subflow_creates_new_ids() {
        let mut nodes = vec![sample_node("a", 10.0, 20.0), sample_node("b", 60.0, 40.0)];
        let mut connections = vec![Connection {
            from: "a".to_string(),
            to: "b".to_string(),
            from_port: Some("out".to_string()),
            to_port: Some("in".to_string()),
        }];
        let selected = vec!["a".to_string(), "b".to_string()];

        let duplicated =
            duplicate_selected_subflow(&mut nodes, &mut connections, &selected, DUPLICATE_OFFSET);

        assert_eq!(duplicated.len(), 2);
        assert_eq!(nodes.len(), 4);
        assert_eq!(connections.len(), 2);
        assert!(duplicated.iter().all(|id| id.rsplit_once('_').is_some()));
        assert!(duplicated.iter().all(|id| {
            id.rsplit_once('_')
                .map(|(_, suffix)| suffix.parse::<usize>().is_ok())
                .unwrap_or(false)
        }));
    }

    #[test]
    fn test_duplicate_selected_subflow_increments_numeric_suffix_series() {
        let mut nodes = vec![
            sample_node("python_custom_1", 10.0, 20.0),
            sample_node("python_custom_2", 60.0, 40.0),
        ];
        let mut connections = Vec::new();
        let selected = vec!["python_custom_1".to_string()];

        let duplicated =
            duplicate_selected_subflow(&mut nodes, &mut connections, &selected, DUPLICATE_OFFSET);

        assert_eq!(duplicated, vec!["python_custom_3".to_string()]);
    }

    #[test]
    fn test_select_all_nodes_returns_all_ids_with_last_as_primary() {
        let nodes = vec![
            sample_node("a", 0.0, 0.0),
            sample_node("b", 0.0, 0.0),
            sample_node("c", 0.0, 0.0),
        ];
        let (selected, primary) = select_all_nodes(&nodes);

        assert_eq!(
            selected,
            vec!["a".to_string(), "b".to_string(), "c".to_string()]
        );
        assert_eq!(primary, Some("c".to_string()));
    }

    #[test]
    fn test_apply_auto_layout_places_downstream_nodes_to_the_right() {
        let mut nodes = vec![
            sample_node("camera", 500.0, 100.0),
            sample_node("detector", 100.0, 300.0),
            sample_node("sink", 50.0, 30.0),
        ];
        let connections = vec![
            Connection {
                from: "camera".to_string(),
                to: "detector".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
            Connection {
                from: "detector".to_string(),
                to: "sink".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
        ];

        let changed = apply_auto_layout(&mut nodes, &connections, AutoLayoutOptions::default());

        assert!(changed);
        let camera = nodes.iter().find(|n| n.id == "camera").expect("camera");
        let detector = nodes.iter().find(|n| n.id == "detector").expect("detector");
        let sink = nodes.iter().find(|n| n.id == "sink").expect("sink");
        assert!(detector.x > camera.x);
        assert!(sink.x > detector.x);
    }

    #[test]
    fn test_apply_auto_layout_top_to_bottom_places_downstream_lower() {
        let mut nodes = vec![
            sample_node("camera", 500.0, 100.0),
            sample_node("detector", 100.0, 300.0),
            sample_node("sink", 50.0, 30.0),
        ];
        let connections = vec![
            Connection {
                from: "camera".to_string(),
                to: "detector".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
            Connection {
                from: "detector".to_string(),
                to: "sink".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
        ];
        let options = AutoLayoutOptions {
            direction: AutoLayoutDirection::TopToBottom,
            ..AutoLayoutOptions::default()
        };

        let changed = apply_auto_layout(&mut nodes, &connections, options);

        assert!(changed);
        let camera = nodes.iter().find(|n| n.id == "camera").expect("camera");
        let detector = nodes.iter().find(|n| n.id == "detector").expect("detector");
        let sink = nodes.iter().find(|n| n.id == "sink").expect("sink");
        assert!(detector.y > camera.y);
        assert!(sink.y > detector.y);
    }

    #[test]
    fn test_apply_auto_layout_groups_disconnected_components() {
        let mut nodes = vec![
            sample_node("a", 5.0, 5.0),
            sample_node("b", 15.0, 15.0),
            sample_node("x", 25.0, 25.0),
            sample_node("y", 35.0, 35.0),
        ];
        let connections = vec![
            Connection {
                from: "a".to_string(),
                to: "b".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
            Connection {
                from: "x".to_string(),
                to: "y".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
        ];
        let options = AutoLayoutOptions {
            group_disconnected: true,
            ..AutoLayoutOptions::default()
        };

        let changed = apply_auto_layout(&mut nodes, &connections, options);

        assert!(changed);
        let a = nodes.iter().find(|n| n.id == "a").expect("a");
        let x = nodes.iter().find(|n| n.id == "x").expect("x");
        assert!(x.y > a.y);
    }

    #[test]
    fn test_apply_auto_layout_returns_false_for_single_node() {
        let mut nodes = vec![sample_node("solo", 12.0, 34.0)];
        let connections: Vec<Connection> = vec![];

        let changed = apply_auto_layout(&mut nodes, &connections, AutoLayoutOptions::default());

        assert!(!changed);
        assert_eq!(nodes[0].x, 12.0);
        assert_eq!(nodes[0].y, 34.0);
    }

    #[test]
    fn test_apply_auto_layout_reorders_layer_to_reduce_crossing() {
        let mut nodes = vec![
            sample_node("a", 0.0, 0.0),
            sample_node("b", 0.0, 120.0),
            sample_node("c", 300.0, 0.0),
            sample_node("d", 300.0, 120.0),
        ];
        let connections = vec![
            Connection {
                from: "a".to_string(),
                to: "d".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
            Connection {
                from: "b".to_string(),
                to: "c".to_string(),
                from_port: Some("out".to_string()),
                to_port: Some("in".to_string()),
            },
        ];

        let changed = apply_auto_layout(&mut nodes, &connections, AutoLayoutOptions::default());
        assert!(changed);

        let c = nodes.iter().find(|n| n.id == "c").expect("c");
        let d = nodes.iter().find(|n| n.id == "d").expect("d");
        assert!(
            d.y < c.y,
            "expected barycenter ordering to place d above c and reduce crossing"
        );
    }
}

// ========================================
// ========================================

#[wasm_bindgen(start)]
pub fn main() {
    let _ = console_log::init_with_level(log::Level::Debug);
    log::info!("DoraMate Frontend starting...");
    log::info!("components loaded: PropertyPanel + Toolbar");

    mount_to_body(App);
}

// ========================================

#[component]
pub fn App() -> impl IntoView {
    // ========================================

    let (nodes, set_nodes) = signal(Vec::<Node>::new());

    let (connections, set_connections) = signal(Vec::<Connection>::new());

    let (has_unsaved_changes, set_has_unsaved_changes) = signal(false);
    let (is_running, set_is_running) = signal(false);
    let (loading, set_loading) = signal(false);
    let (current_process_id, set_current_process_id) = signal(None::<String>);

    let (uptime, set_uptime) = signal(0u64);
    let (start_time, set_start_time) = signal(None::<f64>);

    let (total_nodes, set_total_nodes) = signal(0usize);
    let (running_nodes, set_running_nodes) = signal(0usize);
    let (error_nodes, set_error_nodes) = signal(0usize);
    let (node_runtime_states, set_node_runtime_states) =
        signal(HashMap::<String, NodeState>::new());

    let (current_file_path, set_current_file_path) = signal(None::<String>);

    let (working_dir, set_working_dir) = signal(None::<String>);

    let (show_dir_selector, set_show_dir_selector) = signal(false);
    let (working_dir_draft, set_working_dir_draft) = signal(String::new());
    let (is_selecting_dir, set_is_selecting_dir) = signal(false);
    let (working_dir_error, set_working_dir_error) = signal(None::<String>);
    let (native_picker_hint, set_native_picker_hint) = signal(None::<String>);
    let (validation_feedback, set_validation_feedback) = signal(None::<ValidationFeedback>);

    let (saved_files, set_saved_files) = signal(Vec::<String>::new());
    let (recent_files, set_recent_files) = signal::<Vec<RecentFileEntry>>(get_recent_files());
    let builtin_custom_templates = built_in_custom_node_templates();
    let (persisted_node_templates, set_persisted_node_templates) =
        signal(Vec::<NodeTemplate>::new());

    let current_yaml_node_templates = Signal::derive({
        let nodes = nodes;
        move || collect_yaml_node_templates(&nodes.get())
    });
    let node_panel_templates = Signal::derive({
        let builtin_custom_templates = builtin_custom_templates.clone();
        move || {
            merge_node_template_sources(
                &builtin_custom_templates,
                &persisted_node_templates.get(),
                &current_yaml_node_templates.get(),
            )
        }
    });

    {
        let set_persisted_node_templates = set_persisted_node_templates.clone();
        spawn_local(async move {
            match api::load_node_templates_config().await {
                Ok(resp) if resp.success => {
                    set_persisted_node_templates
                        .set(config_entries_to_node_templates(resp.templates));
                }
                Ok(resp) => {
                    let user_message =
                        api::friendly_error_message(resp.error_code.as_deref(), &resp.message);
                    log::warn!("load node templates config failed: {}", user_message);
                }
                Err(err) => {
                    log::warn!("load node templates config unavailable: {}", err);
                }
            }
        });
    }

    {
        let nodes = nodes;
        let persisted_node_templates = persisted_node_templates;
        let set_persisted_node_templates = set_persisted_node_templates.clone();
        let last_collected_templates = Arc::new(Mutex::new(None::<Vec<NodeTemplate>>));

        Effect::new(move |_| {
            let current_templates = collect_yaml_node_templates(&nodes.get());

            let is_first_observation = {
                let mut guard = last_collected_templates
                    .lock()
                    .expect("lock collected node templates");
                if guard.as_ref() == Some(&current_templates) {
                    return;
                }
                let first = guard.is_none();
                *guard = Some(current_templates.clone());
                first
            };

            // Skip the initial in-memory sample data once.
            if is_first_observation || current_templates.is_empty() {
                return;
            }

            let persisted = persisted_node_templates.get_untracked();
            let merged = merge_node_template_sources(&[], &persisted, &current_templates);
            if merged == persisted {
                return;
            }

            set_persisted_node_templates.set(merged.clone());
            let payload = node_templates_to_config_entries(&merged);
            spawn_local(async move {
                match api::save_node_templates_config(&payload).await {
                    Ok(resp) if resp.success => {}
                    Ok(resp) => {
                        let user_message =
                            api::friendly_error_message(resp.error_code.as_deref(), &resp.message);
                        log::warn!("save node templates config failed: {}", user_message);
                    }
                    Err(err) => {
                        log::warn!("save node templates config unavailable: {}", err);
                    }
                }
            });
        });
    }

    let (selected_node_id, set_selected_node_id) = signal(None::<String>);
    let (selected_node_ids, set_selected_node_ids) = signal(Vec::<String>::new());
    let (clipboard_dataflow, set_clipboard_dataflow) = signal(None::<Dataflow>);
    let (paste_serial, set_paste_serial) = signal(0usize);
    let (layout_focus_targets, set_layout_focus_targets) = signal(Vec::<String>::new());
    let (layout_focus_serial, set_layout_focus_serial) = signal(0u64);

    let (save_dialog_state, set_save_dialog_state) = signal(SaveDialogState::Closed);

    let (confirm_state, set_confirm_state) = signal(ConfirmState::Closed);

    let (node_panel_width, _set_node_panel_width) = signal(320.0);
    let (property_panel_width, _set_property_panel_width) = signal(320.0);

    // 日志面板相关状态
    let (show_log_panel, set_show_log_panel) = signal(false);
    let (_log_panel_height, _set_log_panel_height) = signal(250.0);
    let (shortcut_config, set_shortcut_config) = signal::<ShortcutConfig>(load_shortcut_config());
    let (show_shortcut_settings, set_show_shortcut_settings) = signal(false);
    let (shortcut_settings_error, set_shortcut_settings_error) = signal(None::<String>);
    let auto_layout_options = AutoLayoutOptions::default();

    let window_title = Signal::derive(move || {
        let file_name = current_file_path
            .get()
            .and_then(|p| {
                p.split('\\')
                    .last()
                    .or_else(|| p.split('/').last())
                    .map(|s| s.to_string())
            })
            .unwrap_or_else(|| "untitled".to_string());

        let unsaved_marker = if has_unsaved_changes.get() { " *" } else { "" };
        format!("{}{} - DoraMate", file_name, unsaved_marker)
    });

    let selected_node = Signal::derive(move || {
        selected_node_id
            .get()
            .and_then(|id| nodes.get().into_iter().find(|n| n.id == id))
    });

    // ========================================
    // ========================================

    let dataflow = Signal::derive(move || Dataflow {
        nodes: nodes.get(),
        connections: connections.get(),
    });

    let (undo_stack, set_undo_stack) = signal(Vec::<Dataflow>::new());
    let (redo_stack, set_redo_stack) = signal(Vec::<Dataflow>::new());
    let history_last_snapshot = Arc::new(Mutex::new(None::<Dataflow>));
    let history_last_record_ms = Arc::new(Mutex::new(0.0f64));
    let history_paused = Arc::new(AtomicBool::new(false));

    {
        let history_last_snapshot = history_last_snapshot.clone();
        let history_last_record_ms = history_last_record_ms.clone();
        let history_paused = history_paused.clone();
        let set_undo_stack = set_undo_stack.clone();
        let set_redo_stack = set_redo_stack.clone();
        let dataflow = dataflow.clone();
        Effect::new(move |_| {
            let current = dataflow.get();

            if history_paused.load(Ordering::Relaxed) {
                *history_last_snapshot.lock().expect("lock history snapshot") = Some(current);
                return;
            }

            let previous = history_last_snapshot
                .lock()
                .expect("lock history snapshot")
                .clone();
            match previous {
                None => {
                    *history_last_snapshot.lock().expect("lock history snapshot") = Some(current);
                    *history_last_record_ms
                        .lock()
                        .expect("lock history timestamp") = js_sys::Date::now();
                }
                Some(previous_snapshot) => {
                    if previous_snapshot != current {
                        let now = js_sys::Date::now();
                        let elapsed = now
                            - *history_last_record_ms
                                .lock()
                                .expect("lock history timestamp");
                        if elapsed >= HISTORY_RECORD_INTERVAL_MS {
                            set_undo_stack.update(|stack| {
                                push_history_snapshot(stack, previous_snapshot.clone())
                            });
                            set_redo_stack.set(Vec::new());
                            *history_last_record_ms
                                .lock()
                                .expect("lock history timestamp") = now;
                        }
                        *history_last_snapshot.lock().expect("lock history snapshot") =
                            Some(current);
                    }
                }
            }
        });
    }

    let replace_dataflow_without_history = Arc::new({
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_undo_stack = set_undo_stack.clone();
        let set_redo_stack = set_redo_stack.clone();
        let history_last_snapshot = history_last_snapshot.clone();
        let history_last_record_ms = history_last_record_ms.clone();
        let history_paused = history_paused.clone();
        move |new_dataflow: Dataflow| {
            history_paused.store(true, Ordering::Relaxed);
            set_nodes.set(new_dataflow.nodes.clone());
            set_connections.set(new_dataflow.connections.clone());
            set_has_unsaved_changes.set(false);
            set_selected_node_id.set(None);
            set_selected_node_ids.set(Vec::new());
            set_undo_stack.set(Vec::new());
            set_redo_stack.set(Vec::new());
            *history_last_snapshot.lock().expect("lock history snapshot") = Some(new_dataflow);
            *history_last_record_ms
                .lock()
                .expect("lock history timestamp") = js_sys::Date::now();
            history_paused.store(false, Ordering::Relaxed);
        }
    });

    let can_undo = Signal::derive(move || !undo_stack.get().is_empty());
    let can_redo = Signal::derive(move || !redo_stack.get().is_empty());
    let can_copy = Signal::derive(move || !selected_node_ids.get().is_empty());
    let can_delete_selected = Signal::derive(move || !selected_node_ids.get().is_empty());
    let can_select_all = Signal::derive(move || !nodes.get().is_empty());
    let can_paste = Signal::derive(move || clipboard_dataflow.get().is_some());
    let can_auto_layout = Signal::derive(move || nodes.get().len() > 1);

    // ========================================

    let on_new = Callback::new({
        let set_confirm_state = set_confirm_state.clone();
        move |()| {
            if has_unsaved_changes.get() {
                let config = ConfirmConfig::warning(
                    "未保存更改",
                    "当前存在未保存的更改，继续新建将丢失当前内容。是否继续？",
                );
                set_confirm_state.set(ConfirmState::Open(config));
            } else {
                set_confirm_state.set(ConfirmState::Open(ConfirmConfig {
                    title: "新建数据流".to_string(),
                    message: "确定要新建数据流吗？当前内容将被清空。".to_string(),
                    confirm_text: "确定".to_string(),
                    cancel_text: "取消".to_string(),
                    confirm_type: components::ConfirmType::Info,
                }));
            }
        }
    });

    let on_open = Callback::new({
        let set_current_file_path = set_current_file_path.clone();
        let set_working_dir = set_working_dir.clone();
        let set_native_picker_hint = set_native_picker_hint.clone();
        let set_recent_files = set_recent_files.clone();
        let set_validation_feedback = set_validation_feedback.clone();
        let replace_dataflow_without_history = Arc::clone(&replace_dataflow_without_history);
        move |()| {
            log::info!("open file");

            let trigger_browser_open = || {
                if let Some(input) = web_sys::window()
                    .unwrap()
                    .document()
                    .unwrap()
                    .get_element_by_id("file-input-open")
                {
                    let input = input.dyn_into::<web_sys::HtmlInputElement>().unwrap();
                    input.click();
                    log::info!("fallback: browser file picker");
                } else {
                    log::error!("file input not found (id='file-input-open')");
                }
            };

            let set_current_file_path = set_current_file_path.clone();
            let set_working_dir = set_working_dir.clone();
            let set_native_picker_hint = set_native_picker_hint.clone();
            let set_recent_files = set_recent_files.clone();
            let set_validation_feedback = set_validation_feedback.clone();
            let replace_dataflow_without_history = Arc::clone(&replace_dataflow_without_history);

            set_native_picker_hint.set(Some(
                "系统目录选择窗口已打开，如未显示请按 Alt+Tab 切换到 DoraMate LocalAgent。"
                    .to_string(),
            ));

            spawn_local(async move {
                let open_result = api::open_dataflow_file().await;
                let should_fallback_browser =
                    should_fallback_to_browser_picker_after_open_result(&open_result);

                match open_result {
                    Ok(resp) => {
                        if resp.cancelled {
                            log::info!("open file cancelled");
                        } else if resp.success {
                            let selected_path = resp.file_path.clone();
                            if let Some(file_path) = selected_path.clone() {
                                set_current_file_path.set(Some(file_path.clone()));
                                if should_record_recent_file_for_open(Some(&file_path)) {
                                    let file_name = resp
                                        .file_name
                                        .clone()
                                        .unwrap_or_else(|| file_name_from_path(&file_path));
                                    add_recent_file(file_name, file_path);
                                    set_recent_files.set(get_recent_files());
                                }
                            } else if let Some(file_name) = resp.file_name.clone() {
                                set_current_file_path.set(Some(file_name));
                            }

                            let inferred_dir =
                                selected_path.as_deref().and_then(parent_dir_from_path);
                            let resolved_wd = resp.working_dir.clone().or(inferred_dir);
                            if let Some(wd) = resolved_wd {
                                set_working_dir.set(Some(wd.clone()));
                                log::info!("working directory set from opened file: {}", wd);
                            } else {
                                log::warn!("opened file has no resolvable directory");
                            }

                            if let Some(content) = resp.content.clone() {
                                match crate::utils::file::parse_yaml_text(&content) {
                                    Ok(dataflow) => {
                                        replace_dataflow_without_history(dataflow);
                                        log::info!("opened via LocalAgent picker");
                                    }
                                    Err(e) => {
                                        log::error!("YAML parse failed: {}", e);
                                    }
                                }
                            } else {
                                log::warn!(
                                    "LocalAgent open succeeded but no file content returned"
                                );
                            }
                        } else {
                            log::warn!(
                                "LocalAgent open failed: {}",
                                api::friendly_error_message(
                                    resp.error_code.as_deref(),
                                    &resp.message,
                                )
                            );
                        }
                    }
                    Err(e) => {
                        log::warn!("LocalAgent open unavailable: {}", e);
                    }
                }

                if should_fallback_browser {
                    trigger_browser_open();
                    set_validation_feedback.set(Some(ValidationFeedback {
                        is_success: true,
                        summary: browser_fallback_recent_files_notice().to_string(),
                        details: Vec::new(),
                    }));
                    let set_validation_feedback_clear = set_validation_feedback.clone();
                    setTimeout(move || set_validation_feedback_clear.set(None), 6000);
                }

                set_native_picker_hint.set(None);
            });
        }
    });

    let on_open_recent = Callback::new({
        let set_current_file_path = set_current_file_path.clone();
        let set_working_dir = set_working_dir.clone();
        let set_recent_files = set_recent_files.clone();
        let replace_dataflow_without_history = Arc::clone(&replace_dataflow_without_history);
        move |file_path: String| {
            let selected_path = file_path.trim().to_string();
            if selected_path.is_empty() {
                return;
            }

            let set_current_file_path = set_current_file_path.clone();
            let set_working_dir = set_working_dir.clone();
            let set_recent_files = set_recent_files.clone();
            let replace_dataflow_without_history = Arc::clone(&replace_dataflow_without_history);

            spawn_local(async move {
                match api::read_dataflow_file(&selected_path).await {
                    Ok(resp) => {
                        if resp.success {
                            if let Some(content) = resp.content.clone() {
                                match crate::utils::file::parse_yaml_text(&content) {
                                    Ok(dataflow) => {
                                        replace_dataflow_without_history(dataflow);

                                        let final_path =
                                            resp.file_path.clone().unwrap_or(selected_path.clone());
                                        let final_name = resp
                                            .file_name
                                            .clone()
                                            .unwrap_or_else(|| file_name_from_path(&final_path));

                                        set_current_file_path.set(Some(final_path.clone()));

                                        let inferred_dir = parent_dir_from_path(&final_path);
                                        let resolved_wd = resp.working_dir.clone().or(inferred_dir);
                                        if let Some(wd) = resolved_wd {
                                            set_working_dir.set(Some(wd));
                                        }

                                        add_recent_file(final_name, final_path);
                                        set_recent_files.set(get_recent_files());
                                        log::info!("opened from recent files");
                                    }
                                    Err(e) => {
                                        log::error!("YAML parse failed (recent file): {}", e);
                                    }
                                }
                            } else {
                                log::warn!("read recent file succeeded but no content returned");
                            }
                        } else {
                            let msg = api::friendly_error_message(
                                resp.error_code.as_deref(),
                                &resp.message,
                            );
                            log::warn!("read recent file failed: {}", msg);
                            if should_remove_recent_file_on_open_recent_failure(
                                &resp.message,
                                resp.error_code.as_deref(),
                            ) {
                                remove_recent_file(&selected_path);
                                set_recent_files.set(get_recent_files());
                            }
                        }
                    }
                    Err(e) => {
                        log::warn!("read recent file unavailable: {}", e);
                    }
                }
            });
        }
    });

    let on_save = Callback::new({
        let current_file_path = current_file_path.clone();
        let set_recent_files = set_recent_files.clone();
        let set_save_dialog_state = set_save_dialog_state.clone();
        let dataflow = dataflow.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        move |()| {
            if let Some(file_path) = current_file_path.get() {
                log::info!("save via browser download: {}", file_path);

                let filename = file_path
                    .split('\\')
                    .last()
                    .or_else(|| file_path.split('/').last())
                    .unwrap_or(&file_path);

                crate::utils::file::save_yaml_file(&dataflow.get(), filename);
                log::info!("file saved: {}", filename);
                set_has_unsaved_changes.set(false);
                if should_record_recent_file_for_open(Some(&file_path)) {
                    add_recent_file(filename.to_string(), file_path.clone());
                    set_recent_files.set(get_recent_files());
                }
            } else {
                log::info!("new file: open Save As dialog");
                set_save_dialog_state.set(SaveDialogState::Open);
            }
        }
    });

    let on_export = Callback::new({
        let dataflow = dataflow.clone();
        move |()| {
            let current_dataflow = dataflow.get();

            match crate::utils::converter::dataflow_to_yaml(&current_dataflow) {
                Ok(yaml) => {
                    use wasm_bindgen::JsCast;
                    let bytes = yaml.as_bytes();
                    let js_array = js_sys::Uint8Array::from(bytes);
                    let array = js_sys::Array::new();
                    array.push(&js_array);

                    let blob_options = web_sys::BlobPropertyBag::new();
                    blob_options.set_type("application/yaml");
                    let blob = web_sys::Blob::new_with_u8_array_sequence(&array)
                        .expect("Failed to create blob");

                    if let Ok(url) = web_sys::Url::create_object_url_with_blob(&blob) {
                        let window = web_sys::window().unwrap();
                        let document = window.document().unwrap();

                        if let Ok(anchor) = document.create_element("a") {
                            anchor.set_attribute("href", &url).unwrap();
                            anchor.set_attribute("download", "dataflow.yml").unwrap();
                            anchor.set_attribute("style", "display: none").unwrap();
                            document.body().unwrap().append_child(&anchor).unwrap();

                            let anchor_ref = anchor.clone();
                            anchor_ref
                                .dyn_into::<web_sys::HtmlAnchorElement>()
                                .unwrap()
                                .click();
                            document.body().unwrap().remove_child(&anchor).unwrap();
                            let _ = web_sys::Url::revoke_object_url(&url);
                            log::info!("YAML export succeeded");
                        }
                    }
                }
                Err(e) => {
                    log::error!("YAML export failed: {}", e);
                }
            }
        }
    });

    let on_validate = Callback::new({
        let dataflow = dataflow.clone();
        let set_validation_feedback = set_validation_feedback.clone();
        move |()| {
            let current_dataflow = dataflow.get();

            let mut errors = Vec::new();

            if current_dataflow.nodes.is_empty() {
                errors.push("节点数量为 0".to_string());
            }

            let mut ids = std::collections::HashSet::new();
            for node in &current_dataflow.nodes {
                if !ids.insert(&node.id) {
                    errors.push(format!("存在重复的节点 ID: {}", node.id));
                }
            }

            for conn in &current_dataflow.connections {
                let from_exists =
                    current_dataflow.nodes.iter().any(|n| n.id == conn.from) || conn.from == "dora";
                let to_exists = current_dataflow.nodes.iter().any(|n| n.id == conn.to);

                if !from_exists {
                    errors.push(format!("连接源节点不存在: {}", conn.from));
                }
                if !to_exists {
                    errors.push(format!("连接目标节点不存在: {}", conn.to));
                }
            }

            if errors.is_empty() {
                log::info!("dataflow validation passed");
                set_validation_feedback.set(Some(ValidationFeedback {
                    is_success: true,
                    summary: "验证通过：未发现配置问题".to_string(),
                    details: Vec::new(),
                }));
                let set_validation_feedback_clear = set_validation_feedback.clone();
                setTimeout(move || set_validation_feedback_clear.set(None), 3000);
            } else {
                log::error!("dataflow validation failed");
                for error in &errors {
                    log::error!("   - {}", error);
                }
                set_validation_feedback.set(Some(ValidationFeedback {
                    is_success: false,
                    summary: format!("验证失败：发现 {} 个问题", errors.len()),
                    details: errors,
                }));
                let set_validation_feedback_clear = set_validation_feedback.clone();
                setTimeout(move || set_validation_feedback_clear.set(None), 10000);
            }
        }
    });

    let on_run = Callback::new({
        let dataflow = dataflow.clone();
        let set_loading = set_loading.clone();
        let set_is_running = set_is_running.clone();
        let set_current_process_id = set_current_process_id.clone();
        let set_start_time = set_start_time.clone();
        let set_uptime = set_uptime.clone();
        let set_total_nodes = set_total_nodes.clone();
        let set_running_nodes = set_running_nodes.clone();
        let set_error_nodes = set_error_nodes.clone();
        let set_node_runtime_states = set_node_runtime_states.clone();
        let set_show_log_panel = set_show_log_panel.clone();
        let is_running_for_timer = is_running.clone();
        let working_dir = working_dir.clone();
        let start_time_for_timer = start_time.clone();
        move |()| {
            let dataflow = dataflow.get();
            disconnect_status_stream();
            let yaml_content = match crate::utils::converter::dataflow_to_yaml(&dataflow) {
                Ok(yaml) => yaml,
                Err(e) => {
                    log::error!("YAML 转换失败: {}", e);
                    return;
                }
            };

            log::info!("YAML 预览（前 500 字符）:");
            log::info!("   {}", &yaml_content.chars().take(500).collect::<String>());

            let wd = working_dir.get();
            log::info!("工作目录: {:?}", wd);

            set_loading.set(true);

            spawn_local(async move {
                log::info!("调用 LocalAgent API 运行数据流...");

                match api::run_dataflow(&yaml_content, wd).await {
                    Ok(response) => {
                        let user_message = api::friendly_error_message(
                            response.error_code.as_deref(),
                            &response.message,
                        );
                        log::info!(
                            "执行响应: success={}, message={}",
                            response.success,
                            user_message
                        );
                        if response.success {
                            if let Some(process_id) = response.process_id {
                                set_current_process_id.set(Some(process_id.clone()));
                                set_is_running.set(true);
                                set_show_log_panel.set(true);
                                set_node_runtime_states.set(
                                    dataflow
                                        .nodes
                                        .iter()
                                        .map(|node| (node.id.clone(), NodeState::Starting))
                                        .collect::<HashMap<_, _>>(),
                                );

                                let now = js_sys::Date::now();
                                set_start_time.set(Some(now));
                                set_uptime.set(0);

                                log::info!("数据流启动成功，进程 ID: {}", process_id);

                                connect_status_stream(
                                    process_id.clone(),
                                    set_is_running.clone(),
                                    set_total_nodes.clone(),
                                    set_running_nodes.clone(),
                                    set_error_nodes.clone(),
                                    set_node_runtime_states.clone(),
                                );

                                start_status_polling(
                                    process_id.clone(),
                                    set_is_running.clone(),
                                    set_total_nodes.clone(),
                                    set_running_nodes.clone(),
                                    set_error_nodes.clone(),
                                    set_node_runtime_states.clone(),
                                );

                                start_uptime_timer(
                                    set_uptime,
                                    start_time_for_timer,
                                    is_running_for_timer,
                                );
                            }
                        } else {
                            log::error!("运行失败: {}", user_message);
                            set_node_runtime_states.set(HashMap::new());
                        }
                    }
                    Err(e) => {
                        log::error!("API 调用失败: {}", e);
                        set_node_runtime_states.set(HashMap::new());
                        if e.contains("127.0.0.1:52100") {
                            log::error!("提示: 请先启动 LocalAgent（http://127.0.0.1:52100）。");
                        } else if e.contains("400") {
                            log::error!("提示: 请求参数可能不正确，请检查 YAML 内容。");
                        }
                    }
                }

                set_loading.set(false);
            });
        }
    });

    let on_stop = Callback::new({
        let set_is_running = set_is_running.clone();
        let set_current_process_id = set_current_process_id.clone();
        let set_start_time = set_start_time.clone();
        let set_uptime = set_uptime.clone();
        let set_show_log_panel = set_show_log_panel.clone();
        let set_total_nodes = set_total_nodes.clone();
        let set_running_nodes = set_running_nodes.clone();
        let set_error_nodes = set_error_nodes.clone();
        let set_node_runtime_states = set_node_runtime_states.clone();
        move |()| {
            set_show_log_panel.set(false);
            let process_id = match current_process_id.get() {
                Some(id) => id,
                None => {
                    log::warn!("no running process");
                    return;
                }
            };

            spawn_local(async move {
                log::info!("call LocalAgent stop API: {}", process_id);

                match api::stop_dataflow(&process_id).await {
                    Ok(response) => {
                        if response.success {
                            set_is_running.set(false);
                            set_current_process_id.set(None);
                            set_start_time.set(None);
                            set_uptime.set(0);
                            set_total_nodes.set(0);
                            set_running_nodes.set(0);
                            set_error_nodes.set(0);
                            set_node_runtime_states.set(HashMap::new());
                            disconnect_status_stream();
                            log::info!("dataflow stopped");
                        } else {
                            log::error!(
                                "stop failed: {}",
                                api::friendly_error_message(
                                    response.error_code.as_deref(),
                                    &response.message,
                                )
                            );
                        }
                    }
                    Err(e) => {
                        log::error!("API call failed: {}", e);
                        disconnect_status_stream();
                    }
                }
            });
        }
    });

    let on_undo = Callback::new({
        let dataflow = dataflow.clone();
        let undo_stack = undo_stack;
        let set_undo_stack = set_undo_stack.clone();
        let set_redo_stack = set_redo_stack.clone();
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        let history_last_snapshot = history_last_snapshot.clone();
        let history_last_record_ms = history_last_record_ms.clone();
        let history_paused = history_paused.clone();
        move |()| {
            let mut undo_entries = undo_stack.get_untracked();
            let previous = match undo_entries.pop() {
                Some(snapshot) => snapshot,
                None => return,
            };

            let current = dataflow.get_untracked();
            set_undo_stack.set(undo_entries);
            set_redo_stack.update(|stack| push_history_snapshot(stack, current));

            history_paused.store(true, Ordering::Relaxed);
            set_nodes.set(previous.nodes.clone());
            set_connections.set(previous.connections.clone());
            set_selected_node_id.set(None);
            set_selected_node_ids.set(Vec::new());
            set_has_unsaved_changes.set(true);
            *history_last_snapshot.lock().expect("lock history snapshot") = Some(previous);
            *history_last_record_ms
                .lock()
                .expect("lock history timestamp") = js_sys::Date::now();
            history_paused.store(false, Ordering::Relaxed);
        }
    });

    let on_redo = Callback::new({
        let dataflow = dataflow.clone();
        let redo_stack = redo_stack;
        let set_redo_stack = set_redo_stack.clone();
        let set_undo_stack = set_undo_stack.clone();
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        let history_last_snapshot = history_last_snapshot.clone();
        let history_last_record_ms = history_last_record_ms.clone();
        let history_paused = history_paused.clone();
        move |()| {
            let mut redo_entries = redo_stack.get_untracked();
            let next = match redo_entries.pop() {
                Some(snapshot) => snapshot,
                None => return,
            };

            let current = dataflow.get_untracked();
            set_redo_stack.set(redo_entries);
            set_undo_stack.update(|stack| push_history_snapshot(stack, current));

            history_paused.store(true, Ordering::Relaxed);
            set_nodes.set(next.nodes.clone());
            set_connections.set(next.connections.clone());
            set_selected_node_id.set(None);
            set_selected_node_ids.set(Vec::new());
            set_has_unsaved_changes.set(true);
            *history_last_snapshot.lock().expect("lock history snapshot") = Some(next);
            *history_last_record_ms
                .lock()
                .expect("lock history timestamp") = js_sys::Date::now();
            history_paused.store(false, Ordering::Relaxed);
        }
    });

    let on_clear = Callback::new({
        let set_confirm_state = set_confirm_state.clone();
        move |()| {
            if has_unsaved_changes.get() {
                let config = ConfirmConfig::danger(
                    "清空画布",
                    "确定要清空画布吗？此操作将删除所有节点和连接，且不可撤销。",
                );
                set_confirm_state.set(ConfirmState::Open(config));
            } else {
                set_confirm_state.set(ConfirmState::Open(ConfirmConfig {
                    title: "清空画布".to_string(),
                    message: "确定要清空画布吗？所有节点和连接都会被删除。".to_string(),
                    confirm_text: "清空".to_string(),
                    cancel_text: "取消".to_string(),
                    confirm_type: components::ConfirmType::Warning,
                }));
            }
        }
    });

    let on_copy = Callback::new({
        let nodes = nodes;
        let connections = connections;
        let selected_node_ids = selected_node_ids;
        let set_clipboard_dataflow = set_clipboard_dataflow.clone();
        let set_paste_serial = set_paste_serial.clone();
        move |()| {
            let selected_ids = selected_node_ids.get_untracked();
            let copied = collect_selected_subflow(
                &nodes.get_untracked(),
                &connections.get_untracked(),
                &selected_ids,
            );
            if let Some(dataflow) = copied {
                set_clipboard_dataflow.set(Some(dataflow));
                set_paste_serial.set(0);
                log::info!("copied selected nodes");
            }
        }
    });

    let on_paste = Callback::new({
        let nodes = nodes;
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let clipboard_dataflow = clipboard_dataflow;
        let paste_serial = paste_serial;
        let set_paste_serial = set_paste_serial.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        move |()| {
            let Some(clipboard) = clipboard_dataflow.get_untracked() else {
                return;
            };
            if clipboard.nodes.is_empty() {
                return;
            }

            let offset = 36.0 + (paste_serial.get_untracked() as f64 * 24.0);
            let pasted = build_pasted_dataflow(&clipboard, &nodes.get_untracked(), offset);
            if pasted.nodes.is_empty() {
                return;
            }

            let selected_ids: Vec<String> = pasted.nodes.iter().map(|n| n.id.clone()).collect();
            let primary = selected_ids.last().cloned();
            let new_nodes = pasted.nodes;
            let new_connections = pasted.connections;

            set_nodes.update(|all_nodes| all_nodes.extend(new_nodes));
            set_connections.update(|all_connections| all_connections.extend(new_connections));
            set_selected_node_ids.set(selected_ids);
            set_selected_node_id.set(primary);
            set_has_unsaved_changes.set(true);
            set_paste_serial.update(|v| *v += 1);
            log::info!("pasted nodes from clipboard");
        }
    });

    let on_delete_selected = Callback::new({
        let nodes = nodes;
        let connections = connections;
        let selected_node_ids = selected_node_ids;
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        move |()| {
            let selected_ids = selected_node_ids.get_untracked();
            if selected_ids.is_empty() {
                return;
            }

            let mut all_nodes = nodes.get_untracked();
            let mut all_connections = connections.get_untracked();
            if !remove_selected_nodes(&mut all_nodes, &mut all_connections, &selected_ids) {
                return;
            }

            set_nodes.set(all_nodes);
            set_connections.set(all_connections);
            set_selected_node_id.set(None);
            set_selected_node_ids.set(Vec::new());
            set_has_unsaved_changes.set(true);
            log::info!("deleted selected nodes");
        }
    });

    let on_cut = Callback::new({
        let on_copy = on_copy.clone();
        let on_delete_selected = on_delete_selected.clone();
        move |()| {
            on_copy.run(());
            on_delete_selected.run(());
        }
    });

    let on_duplicate = Callback::new({
        let nodes = nodes;
        let connections = connections;
        let selected_node_ids = selected_node_ids;
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        move |()| {
            let selected_ids = selected_node_ids.get_untracked();
            if selected_ids.is_empty() {
                return;
            }

            let mut all_nodes = nodes.get_untracked();
            let mut all_connections = connections.get_untracked();
            let duplicated_ids = duplicate_selected_subflow(
                &mut all_nodes,
                &mut all_connections,
                &selected_ids,
                DUPLICATE_OFFSET,
            );
            if duplicated_ids.is_empty() {
                return;
            }

            let primary = duplicated_ids.last().cloned();
            set_nodes.set(all_nodes);
            set_connections.set(all_connections);
            set_selected_node_ids.set(duplicated_ids);
            set_selected_node_id.set(primary);
            set_has_unsaved_changes.set(true);
            log::info!("duplicated selected nodes");
        }
    });

    let on_select_all = Callback::new({
        let nodes = nodes;
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        move |()| {
            let (selected, primary) = select_all_nodes(&nodes.get_untracked());
            set_selected_node_ids.set(selected);
            set_selected_node_id.set(primary);
        }
    });

    let on_auto_layout = Callback::new({
        let nodes = nodes;
        let connections = connections;
        let selected_node_ids = selected_node_ids;
        let selected_node_id = selected_node_id;
        let set_nodes = set_nodes.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        let set_layout_focus_targets = set_layout_focus_targets.clone();
        let set_layout_focus_serial = set_layout_focus_serial.clone();
        move |()| {
            let mut all_nodes = nodes.get_untracked();
            let all_connections = connections.get_untracked();
            if !apply_auto_layout(&mut all_nodes, &all_connections, auto_layout_options) {
                return;
            }

            let current_selected: HashSet<String> =
                selected_node_ids.get_untracked().into_iter().collect();
            let next_selected: Vec<String> = all_nodes
                .iter()
                .filter(|node| current_selected.contains(&node.id))
                .map(|node| node.id.clone())
                .collect();
            let next_primary = selected_node_id
                .get_untracked()
                .filter(|id| next_selected.iter().any(|selected| selected == id))
                .or_else(|| next_selected.last().cloned());

            let next_selected_for_focus = next_selected.clone();
            let all_node_ids_for_focus: Vec<String> =
                all_nodes.iter().map(|node| node.id.clone()).collect();
            set_nodes.set(all_nodes);
            set_selected_node_ids.set(next_selected);
            set_selected_node_id.set(next_primary);
            set_has_unsaved_changes.set(true);

            if auto_layout_options.focus_selection_after_layout {
                let focus_targets = if !next_selected_for_focus.is_empty() {
                    next_selected_for_focus
                } else {
                    all_node_ids_for_focus
                };
                set_layout_focus_targets.set(focus_targets);
                set_layout_focus_serial.update(|v| *v += 1);
            }
            log::info!("auto layout applied");
        }
    });

    // ========================================
    // ========================================

    let (pending_delete_connection, set_pending_delete_connection) = signal(None::<Connection>);

    let on_delete_connection = Callback::new({
        let set_confirm_state = set_confirm_state.clone();
        let set_pending_delete_connection = set_pending_delete_connection.clone();
        move |conn: Connection| {
            set_pending_delete_connection.set(Some(conn.clone()));

            let from_node_label = nodes.with(|nodes| {
                nodes
                    .iter()
                    .find(|n| n.id == conn.from)
                    .map(|n| n.label.clone())
                    .unwrap_or_else(|| conn.from.clone())
            });
            let to_node_label = nodes.with(|nodes| {
                nodes
                    .iter()
                    .find(|n| n.id == conn.to)
                    .map(|n| n.label.clone())
                    .unwrap_or_else(|| conn.to.clone())
            });

            let config = ConfirmConfig {
                title: "删除连接".to_string(),
                message: format!(
                    "确定要删除连接 \"{}\" -> \"{}\" 吗？\n\n同时会删除节点输入中的对应引用。",
                    from_node_label, to_node_label
                ),
                confirm_text: "删除连接并清理引用".to_string(),
                cancel_text: "不删除连接".to_string(),
                confirm_type: components::ConfirmType::Warning,
            };
            set_confirm_state.set(ConfirmState::Open(config));
        }
    });

    // ========================================
    // ========================================

    let on_add_node = Callback::new({
        let set_nodes = set_nodes.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        move |template: NodeTemplate| {
            set_nodes.update(|nodes| {
                let type_count = nodes
                    .iter()
                    .filter(|n| n.node_type == template.node_type)
                    .count();
                let instance_number = type_count + 1;

                let timestamp = js_sys::Date::now();
                let node_id = format!(
                    "{}_{:03}_{:010}",
                    template.node_type,
                    instance_number,
                    (timestamp * 1000000.0) as u64
                );

                let label = format!("{} #{}", template.name, instance_number);

                let node_count = nodes.len();
                let row = node_count / 3;
                let col = node_count % 3;

                let x = 400.0 + (col as f64 * 150.0);
                let y = 100.0 + (row as f64 * 100.0);

                let new_node = Node {
                    id: node_id.clone(),
                    x,
                    y,
                    label: label.clone(),
                    node_type: template.node_type.clone(),
                    path: template.path.clone(),
                    env: None,
                    config: None,
                    inputs: template.inputs.clone(),
                    outputs: template.outputs.clone(),
                    scale: Some(1.0),
                };

                nodes.push(new_node);
                set_has_unsaved_changes.set(true);

                log::info!("node added: {} ({})", label, node_id);
                log::info!("   position: ({}, {})", x, y);
            });
        }
    });

    // ========================================
    // ========================================

    let on_save_success = Callback::new({
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        let set_current_file_path = set_current_file_path.clone();
        let set_saved_files = set_saved_files.clone();
        move |filename: String| {
            set_current_file_path.set(Some(filename.clone()));
            set_has_unsaved_changes.set(false);
            set_saved_files.update(|files| {
                if !files.contains(&filename) {
                    files.push(filename.clone());
                }
            });
            log::info!("文件保存成功: {}", filename);
        }
    });

    // ========================================

    let on_confirm_new = Callback::new({
        let set_current_file_path = set_current_file_path.clone();
        let replace_dataflow_without_history = Arc::clone(&replace_dataflow_without_history);
        move |()| {
            replace_dataflow_without_history(Dataflow {
                nodes: vec![],
                connections: vec![],
            });
            set_current_file_path.set(None);
            log::info!("new dataflow created");
        }
    });

    let on_confirm_clear = Callback::new({
        let set_nodes = set_nodes.clone();
        let set_connections = set_connections.clone();
        let set_has_unsaved_changes = set_has_unsaved_changes.clone();
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        move |()| {
            set_nodes.set(vec![]);
            set_connections.set(vec![]);
            set_has_unsaved_changes.set(true);
            set_selected_node_id.set(None);
            set_selected_node_ids.set(Vec::new());
            log::info!("canvas cleared");
        }
    });

    let on_confirm_cancel = Callback::new(move |()| {
        log::info!("cancelled by user");
    });

    // ========================================
    // Shortcut settings
    // ========================================

    let on_open_shortcut_settings = Callback::new({
        let set_show_shortcut_settings = set_show_shortcut_settings.clone();
        let set_shortcut_settings_error = set_shortcut_settings_error.clone();
        move |()| {
            set_shortcut_settings_error.set(None);
            set_show_shortcut_settings.set(true);
        }
    });

    let on_close_shortcut_settings = Callback::new({
        let set_show_shortcut_settings = set_show_shortcut_settings.clone();
        let set_shortcut_settings_error = set_shortcut_settings_error.clone();
        move |()| {
            set_shortcut_settings_error.set(None);
            set_show_shortcut_settings.set(false);
        }
    });

    let on_save_shortcut_settings = Callback::new({
        let set_shortcut_config = set_shortcut_config.clone();
        let set_show_shortcut_settings = set_show_shortcut_settings.clone();
        let set_shortcut_settings_error = set_shortcut_settings_error.clone();
        move |next_config: ShortcutConfig| match save_shortcut_config(&next_config) {
            Ok(()) => {
                set_shortcut_config.set(next_config);
                set_shortcut_settings_error.set(None);
                set_show_shortcut_settings.set(false);
            }
            Err(err) => {
                set_shortcut_settings_error.set(Some(err));
            }
        }
    });

    let on_reset_shortcut_settings = Callback::new({
        let set_shortcut_config = set_shortcut_config.clone();
        let set_show_shortcut_settings = set_show_shortcut_settings.clone();
        let set_shortcut_settings_error = set_shortcut_settings_error.clone();
        move |()| match reset_shortcut_config() {
            Ok(()) => {
                set_shortcut_config.set(ShortcutConfig::default());
                set_shortcut_settings_error.set(None);
                set_show_shortcut_settings.set(false);
            }
            Err(err) => {
                set_shortcut_settings_error.set(Some(err));
            }
        }
    });

    // ========================================
    // ========================================

    let on_selection_change = Callback::new({
        let set_selected_node_id = set_selected_node_id.clone();
        let set_selected_node_ids = set_selected_node_ids.clone();
        move |(selected_ids, primary): (Vec<String>, Option<String>)| {
            let resolved_primary = primary
                .filter(|id| selected_ids.iter().any(|selected| selected == id))
                .or_else(|| selected_ids.last().cloned());
            set_selected_node_ids.set(selected_ids);
            set_selected_node_id.set(resolved_primary);
        }
    });

    // ========================================
    // ========================================

    let _ = Effect::new(move |_| {
        let title = window_title.get();
        if let Some(window) = web_sys::window() {
            if let Some(document) = window.document().as_ref() {
                let _ = document.set_title(&title);
            }
        }
    });
    on_cleanup(move || disconnect_status_stream());

    view! {
        <div class="app-container">
            <div class="title-bar">
                <span class="window-title">{move || window_title.get()}</span>
            </div>

            <Toolbar
                on_new=on_new
                on_open=on_open
                on_open_recent=on_open_recent
                on_save=on_save
                on_export=on_export
                on_validate=on_validate
                on_auto_layout=on_auto_layout
                on_run=on_run
                on_stop=on_stop
                on_undo=on_undo
                on_redo=on_redo
                on_copy=on_copy
                on_cut=on_cut
                on_duplicate=on_duplicate
                on_paste=on_paste
                on_delete_selected=on_delete_selected
                on_select_all=on_select_all
                on_clear=on_clear
                on_open_shortcuts=on_open_shortcut_settings
                shortcut_config=shortcut_config.into()
                has_unsaved_changes=has_unsaved_changes.into()
                is_running=is_running.into()
                can_undo=can_undo
                can_redo=can_redo
                can_copy=can_copy
                can_delete_selected=can_delete_selected
                can_select_all=can_select_all
                can_paste=can_paste
                can_auto_layout=can_auto_layout
                loading=loading.into()
                on_toggle_logs=Callback::new(move |()| {
                    set_show_log_panel.update(|v| *v = !*v);
                })
                show_log_panel=show_log_panel.into()
                recent_files=recent_files.into()
            />

            <ShortcutSettingsDialog
                show=show_shortcut_settings.into()
                shortcut_config=shortcut_config.into()
                error_message=shortcut_settings_error.into()
                on_close=on_close_shortcut_settings
                on_save=on_save_shortcut_settings
                on_reset=on_reset_shortcut_settings
            />

            <Show when=move || native_picker_hint.get().is_some()>
                <div class="picker-hint-banner" role="status">
                    {move || native_picker_hint.get().unwrap_or_default()}
                </div>
            </Show>

            <Show when=move || validation_feedback.get().is_some()>
                {move || {
                        validation_feedback.get().map(|feedback| {
                            let class_name = if feedback.is_success {
                                "validation-banner validation-success"
                            } else {
                                "validation-banner validation-error"
                            };
                            let detail_view = if feedback.details.is_empty() {
                                None
                            } else {
                                Some(
                                    view! {
                                        <ul class="validation-list">
                                            {feedback
                                                .details
                                                .into_iter()
                                                .map(|detail| view! { <li>{detail}</li> })
                                                .collect_view()}
                                        </ul>
                                    },
                                )
                            };

                            view! {
                                <div class=class_name role="status">
                                    <div class="validation-title">{feedback.summary}</div>
                                    {detail_view}
                                </div>
                            }
                        })
                    }}
                </Show>

            <StatusPanel
                is_running=is_running.into()
                uptime=uptime.into()
                total_nodes=total_nodes.into()
                running_nodes=running_nodes.into()
                error_nodes=error_nodes.into()
                process_id=current_process_id.into()
                working_dir=working_dir.into()
                on_set_working_dir=Callback::new({
                    let working_dir = working_dir.clone();
                    let set_show_dir_selector = set_show_dir_selector.clone();
                    let set_working_dir_draft = set_working_dir_draft.clone();
                    let set_working_dir_error = set_working_dir_error.clone();
                    move |_| {
                        set_working_dir_draft.set(working_dir.get().unwrap_or_default());
                        set_working_dir_error.set(None);
                        set_show_dir_selector.set(true);
                    }
                })
            />

            <LogPanel
                process_id=current_process_id.into()
                visible=show_log_panel.into()
            />

            <div class="main-content">
                <div
                    class="node-panel-sidebar"
                    style:width=move || format!("{}px", node_panel_width.get())
                >
                    <NodePanel
                        on_add_node=on_add_node
                        featured_templates=current_yaml_node_templates
                        all_templates=node_panel_templates
                    />
                </div>

                <div class="resizer resizer-left"></div>

                <div class="canvas-container">
                    <Canvas
                        nodes=nodes.into()
                        set_nodes=set_nodes
                        connections=connections.into()
                        set_connections=set_connections
                        on_selection_change=on_selection_change
                        on_delete_connection=on_delete_connection
                        selected_node_ids=selected_node_ids.into()
                        node_runtime_states=node_runtime_states.into()
                        layout_focus_targets=layout_focus_targets.into()
                        layout_focus_serial=layout_focus_serial.into()
                        is_running=is_running.into()
                    />
                </div>

                <div class="resizer resizer-right"></div>

                <div
                    class="property-panel-sidebar"
                    style:width=move || format!("{}px", property_panel_width.get())
                >
                    <PropertyPanel
                        selected_node=selected_node
                        _nodes=nodes.into()
                        set_nodes=set_nodes
                        connections=connections.into()
                        set_connections=set_connections
                    />
                </div>
            </div>

            {let replace_dataflow_without_history_for_file = Arc::clone(&replace_dataflow_without_history);
            let set_working_dir_for_file = set_working_dir.clone();
            let working_dir_for_file = working_dir;
            let set_show_dir_selector_for_file = set_show_dir_selector.clone();
            let set_working_dir_draft_for_file = set_working_dir_draft.clone();
            let set_working_dir_error_for_file = set_working_dir_error.clone();
            let set_native_picker_hint_for_file = set_native_picker_hint.clone();
            view! {
                <input
                    type="file"
                    id="file-input-open"
                    accept=".yml,.yaml"
                    style="display: none"
                    on:change=move |_e: Event| {
                        let input = match web_sys::window()
                            .and_then(|w| w.document())
                            .and_then(|d| d.get_element_by_id("file-input-open"))
                        {
                            Some(elem) => match elem.dyn_into::<web_sys::HtmlInputElement>() {
                                Ok(input) => input,
                                Err(e) => {
                                    log::error!("failed to cast input: {:?}", e);
                                    return;
                                }
                            },
                            None => {
                                log::error!("file input not found");
                                return;
                            }
                        };

                        if let Some(files) = input.files() {
                            if let Some(file) = files.get(0) {
                                let replace_dataflow_without_history = Arc::clone(&replace_dataflow_without_history_for_file);
                                let file_name = file.name();
                                let set_current_file_path = set_current_file_path.clone();
                                let set_working_dir = set_working_dir_for_file.clone();
                                let working_dir = working_dir_for_file;
                                let set_show_dir_selector = set_show_dir_selector_for_file.clone();
                                let set_working_dir_draft = set_working_dir_draft_for_file.clone();
                                let set_working_dir_error = set_working_dir_error_for_file.clone();
                                let set_native_picker_hint = set_native_picker_hint_for_file.clone();

                                spawn_local(async move {
                                    match crate::utils::file::read_yaml_file(file).await {
                                        Ok(dataflow) => {
                                            set_current_file_path.set(Some(file_name));
                                            replace_dataflow_without_history(dataflow);

                                            // Browser file picker cannot provide absolute path.
                                            // Ask for directory once when working dir is not set.
                                            if working_dir.get_untracked().is_none() {
                                                set_native_picker_hint.set(Some(
                                                    "系统目录选择窗口已打开，如未显示请按 Alt+Tab 切换到 DoraMate LocalAgent。"
                                                        .to_string(),
                                                ));

                                                let select_result = api::select_directory().await;

                                                if let Ok(resp) = &select_result {
                                                    if resp.success {
                                                        if let Some(path) = &resp.path {
                                                            set_working_dir.set(Some(path.clone()));
                                                        }
                                                    }
                                                }

                                                if should_open_manual_working_dir_dialog(&select_result) {
                                                    if let Some(error_msg) =
                                                        working_dir_error_message_for_select_result(
                                                            &select_result,
                                                        )
                                                    {
                                                        set_working_dir_error.set(Some(error_msg));
                                                    }
                                                    set_working_dir_draft.set(String::new());
                                                    set_show_dir_selector.set(true);
                                                }

                                                set_native_picker_hint.set(None);
                                            }
                                        }
                                        Err(e) => {
                                            log::error!("failed to load file: {}", e);
                                        }
                                    }
                                });
                            }
                        }

                        input.set_value("");
                    }
                />
            }}

            <SaveFileDialog
                state=save_dialog_state.into()
                set_state=set_save_dialog_state
                yaml_content=Signal::derive(move || {
                    match crate::utils::converter::dataflow_to_yaml(&dataflow.get()) {
                        Ok(yaml) => yaml,
                        Err(e) => {
                            log::error!("YAML convert failed: {}", e);
                            format!("# Error: {}\n# Please check dataflow config", e)
                        }
                    }
                })
                on_save_success=on_save_success
                saved_files=saved_files.into()
            />

            <ConfirmDialog
                state=confirm_state.into()
                set_state=set_confirm_state
                on_confirm=Callback::new({
                    let set_connections = set_connections.clone();
                    let set_nodes = set_nodes.clone();
                    let set_has_unsaved_changes = set_has_unsaved_changes.clone();
                    move |()| {
                        match confirm_state.get() {
                            ConfirmState::Open(ref config) => {
                                match config.title.as_str() {
                                    "新建数据流" | "未保存更改" => {
                                        on_confirm_new.run(());
                                    }
                                    "清空画布" => {
                                        on_confirm_clear.run(());
                                    }
                                    "删除连接" => {
                                        if let Some(conn) = pending_delete_connection.get() {
                                            let conn_clone = conn.clone();

                                            set_connections.update(|conns| {
                                                conns.retain(|c| c != &conn_clone);
                                            });

                                            let from_id_prefix = format!("{}/", conn.from);
                                            set_nodes.update(|nodes| {
                                                if let Some(target) = nodes.iter_mut().find(|n| n.id == conn.to) {
                                                    if let Some(ref mut inputs) = target.inputs {
                                                        inputs.retain(|input| {
                                                            let source = input
                                                                .split_once(':')
                                                                .map(|(_, source)| source.trim())
                                                                .unwrap_or_else(|| input.trim());
                                                            !source.starts_with(&from_id_prefix)
                                                        });
                                                    }
                                                }
                                            });

                                            set_has_unsaved_changes.set(true);
                                        }
                                        set_pending_delete_connection.set(None);
                                    }
                                    _ => {}
                                }
                            }
                            _ => {}
                        }
                    }
                })
                on_cancel=Callback::new({
                    move |()| {
                        set_pending_delete_connection.set(None);
                        on_confirm_cancel.run(());
                    }
                })
            />

            <Show
                when=move || show_dir_selector.get()
            >
                <div class="modal-overlay" on:click=move |_| {
                    let set_show = set_show_dir_selector.clone();
                    let set_error = set_working_dir_error.clone();
                    spawn_local(async move {
                        let _ = gloo_timers::future::sleep(std::time::Duration::from_millis(0)).await;
                        set_error.set(None);
                        set_show.set(false);
                    });
                }>
                    <div class="modal-content" on:click=move |e| e.stop_propagation()>
                        <div class="modal-header">
                            <h3>"设置工作目录"</h3>
                            <button
                                class="modal-close"
                                on:click=move |_| {
                                    let set_show = set_show_dir_selector.clone();
                                    let set_error = set_working_dir_error.clone();
                                    spawn_local(async move {
                                        let _ = gloo_timers::future::sleep(std::time::Duration::from_millis(0)).await;
                                        set_error.set(None);
                                        set_show.set(false);
                                    });
                                }
                            >
                                "×"
                            </button>
                        </div>
                        <div class="modal-body">
                            <p class="help-text">
                                "请选择或输入运行数据流时使用的工作目录路径。"
                            </p>
                            <p class="help-text-example">
                                "示例: C:\\Users\\Administrator\\projects\\dora-yolo-rust"
                            </p>

                            <div class="working-dir-input-row">
                                <input
                                    type="text"
                                    class="working-dir-input"
                                    placeholder="输入工作目录路径..."
                                    prop:value=move || working_dir_draft.get()
                                    on:input=move |ev| {
                                        set_working_dir_draft.set(event_target_value(&ev));
                                    }
                                />
                                <button
                                    class="modal-btn modal-btn-browse"
                                    disabled=move || is_selecting_dir.get()
                                    on:click=move |_| {
                                        if is_selecting_dir.get() {
                                            return;
                                        }

                                        set_is_selecting_dir.set(true);
                                        set_working_dir_error.set(None);
                                        set_native_picker_hint.set(Some(
                                            "系统目录选择窗口已打开，如未显示请按 Alt+Tab 切换到 DoraMate LocalAgent。"
                                                .to_string(),
                                        ));

                                        let set_is_selecting_dir = set_is_selecting_dir.clone();
                                        let set_working_dir_draft = set_working_dir_draft.clone();
                                        let set_working_dir_error = set_working_dir_error.clone();
                                        let set_native_picker_hint = set_native_picker_hint.clone();

                                        spawn_local(async move {
                                            match api::select_directory().await {
                                                Ok(resp) => {
                                                    if resp.cancelled {
                                                        log::info!("directory selection cancelled");
                                                    } else if resp.success {
                                                        if let Some(path) = resp.path {
                                                            set_working_dir_draft.set(path);
                                                        }
                                                    } else {
                                                        set_working_dir_error.set(Some(
                                                            api::friendly_error_message(
                                                                resp.error_code.as_deref(),
                                                                &normalize_working_dir_select_error(
                                                                    &resp.message,
                                                                ),
                                                            ),
                                                        ));
                                                    }
                                                }
                                                Err(e) => {
                                                    set_working_dir_error.set(Some(
                                                        normalize_working_dir_select_error(&e),
                                                    ));
                                                }
                                            }

                                            set_is_selecting_dir.set(false);
                                            set_native_picker_hint.set(None);
                                        });
                                    }
                                >
                                    {move || if is_selecting_dir.get() { "选择中..." } else { "浏览..." }}
                                </button>
                            </div>

                            <Show when=move || working_dir_error.get().is_some()>
                                <p class="help-text-error">
                                    {move || working_dir_error.get().unwrap_or_default()}
                                </p>
                            </Show>

                            <Show when=move || is_selecting_dir.get()>
                                <p class="help-text-picker">
                                    "系统目录选择窗口已打开，如未显示请按 Alt+Tab 切换。"
                                </p>
                            </Show>
                        </div>
                        <div class="modal-footer">
                            <button
                                class="modal-btn modal-btn-cancel"
                                on:click=move |_| {
                                    let set_show = set_show_dir_selector.clone();
                                    let set_error = set_working_dir_error.clone();
                                    spawn_local(async move {
                                        let _ = gloo_timers::future::sleep(std::time::Duration::from_millis(0)).await;
                                        set_error.set(None);
                                        set_show.set(false);
                                    });
                                }
                            >
                                "取消"
                            </button>
                            <button
                                class="modal-btn modal-btn-confirm"
                                on:click=move |_| {
                                    let input = working_dir_draft.get().trim().to_string();
                                    if input.is_empty() {
                                        set_working_dir.set(None);
                                    } else {
                                        set_working_dir.set(Some(input));
                                    }

                                    let set_show = set_show_dir_selector.clone();
                                    let set_error = set_working_dir_error.clone();
                                    spawn_local(async move {
                                        let _ = gloo_timers::future::sleep(std::time::Duration::from_millis(0)).await;
                                        set_error.set(None);
                                        set_show.set(false);
                                    });
                                }
                            >
                                "确定"
                            </button>
                        </div>
                    </div>
                </div>
            </Show>
        </div>
        <style>
            r#"
            * {
                margin: 0;
                padding: 0;
                box-sizing: border-box;
            }

            body {
                font-family: 'Segoe UI', system-ui, sans-serif;
                overflow: hidden;
            }

            .app-container {
                width: 100vw;
                height: 100vh;
                display: flex;
                flex-direction: column;
                background: #1e1e1e;
                color: #e0e0e0;
            }

            .picker-hint-banner {
                position: fixed;
                top: 76px;
                right: 16px;
                z-index: 9000;
                max-width: 520px;
                padding: 10px 14px;
                border-radius: 8px;
                border: 1px solid #1f6f48;
                background: rgba(21, 46, 34, 0.95);
                color: #d9f7e8;
                font-size: 13px;
                line-height: 1.4;
                box-shadow: 0 6px 20px rgba(0, 0, 0, 0.35);
            }

            .validation-banner {
                position: fixed;
                top: 124px;
                right: 16px;
                z-index: 9000;
                max-width: 560px;
                padding: 10px 14px;
                border-radius: 8px;
                font-size: 13px;
                line-height: 1.45;
                box-shadow: 0 6px 20px rgba(0, 0, 0, 0.35);
            }

            .validation-success {
                border: 1px solid #1f6f48;
                background: rgba(21, 46, 34, 0.95);
                color: #d9f7e8;
            }

            .validation-error {
                border: 1px solid #8b2b2b;
                background: rgba(55, 24, 24, 0.96);
                color: #ffd8d8;
            }

            .validation-title {
                font-weight: 600;
            }

            .validation-list {
                margin: 8px 0 0 18px;
                padding: 0;
            }

            .validation-list li + li {
                margin-top: 4px;
            }

            .title-bar {
                height: 32px;
                background: #2d2d2d;
                border-bottom: 1px solid #3e3e3e;
                display: flex;
                align-items: center;
                padding: 0 16px;
                font-size: 13px;
                font-weight: 500;
            }

            .window-title {
                color: #cccccc;
            }

            .window-title::before {
                content: "🧭 ";
                margin-right: 6px;
            }

            .main-content {
                flex: 1;
                display: flex;
                flex-direction: row;
                overflow: hidden;
            }

            .node-panel-sidebar {
                flex-shrink: 0;
                overflow-y: auto;
                overflow-x: hidden;
                border-right: 1px solid #333;
            }

            .canvas-container {
                flex: 1;
                overflow: auto;
                position: relative;
                background: #1e1e1e;
            }

            /* Canvas scrollbar */
            .canvas-container::-webkit-scrollbar {
                width: 12px;
                height: 12px;
            }

            .canvas-container::-webkit-scrollbar-track {
                background: #1e1e1e;
            }

            .canvas-container::-webkit-scrollbar-thumb {
                background: #424242;
                border-radius: 6px;
                border: 3px solid #1e1e1e;
            }

            .canvas-container::-webkit-scrollbar-thumb:hover {
                background: #4a4a4a;
            }

            .canvas-container::-webkit-scrollbar-corner {
                background: #1e1e1e;
            }

            .canvas-container svg {
                display: block;
            }

            /* Property panel area */
            .property-panel-sidebar {
                flex-shrink: 0;
                border-left: 1px solid #333;
                overflow-y: auto;
                overflow-x: hidden;
            }

            /* Panel resizer */
            .resizer {
                width: 4px;
                background: #252526;
                cursor: col-resize;
                transition: background 0.2s;
                flex-shrink: 0;
                position: relative;
                z-index: 10;
            }

            .resizer:hover,
            .resizer.active {
                background: #007acc;
            }

            .resizer-left {
                border-right: 1px solid #333;
            }

            .resizer-right {
                border-left: 1px solid #333;
            }

            /* Side panel scrollbars */
            .node-panel-sidebar::-webkit-scrollbar,
            .property-panel-sidebar::-webkit-scrollbar {
                width: 8px;
            }

            .node-panel-sidebar::-webkit-scrollbar-track,
            .property-panel-sidebar::-webkit-scrollbar-track {
                background: #1e1e1e;
            }

            .node-panel-sidebar::-webkit-scrollbar-thumb,
            .property-panel-sidebar::-webkit-scrollbar-thumb {
                background: #424242;
                border-radius: 4px;
            }

            .node-panel-sidebar::-webkit-scrollbar-thumb:hover,
            .property-panel-sidebar::-webkit-scrollbar-thumb:hover {
                background: #4a4a4a;
            }

            /* Working directory modal styles */
            .modal-overlay {
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: rgba(0, 0, 0, 0.7);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 10000;
            }

            .modal-content {
                background: #252526;
                border: 1px solid #454545;
                border-radius: 8px;
                box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
                min-width: 500px;
                max-width: 600px;
                display: flex;
                flex-direction: column;
            }

            .modal-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                padding: 16px 20px;
                border-bottom: 1px solid #3e3e3e;
            }

            .modal-header h3 {
                margin: 0;
                font-size: 16px;
                font-weight: 500;
                color: #cccccc;
            }

            .modal-close {
                background: none;
                border: none;
                color: #cccccc;
                font-size: 24px;
                cursor: pointer;
                padding: 0;
                width: 32px;
                height: 32px;
                display: flex;
                align-items: center;
                justify-content: center;
                border-radius: 4px;
                transition: background 0.2s;
            }

            .modal-close:hover {
                background: #3e3e3e;
            }

            .modal-body {
                padding: 20px;
            }

            .help-text {
                margin: 0 0 8px 0;
                font-size: 14px;
                color: #cccccc;
            }

            .help-text-example {
                margin: 0 0 16px 0;
                font-size: 13px;
                color: #9cdcfe;
                font-family: 'Consolas', 'Monaco', monospace;
            }

            .help-text-error {
                margin: 10px 0 0 0;
                font-size: 12px;
                color: #f48771;
            }

            .help-text-picker {
                margin: 10px 0 0 0;
                font-size: 12px;
                color: #b8e6cc;
            }

            .working-dir-input-row {
                display: flex;
                gap: 8px;
            }

            .working-dir-input {
                flex: 1;
                padding: 10px 12px;
                background: #1e1e1e;
                border: 1px solid #3e3e3e;
                border-radius: 4px;
                color: #d4d4d4;
                font-size: 14px;
                font-family: 'Consolas', 'Monaco', monospace;
                box-sizing: border-box;
            }

            .working-dir-input:focus {
                outline: none;
                border-color: #007acc;
            }

            .working-dir-input::placeholder {
                color: #6e6e6e;
            }

            .modal-footer {
                display: flex;
                justify-content: flex-end;
                gap: 12px;
                padding: 16px 20px;
                border-top: 1px solid #3e3e3e;
            }

            .modal-btn {
                padding: 8px 20px;
                border: none;
                border-radius: 4px;
                font-size: 13px;
                cursor: pointer;
                transition: background 0.2s;
            }

            .modal-btn-cancel {
                background: #3e3e3e;
                color: #cccccc;
            }

            .modal-btn-cancel:hover {
                background: #4e4e4e;
            }

            .modal-btn-confirm {
                background: #007acc;
                color: white;
            }

            .modal-btn-confirm:hover {
                background: #0088e8;
            }

            .modal-btn-browse {
                background: #2d5a88;
                color: #ffffff;
                min-width: 92px;
            }

            .modal-btn-browse:hover:not(:disabled) {
                background: #3570aa;
            }

            .modal-btn:disabled {
                opacity: 0.65;
                cursor: not-allowed;
            }
            "#
        </style>
        <script>
            r#"

            setTimeout(function() {
                console.log('start querying elements...');

                const leftResizer = document.querySelector('.resizer-left');
                const rightResizer = document.querySelector('.resizer-right');
                const leftPanel = document.querySelector('.node-panel-sidebar');
                const rightPanel = document.querySelector('.property-panel-sidebar');

                console.log('elements found:', {
                    leftResizer: leftResizer,
                    rightResizer: rightResizer,
                    leftPanel: leftPanel,
                    rightPanel: rightPanel
                });

                if (!leftResizer || !rightResizer || !leftPanel || !rightPanel) {
                    console.error('required elements not found');
                    return;
                }

                let isResizing = false;
                let currentResizer = null;
                let startX = 0;
                let startLeftWidth = 0;
                let startRightWidth = 0;

                function initResize(e) {
                    console.log('start resize', e.target.className);
                    isResizing = true;
                    currentResizer = e.target;
                    startX = e.clientX;
                    startLeftWidth = leftPanel.offsetWidth;
                    startRightWidth = rightPanel.offsetWidth;
                    currentResizer.classList.add('active');

                    document.addEventListener('mousemove', resize);
                    document.addEventListener('mouseup', stopResize);
                    e.preventDefault();
                }

                function resize(e) {
                    if (!isResizing) return;

                    const dx = e.clientX - startX;

                    if (currentResizer === leftResizer) {
                        const newLeftWidth = startLeftWidth + dx;
                        if (newLeftWidth >= 200 && newLeftWidth <= 600) {
                            leftPanel.style.width = newLeftWidth + 'px';
                        }
                    } else if (currentResizer === rightResizer) {
                        const newRightWidth = startRightWidth - dx;
                        if (newRightWidth >= 200 && newRightWidth <= 600) {
                            rightPanel.style.width = newRightWidth + 'px';
                        }
                    }
                }

                function stopResize() {
                    if (currentResizer) {
                        currentResizer.classList.remove('active');
                    }
                    isResizing = false;
                    currentResizer = null;
                    document.removeEventListener('mousemove', resize);
                    document.removeEventListener('mouseup', stopResize);
                    console.log('stop resize');
                }

                leftResizer.addEventListener('mousedown', initResize);
                rightResizer.addEventListener('mousedown', initResize);

                console.log('resize listeners registered');
            }, 100);
            "#
        </script>
    }
}
