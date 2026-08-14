import React from 'react';
import { AlertTriangle, CheckCircle2, X } from 'lucide-react';

export interface Notification {
  message: string;
  type: 'success' | 'error';
}

interface NotificationBannerProps {
  notification: Notification | null;
  onDismiss: () => void;
}

/**
 * Status strip.
 *
 * The live region wrapper is always mounted, even with nothing to show.
 * Assistive technology only announces changes inside a region that already
 * existed; mounting the region and its message in the same tick, as the
 * previous version did, means errors were never announced at all.
 */
export const NotificationBanner: React.FC<NotificationBannerProps> = ({
  notification,
  onDismiss,
}) => {
  const isError = notification?.type === 'error';

  return (
    <div
      // Errors interrupt; confirmations wait their turn.
      role={isError ? 'alert' : 'status'}
      aria-live={isError ? 'assertive' : 'polite'}
      className={notification ? 'block' : 'sr-only'}
    >
      {notification && (
        <div
          className={[
            'flex items-start justify-between gap-4 px-4 py-3 rounded-ticket border animate-roll-in',
            isError
              ? 'bg-signal-wash border-signal/40 text-signal-deep'
              : 'bg-stamp-wash border-stamp/35 text-stamp-deep',
          ].join(' ')}
        >
          <div className="flex items-start gap-3 min-w-0">
            {isError ? (
              <AlertTriangle className="w-5 h-5 shrink-0 mt-px" aria-hidden="true" />
            ) : (
              <CheckCircle2 className="w-5 h-5 shrink-0 mt-px" aria-hidden="true" />
            )}
            <p className="text-sm leading-snug">{notification.message}</p>
          </div>

          <button
            type="button"
            onClick={onDismiss}
            aria-label="Dismiss message"
            className="shrink-0 p-1 -m-1 rounded-ticket opacity-70 hover:opacity-100 transition-opacity"
          >
            <X className="w-4 h-4" aria-hidden="true" />
          </button>
        </div>
      )}
    </div>
  );
};
