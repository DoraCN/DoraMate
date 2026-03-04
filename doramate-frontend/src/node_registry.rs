use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::LazyLock;

/// 节点定义 - 描述一个节点类型的所有元数据
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct NodeDefinition {
    /// 节点唯一标识符
    pub id: String,

    /// 节点显示名称
    pub name: String,

    /// 节点描述
    pub description: String,

    /// 节点分类
    pub category: NodeCategory,

    /// 节点类型 (对应 DORA node type)
    pub node_type: String,

    /// 节点图标 (emoji)
    pub icon: String,

    /// 节点路径 (可选，用于自定义 DORA node 路径)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub path: Option<String>,

    /// 构建命令 (可选，用于编译自定义节点)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub build: Option<String>,

    /// 默认环境变量
    #[serde(skip_serializing_if = "Option::is_none")]
    pub default_env: Option<HashMap<String, String>>,

    /// 默认配置
    #[serde(skip_serializing_if = "Option::is_none")]
    pub default_config: Option<HashMap<String, serde_yaml::Value>>,

    /// 输入端口定义
    #[serde(skip_serializing_if = "Option::is_none")]
    pub inputs: Option<Vec<PortDefinition>>,

    /// 输出端口定义
    #[serde(skip_serializing_if = "Option::is_none")]
    pub outputs: Option<Vec<PortDefinition>>,

    /// 可配置参数
    #[serde(skip_serializing_if = "Option::is_none")]
    pub parameters: Option<Vec<ParameterDefinition>>,
}

/// 端口定义
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct PortDefinition {
    /// 端口名称
    pub name: String,

    /// 端口类型
    #[serde(rename = "type")]
    pub port_type: PortDataType,

    /// 端口描述
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,

    /// 是否必需
    #[serde(default = "default_true")]
    pub required: bool,
}

fn default_true() -> bool {
    true
}

/// 端口数据类型
#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "lowercase")]
pub enum PortDataType {
    /// 任意类型
    Any,
    /// 图像数据
    Image,
    /// 文本/字符串
    Text,
    /// JSON 数据
    Json,
    /// 音频数据
    Audio,
    /// 视频数据
    Video,
    /// 数值数组
    Array,
    /// 自定义类型
    Custom(String),
}

/// 参数定义 - 用于配置节点的参数
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ParameterDefinition {
    /// 参数名称
    pub name: String,

    /// 参数显示名称
    pub label: String,

    /// 参数描述
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,

    /// 参数类型
    #[serde(rename = "type")]
    pub param_type: ParameterType,

    /// 默认值
    #[serde(skip_serializing_if = "Option::is_none")]
    pub default: Option<serde_yaml::Value>,

    /// 是否必需
    #[serde(default = "default_true")]
    pub required: bool,

    /// 可选值列表 (用于枚举类型)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub options: Option<Vec<ParameterOption>>,

    /// 验证规则
    #[serde(skip_serializing_if = "Option::is_none")]
    pub validation: Option<ValidationRule>,
}

/// 参数类型
#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "lowercase")]
pub enum ParameterType {
    /// 字符串
    String,
    /// 整数
    Integer,
    /// 浮点数
    Float,
    /// 布尔值
    Boolean,
    /// 枚举选择
    Enum,
    /// 文件路径
    FilePath,
    /// 目录路径
    DirectoryPath,
    /// JSON 对象
    JsonObject,
    /// 字符串数组
    StringArray,
}

/// 参数选项
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ParameterOption {
    /// 选项值
    pub value: String,

    /// 选项显示标签
    pub label: String,

    /// 选项描述
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
}

/// 验证规则
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ValidationRule {
    /// 最小值 (用于数值类型)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub min: Option<f64>,

    /// 最大值 (用于数值类型)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max: Option<f64>,

    /// 最小长度 (用于字符串/数组)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub min_length: Option<usize>,

    /// 最大长度 (用于字符串/数组)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_length: Option<usize>,

    /// 正则表达式 (用于字符串)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub pattern: Option<String>,

    /// 自定义验证器名称
    #[serde(skip_serializing_if = "Option::is_none")]
    pub custom_validator: Option<String>,
}

/// 节点分类
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum NodeCategory {
    /// 自定义节点
    Custom,
}

impl NodeCategory {
    pub fn display_name(&self) -> &'static str {
        "自定义节点"
    }

    pub fn icon(&self) -> &'static str {
        "🔧"
    }
}

/// 节点注册表 - 全局单例
pub static NODE_REGISTRY: LazyLock<NodeRegistry> = LazyLock::new(|| {
    let mut registry = NodeRegistry::new();
    register_builtin_nodes(&mut registry);
    registry
});

/// 节点注册表结构
#[derive(Debug)]
pub struct NodeRegistry {
    /// 节点定义映射 (id -> definition)
    definitions: HashMap<String, NodeDefinition>,
    /// 按分类索引的节点 ID
    by_category: HashMap<NodeCategory, Vec<String>>,
}

impl NodeRegistry {
    /// 创建新的节点注册表
    pub fn new() -> Self {
        Self {
            definitions: HashMap::new(),
            by_category: HashMap::new(),
        }
    }

    /// 注册节点定义
    pub fn register(&mut self, definition: NodeDefinition) {
        let id = definition.id.clone();
        let category = definition.category;
        self.definitions.insert(id.clone(), definition);
        self.by_category.entry(category).or_default().push(id);
    }

    /// 获取节点定义
    pub fn get(&self, id: &str) -> Option<&NodeDefinition> {
        self.definitions.get(id)
    }

    /// 获取所有节点定义
    pub fn get_all(&self) -> Vec<&NodeDefinition> {
        self.definitions.values().collect()
    }

    /// 按分类获取节点定义
    pub fn get_by_category(&self, category: NodeCategory) -> Vec<&NodeDefinition> {
        self.by_category
            .get(&category)
            .map(|ids| {
                ids.iter()
                    .filter_map(|id| self.definitions.get(id))
                    .collect()
            })
            .unwrap_or_default()
    }

    /// 搜索节点
    pub fn search(&self, query: &str) -> Vec<&NodeDefinition> {
        let query = query.to_lowercase();
        self.definitions
            .values()
            .filter(|def| {
                def.name.to_lowercase().contains(&query)
                    || def.description.to_lowercase().contains(&query)
                    || def.id.to_lowercase().contains(&query)
            })
            .collect()
    }
}

/// 注册内置节点（当前仅保留自定义节点类型）
fn register_builtin_nodes(registry: &mut NodeRegistry) {
    registry.register(python_custom());
    registry.register(rust_custom());
    registry.register(c_custom());
    registry.register(csharp_custom());
}

/// Python Custom Node
fn python_custom() -> NodeDefinition {
    NodeDefinition {
        id: "python_custom".to_string(),
        name: "Python Custom".to_string(),
        description: "自定义 Python 节点".to_string(),
        category: NodeCategory::Custom,
        node_type: "python_custom".to_string(),
        icon: "🐍".to_string(),
        path: None,
        build: Some("python $PYFILE".to_string()),
        default_env: None,
        default_config: None,
        inputs: Some(vec![PortDefinition {
            name: "input".to_string(),
            port_type: PortDataType::Any,
            description: Some("输入数据".to_string()),
            required: false,
        }]),
        outputs: Some(vec![PortDefinition {
            name: "output".to_string(),
            port_type: PortDataType::Any,
            description: Some("输出数据".to_string()),
            required: true,
        }]),
        parameters: Some(vec![
            ParameterDefinition {
                name: "script".to_string(),
                label: "Python 脚本".to_string(),
                description: Some("Python 脚本文件路径".to_string()),
                param_type: ParameterType::FilePath,
                default: None,
                required: true,
                options: None,
                validation: None,
            },
            ParameterDefinition {
                name: "dependencies".to_string(),
                label: "依赖包".to_string(),
                description: Some("Python 依赖包列表 (逗号分隔)".to_string()),
                param_type: ParameterType::StringArray,
                default: None,
                required: false,
                options: None,
                validation: None,
            },
        ]),
    }
}

/// Rust Custom Node
fn rust_custom() -> NodeDefinition {
    NodeDefinition {
        id: "rust_custom".to_string(),
        name: "Rust Custom".to_string(),
        description: "自定义 Rust 节点".to_string(),
        category: NodeCategory::Custom,
        node_type: "rust_custom".to_string(),
        icon: "🦀".to_string(),
        path: None,
        build: Some("cargo build --release".to_string()),
        default_env: None,
        default_config: None,
        inputs: Some(vec![PortDefinition {
            name: "input".to_string(),
            port_type: PortDataType::Any,
            description: Some("输入数据".to_string()),
            required: false,
        }]),
        outputs: Some(vec![PortDefinition {
            name: "output".to_string(),
            port_type: PortDataType::Any,
            description: Some("输出数据".to_string()),
            required: true,
        }]),
        parameters: Some(vec![
            ParameterDefinition {
                name: "path".to_string(),
                label: "节点路径".to_string(),
                description: Some("Rust 节点库路径".to_string()),
                param_type: ParameterType::DirectoryPath,
                default: None,
                required: true,
                options: None,
                validation: None,
            },
            ParameterDefinition {
                name: "shared_library".to_string(),
                label: "共享库".to_string(),
                description: Some("编译后的共享库文件名".to_string()),
                param_type: ParameterType::String,
                default: None,
                required: true,
                options: None,
                validation: None,
            },
        ]),
    }
}

/// C/C++ Custom Node
fn c_custom() -> NodeDefinition {
    NodeDefinition {
        id: "c_custom".to_string(),
        name: "C/C++ Custom".to_string(),
        description: "自定义 C/C++ 节点".to_string(),
        category: NodeCategory::Custom,
        node_type: "c_custom".to_string(),
        icon: "⚡".to_string(),
        path: None,
        build: Some("make".to_string()),
        default_env: None,
        default_config: None,
        inputs: Some(vec![PortDefinition {
            name: "input".to_string(),
            port_type: PortDataType::Any,
            description: Some("输入数据".to_string()),
            required: false,
        }]),
        outputs: Some(vec![PortDefinition {
            name: "output".to_string(),
            port_type: PortDataType::Any,
            description: Some("输出数据".to_string()),
            required: true,
        }]),
        parameters: Some(vec![
            ParameterDefinition {
                name: "path".to_string(),
                label: "节点路径".to_string(),
                description: Some("C/C++ 节点路径".to_string()),
                param_type: ParameterType::DirectoryPath,
                default: None,
                required: true,
                options: None,
                validation: None,
            },
            ParameterDefinition {
                name: "shared_library".to_string(),
                label: "共享库".to_string(),
                description: Some("编译后的共享库文件名".to_string()),
                param_type: ParameterType::String,
                default: None,
                required: true,
                options: None,
                validation: None,
            },
        ]),
    }
}

/// C# Custom Node
fn csharp_custom() -> NodeDefinition {
    NodeDefinition {
        id: "csharp_custom".to_string(),
        name: "C# Custom".to_string(),
        description: "自定义 C# 节点".to_string(),
        category: NodeCategory::Custom,
        node_type: "csharp_custom".to_string(),
        icon: "💜".to_string(),
        path: None,
        build: Some("dotnet build -c Release".to_string()),
        default_env: None,
        default_config: None,
        inputs: Some(vec![PortDefinition {
            name: "input".to_string(),
            port_type: PortDataType::Any,
            description: Some("输入数据".to_string()),
            required: false,
        }]),
        outputs: Some(vec![PortDefinition {
            name: "output".to_string(),
            port_type: PortDataType::Any,
            description: Some("输出数据".to_string()),
            required: true,
        }]),
        parameters: Some(vec![
            ParameterDefinition {
                name: "path".to_string(),
                label: "项目路径".to_string(),
                description: Some("C# 项目根目录".to_string()),
                param_type: ParameterType::DirectoryPath,
                default: None,
                required: true,
                options: None,
                validation: None,
            },
            ParameterDefinition {
                name: "assembly".to_string(),
                label: "程序集".to_string(),
                description: Some("编译后的程序集文件名 (如 MyNode.dll)".to_string()),
                param_type: ParameterType::String,
                default: None,
                required: true,
                options: None,
                validation: None,
            },
            ParameterDefinition {
                name: "class_name".to_string(),
                label: "类名".to_string(),
                description: Some("实现节点逻辑的类名 (含命名空间)".to_string()),
                param_type: ParameterType::String,
                default: Some(serde_yaml::Value::String("MyNamespace.MyNode".to_string())),
                required: true,
                options: None,
                validation: None,
            },
        ]),
    }
}

impl Default for ValidationRule {
    fn default() -> Self {
        Self {
            min: None,
            max: None,
            min_length: None,
            max_length: None,
            pattern: None,
            custom_validator: None,
        }
    }
}
