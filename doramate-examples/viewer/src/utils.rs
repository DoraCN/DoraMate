use anyhow::Context;
use dora_node_api::arrow::array::{Array, Float32Array, Int32Array, StringArray, StructArray};
use opencv::core::Rect;

pub fn arrow_to_bboxes(
    struct_array: &StructArray,
) -> Result<Vec<(String, Rect, f32)>, Box<dyn std::error::Error>> {
    let len = struct_array.len();

    let class_array = struct_array
        .column(0)
        .as_any()
        .downcast_ref::<StringArray>()
        .context("Missing or incorrect class_name array")?;
    let conf_array = struct_array
        .column(1)
        .as_any()
        .downcast_ref::<Float32Array>()
        .context("Missing or incorrect confidence array")?;
    let x_array = struct_array
        .column(2)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("Missing or incorrect bbox_x array")?;
    let y_array = struct_array
        .column(3)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("Missing or incorrect bbox_y array")?;
    let w_array = struct_array
        .column(4)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("Missing or incorrect bbox_w array")?;
    let h_array = struct_array
        .column(5)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("Missing or incorrect bbox_h array")?;

    let mut bboxes = Vec::with_capacity(len);
    for i in 0..len {
        let name = class_array.value(i).to_owned();
        let conf = conf_array.value(i);
        let x = x_array.value(i);
        let y = y_array.value(i);
        let w = w_array.value(i);
        let h = h_array.value(i);

        let rect = Rect::new(x, y, w, h);
        bboxes.push((name, rect, conf));
    }

    Ok(bboxes)
}
