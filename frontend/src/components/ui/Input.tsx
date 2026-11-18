import React, { forwardRef, useId } from 'react';
import { colors, radius, motion } from '../../design/tokens';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  hint?: string;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  (
    {
      label,
      error,
      hint,
      leftIcon,
      rightIcon,
      id,
      type = 'text',
      spellCheck,
      autoComplete,
      disabled,
      className = '',
      style,
      ...props
    },
    ref,
  ) => {
    const generatedId = useId();
    const inputId = id || generatedId;
    const errorId = `${inputId}-error`;
    const hintId = `${inputId}-hint`;

    // Disable spellCheck automatically for emails, codes, passwords, usernames
    const isCodeOrCredential =
      type === 'email' ||
      type === 'password' ||
      props.name?.toLowerCase().includes('sku') ||
      props.name?.toLowerCase().includes('code') ||
      props.name?.toLowerCase().includes('username');

    const resolvedSpellCheck = spellCheck !== undefined ? spellCheck : !isCodeOrCredential;

    return (
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: '6px',
          width: '100%',
        }}
        className={`fi-input-group ${className}`}
      >
        <label
          htmlFor={inputId}
          style={{
            fontSize: '12px',
            fontWeight: 500,
            color: error ? colors.accent.danger : colors.text.secondary,
            letterSpacing: '0.02em',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <span>{label}</span>
          {props.required && (
            <span style={{ color: colors.accent.champagne, fontSize: '11px' }}>* Obligatorio</span>
          )}
        </label>

        <div
          style={{
            position: 'relative',
            display: 'flex',
            alignItems: 'center',
            width: '100%',
          }}
        >
          {leftIcon && (
            <span
              style={{
                position: 'absolute',
                left: '12px',
                color: colors.text.tertiary,
                display: 'inline-flex',
                alignItems: 'center',
                pointerEvents: 'none',
              }}
              aria-hidden="true"
            >
              {leftIcon}
            </span>
          )}

          <input
            ref={ref}
            id={inputId}
            type={type}
            disabled={disabled}
            spellCheck={resolvedSpellCheck}
            autoComplete={autoComplete}
            aria-invalid={!!error}
            aria-describedby={error ? errorId : hint ? hintId : undefined}
            style={{
              width: '100%',
              height: '38px',
              backgroundColor: colors.bg.surface,
              color: colors.text.primary,
              border: `1px solid ${error ? colors.accent.danger : colors.border.default}`,
              borderRadius: radius.md,
              padding: leftIcon
                ? rightIcon
                  ? '0 36px'
                  : '0 12px 0 36px'
                : rightIcon
                  ? '0 36px 0 12px'
                  : '0 12px',
              fontSize: '14px',
              fontFamily:
                type === 'number' || props.name?.includes('precio') || props.name?.includes('sku')
                  ? '"Geist Mono", monospace'
                  : '"Geist", sans-serif',
              fontVariantNumeric: 'tabular-nums',
              transition: `border-color ${motion.duration.fast}ms ${motion.easing.standard}`,
              opacity: disabled ? 0.5 : 1,
              cursor: disabled ? 'not-allowed' : 'text',
              ...style,
            }}
            onFocus={(e) => {
              if (!error) {
                e.currentTarget.style.borderColor = colors.accent.champagne;
              }
            }}
            onBlur={(e) => {
              if (!error) {
                e.currentTarget.style.borderColor = colors.border.default;
              }
            }}
            {...props}
          />

          {rightIcon && (
            <span
              style={{
                position: 'absolute',
                right: '12px',
                color: colors.text.tertiary,
                display: 'inline-flex',
                alignItems: 'center',
              }}
              aria-hidden="true"
            >
              {rightIcon}
            </span>
          )}
        </div>

        {error && (
          <span
            id={errorId}
            role="alert"
            style={{
              fontSize: '12px',
              color: colors.accent.danger,
              fontWeight: 400,
              marginTop: '2px',
            }}
          >
            {error}
          </span>
        )}

        {!error && hint && (
          <span
            id={hintId}
            style={{
              fontSize: '11px',
              color: colors.text.tertiary,
              marginTop: '2px',
            }}
          >
            {hint}
          </span>
        )}
      </div>
    );
  },
);

Input.displayName = 'Input';
