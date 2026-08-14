import React, { useId } from 'react';

interface FieldProps {
  label: React.ReactNode;
  /** Guidance shown under the label and wired up via aria-describedby. */
  hint?: React.ReactNode;
  /** Validation message. Sets aria-invalid on the control when present. */
  error?: string | null;
  /** Receives the ids the control must carry to stay properly labelled. */
  children: (ids: {
    id: string;
    'aria-describedby': string | undefined;
    'aria-invalid': boolean | undefined;
  }) => React.ReactNode;
  className?: string;
}

/**
 * Associates a visible label with its control.
 *
 * Every form control in the app previously used a bare `<label>` with no
 * `htmlFor` and no nesting, so the two were never connected: clicking a label
 * did nothing and screen readers announced the inputs unnamed.
 */
export const Field: React.FC<FieldProps> = ({
  label,
  hint,
  error,
  children,
  className = '',
}) => {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;

  const describedBy = [hint ? hintId : null, error ? errorId : null]
    .filter(Boolean)
    .join(' ');

  return (
    <div className={className}>
      <label htmlFor={id} className="block stencil text-ink-muted mb-1.5">
        {label}
      </label>

      {hint && (
        <p id={hintId} className="text-xs text-ink-faint mb-1.5 leading-snug">
          {hint}
        </p>
      )}

      {children({
        id,
        'aria-describedby': describedBy || undefined,
        'aria-invalid': error ? true : undefined,
      })}

      {error && (
        <p id={errorId} className="mt-1.5 text-xs text-signal-deep leading-snug">
          {error}
        </p>
      )}
    </div>
  );
};

/** Shared input styling so every control on the page reads as the same stock. */
export const controlStyles =
  'w-full bg-paper-raised border border-rule-strong rounded-ticket px-3 py-2.5 text-sm text-ink placeholder:text-ink-faint transition-colors hover:border-ink-faint focus:border-signal';
