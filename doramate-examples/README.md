# doramate-examples

`doramate-examples` 是一个基于 Dora 的 Rust 示例工程，演示从摄像头采集到目标检测、实时可视化与录制的完整链路。

## 功能概览

数据流包含 4 个节点：

1. `webcam`：采集摄像头帧并输出 JPEG 字节流
2. `object_detection`：加载 YOLOv8 模型并输出检测结果
3. `viewer`：显示视频画面并叠加检测框
4. `recorder`：将叠加检测框后的画面写入视频文件

## 目录结构

```text
doramate-examples/
├── Cargo.toml
├── Cargo.lock
├── dataflow.yml
├── xydataflow.yml
├── webcam/
├── object_detection/
│   └── models/yolov8n.safetensors
├── viewer/
└── recorder/
```

## 环境要求

1. Rust stable（edition 2021）
2. Dora CLI（可执行 `dora --help`）
3. OpenCV 4（当前工程默认使用 `C:\vcpkg\installed\x64-windows`）
4. 可用摄像头设备

说明：

- 各节点 `build.rs` 会链接 `c:\vcpkg\installed\x64-windows\lib`
- 运行时需要 `c:\vcpkg\installed\x64-windows\bin` 下的 OpenCV DLL
- `dataflow.yml` 已为节点设置 PATH

## 快速开始（Dora CLI）

在 `doramate-examples` 目录执行：

```powershell
cargo build --release
dora start dataflow.yml
```

停止后可在当前目录看到录制文件（默认 `recording.avi`）。

## Dataflow 文件说明

### `dataflow.yml`（给 Dora CLI 运行）

- 标准 Dora 描述文件
- 可直接运行：`dora start dataflow.yml`
- 包含 `build` 字段，可触发各节点构建

### `xydataflow.yml`（给 DoraMate 前端编辑）

- 包含 DoraMate 自定义字段 `__doramate__`（例如布局坐标）
- Dora CLI 会严格校验 schema，不能直接 `dora start`
- 直接执行 `dora start xydataflow.yml` 会报错：`unknown field '__doramate__'`

## 在 doramate-frontend 打开 `xydataflow.yml`

`xydataflow.yml` 的主要用途是给 `doramate-frontend` 网页做可视化编辑。

基本流程：

1. 启动 `doramate-frontend`（以及它依赖的 localagent 服务）
2. 在网页中使用“打开/导入 YAML”功能
3. 选择本目录下的 `xydataflow.yml`
4. 在画布中查看和编辑节点布局、连线、配置

注意：

- 如果你要用命令行直接运行，请使用 `dataflow.yml`
- `xydataflow.yml` 适合前端可视化编辑，不适合直接喂给 `dora start`

## 配置项

`recorder` 节点支持：

- `RECORDER_OUTPUT`：输出文件名，默认 `recording.avi`
- `RECORDER_FPS`：录制帧率，默认 `30`

## 模型文件

`object_detection` 默认从以下路径加载模型：

```text
object_detection/models/yolov8n.safetensors
```

请确保从仓库根目录 `doramate-examples` 启动，避免相对路径失效。

## 节点输入输出

1. `webcam`
   - 输入：`tick`（`dora/timer/millis/100`）
   - 输出：`frame`
2. `object_detection`
   - 输入：`frame`
   - 输出：`detections`
3. `viewer`
   - 输入：`frame`、`detections`
   - 输出：无
4. `recorder`
   - 输入：`frame`、`detections`
   - 输出：无（写文件）

## 常见问题

### 1. `Could not open camera 0`

- 摄像头被占用，或索引不是 `0`
- 关闭占用进程后重试
- 必要时修改 `webcam/src/main.rs` 的 `CAMERA_INDEX`

### 2. OpenCV DLL 缺失

- 检查 `C:\vcpkg\installed\x64-windows\bin` 是否存在对应 DLL
- 检查运行环境 `PATH` 是否包含该目录

### 3. `Model file not found`

- 确认 `object_detection/models/yolov8n.safetensors` 存在
- 确认命令在 `doramate-examples` 根目录执行

### 4. `unknown field '__doramate__'`

- 原因：用 Dora CLI 直接运行了 `xydataflow.yml`
- 处理：改为 `dora start dataflow.yml`

