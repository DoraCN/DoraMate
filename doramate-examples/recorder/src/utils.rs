use anyhow::Context;
use dora_node_api::arrow::array::{Array, Float32Array, Int32Array, StringArray, StructArray};
use opencv::core::Rect;

pub fn arrow_to_bboxes(struct_array: &StructArray) -> Result<Vec<(String, Rect, f32)>, anyhow::Error> {
    let len = struct_array.len();

    let class_array = struct_array
        .column(0)
        .as_any()
        .downcast_ref::<StringArray>()
        .context("missing or invalid class_name array")?;
    let conf_array = struct_array
        .column(1)
        .as_any()
        .downcast_ref::<Float32Array>()
        .context("missing or invalid confidence array")?;
    let x_array = struct_array
        .column(2)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("missing or invalid bbox_x array")?;
    let y_array = struct_array
        .column(3)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("missing or invalid bbox_y array")?;
    let w_array = struct_array
        .column(4)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("missing or invalid bbox_w array")?;
    let h_array = struct_array
        .column(5)
        .as_any()
        .downcast_ref::<Int32Array>()
        .context("missing or invalid bbox_h array")?;

    let mut bboxes = Vec::with_capacity(len);
    for i in 0..len {
        bboxes.push((
            class_array.value(i).to_owned(),
            Rect::new(x_array.value(i), y_array.value(i), w_array.value(i), h_array.value(i)),
            conf_array.value(i),
        ));
    }

    Ok(bboxes)
}
