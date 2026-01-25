import React, { useRef, useState, useEffect, useCallback } from 'react';
import { NodeData, Connection, NodeTemplate } from '@/types/node';
import { NodeCard } from './NodeCard';
import { ConnectionLine } from './ConnectionLine';
import { ConnectionInfoPopover } from './ConnectionInfoPopover';

interface CanvasProps {
  nodes: NodeData[];
  connections: Connection[];
  selectedNodeId: string | null;
  onNodeSelect: (nodeId: string | null) => void;
  onNodePositionChange: (nodeId: string, position: { x: number; y: number }) => void;
  onAddConnection: (sourceNodeId: string, sourcePortId: string, targetNodeId: string, targetPortId: string) => void;
  onDeleteConnection: (connectionId: string) => void;
  onAddNode: (template: NodeTemplate, position: { x: number; y: number }) => void;
  draggedTemplate: NodeTemplate | null;
  setDraggedTemplate: (template: NodeTemplate | null) => void;
}

export const Canvas: React.FC<CanvasProps> = ({
  nodes,
  connections,
  selectedNodeId,
  onNodeSelect,
  onNodePositionChange,
  onAddConnection,
  onDeleteConnection,
  onAddNode,
  draggedTemplate,
  setDraggedTemplate,
}) => {
  const canvasRef = useRef<HTMLDivElement>(null);
  const svgRef = useRef<SVGSVGElement>(null);
  const [canvasOffset, setCanvasOffset] = useState({ x: 0, y: 0 });
  const [connectingFrom, setConnectingFrom] = useState<{
    nodeId: string;
    portId: string;
    portType: 'input' | 'output';
  } | null>(null);
  const [tempLineEnd, setTempLineEnd] = useState<{ x: number; y: number } | null>(null);
  const [portPositions, setPortPositions] = useState<Map<string, { x: number; y: number }>>(new Map());
  
  // Connection click state
  const [selectedConnection, setSelectedConnection] = useState<{
    connection: Connection;
    position: { x: number; y: number };
  } | null>(null);

  useEffect(() => {
    const updateOffset = () => {
      if (canvasRef.current) {
        const rect = canvasRef.current.getBoundingClientRect();
        setCanvasOffset({ x: rect.left, y: rect.top });
      }
    };

    updateOffset();
    window.addEventListener('resize', updateOffset);
    return () => window.removeEventListener('resize', updateOffset);
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => {
      const newPositions = new Map<string, { x: number; y: number }>();
      
      document.querySelectorAll('.node-port').forEach((port) => {
        const portId = port.getAttribute('data-port-id');
        const nodeId = port.getAttribute('data-node-id');
        if (portId && nodeId && canvasRef.current) {
          const portRect = port.getBoundingClientRect();
          const canvasRect = canvasRef.current.getBoundingClientRect();
          newPositions.set(nodeId + '-' + portId, {
            x: portRect.left + portRect.width / 2 - canvasRect.left,
            y: portRect.top + portRect.height / 2 - canvasRect.top,
          });
        }
      });

      setPortPositions(newPositions);
    }, 50);

    return () => clearTimeout(timer);
  }, [nodes]);

  const getPortPosition = useCallback((nodeId: string, portId: string) => {
    return portPositions.get(nodeId + '-' + portId) || null;
  }, [portPositions]);

  const handlePortMouseDown = useCallback((
    nodeId: string,
    portId: string,
    portType: 'input' | 'output',
    e: React.MouseEvent
  ) => {
    setConnectingFrom({ nodeId, portId, portType });
    const canvasRect = canvasRef.current?.getBoundingClientRect();
    if (canvasRect) {
      setTempLineEnd({
        x: e.clientX - canvasRect.left,
        y: e.clientY - canvasRect.top,
      });
    }
  }, []);

  const handlePortMouseUp = useCallback((
    nodeId: string,
    portId: string,
    portType: 'input' | 'output'
  ) => {
    if (connectingFrom && connectingFrom.nodeId !== nodeId) {
      if (connectingFrom.portType === 'output' && portType === 'input') {
        onAddConnection(connectingFrom.nodeId, connectingFrom.portId, nodeId, portId);
      } else if (connectingFrom.portType === 'input' && portType === 'output') {
        onAddConnection(nodeId, portId, connectingFrom.nodeId, connectingFrom.portId);
      }
    }
    setConnectingFrom(null);
    setTempLineEnd(null);
  }, [connectingFrom, onAddConnection]);

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    if (connectingFrom && canvasRef.current) {
      const canvasRect = canvasRef.current.getBoundingClientRect();
      setTempLineEnd({
        x: e.clientX - canvasRect.left,
        y: e.clientY - canvasRect.top,
      });
    }
  }, [connectingFrom]);

  const handleMouseUp = useCallback(() => {
    setConnectingFrom(null);
    setTempLineEnd(null);
  }, []);

  const handleCanvasClick = useCallback((e: React.MouseEvent) => {
    if (e.target === canvasRef.current || e.target === svgRef.current) {
      onNodeSelect(null);
      setSelectedConnection(null);
    }
  }, [onNodeSelect]);

  const handleConnectionClick = useCallback((
    connection: Connection,
    e: React.MouseEvent
  ) => {
    e.stopPropagation();
    const canvasRect = canvasRef.current?.getBoundingClientRect();
    if (canvasRect) {
      setSelectedConnection({
        connection,
        position: {
          x: e.clientX - canvasRect.left,
          y: e.clientY - canvasRect.top,
        },
      });
    }
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    if (draggedTemplate && canvasRef.current) {
      const canvasRect = canvasRef.current.getBoundingClientRect();
      const position = {
        x: e.clientX - canvasRect.left - 96,
        y: e.clientY - canvasRect.top - 30,
      };
      onAddNode(draggedTemplate, position);
      setDraggedTemplate(null);
    }
  }, [draggedTemplate, onAddNode, setDraggedTemplate]);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
  }, []);

  return (
    <div
      ref={canvasRef}
      className="flex-1 canvas-grid relative overflow-hidden"
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onClick={handleCanvasClick}
      onDrop={handleDrop}
      onDragOver={handleDragOver}
    >
      {/* Connection lines - rendered below nodes for display */}
      <svg
        ref={svgRef}
        className="absolute inset-0 w-full h-full pointer-events-none"
        style={{ zIndex: 1 }}
      >
        {connections.map((connection) => {
          const startPos = getPortPosition(connection.sourceNodeId, connection.sourcePortId);
          const endPos = getPortPosition(connection.targetNodeId, connection.targetPortId);
          
          if (!startPos || !endPos) return null;

          const isSelected = selectedConnection?.connection.id === connection.id;

          return (
            <ConnectionLine
              key={connection.id}
              startX={startPos.x}
              startY={startPos.y}
              endX={endPos.x}
              endY={endPos.y}
              isActive={isSelected}
            />
          );
        })}

        {connectingFrom && tempLineEnd && (
          <ConnectionLine
            startX={getPortPosition(connectingFrom.nodeId, connectingFrom.portId)?.x || 0}
            startY={getPortPosition(connectingFrom.nodeId, connectingFrom.portId)?.y || 0}
            endX={tempLineEnd.x}
            endY={tempLineEnd.y}
            isTemp
            isActive
          />
        )}
      </svg>

      {/* Clickable connection hit areas - rendered above nodes */}
      <svg
        className="absolute inset-0 w-full h-full"
        style={{ zIndex: 10, pointerEvents: 'none' }}
      >
        {connections.map((connection) => {
          const startPos = getPortPosition(connection.sourceNodeId, connection.sourcePortId);
          const endPos = getPortPosition(connection.targetNodeId, connection.targetPortId);
          
          if (!startPos || !endPos) return null;

          const dx = Math.abs(endPos.x - startPos.x);
          const controlOffset = Math.min(dx * 0.5, 100);
          const path = `M ${startPos.x} ${startPos.y} C ${startPos.x + controlOffset} ${startPos.y}, ${endPos.x - controlOffset} ${endPos.y}, ${endPos.x} ${endPos.y}`;

          return (
            <path
              key={`hit-${connection.id}`}
              d={path}
              fill="none"
              stroke="transparent"
              strokeWidth={20}
              strokeLinecap="round"
              className="cursor-pointer"
              style={{ pointerEvents: 'stroke' }}
              onClick={(e) => handleConnectionClick(connection, e)}
            />
          );
        })}
      </svg>

      <div className="absolute inset-0" style={{ zIndex: 2 }}>
        {nodes.map((node) => (
          <NodeCard
            key={node.id}
            node={node}
            isSelected={node.id === selectedNodeId}
            onSelect={() => onNodeSelect(node.id)}
            onPositionChange={(pos) => onNodePositionChange(node.id, pos)}
            onPortMouseDown={handlePortMouseDown}
            onPortMouseUp={handlePortMouseUp}
            getPortPosition={getPortPosition}
            canvasOffset={canvasOffset}
          />
        ))}
      </div>

      {/* Connection Info Popover */}
      {selectedConnection && (
        <ConnectionInfoPopover
          connection={selectedConnection.connection}
          nodes={nodes}
          position={selectedConnection.position}
          open={true}
          onOpenChange={(open) => {
            if (!open) setSelectedConnection(null);
          }}
          onDelete={() => {
            onDeleteConnection(selectedConnection.connection.id);
            setSelectedConnection(null);
          }}
        />
      )}

      {nodes.length === 0 && (
        <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
          <div className="text-center text-muted-foreground animate-fade-in">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-secondary/50 flex items-center justify-center">
              <svg className="w-8 h-8 opacity-50" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                <rect x="3" y="3" width="7" height="7" rx="1" />
                <rect x="14" y="3" width="7" height="7" rx="1" />
                <rect x="8.5" y="14" width="7" height="7" rx="1" />
                <path d="M6.5 10v2.5a1 1 0 001 1h3.5" />
                <path d="M17.5 10v2.5a1 1 0 01-1 1h-3.5" />
              </svg>
            </div>
            <p className="text-lg font-medium mb-1">从左侧拖入节点开始</p>
            <p className="text-sm">或导入现有的 YAML 文件</p>
          </div>
        </div>
      )}
    </div>
  );
};
