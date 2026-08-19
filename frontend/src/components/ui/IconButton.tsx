import React from 'react';

interface IconButtonProps extends Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {
  /** Required: an icon alone has no accessible name without it. */
  label: string;
  icon: React.ReactNode;
  variant?: 'ghost' | 'bordered';
  /** Pulls the visual box back so the 44px target does not add layout padding. */
  inset?: boolean;
}

const variantStyles: Record<NonNullable<IconButtonProps['variant']>, string> = {
  ghost: 'text-content-muted hover:text-content hover:bg-surface-sunk border border-transparent',
  bordered:
    'text-content-muted hover:text-content hover:bg-surface-sunk border border-rule-strong',
};

/**
 * A labelled, correctly-sized icon-only control.
 *
 * Exists for two reasons. Icon-only buttons were being hand-rolled in three
 * places with three different paddings and two different hover treatments, so
 * the same affordance looked different depending on where it appeared. And each
 * of those was 24-36px, well under a comfortable touch target. This centralises
 * both: one treatment, and a 44x44 hit area regardless of icon size.
 *
 * `inset` keeps the enlarged target from pushing surrounding layout around, by
 * pulling the box back with a negative margin while the hit area stays full size.
 */
export const IconButton: React.FC<IconButtonProps> = ({
  label,
  icon,
  variant = 'ghost',
  inset = false,
  className = '',
  type = 'button',
  ...props
}) => {
  return (
    <button
      type={type}
      aria-label={label}
      title={label}
      className={[
        'inline-flex items-center justify-center shrink-0',
        'h-11 w-11 rounded-ticket transition-colors duration-150',
        'cursor-pointer disabled:cursor-not-allowed disabled:opacity-45',
        inset ? '-m-2' : '',
        variantStyles[variant],
        className,
      ].join(' ')}
      {...props}
    >
      <span aria-hidden="true" className="inline-flex">
        {icon}
      </span>
    </button>
  );
};
