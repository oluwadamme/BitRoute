import React, { useEffect, useId, useRef } from 'react';
import { X } from 'lucide-react';
import { IconButton } from './IconButton';

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
     * Key handling lives on the document, in the CAPTURE phase, rather than on
     * a JSX handler. That makes Escape work wherever focus happens to be —
     * including places the dialog does not own — and keeps it ahead of anything
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
     * Click-outside is a pointer-only convenience, so it listens on the document
     * rather than sitting as a handler on a presentational backdrop div — which
     * would be an interactive element with no role and no keyboard path.
     * Keyboard users are served by Escape and the labelled close button.
     *
     * It tracks MOUSEDOWN, not click, so a drag that starts inside the panel and
     * releases outside it does not dismiss the dialog.
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
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-surface-sunk/70 backdrop-blur-[2px]">
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-label={titleText}
        tabIndex={-1}
        /*
         * `border-rule-strong` overrides `.stock`'s hairline deliberately. On the
         * dark theme the panel sits at 1.1:1 against the dimmed page behind it,
         * and `shadow-raised`'s drop shadow is black-on-near-black so it adds
         * nothing. The stronger border is what makes the panel edge visible.
         */
        className={`stock shadow-raised border-rule-strong w-full ${widthStyles[maxWidth]} outline-none animate-stub-in`}
      >
        <div className="flex items-start justify-between gap-4 px-6 py-4 border-b border-rule">
          <div id={titleId} className="min-w-0">
            {title}
          </div>
          <IconButton
            label="Close dialog"
            inset
            onClick={onClose}
            icon={<X className="w-5 h-5" />}
          />
        </div>

        <div className="px-6 py-5">{children}</div>
      </div>
    </div>
  );
};
