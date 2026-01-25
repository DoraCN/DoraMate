import React from 'react';

interface ConnectionLineProps {
  startX: number;
  startY: number;
  endX: number;
  endY: number;
  isActive?: boolean;
  isInvalid?: boolean;
  isTemp?: boolean;
}

export const ConnectionLine: React.FC<ConnectionLineProps> = ({
  startX,
  startY,
  endX,
  endY,
  isActive = false,
  isInvalid = false,
  isTemp = false,
}) => {
  // Calculate control points for bezier curve
  const dx = Math.abs(endX - startX);
  const controlOffset = Math.min(dx * 0.5, 100);
  
  const path = `M ${startX} ${startY} C ${startX + controlOffset} ${startY}, ${endX - controlOffset} ${endY}, ${endX} ${endY}`;

  const strokeColor = isInvalid
    ? 'hsl(var(--connection-invalid))'
    : isActive
    ? 'hsl(var(--connection-active))'
    : 'hsl(var(--connection-default))';

  return (
    <g>
      {/* Shadow/glow effect */}
      <path
        d={path}
        fill="none"
        stroke={strokeColor}
        strokeWidth={isActive ? 6 : 4}
        strokeOpacity={0.2}
        strokeLinecap="round"
        style={{ pointerEvents: 'none' }}
      />
      {/* Main line */}
      <path
        d={path}
        fill="none"
        stroke={strokeColor}
        strokeWidth={2}
        strokeLinecap="round"
        strokeDasharray={isTemp ? '5,5' : 'none'}
        style={{ pointerEvents: 'none' }}
      />
      {/* Arrow at end */}
      {!isTemp && (
        <circle
          cx={endX}
          cy={endY}
          r={4}
          fill={strokeColor}
          style={{ pointerEvents: 'none' }}
        />
      )}
    </g>
  );
};
