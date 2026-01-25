import { useState, useCallback } from 'react';
import { NodeData, Connection, NodeTemplate, Port } from '@/types/node';

let nodeIdCounter = 0;
const generateNodeId = () => `node-${++nodeIdCounter}`;
const generatePortId = () => `port-${Math.random().toString(36).substr(2, 9)}`;
const generateConnectionId = () => `conn-${Math.random().toString(36).substr(2, 9)}`;

export function useNodeEditor() {
  const [nodes, setNodes] = useState<NodeData[]>([]);
  const [connections, setConnections] = useState<Connection[]>([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [connectingFrom, setConnectingFrom] = useState<{
    nodeId: string;
    portId: string;
    portType: 'input' | 'output';
  } | null>(null);

  const addNode = useCallback((template: NodeTemplate, position: { x: number; y: number }) => {
    const newNode: NodeData = {
      id: generateNodeId(),
      type: template.type,
      name: template.name,
      icon: template.icon,
      description: template.description,
      inputs: template.defaultInputs.map((input) => ({
        ...input,
        id: generatePortId(),
      })),
      outputs: template.defaultOutputs.map((output) => ({
        ...output,
        id: generatePortId(),
      })),
      config: { ...template.defaultConfig },
      position,
    };

    setNodes((prev) => [...prev, newNode]);
    setSelectedNodeId(newNode.id);
    return newNode;
  }, []);

  const updateNodePosition = useCallback((nodeId: string, position: { x: number; y: number }) => {
    setNodes((prev) =>
      prev.map((node) =>
        node.id === nodeId ? { ...node, position } : node
      )
    );
  }, []);

  const updateNodeConfig = useCallback((nodeId: string, config: Record<string, any>) => {
    setNodes((prev) =>
      prev.map((node) =>
        node.id === nodeId ? { ...node, config: { ...node.config, ...config } } : node
      )
    );
  }, []);

  const deleteNode = useCallback((nodeId: string) => {
    setNodes((prev) => prev.filter((node) => node.id !== nodeId));
    setConnections((prev) =>
      prev.filter(
        (conn) => conn.sourceNodeId !== nodeId && conn.targetNodeId !== nodeId
      )
    );
    if (selectedNodeId === nodeId) {
      setSelectedNodeId(null);
    }
  }, [selectedNodeId]);

  const addConnection = useCallback((
    sourceNodeId: string,
    sourcePortId: string,
    targetNodeId: string,
    targetPortId: string
  ) => {
    // Prevent duplicate connections
    const exists = connections.some(
      (conn) =>
        conn.sourceNodeId === sourceNodeId &&
        conn.sourcePortId === sourcePortId &&
        conn.targetNodeId === targetNodeId &&
        conn.targetPortId === targetPortId
    );

    if (exists) return;

    // Prevent connecting to same node
    if (sourceNodeId === targetNodeId) return;

    const newConnection: Connection = {
      id: generateConnectionId(),
      sourceNodeId,
      sourcePortId,
      targetNodeId,
      targetPortId,
    };

    setConnections((prev) => [...prev, newConnection]);
  }, [connections]);

  const deleteConnection = useCallback((connectionId: string) => {
    setConnections((prev) => prev.filter((conn) => conn.id !== connectionId));
  }, []);

  const clearCanvas = useCallback(() => {
    setNodes([]);
    setConnections([]);
    setSelectedNodeId(null);
  }, []);

  const selectedNode = nodes.find((node) => node.id === selectedNodeId) || null;

  return {
    nodes,
    connections,
    selectedNode,
    selectedNodeId,
    isDragging,
    connectingFrom,
    setSelectedNodeId,
    setIsDragging,
    setConnectingFrom,
    addNode,
    updateNodePosition,
    updateNodeConfig,
    deleteNode,
    addConnection,
    deleteConnection,
    clearCanvas,
    setNodes,
    setConnections,
  };
}
