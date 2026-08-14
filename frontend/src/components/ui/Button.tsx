import React from 'react';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  isLoading?: boolean;
  /** Announced to screen readers while `isLoading` is set. */
  loadingLabel?: string;
  icon?: React.ReactNode;
}

const variantStyles: Record<NonNullable<ButtonProps['variant']>, string> = {
  primary:
    'bg-signal text-paper-raised border border-signal-deep hover:bg-signal-deep active:shadow-press shadow-stub',
  secondary:
    'bg-paper-raised text-ink border border-rule-strong hover:bg-paper-sunk active:shadow-press',
  danger:
    'bg-paper-raised text-signal-deep border border-signal/45 hover:bg-signal-wash active:shadow-press',
  ghost: 'text-ink-muted border border-transparent hover:text-ink hover:bg-paper-sunk',
};

const sizeStyles: Record<NonNullable<ButtonProps['size']>, string> = {
  sm: 'px-3 py-1.5 text-xs',
  md: 'px-4 py-2.5 text-sm',
  lg: 'px-6 py-3.5 text-base',
};

export const Button: React.FC<ButtonProps> = ({
  children,
  variant = 'primary',
  size = 'md',
  isLoading = false,
  loadingLabel = 'Working, please wait',
  icon,
  className = '',
  disabled,
  type = 'button',
  ...props
}) => {
  return (
    <button
      // An explicit default matters: a bare <button> inside a form submits it.
      type={type}
      disabled={disabled || isLoading}
      aria-busy={isLoading || undefined}
      className={[
        'inline-flex items-center justify-center gap-2 rounded-ticket font-semibold',
        'tracking-signage transition-colors duration-150',
        'disabled:opacity-45 disabled:cursor-not-allowed disabled:shadow-none',
        variantStyles[variant],
        sizeStyles[size],
        className,
      ].join(' ')}
      {...props}
    >
      {isLoading ? (
        <>
          <span
            aria-hidden="true"
            className="w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0"
          />
          {/*
            The label stays mounted while loading. Swapping the children out
            for a bare spinner, as the previous version did, strips the
            button's accessible name mid-action, so a screen reader announces
            nothing at the moment the user most needs feedback.
          */}
          <span>{children}</span>
          <span className="sr-only">{loadingLabel}</span>
        </>
      ) : (
        <>
          {icon && (
            <span aria-hidden="true" className="shrink-0 inline-flex">
              {icon}
            </span>
          )}
          <span>{children}</span>
        </>
      )}
    </button>
  );
};
