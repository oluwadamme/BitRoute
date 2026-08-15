import React, { useEffect, useState } from 'react';
import { AlertTriangle, Clock, CreditCard, ShieldCheck } from 'lucide-react';
import { BookingDto } from '../types';
import { Modal } from './ui/Modal';
import { Button } from './ui/Button';
import { Badge } from './ui/Badge';
import { NotificationBanner } from './ui/NotificationBanner';
import { describeCountdown, formatCountdown, formatFare, formatTravelDate, shortRef } from '../lib/format';

interface CheckoutModalProps {
  booking: BookingDto;
  onClose: () => void;
  onPayWithPaystack: (bookingId: string) => void;
  isProcessing: boolean;
  paymentError?: string | null;
  onClearPaymentError?: () => void;
}

/** Seconds remaining until `holdExpiry`, or null when there is no hold to count down. */
const computeSecondsLeft = (holdExpiry: string | null): number | null => {
  if (!holdExpiry) return null;
  const expiryTime = new Date(holdExpiry).getTime();
  return Math.max(0, Math.floor((expiryTime - Date.now()) / 1000));
};

/** Below this many seconds, the live region announces more often. */
const URGENT_THRESHOLD_SECONDS = 60;
const NORMAL_ANNOUNCE_INTERVAL = 60;
const URGENT_ANNOUNCE_INTERVAL = 15;

type HoldState = 'active' | 'expired' | 'none';


const describeJourneyLength = (boardingIndex: number, alightingIndex: number): string => {
  // A valid booking always spans at least one stop; clamping means malformed
  // data can never render "0 stops".
  const stops = Math.max(1, alightingIndex - boardingIndex);
  return `${stops} stop${stops === 1 ? '' : 's'}`;
};

export const CheckoutModal: React.FC<CheckoutModalProps> = ({
  booking,
  onClose,
  onPayWithPaystack,
  isProcessing,
  paymentError,
  onClearPaymentError,
}) => {
  // Lazy initialiser: a hold created 8 minutes ago must show ~2 minutes left on
  // the very first paint, not a hardcoded 10:00 that only self-corrects a
  // second later.
  const [secondsLeft, setSecondsLeft] = useState<number | null>(() =>
    computeSecondsLeft(booking.holdExpiry)
  );

  // The seconds figure the live region last spoke. Only advances on the
  // announce schedule below, so assistive tech isn't read a fresh time every
  // single second.
  const [announcedSeconds, setAnnouncedSeconds] = useState<number | null>(() =>
    computeSecondsLeft(booking.holdExpiry)
  );

  useEffect(() => {
    // Re-sync immediately in case the booking prop changed while mounted, then
    // start (or skip) the ticking interval.
    setSecondsLeft(computeSecondsLeft(booking.holdExpiry));

    if (!booking.holdExpiry) return;

    const expiryTime = new Date(booking.holdExpiry).getTime();
    const interval = setInterval(() => {
      const remaining = Math.max(0, Math.floor((expiryTime - Date.now()) / 1000));
      setSecondsLeft(remaining);
      if (remaining <= 0) clearInterval(interval);
    }, 1000);

    return () => clearInterval(interval);
  }, [booking.holdExpiry]);

  useEffect(() => {
    if (secondsLeft === null) return;
    const isUrgent = secondsLeft <= URGENT_THRESHOLD_SECONDS;
    const announceInterval = isUrgent ? URGENT_ANNOUNCE_INTERVAL : NORMAL_ANNOUNCE_INTERVAL;
    if (secondsLeft === 0 || secondsLeft % announceInterval === 0) {
      setAnnouncedSeconds(secondsLeft);
    }
  }, [secondsLeft]);

  // A hold that never had an expiry (holdExpiry === null) is a distinct,
  // honest state from one that ran out at 0 — the previous version showed a
  // frozen "10:00" forever in the first case and never explained the second.
  const holdState: HoldState =
    secondsLeft === null ? 'none' : secondsLeft <= 0 ? 'expired' : 'active';

  const isPaymentDisabled = holdState === 'expired';

  const expiryClockTime = booking.holdExpiry
    ? new Date(booking.holdExpiry).toLocaleTimeString('en-NG', { hour: '2-digit', minute: '2-digit' })
    : null;

  const titleText = 'Complete your payment';
  const modalTitle = (
    <div className="space-y-1.5">
      <Badge
        variant={holdState === 'expired' ? 'signal' : holdState === 'active' ? 'ochre' : 'neutral'}
        blink={holdState === 'active' && secondsLeft !== null && secondsLeft <= URGENT_THRESHOLD_SECONDS}
      >
        {holdState === 'expired' ? 'Hold expired' : holdState === 'active' ? 'Seat held' : 'No countdown'}
      </Badge>
      <h2 className="board text-xl text-content">{titleText}</h2>
    </div>
  );

  return (
    <Modal isOpen={true} onClose={onClose} title={modalTitle} titleText={titleText} maxWidth="lg">
      <div className="space-y-5">
        {paymentError && (
          <NotificationBanner
            notification={{ type: 'error', message: paymentError }}
            onDismiss={() => onClearPaymentError?.()}
          />
        )}

        {/*
          Always mounted so the browser has somewhere to announce into before
          the first message arrives. Spoken form only, kept out of the visual
          flow — the visible countdown below is aria-hidden so the two never
          fight for the same announcement.
        */}
        <p className="sr-only" role="status" aria-live="polite">
          {announcedSeconds !== null ? describeCountdown(announcedSeconds) : ''}
        </p>

        {holdState === 'active' && secondsLeft !== null && (
          <div className="flex items-center justify-between gap-4 rounded-ticket border border-ochre/40 bg-ochre-wash p-4">
            <div className="flex items-center gap-3 min-w-0">
              <Clock aria-hidden="true" className="w-5 h-5 text-ochre-deep shrink-0" />
              <div className="min-w-0">
                <p className="stencil text-ochre-deep">Pay before this seat is released</p>
                {expiryClockTime && (
                  <p className="text-xs text-content-muted mt-0.5">Your hold ends at {expiryClockTime}</p>
                )}
              </div>
            </div>
            <span className="board text-3xl text-ochre-deep shrink-0" aria-hidden="true">
              {formatCountdown(secondsLeft)}
            </span>
          </div>
        )}

        {holdState === 'expired' && (
          <div className="flex items-center gap-3 rounded-ticket border border-signal/40 bg-signal-wash p-4">
            <AlertTriangle aria-hidden="true" className="w-5 h-5 text-signal-deep shrink-0" />
            <div>
              <p className="stencil text-signal-deep">Your hold has run out</p>
              <p className="text-xs text-content-muted mt-0.5">
                That seat is back on sale. Close this window and hold it again if it&rsquo;s still
                free.
              </p>
            </div>
          </div>
        )}

        {holdState === 'none' && (
          <div className="flex items-center gap-3 rounded-ticket border border-rule-strong bg-surface-sunk p-4">
            <Clock aria-hidden="true" className="w-5 h-5 text-content-faint shrink-0" />
            <p className="text-xs text-content-muted">
              There&rsquo;s no countdown on this booking. Pay now to be sure of your seat.
            </p>
          </div>
        )}

        {/*
          The boarding-pass tear: journey details above, fare below, split by
          a genuine perforation rather than a plain divider.
        */}
        <div className="rounded-ticket border border-rule bg-surface-raised">
          <div className="px-4 py-4 space-y-2 text-sm">
            <div className="flex justify-between gap-4">
              <span className="stencil text-content-faint">Booking ref</span>
              <span className="figures text-content">{shortRef(booking.id)}</span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="stencil text-content-faint">Travel date</span>
              <span className="text-content">{formatTravelDate(booking.travelDate)}</span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="stencil text-content-faint">Journey</span>
              <span className="text-content">
                {describeJourneyLength(booking.boardingIndex, booking.alightingIndex)} along the route
              </span>
            </div>
          </div>

          <div className="perforation" aria-hidden="true" />

          <div className="px-4 py-4 flex items-center justify-between">
            <span className="board text-sm text-content-muted">Total fare</span>
            <span className="board text-2xl text-signal-deep">{formatFare(booking.price)}</span>
          </div>
        </div>

        <div className="flex items-center justify-center gap-2 text-xs text-content-muted">
          <ShieldCheck aria-hidden="true" className="w-4 h-4 text-stamp" />
          <span>Payment secured by Paystack</span>
        </div>

        <Button
          size="lg"
          className="w-full"
          isLoading={isProcessing}
          loadingLabel="Opening Paystack"
          disabled={isPaymentDisabled}
          onClick={() => onPayWithPaystack(booking.id)}
          icon={<CreditCard className="w-5 h-5" />}
        >
          Pay {formatFare(booking.price, true)} with Paystack
        </Button>

        {isPaymentDisabled && (
          <p className="text-xs text-signal-deep text-center">
            You can&rsquo;t pay for a hold that has run out.
          </p>
        )}
      </div>
    </Modal>
  );
};
