# 36 - Dora C# 绑定性能基准测试报告

> C11 基准测试 baseline。目标是用可重复脚本对比 C# 绑定与 Dora Rust 原生节点的基础 bytes 链路延迟和吞吐，识别后续 C7 异步深度重构的优先优化方向。
> 测试日期：2026-06-29

---

## 1. 结论摘要

C11 baseline 已完成，并新增了可复现的 C# benchmark dataflow 与自动化脚本：

- C# benchmark 示例：`dora-api-csharp/samples/csharp-benchmark-dataflow/`
- 自动化脚本：`dora-api-csharp/scripts/benchmark-csharp-bindings.ps1`
- 根目录 wrapper：`scripts/benchmark-csharp-bindings.ps1`
- 本次结果 artifact：`dora-api-csharp/artifacts/benchmark/benchmark-20260629-151200/`

本次 Windows 本地 baseline 显示：

- C# bytes 链路可正常完成 10 档 payload 的 latency / throughput 测量。
- 小 payload 延迟上，C# 绑定相对 Rust 原生节点有明显固定开销。
- 512B 以上多数 payload 的平均延迟与 Rust 的差距收敛到约 1.0x-1.8x。
- 大 payload 吞吐在 16KB 以上基本接近 Rust，说明数据拷贝和调度成本在大块传输中被摊薄。
- 小 payload 吞吐波动较大，需要后续多轮运行取中位数，不能只凭单次结果下最终判断。

---

## 2. 运行环境

| 项目 | 值 |
| ---- | -- |
| OS | Microsoft Windows 10.0.22621 |
| 架构 | X64 |
| CPU 标识 | Intel64 Family 6 Model 158 Stepping 9, GenuineIntel |
| .NET SDK | 10.0.100 |
| PowerShell | 7.5.5 |
| Dora CLI | dora-cli 0.5.0 / dora-message 0.8.0 / dora-rs Python 0.5.0 |

说明：当前权限下 `Get-CimInstance Win32_Processor` / `Win32_OperatingSystem` 被拒绝访问，因此未记录核心数、内存容量等完整硬件信息。

---

## 3. 方法

### 3.1 C# benchmark

C# benchmark 复刻 Dora Rust 官方 benchmark 的 payload 档位：

```text
0, 8, 64, 512, 2048, 4096, 16384, 40960, 409600, 4096000 bytes
```

C# producer:

- latency 阶段每个 payload size 发送 1 条消息；
- throughput 阶段每个 payload size 发送 100 条消息；
- latency payload 前 8 字节写入 `Stopwatch.GetTimestamp()`，sink 使用同机 monotonic clock 计算微秒延迟；
- throughput 按 sink 端收到同 size 分组的时间窗口计算 messages/s。

C# sink 输出两类结果：

- 人类可读摘要；
- `BENCH,csharp,...` 机器可解析行，用于脚本生成 CSV / Markdown。

### 3.2 Rust benchmark

Rust 对照使用 Dora vendored source 内置 benchmark：

- `dora-api-csharp/third_party/dora/examples/benchmark/node`
- `dora-api-csharp/third_party/dora/examples/benchmark/sink`

脚本会先构建：

```powershell
cargo build -p benchmark-example-node -p benchmark-example-sink --release
```

然后运行官方 `examples/benchmark/dataflow.yml` 并解析输出。

### 3.3 复现命令

```powershell
pwsh ./dora-api-csharp/scripts/benchmark-csharp-bindings.ps1 `
  -SkipBuild `
  -IncludeRust `
  -ThroughputMessages 100 `
  -TimeoutSeconds 180 `
  -DoraPath ./dora-api-csharp/third_party/dora/target/release/dora.exe
```

---

## 4. Baseline 数据

### 4.1 Latency

单位：微秒。C# 的 P50 / P95 / P99 当前基于每档 1 条 latency 消息，仅作为格式占位；后续要做稳定分位数需要提高 latency sample count。

| Size bytes | C# avg us | Rust avg us | C# / Rust |
| ---------- | --------- | ----------- | --------- |
| 0 | 14088.1 | 543.9 | 25.90x |
| 8 | 23941.0 | 303.3 | 78.94x |
| 64 | 8040.4 | 225.6 | 35.64x |
| 512 | 486.4 | 385.7 | 1.26x |
| 2048 | 568.4 | 325.0 | 1.75x |
| 4096 | 7962.5 | 6417.7 | 1.24x |
| 16384 | 7373.6 | 6132.6 | 1.20x |
| 40960 | 7409.8 | 7264.5 | 1.02x |
| 409600 | 9969.9 | 6206.1 | 1.61x |
| 4096000 | 11984.7 | 6661.9 | 1.80x |

### 4.2 Throughput

单位：messages/s。每档 100 条消息。

| Size bytes | C# msg/s | Rust msg/s | C# / Rust |
| ---------- | -------- | ---------- | --------- |
| 0 | 6550.547 | 22720 | 0.29x |
| 8 | 24598.431 | 10132 | 2.43x |
| 64 | 4487.867 | 16068 | 0.28x |
| 512 | 19337.497 | 25272 | 0.77x |
| 2048 | 20907.380 | 7849 | 2.66x |
| 4096 | 1120.664 | 835 | 1.34x |
| 16384 | 142.017 | 151 | 0.94x |
| 40960 | 143.236 | 149 | 0.96x |
| 409600 | 125.188 | 145 | 0.86x |
| 4096000 | 114.291 | 116 | 0.99x |

---

## 5. 初步分析

### 5.1 小 payload 固定开销明显

0B / 8B / 64B 的 latency 差距较大，说明 C# 绑定的固定成本在小消息场景里占主导。可能来源：

- P/Invoke 边界；
- `DoraEvent` 托管对象创建与释放；
- `byte[]` materialization；
- 当前同步读取路径的调度成本；
- C# benchmark 自身使用 payload header 计时，与 Rust 使用 Dora metadata timestamp 的口径不同。

### 5.2 中大 payload 差距收敛

512B 以上 latency 差距显著缩小，大部分在 1.0x-1.8x 区间。16KB 以上 throughput 也基本接近 Rust。这说明 C# 绑定在大块 bytes 传输下不是明显瓶颈。

### 5.3 throughput 单次结果存在波动

8B / 2048B 档位出现 C# 高于 Rust 的结果，不应解读为 C# 更快。当前每档只有 100 条消息，且只跑单轮，容易受进程启动、调度、JIT、队列状态影响。后续更严谨版本应：

- 每个 benchmark 至少运行 5 轮；
- 丢弃 warmup；
- 记录 median / min / max；
- 固定 CPU governor / 电源模式；
- 同时采集 CPU 与 GC 指标。

---

## 6. 对 C7 的建议

C11 baseline 支持先做以下优化方向：

| 优化方向 | 价值 | 说明 |
| -------- | ---- | ---- |
| 减少小消息 materialization | 高 | `ev.Data` 当前会复制到 `byte[]`，小消息固定成本明显 |
| 增加 zero-copy / span reader API | 高 | 为高频小消息和中等 payload 降低托管分配 |
| 优化 async event pump | 中高 | C7 可基于 baseline 重点观察同步读取与后台线程泵差异 |
| 增加多轮 benchmark runner | 中 | 让结果从 smoke baseline 变成可比较性能门禁 |
| 引入 GC / allocation 统计 | 中 | 明确 C# 固定开销来自分配还是 native 边界 |

---

## 7. C11 状态

C11 当前状态：✅ baseline 已完成。

已完成内容：

- 新增 C# benchmark dataflow；
- 新增自动化 benchmark runner；
- 支持 C# / Rust 对照运行；
- 输出 CSV 与 Markdown artifact；
- 形成本报告并给出 C7 后续优化方向。

后续增强可作为独立性能工程继续推进：

- 多轮统计；
- CI 手动 benchmark workflow；
- allocation / GC 指标；
- Operator benchmark；
- Arrow / RecordBatch benchmark。
