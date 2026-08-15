import React from 'react';

interface CardProps {
  children: React.ReactNode;
  className?: string;
  /** Headline rendered in the signage face. */
  title?: React.ReactNode;
  /** Supporting line beneath the title. */
  subtitle?: React.ReactNode;
  /** Controls or status pinned to the right of the header. */
  action?: React.ReactNode;
  /** Marks the card as the page's primary surface. */
  emphasis?: boolean;
  as?: 'div' | 'section' | 'article' | 'aside';
}

/**
 * Ticket stock: the app's one panel surface.
 *
 * Replaces the previous GlassCard. A translucent blurred panel is the exact
 * generic treatment the project's design skill warns against, and it also made
 * text contrast depend on whatever happened to sit behind it.
 */
export const Card: React.FC<CardProps> = ({
  children,
  className = '',
  title,
  subtitle,
  action,
  emphasis = false,
  as: Tag = 'section',
}) => {
  const hasHeader = Boolean(title || action);

  return (
    <Tag
      className={[
        'stock',
        emphasis ? 'shadow-raised border-rule-strong' : '',
        className,
      ].join(' ')}
    >
      {hasHeader && (
        <header className="flex items-start justify-between gap-4 px-5 py-4 border-b border-rule">
          <div className="min-w-0">
            {title && (
              <h2 className="board text-xl text-content font-bold">{title}</h2>
            )}
            {subtitle && (
              <p className="mt-1 text-xs text-content-muted leading-snug">{subtitle}</p>
            )}
          </div>
          {action && <div className="shrink-0 flex items-center gap-2">{action}</div>}
        </header>
      )}

      <div className="px-5 py-5">{children}</div>
    </Tag>
  );
};
