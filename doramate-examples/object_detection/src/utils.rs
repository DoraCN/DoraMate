use std::sync::Arc;

use dora_node_api::arrow::array::{ArrayRef, Float32Array, Int32Array, StringArray, StructArray};
use dora_node_api::arrow::datatypes::{DataType, Field, Fields, Schema};
use opencv::core::Rect;

pub fn bboxes_to_arrow(
    bboxes: Vec<(&'static str, Rect, f32)>,
) -> Result<StructArray, Box<dyn std::error::Error>> {
    let num_detections = bboxes.len();

    let mut class_names = Vec::with_capacity(num_detections);
    let mut confidences = Vec::with_capacity(num_detections);
    let mut bboxes_x = Vec::with_capacity(num_detections);
    let mut bboxes_y = Vec::with_capacity(num_detections);
    let mut bboxes_w = Vec::with_capacity(num_detections);
    let mut bboxes_h = Vec::with_capacity(num_detections);

    for (name, rect, conf) in bboxes {
        class_names.push(name);
        confidences.push(conf);
        bboxes_x.push(rect.x);
        bboxes_y.push(rect.y);
        bboxes_w.push(rect.width);
        bboxes_h.push(rect.height);
    }

    let class_array = StringArray::from(class_names);
    let conf_array = Float32Array::from(confidences);
    let x_array = Int32Array::from(bboxes_x);
    let y_array = Int32Array::from(bboxes_y);
    let w_array = Int32Array::from(bboxes_w);
    let h_array = Int32Array::from(bboxes_h);

    let fields = Fields::from(vec![
        Field::new("class_name", DataType::Utf8, false),
        Field::new("confidence", DataType::Float32, false),
        Field::new("bbox_x", DataType::Int32, false),
        Field::new("bbox_y", DataType::Int32, false),
        Field::new("bbox_w", DataType::Int32, false),
        Field::new("bbox_h", DataType::Int32, false),
    ]);
    let schema = Arc::new(Schema::new(fields));

    let arrays: Vec<ArrayRef> = vec![
        Arc::new(class_array),
        Arc::new(conf_array),
        Arc::new(x_array),
        Arc::new(y_array),
        Arc::new(w_array),
        Arc::new(h_array),
    ];

    let struct_array = StructArray::new(schema.fields.clone(), arrays, None);

    Ok(struct_array)
}
