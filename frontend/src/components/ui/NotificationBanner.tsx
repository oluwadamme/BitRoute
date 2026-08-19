import React from 'react';
import { AlertTriangle, CheckCircle2, X } from 'lucide-react';
import { IconButton } from './IconButton';

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
 * The live-region wrapper is ALWAYS mounted, even with nothing to show — that
 * is why the `sr-only` branch below looks like dead markup. It is not: assistive
 * technology only announces changes inside a region that already existed, so
 * mounting the region and its message in the same tick means the message is
 * never announced at all. Do not collapse this to `{notification && ...}`.
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
              ? 'bg-signal-wash border-signal/60 text-signal-deep'
              : 'bg-stamp-wash border-stamp/55 text-stamp-deep',
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

          <IconButton
            label="Dismiss message"
            inset
            onClick={onDismiss}
            icon={<X className="w-4 h-4" />}
          />
        </div>
      )}
    </div>
  );
};
