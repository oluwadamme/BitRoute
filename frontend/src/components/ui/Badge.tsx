import React from 'react';

interface BadgeProps {
  children: React.ReactNode;
  variant?: 'signal' | 'ochre' | 'stamp' | 'neutral';
  /** Departure-board blink, for a state actively counting down. */
  blink?: boolean;
}

const variantStyles: Record<NonNullable<BadgeProps['variant']>, string> = {
  signal: 'bg-signal-wash border-signal/40 text-signal-deep',
  ochre: 'bg-ochre-wash border-ochre/40 text-ochre-deep',
  stamp: 'bg-stamp-wash border-stamp/35 text-stamp-deep',
  neutral: 'bg-paper-sunk border-rule-strong text-ink-muted',
};

const dotStyles: Record<NonNullable<BadgeProps['variant']>, string> = {
  signal: 'bg-signal',
  ochre: 'bg-ochre',
  stamp: 'bg-stamp',
  neutral: 'bg-ink-faint',
};

export const Badge: React.FC<BadgeProps> = ({
  children,
  variant = 'signal',
  blink = false,
}) => {
  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-ticket border stencil ${variantStyles[variant]}`}
    >
      {blink && (
        <span
          aria-hidden="true"
          className={`w-1.5 h-1.5 rounded-full shrink-0 animate-tick ${dotStyles[variant]}`}
        />
      )}
      <span>{children}</span>
    </span>
  );
};
