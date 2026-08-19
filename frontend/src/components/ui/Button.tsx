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
  // Dark label on the lit accent: measured at 6.58:1.
  primary: 'bg-signal text-surface border border-signal hover:bg-signal-deep active:shadow-press',
  secondary:
    'bg-surface-raised text-content border border-rule-strong hover:bg-surface-sunk active:shadow-press',
  danger:
    'bg-surface-raised text-signal-deep border border-signal/45 hover:bg-signal-wash active:shadow-press',
  ghost: 'text-content-muted border border-transparent hover:text-content hover:bg-surface-sunk',
};

/*
 * Every size clears a 44x44 CSS-pixel target. The previous scale produced 30px
 * and 42px controls, which are awkward on a phone and below the interaction
 * guidance this project holds itself to.
 */
const sizeStyles: Record<NonNullable<ButtonProps['size']>, string> = {
  sm: 'min-h-11 px-3.5 text-sm',
  md: 'min-h-11 px-4 text-sm',
  lg: 'min-h-[3.25rem] px-6 text-base',
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
        // A native <button> computes to `cursor: default`, so the pointer has
        // to be asked for explicitly.
        'cursor-pointer disabled:cursor-not-allowed',
        'disabled:opacity-45 disabled:shadow-none',
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
            The label stays mounted while loading. Swapping the children out for
            a bare spinner strips the button's accessible name at the moment the
            user most needs feedback.
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
