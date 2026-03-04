use crate::node_registry::{NodeCategory, NodeDefinition};
use leptos::prelude::*;
use serde::{Deserialize, Serialize};
use std::collections::HashSet;

/// Node template used by the left panel and drag/drop.
#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
pub struct NodeTemplate {
    pub id: String,
    pub name: String,
    pub description: String,
    pub category: NodeCategory,
    pub node_type: String,
    pub icon: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub path: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub inputs: Option<Vec<String>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub outputs: Option<Vec<String>>,
}

impl From<&NodeDefinition> for NodeTemplate {
    fn from(def: &NodeDefinition) -> Self {
        Self {
            id: def.id.clone(),
            name: def.name.clone(),
            description: def.description.clone(),
            category: def.category,
            node_type: def.node_type.clone(),
            icon: def.icon.clone(),
            path: def.path.clone(),
            inputs: def
                .inputs
                .as_ref()
                .map(|ports| ports.iter().map(|p| p.name.clone()).collect()),
            outputs: def
                .outputs
                .as_ref()
                .map(|ports| ports.iter().map(|p| p.name.clone()).collect()),
        }
    }
}

#[component]
pub fn NodePanel(
    on_add_node: Callback<NodeTemplate>,
    featured_templates: Signal<Vec<NodeTemplate>>,
    all_templates: Signal<Vec<NodeTemplate>>,
) -> impl IntoView {
    let (search_query, set_search_query) = signal(String::new());

    let featured_filtered = move || {
        let query = search_query.get().to_lowercase();
        featured_templates
            .get()
            .into_iter()
            .filter(|template| {
                if query.is_empty() {
                    return true;
                }
                template.name.to_lowercase().contains(&query)
                    || template.description.to_lowercase().contains(&query)
                    || template.node_type.to_lowercase().contains(&query)
            })
            .collect::<Vec<_>>()
    };

    let filtered_nodes = move || {
        let query = search_query.get().to_lowercase();
        let featured_node_types: HashSet<String> = featured_filtered()
            .into_iter()
            .map(|template| template.node_type)
            .collect();

        all_templates
            .get()
            .into_iter()
            .filter(|template| {
                if featured_node_types.contains(&template.node_type) {
                    return false;
                }

                if query.is_empty() {
                    return true;
                }

                template.name.to_lowercase().contains(&query)
                    || template.description.to_lowercase().contains(&query)
                    || template.node_type.to_lowercase().contains(&query)
            })
            .collect::<Vec<_>>()
    };

    view! {
        <div class="node-panel">
            <div class="node-panel-header">
                <h3>"节点库"</h3>
            </div>

            <div class="node-search">
                <input
                    type="text"
                    placeholder="搜索节点..."
                    prop:value=move || search_query.get()
                    on:input=move |ev| {
                        set_search_query.set(event_target_value(&ev));
                    }
                />
                <span class="search-icon">"🔍"</span>
            </div>

            <Show when=move || !featured_filtered().is_empty()>
                <div class="node-featured">
                    <div class="node-featured-header">"当前 YAML 节点类型"</div>
                    <NodeList nodes=featured_filtered() on_add_node=on_add_node.clone() />
                </div>
            </Show>

            <div class="node-list">
                {move || {
                    let nodes = filtered_nodes();
                    view! { <NodeList nodes=nodes on_add_node=on_add_node.clone() /> }
                }}
            </div>
        </div>
    }
}

#[component]
fn NodeTemplateItem(template: NodeTemplate, _on_add: Callback<NodeTemplate>) -> impl IntoView {
    let (is_dragging, set_is_dragging) = signal(false);

    let on_drag_start = {
        let template = template.clone();
        move |ev: web_sys::DragEvent| {
            set_is_dragging.set(true);

            if let Some(data_transfer) = ev.data_transfer() {
                let _ = data_transfer.set_effect_allowed("copy");
                if let Ok(json) = serde_json::to_string(&template) {
                    let _ = data_transfer.set_data("application/json", &json);
                }
            }
        }
    };

    let on_drag_end = move |_ev: web_sys::DragEvent| {
        set_is_dragging.set(false);
    };

    view! {
        <div
            class={move || format!(
                "node-template-item {}",
                if is_dragging.get() { "dragging" } else { "" }
            )}
            prop:draggable=true
            on:dragstart=on_drag_start
            on:dragend=on_drag_end
            style="cursor: grab;"
            title=template.node_type.clone()
        >
            <div class="node-info">
                <div class="node-name">{template.name}</div>
                <div class="node-type">{template.node_type.clone()}</div>
            </div>
            <div style="display:block;height:2px;margin-top:6px;background:#ff2d2d;border-radius:1px;"></div>
        </div>
    }
}

#[component]
fn NodeList(nodes: Vec<NodeTemplate>, on_add_node: Callback<NodeTemplate>) -> impl IntoView {
    let is_empty = nodes.is_empty();

    view! {
        <div class="node-list-content">
            {is_empty.then(|| view! {
                <div class="empty-state">
                    <p>"没有找到匹配的节点"</p>
                </div>
            })}

            {nodes
                .into_iter()
                .map(|template| {
                    let on_add = on_add_node.clone();
                    view! {
                        <NodeTemplateItem template=template _on_add=on_add />
                    }
                })
                .collect::<Vec<_>>()}
        </div>
    }
}
