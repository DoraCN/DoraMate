use anyhow::{Context, Result};
use dora_node_api::{
    arrow::array::{StructArray, UInt8Array},
    DoraNode, Event,
};
use opencv::{
    core::{Point, Scalar, Size, Vector},
    imgcodecs, imgproc,
    prelude::*,
    videoio::{self, VideoWriter},
};

mod utils;

use utils::arrow_to_bboxes;

const OUTPUT_FILENAME: &str = "recording.avi";
const FPS: f64 = 30.0;

fn init_video_writer(
    output_filename: &str,
    fps: f64,
    width: i32,
    height: i32,
) -> Result<(VideoWriter, &'static str)> {
    let fourcc_codes = [
        (videoio::VideoWriter::fourcc('M', 'J', 'P', 'G')?, "MJPEG"),
        (videoio::VideoWriter::fourcc('X', 'V', 'I', 'D')?, "XVID"),
        (videoio::VideoWriter::fourcc('D', 'I', 'V', 'X')?, "DIVX"),
        (videoio::VideoWriter::fourcc('U', 'N', 'D', 'F')?, "UNDF"),
    ];

    for (fourcc, name) in fourcc_codes {
        let writer = VideoWriter::new(output_filename, fourcc, fps, Size::new(width, height), true);
        match writer {
            Ok(w) if w.is_opened()? => return Ok((w, name)),
            Ok(_) => {}
            Err(e) => {
                eprintln!("Codec {name} failed: {e}");
            }
        }
    }

    anyhow::bail!("failed to initialize VideoWriter with available codecs")
}

fn main() -> Result<()> {
    let (mut _node, mut events) =
        DoraNode::init_from_env().map_err(|e| anyhow::anyhow!("Dora init failed: {e}"))?;

    let output_filename =
        std::env::var("RECORDER_OUTPUT").unwrap_or_else(|_| OUTPUT_FILENAME.to_string());
    let fps: f64 = std::env::var("RECORDER_FPS")
        .ok()
        .and_then(|s| s.parse().ok())
        .filter(|v: &f64| *v > 0.0)
        .unwrap_or(FPS);

    println!("Recorder node started.");
    println!("  output: {output_filename}");
    println!("  fps: {fps}");

    let mut writer: Option<VideoWriter> = None;
    let mut used_fourcc_name: Option<&'static str> = None;
    let mut frame_count = 0_u64;
    let mut bboxes = Vec::new();

    while let Some(event) = events.recv() {
        match event {
            Event::Input { id, data, .. } => match id.as_str() {
                "detections" => {
                    let struct_array = data
                        .as_any()
                        .downcast_ref::<StructArray>()
                        .context("detections input is not StructArray")?;
                    bboxes = arrow_to_bboxes(struct_array)?;
                }
                "frame" => {
                    let uint8_array = data
                        .as_any()
                        .downcast_ref::<UInt8Array>()
                        .context("frame input is not UInt8Array")?;
                    let buffer = Vector::from_slice(uint8_array.values());
                    let frame = imgcodecs::imdecode(&buffer, imgcodecs::IMREAD_COLOR)
                        .context("failed to decode frame JPEG")?;

                    let size = frame.size()?;
                    if size.width <= 0 || size.height <= 0 {
                        continue;
                    }

                    let mut frame_to_write = frame.clone();

                    for (class_name, bbox, conf) in &bboxes {
                        imgproc::rectangle(
                            &mut frame_to_write,
                            *bbox,
                            Scalar::new(0.0, 255.0, 0.0, 0.0),
                            2,
                            imgproc::LINE_8,
                            0,
                        )?;

                        let text_origin = Point::new(bbox.x, (bbox.y - 6).max(0));
                        let label = format!("{class_name}: {conf:.2}");
                        imgproc::put_text(
                            &mut frame_to_write,
                            &label,
                            text_origin,
                            imgproc::FONT_HERSHEY_SIMPLEX,
                            0.7,
                            Scalar::new(0.0, 255.0, 0.0, 0.0),
                            2,
                            imgproc::LINE_8,
                            false,
                        )?;
                    }

                    if writer.is_none() {
                        let height = frame_to_write.rows();
                        let width = frame_to_write.cols();
                        let (new_writer, codec_name) =
                            init_video_writer(&output_filename, fps, width, height)?;
                        println!("VideoWriter initialized with codec: {codec_name} ({width}x{height})");
                        writer = Some(new_writer);
                        used_fourcc_name = Some(codec_name);
                    }

                    if let Some(w) = writer.as_mut() {
                        w.write(&frame_to_write)?;
                        frame_count += 1;
                        if frame_count % 30 == 0 {
                            println!("Recorded {frame_count} frames");
                        }
                    }
                }
                other => eprintln!("Unknown input `{other}`"),
            },
            _ => {}
        }
    }

    drop(writer);

    println!("Recorder node stopped.");
    println!("  total frames: {frame_count}");
    println!("  output: {output_filename}");
    if let Some(name) = used_fourcc_name {
        println!("  codec: {name}");
    }
    if frame_count > 0 {
        let duration = frame_count as f64 / fps;
        println!("  duration: {duration:.2}s");
    }

    Ok(())
}
