# Interaction States Specification
> DoraMate UI 交互状态全集

本文档定义 DoraMate V1.0 中所有核心 UI 元素的 **状态变化规范**，
用于确保交互一致性、降低学习成本，并支持 CSS / AI 自动生成样式。

---

## 1. 状态设计的基本原则

- 状态可感知（视觉明确）
- 状态可预测（行为一致）
- 状态不过度（不干扰主流程）

---

## 2. 全局状态分类

| 状态 | 说明 |
|----|----|
| Default | 默认静态状态 |
| Hover | 指针悬停 |
| Active | 点击 / 拖拽中 |
| Selected | 已选中 |
| Disabled | 不可用 |
| Running | 执行中 |
| Error | 错误状态 |

---

## 3. 节点（Node）状态规范

### 3.1 Default
- 背景：基础色
- 边框：中性灰
- 阴影：轻微

---

### 3.2 Hover
- 边框高亮
- 阴影增强
- Header 可见拖拽提示

---

### 3.3 Selected
- 边框加粗
- 显示操作控件
- 画布其他节点弱化

---

### 3.4 Active（拖拽中）
- 半透明
- 禁止误触内部按钮

---

### 3.5 Running
- Header 显示运行指示
- 可使用动画（pulse / flow）
- 禁止结构性编辑

---

### 3.6 Error
- 边框红色
- 显示错误 icon
- Hover 时显示错误详情

---

## 4. 端口（Port）状态规范

| 状态 | 表现 |
|----|----|
| Default | 中性颜色 |
| Hover | 高亮 + 放大 |
| Connectable | 绿色提示 |
| Not Connectable | 灰色 / 红色 |
| Active | 连线中高亮 |

---

## 5. 连线（Edge）状态规范

### 5.1 Default
- 细线
- 中性颜色

### 5.2 Hover
- 线条加粗
- 高亮

### 5.3 Active / Dragging
- 跟随指针
- 半透明

### 5.4 Error
- 虚线 / 红色
- Hover 显示错误来源

---

## 6. 按钮与控件状态

| 状态 | 规范 |
|----|----|
| Hover | 亮度提升 |
| Active | 下压感 |
| Disabled | 低对比度 |
| Loading | Spinner + 禁用 |

---

## 7. 动画与过渡建议

- Hover / Active：150–200ms
- 状态切换：使用 ease-out
- 避免连续闪烁

---

## 8. 可访问性建议（基础）

- 状态不只靠颜色区分
- Error 必须有 icon 或文本
- Hover 不作为唯一反馈

---

## 9. 与 CSS / 前端的映射建议

- 使用统一状态类名：
  - `.is-hover`
  - `.is-active`
  - `.is-selected`
  - `.is-running`
  - `.is-error`

---

## 10. 设计目标总结

- 状态一致
- 行为可预期
- 学习成本低
- 工程可维护
