import React, { useState, useCallback } from 'react';
import { Toolbar } from './Toolbar';
import { NodePalette } from './NodePalette';
import { Canvas } from './Canvas';
import { PropertyPanel } from './PropertyPanel';
import { useNodeEditor } from '@/hooks/useNodeEditor';
import { NodeTemplate } from '@/types/node';
import { useToast } from '@/hooks/use-toast';

export const NodeEditor: React.FC = () => {
  const { toast } = useToast();
  const {
    nodes,
    connections,
    selectedNode,
    selectedNodeId,
    setSelectedNodeId,
    addNode,
    updateNodePosition,
    updateNodeConfig,
    deleteNode,
    addConnection,
    deleteConnection,
    clearCanvas,
  } = useNodeEditor();

  const [draggedTemplate, setDraggedTemplate] = useState<NodeTemplate | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [customTemplates, setCustomTemplates] = useState<NodeTemplate[]>([]);

  const handleAddCustomTemplate = useCallback((template: NodeTemplate) => {
    setCustomTemplates((prev) => [...prev, template]);
    toast({
      title: '节点已创建',
      description: `自定义节点 "${template.name}" 已添加到面板`,
    });
  }, [toast]);

  const handleOpen = useCallback(() => {
    toast({
      title: '打开文件',
      description: '文件选择对话框将在这里打开',
    });
  }, [toast]);

  const handleSave = useCallback(() => {
    toast({
      title: '保存成功',
      description: '数据流已保存到本地',
    });
  }, [toast]);

  const handleImportYAML = useCallback(() => {
    toast({
      title: '导入 YAML',
      description: '选择 YAML 文件进行导入',
    });
  }, [toast]);

  const handleExportYAML = useCallback(() => {
    // Simple YAML export simulation
    const yaml = nodes.map((node) => ({
      id: node.id,
      type: node.type,
      name: node.name,
      config: node.config,
      position: node.position,
    }));
    
    console.log('Exported YAML:', yaml);
    
    toast({
      title: '导出成功',
      description: `已导出 ${nodes.length} 个节点`,
    });
  }, [nodes, toast]);

  const handleRun = useCallback(() => {
    if (isRunning) {
      setIsRunning(false);
      toast({
        title: '已停止',
        description: '数据流执行已停止',
      });
    } else {
      setIsRunning(true);
      toast({
        title: '开始运行',
        description: '数据流正在执行中...',
      });
      
      // Simulate running for 3 seconds
      setTimeout(() => {
        setIsRunning(false);
        toast({
          title: '运行完成',
          description: '数据流执行成功',
        });
      }, 3000);
    }
  }, [isRunning, toast]);

  const handleValidate = useCallback(() => {
    // Check if all nodes have required connections
    const hasOrphanNodes = nodes.some((node) => {
      const hasInputConnections = connections.some(
        (c) => c.targetNodeId === node.id
      );
      const hasOutputConnections = connections.some(
        (c) => c.sourceNodeId === node.id
      );
      
      // Input nodes don't need input connections
      // Output nodes don't need output connections
      if (node.type === 'input') return !hasOutputConnections && node.outputs.length > 0;
      if (node.type === 'output') return !hasInputConnections && node.inputs.length > 0;
      
      return (!hasInputConnections && node.inputs.length > 0) || 
             (!hasOutputConnections && node.outputs.length > 0);
    });

    if (hasOrphanNodes) {
      toast({
        title: '验证失败',
        description: '存在未连接的节点',
        variant: 'destructive',
      });
    } else {
      toast({
        title: '验证通过',
        description: '所有节点连接正常',
      });
    }
  }, [nodes, connections, toast]);

  const handleClear = useCallback(() => {
    clearCanvas();
    toast({
      title: '已清空',
      description: '画布已清空',
    });
  }, [clearCanvas, toast]);

  return (
    <div className="h-screen flex flex-col bg-background">
      <Toolbar
        onOpen={handleOpen}
        onSave={handleSave}
        onImportYAML={handleImportYAML}
        onExportYAML={handleExportYAML}
        onRun={handleRun}
        onValidate={handleValidate}
        onClear={handleClear}
        isRunning={isRunning}
        hasNodes={nodes.length > 0}
      />

      <div className="flex-1 flex overflow-hidden">
        <NodePalette
          onDragStart={setDraggedTemplate}
          customTemplates={customTemplates}
          onAddCustomTemplate={handleAddCustomTemplate}
        />
        
        <Canvas
          nodes={nodes}
          connections={connections}
          selectedNodeId={selectedNodeId}
          onNodeSelect={setSelectedNodeId}
          onNodePositionChange={updateNodePosition}
          onAddConnection={addConnection}
          onDeleteConnection={deleteConnection}
          onAddNode={addNode}
          draggedTemplate={draggedTemplate}
          setDraggedTemplate={setDraggedTemplate}
        />

        <PropertyPanel
          selectedNode={selectedNode}
          onConfigChange={updateNodeConfig}
          onDeleteNode={deleteNode}
        />
      </div>
    </div>
  );
};
