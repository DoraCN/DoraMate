import React, { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { NodeTemplate, NodeType } from '@/types/node';
import { Plus, Trash2 } from 'lucide-react';

interface CustomNodeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreateTemplate: (template: NodeTemplate) => void;
}

const iconOptions = [
  'Box', 'Star', 'Zap', 'Terminal', 'Code', 'Database', 'Cloud', 'Cpu',
  'Layers', 'GitBranch', 'Settings', 'Tool', 'Package', 'Puzzle', 'Workflow'
];

export const CustomNodeDialog: React.FC<CustomNodeDialogProps> = ({
  open,
  onOpenChange,
  onCreateTemplate,
}) => {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [nodeType, setNodeType] = useState<NodeType>('process');
  const [icon, setIcon] = useState('Box');
  const [inputs, setInputs] = useState<{ name: string; dataType: string }[]>([]);
  const [outputs, setOutputs] = useState<{ name: string; dataType: string }[]>([]);

  const handleAddInput = () => {
    setInputs([...inputs, { name: '', dataType: 'any' }]);
  };

  const handleAddOutput = () => {
    setOutputs([...outputs, { name: '', dataType: 'any' }]);
  };

  const handleRemoveInput = (index: number) => {
    setInputs(inputs.filter((_, i) => i !== index));
  };

  const handleRemoveOutput = (index: number) => {
    setOutputs(outputs.filter((_, i) => i !== index));
  };

  const handleUpdateInput = (index: number, field: 'name' | 'dataType', value: string) => {
    const newInputs = [...inputs];
    newInputs[index][field] = value;
    setInputs(newInputs);
  };

  const handleUpdateOutput = (index: number, field: 'name' | 'dataType', value: string) => {
    const newOutputs = [...outputs];
    newOutputs[index][field] = value;
    setOutputs(newOutputs);
  };

  const handleSubmit = () => {
    if (!name.trim()) return;

    const template: NodeTemplate = {
      type: nodeType,
      name: name.trim(),
      icon,
      description: description.trim(),
      category: 'custom',
      defaultInputs: inputs.filter(i => i.name.trim()).map(i => ({
        name: i.name.trim(),
        type: 'input' as const,
        dataType: i.dataType,
      })),
      defaultOutputs: outputs.filter(o => o.name.trim()).map(o => ({
        name: o.name.trim(),
        type: 'output' as const,
        dataType: o.dataType,
      })),
      defaultConfig: {},
    };

    onCreateTemplate(template);
    resetForm();
    onOpenChange(false);
  };

  const resetForm = () => {
    setName('');
    setDescription('');
    setNodeType('process');
    setIcon('Box');
    setInputs([]);
    setOutputs([]);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>创建自定义节点</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="name">节点名称 *</Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="例如：数据处理器"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">描述</Label>
            <Textarea
              id="description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="描述此节点的功能..."
              rows={2}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>节点类型</Label>
              <Select value={nodeType} onValueChange={(v) => setNodeType(v as NodeType)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="input">输入</SelectItem>
                  <SelectItem value="process">处理</SelectItem>
                  <SelectItem value="output">输出</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>图标</Label>
              <Select value={icon} onValueChange={setIcon}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {iconOptions.map((iconName) => (
                    <SelectItem key={iconName} value={iconName}>
                      {iconName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label>输入端口</Label>
              <Button type="button" variant="outline" size="sm" onClick={handleAddInput}>
                <Plus className="w-3 h-3 mr-1" />
                添加
              </Button>
            </div>
            {inputs.map((input, index) => (
              <div key={index} className="flex gap-2">
                <Input
                  value={input.name}
                  onChange={(e) => handleUpdateInput(index, 'name', e.target.value)}
                  placeholder="端口名称"
                  className="flex-1"
                />
                <Select
                  value={input.dataType}
                  onValueChange={(v) => handleUpdateInput(index, 'dataType', v)}
                >
                  <SelectTrigger className="w-24">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="any">any</SelectItem>
                    <SelectItem value="string">string</SelectItem>
                    <SelectItem value="number">number</SelectItem>
                    <SelectItem value="image">image</SelectItem>
                    <SelectItem value="audio">audio</SelectItem>
                    <SelectItem value="json">json</SelectItem>
                  </SelectContent>
                </Select>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={() => handleRemoveInput(index)}
                >
                  <Trash2 className="w-4 h-4 text-destructive" />
                </Button>
              </div>
            ))}
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label>输出端口</Label>
              <Button type="button" variant="outline" size="sm" onClick={handleAddOutput}>
                <Plus className="w-3 h-3 mr-1" />
                添加
              </Button>
            </div>
            {outputs.map((output, index) => (
              <div key={index} className="flex gap-2">
                <Input
                  value={output.name}
                  onChange={(e) => handleUpdateOutput(index, 'name', e.target.value)}
                  placeholder="端口名称"
                  className="flex-1"
                />
                <Select
                  value={output.dataType}
                  onValueChange={(v) => handleUpdateOutput(index, 'dataType', v)}
                >
                  <SelectTrigger className="w-24">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="any">any</SelectItem>
                    <SelectItem value="string">string</SelectItem>
                    <SelectItem value="number">number</SelectItem>
                    <SelectItem value="image">image</SelectItem>
                    <SelectItem value="audio">audio</SelectItem>
                    <SelectItem value="json">json</SelectItem>
                  </SelectContent>
                </Select>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={() => handleRemoveOutput(index)}
                >
                  <Trash2 className="w-4 h-4 text-destructive" />
                </Button>
              </div>
            ))}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            取消
          </Button>
          <Button onClick={handleSubmit} disabled={!name.trim()}>
            创建节点
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
