import React, { useEffect, useId, useRef } from 'react';
import { X } from 'lucide-react';

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: React.ReactNode;
  /** Plain-text title for the accessible name when `title` is rich markup. */
  titleText?: string;
  children: React.ReactNode;
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl';
}

const widthStyles = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-lg',
  xl: 'max-w-xl',
};

const FOCUSABLE = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

/**
 * An accessible dialog.
 *
 * The previous implementation was a plain div: no role, no focus management,
 * no Escape handling, and a backdrop that swallowed clicks. Keyboard users
 * could tab straight out of the dialog into the page behind it while it was
 * still covering the screen.
 */
export const Modal: React.FC<ModalProps> = ({
  isOpen,
  onClose,
  title,
  titleText,
  children,
  maxWidth = 'md',
}) => {
  const panelRef = useRef<HTMLDivElement>(null);
  const titleId = useId();

  // Remembers whatever had focus so it can be handed back on close.
  const restoreFocusRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!isOpen) return;

    restoreFocusRef.current = document.activeElement as HTMLElement | null;

    // Move focus to the first control, or to the panel when there is none.
    const focusable = panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE);
    (focusable?.length ? focusable[0] : panelRef.current)?.focus();

    // Stop the page behind the dialog from scrolling under the overlay.
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    /*
     * Key handling lives on the document rather than on a JSX handler so that
     * Escape works wherever focus happens to be, including on the browser
     * chrome the dialog does not own. Capture phase keeps it ahead of anything
     * inside the dialog that also listens for Escape.
     */
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.stopPropagation();
        onClose();
        return;
      }

      if (event.key !== 'Tab') return;

      const inPanel = panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE);
      if (!inPanel?.length) return;

      const first = inPanel[0];
      const last = inPanel[inPanel.length - 1];
      const active = document.activeElement;

      // Wrap focus at both ends so Tab can never escape the dialog.
      if (event.shiftKey && (active === first || active === panelRef.current)) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
      } else if (active && !panelRef.current?.contains(active)) {
        // Focus somehow escaped the dialog; pull it back in.
        event.preventDefault();
        first.focus();
      }
    };

    /*
     * Click-outside is a pointer-only convenience, so it listens on the
     * document rather than sitting as a handler on a presentational backdrop
     * div. Keyboard users are served by Escape and the labelled close button.
     * Tracking mousedown, not click, means a drag that starts inside the panel
     * and releases outside it does not dismiss the dialog.
     */
    const onPointerDown = (event: MouseEvent) => {
      const target = event.target as Node | null;
      if (target && panelRef.current && !panelRef.current.contains(target)) {
        onClose();
      }
    };

    document.addEventListener('keydown', onKeyDown, true);
    document.addEventListener('mousedown', onPointerDown);

    return () => {
      document.removeEventListener('keydown', onKeyDown, true);
      document.removeEventListener('mousedown', onPointerDown);
      document.body.style.overflow = previousOverflow;
      restoreFocusRef.current?.focus();
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink/45 backdrop-blur-[2px]">
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-label={titleText}
        tabIndex={-1}
        className={`stock shadow-raised w-full ${widthStyles[maxWidth]} outline-none animate-stub-in`}
      >
        <div className="flex items-start justify-between gap-4 px-6 py-4 border-b border-rule">
          <div id={titleId} className="min-w-0">
            {title}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close dialog"
            className="shrink-0 p-1.5 -m-1 rounded-ticket text-ink-muted hover:text-ink hover:bg-paper-sunk transition-colors"
          >
            <X className="w-5 h-5" aria-hidden="true" />
          </button>
        </div>

        <div className="px-6 py-5">{children}</div>
      </div>
    </div>
  );
};
