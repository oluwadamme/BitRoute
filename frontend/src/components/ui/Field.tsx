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
      <label htmlFor={id} className="block stencil text-content-muted mb-1.5">
        {label}
      </label>

      {children({
        id,
        'aria-describedby': describedBy || undefined,
        'aria-invalid': error ? true : undefined,
      })}

      {/*
        The hint sits BELOW the control, not between the label and it.
        Above the control, a hinted field pushes its input down by the hint's
        height, so in any side-by-side grid it stops lining up with an unhinted
        neighbour — which it did in four places on the operator console. Below
        the control, every label and every input aligns regardless of which
        fields carry hints. `aria-describedby` is unaffected by DOM order, so
        the control is still described by both hint and error.
      */}
      {hint && (
        <p id={hintId} className="mt-1.5 text-xs text-content-muted leading-snug">
          {hint}
        </p>
      )}

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
  'w-full bg-surface-raised border border-rule-strong rounded-ticket px-3 py-2.5 text-sm text-content placeholder:text-content-faint transition-colors hover:border-content-faint focus:border-signal';
