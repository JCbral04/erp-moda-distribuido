import React, { forwardRef } from 'react';
import { colors, radius, motion, shadow } from '../../design/tokens';

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  hoverable?: boolean;
  variant?: 'raised' | 'surface';
}

export const Card = forwardRef<HTMLDivElement, CardProps>(
  (
    {
      hoverable = false,
      variant = 'raised',
      children,
      style,
      className = '',
      ...props
    },
    ref,
  ) => {
    return (
      <div
        ref={ref}
        className={`fi-card ${className}`}
        style={{
          backgroundColor: variant === 'raised' ? colors.bg.raised : colors.bg.surface,
          border: `1px solid ${colors.border.subtle}`,
          borderRadius: radius.lg,
          padding: '20px',
          boxShadow: shadow.sm,
          position: 'relative',
          transition: hoverable
            ? `transform ${motion.duration.fast}ms ${motion.easing.standard}`
            : undefined,
          ...style,
        }}
        onMouseEnter={(e) => {
          if (hoverable) {
            e.currentTarget.style.transform = 'translateY(-2px)';
          }
        }}
        onMouseLeave={(e) => {
          if (hoverable) {
            e.currentTarget.style.transform = 'translateY(0)';
          }
        }}
        {...props}
      >
        {children}
      </div>
    );
  },
);

Card.displayName = 'Card';

export const CardHeader = ({
  children,
  style,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={`fi-card-header ${className}`}
    style={{
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      marginBottom: '14px',
      ...style,
    }}
    {...props}
  >
    {children}
  </div>
);

export const CardTitle = ({
  children,
  style,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLHeadingElement>) => (
  <h3
    className={`fi-card-title ${className}`}
    style={{
      fontSize: '15px',
      fontWeight: 600,
      color: colors.text.primary,
      letterSpacing: '0.01em',
      ...style,
    }}
    {...props}
  >
    {children}
  </h3>
);

export const CardDescription = ({
  children,
  style,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLParagraphElement>) => (
  <p
    className={`fi-card-description ${className}`}
    style={{
      fontSize: '12px',
      color: colors.text.secondary,
      marginTop: '2px',
      ...style,
    }}
    {...props}
  >
    {children}
  </p>
);

export const CardContent = ({
  children,
  style,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={`fi-card-content ${className}`}
    style={{
      position: 'relative',
      ...style,
    }}
    {...props}
  >
    {children}
  </div>
);

export const CardFooter = ({
  children,
  style,
  className = '',
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={`fi-card-footer ${className}`}
    style={{
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'flex-end',
      marginTop: '16px',
      paddingTop: '12px',
      borderTop: `1px solid ${colors.border.subtle}`,
      gap: '8px',
      ...style,
    }}
    {...props}
  >
    {children}
  </div>
);
