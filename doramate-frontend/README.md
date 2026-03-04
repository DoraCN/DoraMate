# DoraMate Frontend

基于 Leptos 和 WebAssembly 构建的可视化数据流编辑器，用于编辑和运行 [DORA](https://github.com/dora-rs/dora) 数据流图。

## 2026-03-03 今日更新

- 新增快捷键可视化配置窗口（工具栏 `快捷键` 按钮）
- 快捷键支持冲突检测、保存、恢复默认，并持久化到 `doramate_shortcuts_v1`
- 工具栏快捷键提示和全局监听改为响应式读取配置，修改后立即生效
- 自动布局新增同层 barycenter 正反向重排，复杂图连线交叉减少
- 节点模板支持自动持久化（内置模板 + 已保存模板 + 当前 YAML 模板合并）

## 项目结构

```
doramate-frontend/
├── src/
│   ├── lib.rs                 # 应用主入口和核心状态管理
│   ├── types.rs               # 核心数据类型定义
│   ├── node_registry.rs       # 节点注册表和内置节点定义
│   ├── components/            # UI 组件
│   │   ├── mod.rs             # 组件导出
│   │   ├── canvas.rs          # 画布组件（SVG 渲染、拖拽、缩放）
│   │   ├── node_panel.rs      # 节点面板（节点模板列表）
│   │   ├── property_panel.rs  # 属性面板（节点参数编辑）
│   │   ├── toolbar.rs         # 工具栏（菜单和快捷键）
│   │   ├── shortcut_settings.rs # 快捷键配置窗口
│   │   ├── log_panel.rs       # 日志面板
│   │   ├── status_panel.rs    # 状态面板
│   │   ├── connection.rs      # 连线组件（贝塞尔曲线）
│   │   └── ...
│   └── utils/
│       ├── mod.rs             # 工具模块导出
│       ├── api.rs             # LocalAgent API 客户端
│       ├── converter.rs       # YAML 转换器
│       ├── file.rs            # 文件操作
│       ├── geometry.rs        # 几何计算
│       ├── recent_files.rs    # 最近文件管理
│       └── shortcuts.rs       # 快捷键配置
├── style/                     # CSS 样式
├── examples/                  # 示例代码
├── Cargo.toml                 # Rust 依赖配置
└── index.html                 # HTML 入口
```

## 核心功能

### 1. 可视化编辑
- **节点拖放** - 从节点面板拖拽节点到画布
- **连线管理** - 连接节点输入输出端口，支持多端口
- **画布操作** - 平移、缩放、框选
- **自动布局** - 基于分层拓扑 + 同层 barycenter sweep 的布局算法（降低边交叉）

### 2. 节点系统
- **内置节点库** - 预定义多种 DORA 节点类型
  - 输入节点：Camera、Microphone、Timer、Keyboard、MQTT Source
  - 处理节点：YOLOv8、SAM2、ResNet、Whisper、OpenCV Processor
  - 输出节点：OpenCV Plot、WebSocket Sink、Terminal Log、File Writer、MQTT Sink
  - 自定义节点：Python、Rust、C、C#
- **节点分类** - 按功能分类（输入/处理/输出/自定义）
- **端口定义** - 支持多种数据类型（Image、Text、Json、Audio、Video、Array）

### 3. 数据流管理
- **YAML 转换** - DoraMate 格式与 DORA YAML 格式互转
- **最近文件** - 自动记录打开过的数据流文件
- **撤销重做** - 支持 100 步历史记录
- **复制粘贴** - 支持节点复制、粘贴、重复
- **模板持久化** - 当前 YAML 节点模板自动与持久化模板合并并保存

### 4. 运行控制
- **启动/停止** - 通过 LocalAgent 运行 DORA 数据流
- **状态监控** - WebSocket 实时查看节点运行状态
- **日志推送** - 实时日志流查看

### 5. 快捷键支持（含可视化配置）

- 支持在界面中修改快捷键（`快捷键` 按钮）
- 支持冲突检测与恢复默认
- 快捷键保存后实时生效并持久化

| 操作 | 快捷键 |
|------|--------|
| 新建 | Ctrl+N |
| 打开 | Ctrl+O |
| 保存 | Ctrl+S |
| 导出 YAML | Ctrl+E |
| 运行/停止 | Ctrl+R |
| 切换日志 | Ctrl+L |
| 撤销 | Ctrl+Z |
| 重做 | Ctrl+Y / Ctrl+Shift+Z |
| 复制 | Ctrl+C |
| 剪切 | Ctrl+X |
| 复制副本 | Ctrl+D |
| 粘贴 | Ctrl+V |
| 删除选中 | Delete |
| 全选 | Ctrl+A |
| 自动布局 | Ctrl+Shift+A |
| 清空画布 | Ctrl+Delete |

## 技术栈

| 技术 | 用途 |
|------|------|
| **Leptos 0.7** | 响应式前端框架 |
| **WebAssembly** | 编译为 wasm 在浏览器运行 |
| **wasm-bindgen** | Rust 与 JavaScript 互操作 |
| **serde / serde_yaml** | 序列化/反序列化 YAML |
| **web-sys** | Web API 绑定 |
| **gloo-storage** | 浏览器本地存储 |
| **gloo-timers** | 异步定时器 |
| **log / console_log** | 日志记录 |

## 构建与运行

### 使用 Trunk（开发与发布统一）

Trunk 是当前项目唯一推荐的 WebAssembly 构建与发布工具，提供：
- HTML 处理和资源管理
- 开发服务器
- 热重载（HMR）
- 自动构建与发布产物生成

```bash
# 检查 Trunk（若命令不存在，再执行安装）
trunk --version

# 安装 Trunk（环境中不存在 trunk 时）
cargo install --locked trunk
trunk --version

# 开发模式（自动监听文件变化并刷新浏览器）
trunk serve --open

# 发布构建
trunk build --release
```

发布产物位于 `dist/` 目录。

### 依赖要求

- Rust stable (1.70+)
- `trunk`
- 若环境中不存在 `trunk`，执行：`cargo install --locked trunk`

### DORA 运行时依赖（运行功能必需）

前端“运行/停止”功能依赖 LocalAgent，而 LocalAgent 需要在本机可执行 `dora` 命令。

先验证：

```bash
dora --version
```

若未安装，可使用 Cargo 方式安装：

```bash
cargo install --locked dora-cli
dora --version
```

如需其他安装方式，请参考 DORA 官方文档。

### 本地服务器

构建后可使用任意 HTTP 服务器运行：

```bash
# 方式 1: 使用 Python
python -m http.server 8080

# 方式 2: 使用 basic-http-server
cargo install basic-http-server
basic-http-server .

# 方式 3: 使用 Node.js
npx serve .
```

如果使用 `trunk serve`，则内置了开发服务器，无需额外配置。

## 数据类型定义

### Node（节点）
```rust
pub struct Node {
    pub id: String,           // 唯一标识符
    pub x: f64,               // X 坐标
    pub y: f64,               // Y 坐标
    pub label: String,        // 显示标签
    pub node_type: String,    // 节点类型
    pub path: Option<String>, // 节点路径
    pub env: Option<HashMap<String, String>>, // 环境变量
    pub config: Option<serde_yaml::Value>,    // 自定义配置
    pub outputs: Option<Vec<String>>,  // 输出端口
    pub inputs: Option<Vec<String>>,   // 输入端口
    pub scale: Option<f64>,            // 缩放比例
}
```

### Connection（连线）
```rust
pub struct Connection {
    pub from: String,
    pub to: String,
    pub from_port: Option<String>, // 输出端口名称
    pub to_port: Option<String>,   // 输入端口名称
}
```

### Dataflow（数据流图）
```rust
pub struct Dataflow {
    pub nodes: Vec<Node>,
    pub connections: Vec<Connection>,
}
```

## 与 LocalAgent 通信

前端通过 HTTP 和 WebSocket 与 LocalAgent 交互：

### API 端点
- `POST /api/run` - 运行数据流
- `POST /api/stop` - 停止数据流
- `GET /api/health` - 健康检查
- `POST /api/select-directory` - 选择目录
- `POST /api/open-dataflow-file` - 打开文件
- `POST /api/read-dataflow-file` - 读取文件
- `GET /api/node-templates-config` - 加载节点模板
- `POST /api/node-templates-config` - 保存节点模板
- `GET /api/status/:process_id` - 查询状态
- `GET /api/status-stream/:process_id` - 状态流 WebSocket
- `GET /api/logs/:process_id` - 日志 WebSocket

### WebSocket 连接
```rust
// 日志 WebSocket
let log_ws = LogWebSocket::new();
log_ws.connect(&process_id);
log_ws.set_on_message(|msg| { /* 处理日志 */ });

// 状态 WebSocket
let status_ws = StatusWebSocket::new();
status_ws.connect(&process_id);
status_ws.set_on_message(|status| { /* 处理状态 */ });
```

## 节点注册表

节点注册表 (`node_registry.rs`) 管理所有可用节点的定义：

```rust
// 获取内置节点
let camera_node = NODE_REGISTRY.get("camera_opencv");
let yolo_node = NODE_REGISTRY.get("yolo_v8");

// 按分类获取
let input_nodes = NODE_REGISTRY.get_by_category(NodeCategory::Input);

// 搜索节点
let results = NODE_REGISTRY.search("camera");
```

## 自动布局算法

采用基于拓扑排序的分层布局算法，并在同层加入 barycenter 正反向重排：

1. 构建节点依赖图
2. 收集弱连通分量
3. 对每个分量应用拓扑排序
4. 执行同层顺序优化（forward/backward sweeps）
5. 按层分配位置
6. 应用间距和偏移

```rust
fn apply_auto_layout(
    nodes: &mut [Node],
    connections: &[Connection],
    options: AutoLayoutOptions,
) -> bool {
    // 1. 构建入度表
    // 2. Kahn 算法拓扑排序
    // 3. 分层分配位置
    // 4. 应用新坐标
}
```

## 贡献指南

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 创建 Pull Request

## 许可证

本项目采用 MIT 许可证。

## 相关链接

- [DoraMate 项目主页](../README.md)
- [DORA 官方文档](https://dora-rs.github.io/)
- [Leptos 文档](https://leptos.dev/)
- [wasm-bindgen 指南](https://rustwasm.github.io/wasm-bindgen/)
