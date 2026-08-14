import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Clock, CreditCard, Trash2 } from 'lucide-react';
import { api, isAbortError, toErrorMessage } from '../services/api';
import { BookingDto } from '../types';
import { Card } from './ui/Card';
import { Button } from './ui/Button';
import { Badge } from './ui/Badge';
import { Modal } from './ui/Modal';
import { NotificationBanner, Notification } from './ui/NotificationBanner';
import {
  bookingStatusLabel,
  formatCountdown,
  formatFare,
  formatSegment,
  formatTravelDate,
  shortRef,
} from '../lib/format';

interface MyBookingsTabProps {
  onPayBooking: (booking: BookingDto) => void;
}

/** Held bookings are time-critical, confirmed ones are settled business, the
 * rest is history. Within a tier, soonest travel date first. */
const STATUS_TIER: Record<BookingDto['status'], number> = {
  Held: 0,
  Confirmed: 1,
  Expired: 2,
  Cancelled: 2,
};

const STATUS_BADGE_VARIANT: Record<BookingDto['status'], 'ochre' | 'stamp' | 'neutral'> = {
  Held: 'ochre',
  Confirmed: 'stamp',
  Expired: 'neutral',
  Cancelled: 'neutral',
};

const sortBookings = (bookings: BookingDto[]): BookingDto[] =>
  [...bookings].sort((a, b) => {
    const tierDiff = STATUS_TIER[a.status] - STATUS_TIER[b.status];
    if (tierDiff !== 0) return tierDiff;
    return a.travelDate.localeCompare(b.travelDate);
  });

const secondsUntil = (isoTime: string, now: number): number =>
  Math.max(0, Math.floor((new Date(isoTime).getTime() - now) / 1000));

export const MyBookingsTab: React.FC<MyBookingsTabProps> = ({ onPayBooking }) => {
  const [bookings, setBookings] = useState<BookingDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [notification, setNotification] = useState<Notification | null>(null);
  const [cancelTarget, setCancelTarget] = useState<BookingDto | null>(null);
  const [isCancelling, setIsCancelling] = useState(false);
  const [now, setNow] = useState<number>(() => Date.now());

  const abortRef = useRef<AbortController | null>(null);

  const loadBookings = useCallback(async () => {
    // A stale in-flight request must never win a race against a fresher one.
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsLoading(true);
    try {
      const data = await api.getMyBookings(controller.signal);
      setBookings(data);
    } catch (err: unknown) {
      if (isAbortError(err)) return;
      setBookings([]);
      setNotification({
        type: 'error',
        message: toErrorMessage(err, "We couldn't load your bookings. Sign in and try again."),
      });
    } finally {
      if (!controller.signal.aborted) setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadBookings();
    // Cancel any request still in flight when this component goes away, so a
    // fast unmount never sets state on a dead component.
    return () => abortRef.current?.abort();
  }, [loadBookings]);

  const sortedBookings = useMemo(() => sortBookings(bookings), [bookings]);

  const hasActiveHold = useMemo(
    () => bookings.some((b) => b.status === 'Held' && b.holdExpiry),
    [bookings]
  );

  useEffect(() => {
    if (!hasActiveHold) return;
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, [hasActiveHold]);

  const handleRefreshClick = () => {
    setNotification(null);
    loadBookings();
  };

  const handleCancelConfirmed = async () => {
    if (!cancelTarget) return;
    setIsCancelling(true);
    try {
      await api.cancelBooking(cancelTarget.id);
      setNotification({
        type: 'success',
        message: `Booking ${shortRef(cancelTarget.id)} is cancelled. Your seat is back on sale.`,
      });
      setCancelTarget(null);
      loadBookings();
    } catch (err: unknown) {
      setNotification({
        type: 'error',
        message: toErrorMessage(err, "We couldn't cancel that booking. Try again in a moment."),
      });
    } finally {
      setIsCancelling(false);
    }
  };

  return (
    <>
      <Card
        title="My bookings"
        subtitle="Seats you've held, paid for or cancelled."
        action={
          <Button
            variant="secondary"
            size="sm"
            onClick={handleRefreshClick}
            isLoading={isLoading}
            loadingLabel="Refreshing your bookings"
          >
            Refresh
          </Button>
        }
      >
        <div className="space-y-4">
          <NotificationBanner notification={notification} onDismiss={() => setNotification(null)} />

          <div aria-busy={isLoading || undefined}>
            {isLoading && bookings.length === 0 ? (
              <p className="py-10 text-center text-sm text-ink-muted">Loading your bookings&hellip;</p>
            ) : sortedBookings.length === 0 ? (
              <p className="py-10 text-center text-sm text-ink-muted">
                You haven't booked a seat yet. Search for a departure to get started.
              </p>
            ) : (
              <ul aria-label="Your bookings" className="stagger space-y-3">
                {sortedBookings.map((booking) => {
                  const holdSecondsLeft =
                    booking.status === 'Held' && booking.holdExpiry
                      ? secondsUntil(booking.holdExpiry, now)
                      : null;

                  return (
                    <li key={booking.id}>
                      <Card as="article">
                        <div className="flex flex-wrap items-start justify-between gap-4">
                          <div className="space-y-1.5 min-w-0">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="figures text-xs text-ink-faint">
                                Ref {shortRef(booking.id)}
                              </span>
                              <Badge
                                variant={STATUS_BADGE_VARIANT[booking.status]}
                                blink={holdSecondsLeft !== null && holdSecondsLeft > 0}
                              >
                                {bookingStatusLabel(booking.status)}
                              </Badge>
                            </div>
                            <p className="board text-lg text-ink">{formatTravelDate(booking.travelDate)}</p>
                            <p className="text-xs text-ink-muted">
                              <span className="figures">
                                Stop {booking.boardingIndex} &rarr; Stop {booking.alightingIndex}
                              </span>
                              {' · segment '}
                              <span className="figures">
                                {formatSegment(booking.boardingIndex, booking.alightingIndex)}
                              </span>
                              {' · '}
                              <span className="figures">{formatFare(booking.price, true)}</span>
                            </p>
                            {holdSecondsLeft !== null && (
                              <p className="flex items-center gap-1.5 text-xs font-semibold text-ochre-deep">
                                <Clock aria-hidden="true" className="w-3.5 h-3.5 shrink-0" />
                                {holdSecondsLeft > 0
                                  ? `Hold ends in ${formatCountdown(holdSecondsLeft)}`
                                  : 'Hold has run out — refresh to see the latest'}
                              </p>
                            )}
                          </div>

                          <div className="shrink-0 flex items-center gap-2">
                            {booking.status === 'Held' && (
                              <Button
                                size="sm"
                                onClick={() => onPayBooking(booking)}
                                icon={<CreditCard className="w-3.5 h-3.5" />}
                              >
                                Pay {formatFare(booking.price, true)}
                              </Button>
                            )}

                            {booking.status === 'Confirmed' && (
                              <Button
                                variant="danger"
                                size="sm"
                                onClick={() => setCancelTarget(booking)}
                                icon={<Trash2 className="w-3.5 h-3.5" />}
                              >
                                Cancel booking
                              </Button>
                            )}
                          </div>
                        </div>
                      </Card>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        </div>
      </Card>

      <Modal
        isOpen={cancelTarget !== null}
        onClose={() => (isCancelling ? undefined : setCancelTarget(null))}
        title="Cancel this booking?"
        maxWidth="sm"
      >
        {cancelTarget && (
          <div className="space-y-4">
            <p className="text-sm text-ink">
              This releases your seat on{' '}
              <strong>{formatTravelDate(cancelTarget.travelDate)}</strong>, Stop{' '}
              {cancelTarget.boardingIndex} &rarr; Stop {cancelTarget.alightingIndex}, segment{' '}
              <strong className="figures">
                {formatSegment(cancelTarget.boardingIndex, cancelTarget.alightingIndex)}
              </strong>{' '}
              (fare <strong className="figures">{formatFare(cancelTarget.price, true)}</strong>). Once
              you cancel, that seat goes back on sale and you can't undo this.
            </p>

            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setCancelTarget(null)} disabled={isCancelling}>
                Keep booking
              </Button>
              <Button
                variant="danger"
                onClick={handleCancelConfirmed}
                isLoading={isCancelling}
                loadingLabel="Cancelling your booking"
                icon={<Trash2 className="w-4 h-4" />}
              >
                Cancel booking
              </Button>
            </div>
          </div>
        )}
      </Modal>
    </>
  );
};
