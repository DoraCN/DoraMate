import React from 'react';
import { NodeData } from '@/types/node';
import * as Icons from 'lucide-react';
import { cn } from '@/lib/utils';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';

interface PropertyPanelProps {
  selectedNode: NodeData | null;
  onConfigChange: (nodeId: string, config: Record<string, any>) => void;
  onDeleteNode: (nodeId: string) => void;
}

export const PropertyPanel: React.FC<PropertyPanelProps> = ({
  selectedNode,
  onConfigChange,
  onDeleteNode,
}) => {
  if (!selectedNode) {
    return (
      <div className="w-72 panel border-l flex flex-col">
        <div className="p-4 border-b border-border">
          <h2 className="font-semibold text-sm flex items-center gap-2">
            <Icons.Settings className="w-4 h-4 text-primary" />
            属性面板
          </h2>
        </div>
        <div className="flex-1 flex items-center justify-center p-6">
          <div className="text-center text-muted-foreground">
            <Icons.MousePointerClick className="w-10 h-10 mx-auto mb-3 opacity-30" />
            <p className="text-sm">选择一个节点</p>
            <p className="text-xs mt-1">查看和编辑属性</p>
          </div>
        </div>
      </div>
    );
  }

  const IconComponent = (Icons as any)[selectedNode.icon] || Icons.Box;

  const getNodeTypeStyles = (type: string) => {
    switch (type) {
      case 'input':
        return 'bg-node-input/10 text-node-input border-node-input/30';
      case 'process':
        return 'bg-node-process/10 text-node-process border-node-process/30';
      case 'output':
        return 'bg-node-output/10 text-node-output border-node-output/30';
      default:
        return 'bg-muted text-muted-foreground border-border';
    }
  };

  const handleConfigChange = (key: string, value: any) => {
    onConfigChange(selectedNode.id, { [key]: value });
  };

  return (
    <div className="w-72 panel border-l flex flex-col">
      <div className="p-4 border-b border-border">
        <h2 className="font-semibold text-sm flex items-center gap-2">
          <Icons.Settings className="w-4 h-4 text-primary" />
          属性面板
        </h2>
      </div>

      <ScrollArea className="flex-1">
        <div className="p-4 space-y-4">
          {/* Node Info */}
          <div className="space-y-3">
            <div className="flex items-center gap-3">
              <div className={cn(
                'w-10 h-10 rounded-xl flex items-center justify-center border',
                getNodeTypeStyles(selectedNode.type)
              )}>
                <IconComponent className="w-5 h-5" />
              </div>
              <div>
                <h3 className="font-medium">{selectedNode.name}</h3>
                <p className="text-xs text-muted-foreground capitalize">{selectedNode.type} 节点</p>
              </div>
            </div>

            {selectedNode.description && (
              <p className="text-sm text-muted-foreground bg-secondary/50 rounded-lg p-3">
                {selectedNode.description}
              </p>
            )}
          </div>

          <Separator />

          {/* Ports Info */}
          <div className="space-y-2">
            <Label className="text-xs text-muted-foreground uppercase tracking-wider">端口</Label>
            <div className="space-y-2">
              {selectedNode.inputs.length > 0 && (
                <div className="bg-secondary/30 rounded-lg p-3">
                  <div className="text-xs text-muted-foreground mb-2">输入</div>
                  <div className="flex flex-wrap gap-1.5">
                    {selectedNode.inputs.map((port) => (
                      <span
                        key={port.id}
                        className="px-2 py-1 text-xs rounded-md bg-node-input/10 text-node-input border border-node-input/30"
                      >
                        {port.name}
                      </span>
                    ))}
                  </div>
                </div>
              )}
              {selectedNode.outputs.length > 0 && (
                <div className="bg-secondary/30 rounded-lg p-3">
                  <div className="text-xs text-muted-foreground mb-2">输出</div>
                  <div className="flex flex-wrap gap-1.5">
                    {selectedNode.outputs.map((port) => (
                      <span
                        key={port.id}
                        className="px-2 py-1 text-xs rounded-md bg-node-output/10 text-node-output border border-node-output/30"
                      >
                        {port.name}
                      </span>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>

          <Separator />

          {/* Configuration */}
          <div className="space-y-3">
            <Label className="text-xs text-muted-foreground uppercase tracking-wider">配置</Label>
            {Object.entries(selectedNode.config).map(([key, value]) => (
              <div key={key} className="space-y-1.5">
                <Label htmlFor={key} className="text-sm capitalize">
                  {key.replace(/([A-Z])/g, ' $1').trim()}
                </Label>
                <Input
                  id={key}
                  value={typeof value === 'object' ? JSON.stringify(value) : String(value)}
                  onChange={(e) => handleConfigChange(key, e.target.value)}
                  className="bg-secondary/50 border-border/50"
                />
              </div>
            ))}
          </div>

          <Separator />

          {/* Actions */}
          <div className="space-y-2">
            <Button
              variant="destructive"
              size="sm"
              className="w-full"
              onClick={() => onDeleteNode(selectedNode.id)}
            >
              <Icons.Trash2 className="w-4 h-4 mr-2" />
              删除节点
            </Button>
          </div>
        </div>
      </ScrollArea>
    </div>
  );
};
