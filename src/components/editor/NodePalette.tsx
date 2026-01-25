import React, { useState } from 'react';
import { NodeTemplate } from '@/types/node';
import { nodeTemplates as defaultTemplates, nodeCategories } from '@/data/nodeTemplates';
import * as Icons from 'lucide-react';
import { cn } from '@/lib/utils';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Button } from '@/components/ui/button';
import { CustomNodeDialog } from './CustomNodeDialog';

interface NodePaletteProps {
  onDragStart: (template: NodeTemplate) => void;
  customTemplates: NodeTemplate[];
  onAddCustomTemplate: (template: NodeTemplate) => void;
}

export const NodePalette: React.FC<NodePaletteProps> = ({
  onDragStart,
  customTemplates = [],
  onAddCustomTemplate,
}) => {
  const [dialogOpen, setDialogOpen] = useState(false);

  const allTemplates = [...defaultTemplates, ...(customTemplates || [])];

  const getTemplatesByCategory = (category: string) => {
    return allTemplates.filter((t) => t.category === category);
  };

  const getCategoryIcon = (iconName: string) => {
    const IconComponent = (Icons as any)[iconName] || Icons.Box;
    return IconComponent;
  };

  const getNodeTypeColor = (type: string) => {
    switch (type) {
      case 'input':
        return 'text-node-input bg-node-input/10 border-node-input/30';
      case 'process':
        return 'text-node-process bg-node-process/10 border-node-process/30';
      case 'output':
        return 'text-node-output bg-node-output/10 border-node-output/30';
      default:
        return 'text-muted-foreground bg-muted/50 border-border';
    }
  };

  return (
    <div className="w-64 panel border-r flex flex-col">
      <div className="p-4 border-b border-border">
        <h2 className="font-semibold text-sm flex items-center gap-2">
          <Icons.Boxes className="w-4 h-4 text-primary" />
          节点面板
        </h2>
        <p className="text-xs text-muted-foreground mt-1">拖拽节点到画布</p>
      </div>

      <ScrollArea className="flex-1">
        <div className="p-3 space-y-4">
          {nodeCategories.map((category) => {
            const CategoryIcon = getCategoryIcon(category.icon);
            const templates = getTemplatesByCategory(category.id);

            // Skip custom category if empty and show add button
            if (category.id === 'custom') {
              return (
                <div key={category.id}>
                  <div className="flex items-center gap-2 mb-2 px-1">
                    <CategoryIcon className="w-3.5 h-3.5 text-muted-foreground" />
                    <span className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                      {category.name}
                    </span>
                  </div>

                  <div className="space-y-1.5">
                    {templates.map((template) => {
                      const IconComponent = (Icons as any)[template.icon] || Icons.Box;

                      return (
                        <div
                          key={`${template.category}-${template.name}`}
                          className="palette-item"
                          draggable
                          onDragStart={(e) => {
                            e.dataTransfer.effectAllowed = 'copy';
                            onDragStart(template);
                          }}
                        >
                          <div className={cn(
                            'w-8 h-8 rounded-lg flex items-center justify-center border',
                            getNodeTypeColor(template.type)
                          )}>
                            <IconComponent className="w-4 h-4" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="text-sm font-medium truncate">{template.name}</div>
                            <div className="text-xs text-muted-foreground truncate">
                              {template.description}
                            </div>
                          </div>
                        </div>
                      );
                    })}

                    <Button
                      variant="outline"
                      size="sm"
                      className="w-full mt-2 border-dashed"
                      onClick={() => setDialogOpen(true)}
                    >
                      <Icons.Plus className="w-4 h-4 mr-2" />
                      添加自定义节点
                    </Button>
                  </div>
                </div>
              );
            }

            return (
              <div key={category.id}>
                <div className="flex items-center gap-2 mb-2 px-1">
                  <CategoryIcon className={cn(
                    'w-3.5 h-3.5',
                    category.id === 'input' && 'text-node-input',
                    category.id === 'process' && 'text-node-process',
                    category.id === 'output' && 'text-node-output',
                  )} />
                  <span className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                    {category.name}
                  </span>
                </div>

                <div className="space-y-1.5">
                  {templates.map((template) => {
                    const IconComponent = (Icons as any)[template.icon] || Icons.Box;

                    return (
                      <div
                        key={`${template.category}-${template.name}`}
                        className="palette-item"
                        draggable
                        onDragStart={(e) => {
                          e.dataTransfer.effectAllowed = 'copy';
                          onDragStart(template);
                        }}
                      >
                        <div className={cn(
                          'w-8 h-8 rounded-lg flex items-center justify-center border',
                          getNodeTypeColor(template.type)
                        )}>
                          <IconComponent className="w-4 h-4" />
                        </div>
                        <div className="flex-1 min-w-0">
                          <div className="text-sm font-medium truncate">{template.name}</div>
                          <div className="text-xs text-muted-foreground truncate">
                            {template.description}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>
      </ScrollArea>

      <CustomNodeDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onCreateTemplate={onAddCustomTemplate}
      />
    </div>
  );
};
