import React from 'react';
import { radius } from '../../design/tokens';

export interface SkeletonProps extends React.HTMLAttributes<HTMLDivElement> {
  width?: string | number;
  height?: string | number;
  borderRadius?: string | number;
  variant?: 'rect' | 'circle' | 'text';
}

export const Skeleton: React.FC<SkeletonProps> = ({
  width = '100%',
  height = '16px',
  borderRadius,
  variant = 'rect',
  className = '',
  style,
  ...props
}) => {
  const getRadius = () => {
    if (borderRadius !== undefined) return borderRadius;
    if (variant === 'circle') return '50%';
    if (variant === 'text') return radius.sm;
    return radius.md;
  };

  return (
    <div
      className={`fi-skeleton skeleton-shimmer ${className}`}
      style={{
        width: typeof width === 'number' ? `${width}px` : width,
        height: typeof height === 'number' ? `${height}px` : height,
        borderRadius: getRadius(),
        display: 'inline-block',
        ...style,
      }}
      aria-hidden="true"
      {...props}
    />
  );
};
