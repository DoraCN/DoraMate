import React from 'react';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { Connection, NodeData } from '@/types/node';
import { ArrowRight, Trash2, Link2 } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ConnectionInfoPopoverProps {
  connection: Connection;
  nodes: NodeData[];
  position: { x: number; y: number };
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDelete: () => void;
}

export const ConnectionInfoPopover: React.FC<ConnectionInfoPopoverProps> = ({
  connection,
  nodes,
  position,
  open,
  onOpenChange,
  onDelete,
}) => {
  const sourceNode = nodes.find((n) => n.id === connection.sourceNodeId);
  const targetNode = nodes.find((n) => n.id === connection.targetNodeId);
  const sourcePort = sourceNode?.outputs.find((p) => p.id === connection.sourcePortId);
  const targetPort = targetNode?.inputs.find((p) => p.id === connection.targetPortId);

  if (!sourceNode || !targetNode) return null;

  const getNodeTypeColor = (type: string) => {
    switch (type) {
      case 'input':
        return 'text-node-input';
      case 'process':
        return 'text-node-process';
      case 'output':
        return 'text-node-output';
      default:
        return 'text-muted-foreground';
    }
  };

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>
        <div
          className="absolute w-1 h-1"
          style={{
            left: position.x,
            top: position.y,
            pointerEvents: 'none',
          }}
        />
      </PopoverTrigger>
      <PopoverContent className="w-72 p-0" side="top" sideOffset={10}>
        <div className="p-3 border-b border-border">
          <div className="flex items-center gap-2 text-sm font-medium">
            <Link2 className="w-4 h-4 text-primary" />
            连接信息
          </div>
        </div>

        <div className="p-3 space-y-3">
          <div className="flex items-center gap-2">
            <div className="flex-1 p-2 rounded-lg bg-secondary/50">
              <div className={cn('text-sm font-medium', getNodeTypeColor(sourceNode.type))}>
                {sourceNode.name}
              </div>
              <div className="text-xs text-muted-foreground mt-0.5">
                输出: {sourcePort?.name || '未知'}
                {sourcePort?.dataType && (
                  <span className="ml-1 opacity-70">({sourcePort.dataType})</span>
                )}
              </div>
            </div>

            <ArrowRight className="w-4 h-4 text-muted-foreground shrink-0" />

            <div className="flex-1 p-2 rounded-lg bg-secondary/50">
              <div className={cn('text-sm font-medium', getNodeTypeColor(targetNode.type))}>
                {targetNode.name}
              </div>
              <div className="text-xs text-muted-foreground mt-0.5">
                输入: {targetPort?.name || '未知'}
                {targetPort?.dataType && (
                  <span className="ml-1 opacity-70">({targetPort.dataType})</span>
                )}
              </div>
            </div>
          </div>

          <div className="text-xs text-muted-foreground bg-secondary/30 rounded-lg p-2">
            <div className="grid grid-cols-2 gap-1">
              <span>连接ID:</span>
              <span className="font-mono text-foreground/70">{connection.id.slice(0, 12)}...</span>
            </div>
          </div>
        </div>

        <div className="p-3 border-t border-border">
          <Button
            variant="destructive"
            size="sm"
            className="w-full"
            onClick={() => {
              onDelete();
              onOpenChange(false);
            }}
          >
            <Trash2 className="w-4 h-4 mr-2" />
            删除连接
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
};
