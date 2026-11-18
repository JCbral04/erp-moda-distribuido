import React, { forwardRef } from 'react';
import { Loader2 } from 'lucide-react';
import { colors, radius, motion } from '../../design/tokens';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  isLoading?: boolean;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
  'aria-label'?: string;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      variant = 'primary',
      size = 'md',
      isLoading = false,
      leftIcon,
      rightIcon,
      disabled,
      children,
      style,
      className = '',
      ...props
    },
    ref,
  ) => {
    // Size metrics
    const sizeStyles: Record<ButtonSize, React.CSSProperties> = {
      sm: {
        height: '28px',
        padding: '0 10px',
        fontSize: '12px',
        gap: '6px',
        borderRadius: radius.sm,
      },
      md: {
        height: '36px',
        padding: '0 14px',
        fontSize: '14px',
        gap: '8px',
        borderRadius: radius.md,
      },
      lg: {
        height: '44px',
        padding: '0 18px',
        fontSize: '16px',
        gap: '10px',
        borderRadius: radius.lg,
      },
    };

    // Variant color configs
    const variantStyles: Record<ButtonVariant, React.CSSProperties> = {
      primary: {
        backgroundColor: colors.accent.champagne,
        color: colors.text.inverse,
        border: 'none',
        fontWeight: 600,
      },
      secondary: {
        backgroundColor: 'transparent',
        color: colors.text.primary,
        border: `1px solid ${colors.border.default}`,
        fontWeight: 500,
      },
      ghost: {
        backgroundColor: 'transparent',
        color: colors.text.secondary,
        border: 'none',
        fontWeight: 500,
      },
      danger: {
        backgroundColor: colors.accent.danger,
        color: colors.text.primary,
        border: 'none',
        fontWeight: 600,
      },
    };

    const isDisabled = disabled || isLoading;

    // Base button styles
    const baseStyle: React.CSSProperties = {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontFamily: '"Geist", "Inter", system-ui, sans-serif',
      cursor: isDisabled ? 'not-allowed' : 'pointer',
      opacity: isDisabled ? 0.6 : 1,
      transition: `transform ${motion.duration.instant}ms ${motion.easing.standard}, background-color ${motion.duration.fast}ms ${motion.easing.standard}, border-color ${motion.duration.fast}ms ${motion.easing.standard}`,
      touchAction: 'manipulation',
      userSelect: 'none',
      position: 'relative',
      textDecoration: 'none',
      whiteSpace: 'nowrap',
      ...sizeStyles[size],
      ...variantStyles[variant],
      ...style,
    };

    return (
      <button
        ref={ref}
        disabled={isDisabled}
        style={baseStyle}
        className={`fi-button fi-button-${variant} fi-button-${size} ${className}`}
        onMouseEnter={(e) => {
          if (!isDisabled && variant === 'primary') {
            e.currentTarget.style.transform = 'scale(1.02)';
          } else if (!isDisabled && variant === 'secondary') {
            e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.05)';
            e.currentTarget.style.borderColor = colors.border.strong;
          } else if (!isDisabled && variant === 'ghost') {
            e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.04)';
            e.currentTarget.style.color = colors.text.primary;
          }
        }}
        onMouseLeave={(e) => {
          if (variant === 'primary') {
            e.currentTarget.style.transform = 'scale(1)';
          } else if (variant === 'secondary') {
            e.currentTarget.style.backgroundColor = 'transparent';
            e.currentTarget.style.borderColor = colors.border.default;
          } else if (variant === 'ghost') {
            e.currentTarget.style.backgroundColor = 'transparent';
            e.currentTarget.style.color = colors.text.secondary;
          }
        }}
        {...props}
      >
        {isLoading && (
          <Loader2
            size={size === 'sm' ? 14 : size === 'md' ? 16 : 18}
            style={{
              animation: 'spin 1s linear infinite',
              marginRight: children ? '6px' : 0,
            }}
            aria-hidden="true"
          />
        )}
        {!isLoading && leftIcon && (
          <span style={{ display: 'inline-flex', alignItems: 'center' }} aria-hidden="true">
            {leftIcon}
          </span>
        )}
        {children}
        {!isLoading && rightIcon && (
          <span style={{ display: 'inline-flex', alignItems: 'center' }} aria-hidden="true">
            {rightIcon}
          </span>
        )}
      </button>
    );
  },
);

Button.displayName = 'Button';
