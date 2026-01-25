import React from 'react';
import * as Icons from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip';

interface ToolbarProps {
  onOpen: () => void;
  onSave: () => void;
  onImportYAML: () => void;
  onExportYAML: () => void;
  onRun: () => void;
  onValidate: () => void;
  onClear: () => void;
  isRunning?: boolean;
  hasNodes: boolean;
}

export const Toolbar: React.FC<ToolbarProps> = ({
  onOpen,
  onSave,
  onImportYAML,
  onExportYAML,
  onRun,
  onValidate,
  onClear,
  isRunning = false,
  hasNodes,
}) => {
  const ToolbarButton = ({
    icon: Icon,
    label,
    onClick,
    variant = 'secondary',
    disabled = false,
    className,
  }: {
    icon: React.ElementType;
    label: string;
    onClick: () => void;
    variant?: 'secondary' | 'primary' | 'destructive';
    disabled?: boolean;
    className?: string;
  }) => (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          size="sm"
          variant={variant === 'primary' ? 'default' : variant === 'destructive' ? 'destructive' : 'secondary'}
          onClick={onClick}
          disabled={disabled}
          className={cn(
            'gap-2 transition-all duration-150',
            variant === 'primary' && 'bg-primary hover:bg-primary/90',
            className
          )}
        >
          <Icon className="w-4 h-4" />
          <span className="hidden sm:inline">{label}</span>
        </Button>
      </TooltipTrigger>
      <TooltipContent>
        <p>{label}</p>
      </TooltipContent>
    </Tooltip>
  );

  return (
    <div className="h-14 panel border-b flex items-center justify-between px-4">
      {/* Logo & Title */}
      <div className="flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary to-accent flex items-center justify-center">
          <Icons.Workflow className="w-4 h-4 text-primary-foreground" />
        </div>
        <div>
          <h1 className="font-bold text-lg leading-tight">DoraMate</h1>
          <p className="text-xs text-muted-foreground leading-tight">可视化节点编辑器</p>
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2">
        {/* File Operations */}
        <div className="flex items-center gap-1.5">
          <ToolbarButton icon={Icons.FolderOpen} label="打开" onClick={onOpen} />
          <ToolbarButton icon={Icons.Save} label="保存" onClick={onSave} disabled={!hasNodes} />
        </div>

        <Separator orientation="vertical" className="h-6 mx-1" />

        {/* YAML Operations */}
        <div className="flex items-center gap-1.5">
          <ToolbarButton icon={Icons.FileDown} label="导入YAML" onClick={onImportYAML} />
          <ToolbarButton icon={Icons.FileUp} label="导出" onClick={onExportYAML} disabled={!hasNodes} />
        </div>

        <Separator orientation="vertical" className="h-6 mx-1" />

        {/* Execution */}
        <div className="flex items-center gap-1.5">
          <ToolbarButton
            icon={Icons.CheckCircle2}
            label="验证"
            onClick={onValidate}
            disabled={!hasNodes}
          />
          <ToolbarButton
            icon={isRunning ? Icons.Square : Icons.Play}
            label={isRunning ? '停止' : '运行'}
            onClick={onRun}
            variant="primary"
            disabled={!hasNodes}
            className={cn(
              isRunning && 'bg-node-error hover:bg-node-error/90'
            )}
          />
        </div>

        <Separator orientation="vertical" className="h-6 mx-1" />

        {/* Clear */}
        <ToolbarButton
          icon={Icons.Trash2}
          label="清空"
          onClick={onClear}
          variant="destructive"
          disabled={!hasNodes}
        />
      </div>
    </div>
  );
};
