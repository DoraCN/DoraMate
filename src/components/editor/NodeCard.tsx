import React, { useState, useRef, useEffect } from 'react';
import { NodeData, Port } from '@/types/node';
import * as Icons from 'lucide-react';
import { cn } from '@/lib/utils';

interface NodeCardProps {
  node: NodeData;
  isSelected: boolean;
  onSelect: () => void;
  onPositionChange: (position: { x: number; y: number }) => void;
  onPortMouseDown: (nodeId: string, portId: string, portType: 'input' | 'output', e: React.MouseEvent) => void;
  onPortMouseUp: (nodeId: string, portId: string, portType: 'input' | 'output') => void;
  getPortPosition: (nodeId: string, portId: string) => { x: number; y: number } | null;
  canvasOffset: { x: number; y: number };
}

export const NodeCard: React.FC<NodeCardProps> = ({
  node,
  isSelected,
  onSelect,
  onPositionChange,
  onPortMouseDown,
  onPortMouseUp,
  canvasOffset,
}) => {
  const [isDragging, setIsDragging] = useState(false);
  const [dragOffset, setDragOffset] = useState({ x: 0, y: 0 });
  const nodeRef = useRef<HTMLDivElement>(null);

  const nodeTypeClass = {
    input: 'node-input',
    process: 'node-process',
    output: 'node-output',
    error: 'node-error',
  }[node.type];

  const nodeBorderColor = {
    input: 'border-node-input',
    process: 'border-node-process',
    output: 'border-node-output',
    error: 'border-node-error',
  }[node.type];

  const IconComponent = (Icons as any)[node.icon] || Icons.Box;

  const handleMouseDown = (e: React.MouseEvent) => {
    if ((e.target as HTMLElement).closest('.node-port')) return;
    
    e.stopPropagation();
    onSelect();
    setIsDragging(true);
    
    const rect = nodeRef.current?.getBoundingClientRect();
    if (rect) {
      setDragOffset({
        x: e.clientX - rect.left,
        y: e.clientY - rect.top,
      });
    }
  };

  useEffect(() => {
    if (!isDragging) return;

    const handleMouseMove = (e: MouseEvent) => {
      const newX = e.clientX - canvasOffset.x - dragOffset.x;
      const newY = e.clientY - canvasOffset.y - dragOffset.y;
      onPositionChange({ x: Math.max(0, newX), y: Math.max(0, newY) });
    };

    const handleMouseUp = () => {
      setIsDragging(false);
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('mouseup', handleMouseUp);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isDragging, dragOffset, canvasOffset, onPositionChange]);

  return (
    <div
      ref={nodeRef}
      className={cn(
        'node-card absolute w-48 select-none',
        nodeTypeClass,
        isSelected && 'ring-2 ring-primary glow-primary',
        isDragging && 'z-50'
      )}
      style={{
        left: node.position.x,
        top: node.position.y,
      }}
      onMouseDown={handleMouseDown}
    >
      {/* Header */}
      <div className={cn('flex items-center gap-2 p-3 border-b', nodeBorderColor, 'border-opacity-30')}>
        <div className={cn(
          'w-8 h-8 rounded-lg flex items-center justify-center',
          node.type === 'input' && 'bg-node-input/20 text-node-input',
          node.type === 'process' && 'bg-node-process/20 text-node-process',
          node.type === 'output' && 'bg-node-output/20 text-node-output',
          node.type === 'error' && 'bg-node-error/20 text-node-error',
        )}>
          <IconComponent className="w-4 h-4" />
        </div>
        <span className="font-medium text-sm truncate">{node.name}</span>
      </div>

      {/* Ports */}
      <div className="py-2">
        {/* Input ports */}
        {node.inputs.map((port) => (
          <div
            key={port.id}
            className="flex items-center gap-2 px-3 py-1.5 relative"
          >
            <div
              className="node-port port-input absolute -left-1.5 cursor-crosshair"
              data-port-id={port.id}
              data-node-id={node.id}
              data-port-type="input"
              onMouseDown={(e) => {
                e.stopPropagation();
                onPortMouseDown(node.id, port.id, 'input', e);
              }}
              onMouseUp={() => onPortMouseUp(node.id, port.id, 'input')}
            />
            <span className="text-xs text-muted-foreground pl-2">{port.name}</span>
          </div>
        ))}

        {/* Output ports */}
        {node.outputs.map((port) => (
          <div
            key={port.id}
            className="flex items-center justify-end gap-2 px-3 py-1.5 relative"
          >
            <span className="text-xs text-muted-foreground pr-2">{port.name}</span>
            <div
              className="node-port port-output absolute -right-1.5 cursor-crosshair"
              data-port-id={port.id}
              data-node-id={node.id}
              data-port-type="output"
              onMouseDown={(e) => {
                e.stopPropagation();
                onPortMouseDown(node.id, port.id, 'output', e);
              }}
              onMouseUp={() => onPortMouseUp(node.id, port.id, 'output')}
            />
          </div>
        ))}
      </div>
    </div>
  );
};
