export type NodeType = 'input' | 'process' | 'output' | 'error';

export interface Port {
  id: string;
  name: string;
  type: 'input' | 'output';
  dataType?: string;
}

export interface NodeData {
  id: string;
  type: NodeType;
  name: string;
  icon: string;
  description?: string;
  inputs: Port[];
  outputs: Port[];
  config: Record<string, any>;
  position: { x: number; y: number };
}

export interface Connection {
  id: string;
  sourceNodeId: string;
  sourcePortId: string;
  targetNodeId: string;
  targetPortId: string;
}

export interface NodeTemplate {
  type: NodeType;
  name: string;
  icon: string;
  description: string;
  category: string;
  defaultInputs: Omit<Port, 'id'>[];
  defaultOutputs: Omit<Port, 'id'>[];
  defaultConfig: Record<string, any>;
}
