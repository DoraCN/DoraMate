# 17 - DoraMate 用户手册

## 1. 文档目的

本文档面向 DoraMate 系统的使用者、维护者和联调人员，覆盖以下内容：

- 系统概览和架构
- 前端部署与使用
- LocalAgent 部署与使用
- 功能使用说明
- 测试与验证
- 常见问题与排障

适用目录：
- `doramate-frontend/` - 前端可视化编辑器
- `doramate-localagent/` - 本地代理服务

关键关联：前端通过 HTTP/WebSocket 与 LocalAgent 通信（默认 `127.0.0.1:52100`）

---

## 2. 系统概览

DoraMate 是一个可视化数据流编辑器和运行平台，用于创建、编辑和运行 DORA 数据流图。系统由两个主要组件组成：

### 2.1 DoraMate Frontend
基于 Leptos + WebAssembly 构建的可视化数据流编辑器，提供：
- 节点拖拽建模界面
- 连接线创建和管理
- 属性和参数编辑面板
- YAML 导入、保存、导出与校验
- 本地运行/停止数据流（通过 LocalAgent API）
- 运行状态监控和日志面板
- 最近文件、工作目录、撤销重做、快捷键
- 自定义快捷键可视化配置
- 自定义节点模板自动持久化

### 2.2 DoraMate LocalAgent
DoraMate 前端与本机 DORA 运行环境之间的桥梁，负责：
- 接收前端请求并运行或停止数据流
- 提供本地文件与目录选择能力
- 返回数据流运行状态
- 通过 WebSocket 推送日志和状态
- 读写节点模板配置

---

## 3. 环境要求

### 3.1 前端环境要求
- Rust 工具链（建议 stable）
- `trunk`（用于构建/运行 WASM 前端）
- 现代浏览器（Chrome/Edge 等）

### 3.2 LocalAgent 环境要求
- Rust stable
- 可执行命令 `dora`（DORA CLI）

### 3.3 依赖安装说明

#### Trunk 安装
```powershell
trunk --version
# 如果提示找不到命令：
cargo install --locked trunk
trunk --version
```

#### DORA CLI 安装
```powershell
dora --version
# 如果提示找不到命令：
cargo install --locked dora-cli
dora --version
```

---

## 4. 部署与启动

### 4.1 开发模式部署（推荐）

步骤 1：启动 LocalAgent
```powershell
cd doramate-localagent
cargo run
```

或在仓库根目录使用：
```powershell
start-localagent.bat
```

步骤 2：启动前端开发服务
```powershell
cd doramate-frontend
trunk serve --open
```

默认前端访问地址：`http://127.0.0.1:8080`

### 4.2 发布构建部署

#### 前端发布构建
```powershell
cd doramate-frontend
trunk build --release
```
构建产物输出到 `doramate-frontend/dist/`，可由任意静态文件服务器托管。

注意：即使是发布构建，运行功能仍依赖本机 `127.0.0.1:52100` 的 LocalAgent。

#### LocalAgent 发布构建
```powershell
cd doramate-localagent
cargo build --release
```
产物位置：
- Windows：`doramate-localagent/target/release/doramate-localagent.exe`
- Linux/macOS：`doramate-localagent/target/release/doramate-localagent`

### 4.3 健康检查建议

LocalAgent 启动后，可检查：
```powershell
curl http://127.0.0.1:52100/api/health
```

预期返回：
```json
{
  "status": "ok",
  "version": "0.1.0",
  "dora_installed": true,
  "dora_coordinator_running": false,
  "dora_daemon_running": false
}
```

---

## 5. 前端功能详解

### 5.1 主界面结构
- 顶部：工具栏（新建/打开/保存/导出/校验/运行等）
- 上方状态区：运行状态、工作目录、节点统计、运行时长
- 左侧：节点库（含搜索、当前 YAML 节点类型）
- 中央：画布（节点与连线编辑）
- 右侧：属性面板（基础属性、环境变量、端口、配置）
- 可选日志面板：运行日志与过滤器

### 5.2 节点库使用
- 在左侧搜索框按名称/描述/`node_type` 搜索节点
- 从节点库拖拽节点到画布完成添加
- “当前 YAML 节点类型”分组会展示当前数据流中自动收集到的节点类型

内置分类（Node Registry）：
- Input：`camera_opencv`、`camera_v4l2`、`microphone`、`timer`、`keyboard`、`mqtt_source`
- Process：`yolo_v8`、`sam2`、`resnet`、`whisper`、`opencv_processor`、`text_detector`、`pose_estimation`、`depth_estimation`
- Output：`opencv_plot`、`websocket_sink`、`terminal_log`、`file_writer`、`mqtt_sink`
- Custom：`python_custom`、`rust_custom`、`c_custom`、`csharp_custom`

### 5.3 画布操作
- 添加节点：从左侧拖拽到画布
- 移动节点：鼠标左键拖拽节点
- 多选节点支持单击选择、`Shift + 单击` 追加/取消、空白区域框选
- `Shift + 框选` 可叠加到当前选择集
- 平移画布支持鼠标中键拖拽，或 `Alt + 左键` 拖拽
- 缩放画布：鼠标滚轮
- 自动聚焦：执行自动布局后会自动缩放与居中到目标节点区域

### 5.4 连线操作
- 点击某节点输入/输出端口进入“连线中”状态
- 再点击另一节点相反方向端口完成连线
- 系统会避免创建重复 `from -> to` 连线
- 连线中点有删除按钮（`x`），删除时会弹出确认框并同步清理目标节点输入引用

### 5.5 属性面板
选中节点后可查看/编辑：
- 基础信息：`id`（只读）、`node_type`、`label`、`path`
- 环境变量：键值对增删改
- 端口配置：输入端口、输出端口增删改
- 节点配置：YAML 文本编辑（`MinimalParameterEditor`）

说明：
- 属性编辑采用“查看/编辑”双模式，需保存后生效
- 输出端口名称变更时，会自动同步下游连接与输入映射
- 配置编辑器支持 YAML 语法错误友好提示（例如 JSON 花括号误用）

### 5.6 文件操作
#### 新建
- 点击“新建”
- 若存在未保存改动，会弹出确认框

#### 打开
优先流程（LocalAgent 可用）：
- 调用 LocalAgent 原生文件选择
- 读取内容并加载画布
- 自动设置 `current_file_path`
- 自动推断或接收 `working_dir`
- 若是绝对路径文件，写入最近文件

降级流程（LocalAgent 不可用或返回失败）：
- 回退到浏览器 `<input type="file">`
- 可读取 YAML，但无法获取绝对路径
- 因此通常不会新增最近文件记录

#### 最近文件
- 工具栏可直接打开最近文件
- 最近文件通过 LocalStorage 保存，最多 10 条，按路径去重
- 如果最近文件已失效（文件不存在），打开失败后会自动移除

#### 保存
- 有 `current_file_path` 时，按当前文件名下载 YAML，并清除未保存标记
- 无 `current_file_path` 时，弹出 Save As 对话框
- Save As 文件名要求：非空，后缀 `.yml/.yaml`，且不含非法字符 `< > : " | ? *`

#### 导出 YAML
- 生成并下载 `dataflow.yml`
- 与“保存”类似，都基于浏览器下载机制

### 5.7 运行与停止
运行流程：
- 点击“运行”
- 前端将当前数据流转换为 YAML
- 调用 `POST /api/run`，请求体包含 `dataflow_yaml` 与可选 `working_dir`
- 启动成功后会显示运行状态、自动展开日志面板、建立状态流（WebSocket）与轮询、启动运行时长计时

停止流程：
- 点击“停止”
- 调用 `POST /api/stop`
- 清理当前进程状态、节点运行态、计时与统计信息

### 5.8 状态与日志
状态面板显示：
- 运行中/已停止
- PID（短显示）
- 工作目录
- 运行时长
- 运行节点数/总节点数
- 错误节点数与进度条

日志面板支持：
- WebSocket 实时日志
- 级别筛选（info/warn/error/debug）
- 节点过滤
- 关键字搜索
- 自动滚动开关
- 清空与导出日志

---

## 6. LocalAgent 功能详解

### 6.1 LocalAgent 是什么
LocalAgent 是 DoraMate 前端与本机 DORA 运行环境之间的桥梁，负责：
1. 接收前端请求并运行或停止数据流
2. 提供本地文件与目录选择能力
3. 返回数据流运行状态
4. 通过 WebSocket 推送日志和状态
5. 读写节点模板配置

它不是云服务，也不是公网 API 网关。默认设计目标是：
- 本机使用
- 前端配套使用
- 本地 DORA 运行环境联动

### 6.2 当前版本已具备的能力
截至 2026-03-10，LocalAgent 已具备：
- `health / run / stop / status` 主链路
- 原生目录选择
- 原生文件打开
- 按路径读取文件
- 打开保存对话框并写文件
- 按路径直接写文件
- 节点模板配置读写
- 日志 WebSocket
- 状态流 WebSocket
- `dora start` 有界重试
- 失败诊断增强
- 残留节点进程清理

### 6.3 API 一览
当前代码实际提供的主要接口为：
- `GET /api/health`
- `POST /api/run`
- `POST /api/stop`
- `POST /api/select-directory`
- `POST /api/open-dataflow-file`
- `POST /api/read-dataflow-file`
- `POST /api/save-dataflow-file`
- `POST /api/write-dataflow-file`
- `GET /api/node-templates-config`
- `POST /api/node-templates-config`
- `GET /api/status/:process_id`
- `GET /api/status-stream/:process_id`
- `GET /api/logs/:process_id`

### 6.4 核心 API 说明

#### 运行数据流：`POST /api/run`
请求体示例：
```json
{
  "dataflow_yaml": "nodes:\n  - id: camera\n    path: opencv-video-capture\n",
  "working_dir": "C:\\Users\\Administrator\\projects\\dora-work"
}
```

当前行为：
- `working_dir` 可选；为空时使用 LocalAgent 当前工作目录
- LocalAgent 会把 YAML 写到工作目录中的临时文件
- 若 YAML 中包含 `__doramate__` 元数据，会先尝试清理后再交给 DORA
- 若运行时未就绪，会尝试自动拉起 coordinator 和 daemon
- `dora start` 采用有界重试：
  - 最大尝试次数：2
  - 重试间隔：800ms
  - 仅对可恢复错误重试

成功响应示例：
```json
{
  "success": true,
  "message": "Dataflow started successfully",
  "process_id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "error_code": null
}
```

常见失败错误码：
- `YAML_WRITE_FAILED`
- `DORA_NOT_INSTALLED`
- `DORA_RUNTIME_INIT_FAILED`
- `DORA_START_WAIT_FAILED`
- `DORA_START_TIMEOUT`
- `DORA_START_FAILED`
- `DORA_START_SPAWN_FAILED`

#### 停止数据流：`POST /api/stop`
停止指定流程：
```json
{
  "process_id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

停止当前注册的全部流程：
```json
{
  "process_id": null
}
```

行为说明：
- 若该流程已记录 DORA UUID，则优先按 UUID 执行 `dora stop`
- 若无 UUID，则退回到 `dora stop --all`
- 停止后会尝试清理残留节点进程

若部分流程停止失败，响应可能仍然是 `success=true`，但：
- `message` 会显示 `with N errors`
- `error_code` 会是 `STOP_PARTIAL_FAILURE`

#### 目录选择：`POST /api/select-directory`
打开系统原生目录选择器。

可能结果：
- 选择成功：返回 `path`
- 用户取消：返回 `cancelled=true`
- 打开选择器失败：返回错误码

常见错误码：
- `DIRECTORY_SELECTION_CANCELLED`
- `DIRECTORY_PICKER_FAILED`

#### 打开文件：`POST /api/open-dataflow-file`
打开系统原生文件选择器，并直接读取 YAML 内容返回。

返回中常见字段：
- `file_path`
- `file_name`
- `working_dir`
- `content`

常见错误码：
- `FILE_SELECTION_CANCELLED`
- `FILE_PICKER_FAILED`
- `FILE_READ_FAILED`

#### 按路径读取文件：`POST /api/read-dataflow-file`
请求体：
```json
{
  "file_path": "C:\\path\\to\\flow.yml"
}
```

常见错误码：
- `FILE_PATH_EMPTY`
- `FILE_READ_FAILED`

#### 保存文件：`POST /api/save-dataflow-file`
当前已实现“打开原生保存对话框并写入文件”的流程，供前端保存和另存为使用。

适合场景：
- 首次保存
- 另存为
- 需要通过系统保存对话框决定目标路径

#### 按路径写文件：`POST /api/write-dataflow-file`
当前已实现“已知路径直接写入”的流程，供普通保存使用。

适合场景：
- 已经有现成文件路径
- 需要静默保存

若路径为空，常见错误码为：
- `FILE_PATH_EMPTY`

#### 节点模板配置：`/api/node-templates-config`
##### 读取：`GET /api/node-templates-config`
行为：
- 配置不存在时，通常返回空模板列表而不是硬错误
- 支持常见配置结构读取

##### 写入：`POST /api/node-templates-config`
请求体示例：
```json
{
  "templates": [
    {
      "node_type": "python_custom",
      "name": "Python Custom",
      "description": "custom node",
      "icon": "PY",
      "path": null,
      "inputs": ["input"],
      "outputs": ["output"]
    }
  ]
}
```

写入前会进行标准化：
- `node_type` 去空
- 按 `node_type` 去重
- 端口去空、去重
- 缺省字段补默认值

常见错误码：
- `NODE_TEMPLATES_CONFIG_PATH_UNAVAILABLE`
- `NODE_TEMPLATES_CONFIG_READ_FAILED`
- `NODE_TEMPLATES_CONFIG_WRITE_FAILED`

配置文件路径规则：
- Windows 优先：`%APPDATA%\DoraMate\node_templates.yml`
- Windows 回退：`%USERPROFILE%\AppData\Roaming\DoraMate\node_templates.yml`
- Linux/macOS 优先：`$XDG_CONFIG_HOME/doramate/node_templates.yml`
- Linux/macOS 回退：`$HOME/.config/doramate/node_templates.yml`

#### 状态查询：`GET /api/status/:process_id`
返回中常见字段：
- `status`
- `uptime_seconds`
- `total_nodes`
- `running_nodes`
- `error_nodes`
- `node_details`

当前 `status` 常见值：
- `running`
- `stopped`
- `not_found`

说明：
- 节点运行状态属于启发式检测
- 服务重启后，内存中的 process registry 会丢失

### 6.5 WebSocket 使用说明

#### 状态流：`GET /api/status-stream/:process_id`
行为：
- 约 800ms 推送一次状态
- 推送结构与 `GET /api/status/:process_id` 接近
- 当状态变为 `stopped` 或 `not_found` 时，服务端会结束推送

#### 日志流：`GET /api/logs/:process_id`
连接后行为：
1. 先发送一条系统连接日志
2. 回放 backlog
3. 继续实时推送 stdout / stderr / system 日志

当前日志 backlog 限制：
- 最多 1000 条

日志结构常见字段：
- `timestamp`
- `level`
- `source`
- `message`
- `node_id`
- `process_id`

### 6.6 运行机制与当前限制

#### 运行时自动拉起
在执行 `/api/run` 之前，LocalAgent 会尝试检查：
1. coordinator 是否在线
2. daemon 是否在线

若未就绪，会尝试自动启动。

#### 状态是内存态
LocalAgent 当前用内存维护运行进程信息。这意味着：
- LocalAgent 重启后，这部分状态不会自动恢复
- 前端重新查询旧 `process_id` 时，可能得到 `not_found`

---

## 7. 快捷键支持

### 7.1 默认快捷键
快捷键在非输入框焦点时生效（避免干扰文本编辑）：

| 动作 | 默认快捷键 |
|------|------------|
| 新建 | `Ctrl+N` |
| 打开 | `Ctrl+O` |
| 保存 | `Ctrl+S` |
| 导出 YAML | `Ctrl+E` |
| 运行/停止切换 | `Ctrl+R` |
| 日志面板开关 | `Ctrl+L` |
| 撤销 | `Ctrl+Z` |
| 重做 | `Ctrl+Y` / `Ctrl+Shift+Z` |
| 复制 | `Ctrl+C` |
| 剪切 | `Ctrl+X` |
| 复制并偏移（Duplicate） | `Ctrl+D` |
| 粘贴 | `Ctrl+V` |
| 删除选中 | `Delete` |
| 全选 | `Ctrl+A` |
| 自动布局 | `Ctrl+Shift+A` |
| 清空画布 | `Ctrl+Delete` |

### 7.2 可视化快捷键配置（2026-03-03 新增）
入口：
- 工具栏点击 `快捷键` 按钮（`K` 图标）

能力：
- 按操作项编辑主快捷键
- 支持组合键修饰（`Ctrl/Shift/Alt`）
- 实时显示预览文本
- 冲突检测（同一组合键绑定到多个动作时会阻止保存）
- 一键恢复默认

持久化说明：
- 保存后写入浏览器 LocalStorage：`doramate_shortcuts_v1`
- 刷新页面后配置会自动恢复
- 键盘监听与工具栏提示会立即使用最新配置

---

## 8. 测试与验证

### 8.1 前端测试
在 `doramate-frontend` 目录执行：
```powershell
cargo test --lib
```

当前仓库验证结果（2026-03-03）：
- 共 35 个测试
- 通过 35，失败 0

覆盖范围包括：
- 打开流程状态判断（fallback、工作目录、最近文件）
- 撤销重做与历史快照
- 自动布局算法
- 子图复制/粘贴/删除
- YAML 双向转换
- API URL 与错误映射
- 快捷键解析
- 快捷键冲突检测与主绑定更新

#### 构建验证
```powershell
trunk --version
trunk build --release
```

当前验证（2026-03-01）：
- `trunk 0.21.14`
- 发布构建成功，产物已生成到 `dist/`

### 8.2 LocalAgent 测试
在 `doramate-localagent` 目录执行：
```powershell
cargo test --locked
```

结果：
- 28 passed
- 0 failed

当前覆盖重点包括：
- `/api/run` 各类错误码路径
- `/api/stop` 成功与部分失败路径
- `/api/status` 的 `not_found` 与 `stopped`
- 文件读取与写入参数校验
- 进程名标准化与清理逻辑
- 节点模板配置标准化
- `dora start` 可重试与不可重试错误判定

### 8.3 手工验收建议

#### 前端验收
- 打开 LocalAgent 正常时，验证“打开 -> 解析 -> 工作目录自动填充”
- 停掉 LocalAgent 后，验证浏览器 fallback 打开
- 新建 3~5 节点并连线，执行自动布局
- 编辑节点端口并检查下游引用同步
- 运行/停止数据流并观察状态、日志、运行时长
- 执行撤销/重做、复制/粘贴、删除/清空

#### LocalAgent 验收
```powershell
# 健康检查
curl http://127.0.0.1:52100/api/health

# 运行测试流程
$body = @{
  dataflow_yaml = "nodes:`n  - id: camera`n    path: opencv-video-capture`n"
  working_dir   = "C:\Users\Administrator\projects\dora-work"
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
  -Uri "http://127.0.0.1:52100/api/run" `
  -ContentType "application/json" `
  -Body $body

# 查询状态
Invoke-RestMethod -Method Get `
  -Uri "http://127.0.0.1:52100/api/status/<process_id>"

# 停止流程
$body = @{ process_id = "<process_id>" } | ConvertTo-Json
Invoke-RestMethod -Method Post `
  -Uri "http://127.0.0.1:52100/api/stop" `
  -ContentType "application/json" `
  -Body $body
```

---

## 9. 常见问题（FAQ）

### 9.1 前端常见问题

#### 9.1.1 点击运行无响应或报连接错误
现象：日志提示无法连接 `127.0.0.1:52100`。
处理：
- 确认 `doramate-localagent` 已启动
- 检查端口是否监听
- 校验本机防火墙策略

#### 9.1.2 “打开”后最近文件没有新增
常见于浏览器 fallback 打开。因为浏览器文件 API 无法提供绝对路径，系统不会记录到最近文件列表。
建议：启动 LocalAgent 后使用原生文件选择打开。

#### 9.1.3 工作目录无法浏览选择
原因通常是 LocalAgent 未启动或目录选择 API 调用失败。
处理：
- 启动 LocalAgent
- 在工作目录弹窗中手动输入绝对路径并确认

#### 9.1.4 YAML 配置保存报格式错误
请使用标准 YAML 语法，不要使用 JSON 花括号风格。
示例：
```yaml
width: 640
height: 480
fps: 30
```

### 9.2 LocalAgent 常见问题

#### 9.2.1 `health` 显示 `dora_installed=false`
原因：
- 系统找不到 `dora`

处理：
```powershell
cargo install --locked dora-cli
dora --version
```

#### 9.2.2 `run` 返回 `YAML_WRITE_FAILED`
原因：
- `working_dir` 不存在
- `working_dir` 无写权限

处理：
- 换成可写目录
- 先手工创建目录

#### 9.2.3 `run` 返回 `DORA_RUNTIME_INIT_FAILED`
原因：
- runtime 自动拉起失败
- coordinator / daemon 端口冲突

处理建议：
1. 手动检查 `dora coordinator` 和 `dora daemon`
2. 检查端口 `54500 / 54501 / 6012`
3. 查看 LocalAgent 控制台输出

#### 9.2.4 `run` 返回 `DORA_START_FAILED`
可能原因：
- YAML 本身不合法
- descriptor 不符合当前 DORA 环境要求
- 节点路径或配置错误

处理建议：
1. 先看返回消息中的 stderr 摘要
2. 检查工作目录下实际写出的 YAML
3. 检查节点 `path`、配置结构、descriptor 格式

#### 9.2.5 `stop` 返回成功但带错误数
表现：
- `success=true`
- `message` 中有 `with N errors`
- `error_code=STOP_PARTIAL_FAILURE`

说明：
- 这是“部分停止成功，部分失败”
- 需要继续检查 DORA 运行态和残留进程

#### 9.2.6 原生文件或目录选择器打不开
可能原因：
- 没有图形桌面会话
- 当前系统限制了弹窗权限

建议：
- 在有桌面环境的本机会话中运行 LocalAgent

#### 9.2.7 LocalAgent 重启后查不到旧进程
原因：
- 进程登记当前保存在内存中
- 重启后不会自动恢复历史 `process_id`

---

## 10. 推荐使用习惯

为了减少出错，建议每次都按下面顺序操作：
1. 新建或打开
2. 设置工作目录
3. 拖节点并连线
4. 编辑参数
5. 点击校验
6. 点击运行
7. 看日志和状态
8. 点击停止
9. 保存或导出 YAML

## 11. 一句话流程回顾
新建或打开 -> 拖节点 -> 连线 -> 改参数 -> 设工作目录 -> 校验 -> 运行 -> 看状态和日志 -> 停止 -> 保存。

## 12. 数据与安全说明
- 前端通过 LocalStorage 保存最近文件与快捷键，键名为 `doramate_recent_files` 与 `doramate_shortcuts_v1`
- 文件保存/导出通过浏览器下载机制落地到用户下载目录
- LocalAgent 默认本地监听 `127.0.0.1:52100`，不建议暴露到公网

## 13. 已知限制
- API 基地址当前写死为 `127.0.0.1:52100`，不支持前端动态配置远端代理
- 浏览器 fallback 打开无法获取绝对路径，影响最近文件与工作目录自动化
- 连线创建逻辑会基于节点首个输入/输出端口推断映射，不是完整多端口矩阵编辑器
- LocalAgent 当前用内存维护运行进程信息，重启后状态不会自动恢复

## 14. 参考文件
- `doramate-frontend/src/lib.rs`
- `doramate-frontend/src/components/toolbar.rs`
- `doramate-frontend/src/components/canvas.rs`
- `doramate-frontend/src/components/property_panel.rs`
- `doramate-frontend/src/components/log_panel.rs`
- `doramate-frontend/src/components/status_panel.rs`
- `doramate-frontend/src/components/save_dialog.rs`
- `doramate-frontend/src/utils/api.rs`
- `doramate-frontend/src/utils/converter.rs`
- `doramate-frontend/src/utils/file.rs`
- `doramate-frontend/src/utils/recent_files.rs`
- `doramate-frontend/src/utils/shortcuts.rs`
- `doramate-localagent/src/main.rs`
- `doramate-localagent/README.md`
- `docs/DELIVERY_STATUS.md`
- `docs/NEXT_SPRINT_2026-03.md`