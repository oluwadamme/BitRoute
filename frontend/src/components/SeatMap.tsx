import React from 'react';
import { Armchair, CheckCircle2, Lock } from 'lucide-react';
import { SeatAvailabilityDto } from '../types';
import { Card } from './ui/Card';

interface SeatMapProps {
  seats: SeatAvailabilityDto[];
  selectedSeatId: string | null;
  onSelectSeat: (seatId: string) => void;
  isLoading: boolean;
}

/**
 * Each seat renders as a ticket stub: an ink outline when free, solid signal
 * fill when chosen, and diagonal hatching plus a lock when it's already
 * booked for this leg — availability is never conveyed by colour alone.
 */
export const SeatMap: React.FC<SeatMapProps> = ({
  seats,
  selectedSeatId,
  onSelectSeat,
  isLoading,
}) => {
  const availableCount = seats.filter((seat) => seat.isAvailable).length;

  const statusMessage = isLoading
    ? 'Checking which seats are free…'
    : seats.length === 0
    ? 'No seats to show yet. Choose a route, date and departure first.'
    : `${seats.length} seat${seats.length === 1 ? '' : 's'} shown, ${availableCount} available`;

  return (
    <Card
      title="Choose your seat"
      subtitle="Seats already booked for this part of the journey are locked."
    >
      <div className="space-y-5">
        <ul className="stencil flex flex-wrap items-center gap-x-5 gap-y-2 text-ink-muted">
          <li className="flex items-center gap-1.5">
            <span aria-hidden="true" className="h-4 w-4 rounded-ticket border border-ink bg-paper-raised" />
            Available
          </li>
          <li className="flex items-center gap-1.5">
            <span aria-hidden="true" className="h-4 w-4 rounded-ticket border border-signal-deep bg-signal" />
            Selected
          </li>
          <li className="flex items-center gap-1.5">
            <span
              aria-hidden="true"
              className="hatched flex h-4 w-4 items-center justify-center rounded-ticket border border-rule-strong"
            >
              <Lock className="h-2.5 w-2.5 text-ink-faint" />
            </span>
            Already booked
          </li>
        </ul>

        <div aria-busy={isLoading}>
          {isLoading ? (
            <div className="space-y-3 py-16 text-center">
              <div
                aria-hidden="true"
                className="mx-auto h-8 w-8 animate-spin rounded-full border-2 border-signal border-t-transparent"
              />
              <p className="text-sm text-ink-muted">Checking which seats are free…</p>
            </div>
          ) : seats.length === 0 ? (
            <p className="rounded-ticket border border-dashed border-rule-strong px-4 py-10 text-center text-sm text-ink-muted">
              No seats to show yet. Choose a route, date and departure first.
            </p>
          ) : (
            <div
              role="group"
              aria-label="Seat map"
              className="stagger grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-5"
            >
              {seats.map((seat) => {
                const isSelected = selectedSeatId === seat.seatId;
                const isAvailable = seat.isAvailable;

                return (
                  <button
                    key={seat.seatId}
                    type="button"
                    disabled={!isAvailable}
                    aria-pressed={isSelected}
                    aria-label={`Seat ${seat.seatNumber}, ${
                      isAvailable ? 'available' : 'already booked for this part of the journey'
                    }`}
                    onClick={() => onSelectSeat(seat.seatId)}
                    className={[
                      'flex flex-col items-center justify-center gap-1.5 rounded-ticket border px-2 py-3 text-center transition-colors',
                      isSelected
                        ? 'border-signal-deep bg-signal text-paper-raised shadow-stub'
                        : isAvailable
                        ? 'border-ink bg-paper-raised text-ink hover:bg-paper-sunk'
                        : 'hatched cursor-not-allowed border-rule-strong text-ink-faint',
                    ].join(' ')}
                  >
                    {isSelected && <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />}
                    {!isAvailable && <Lock className="h-3.5 w-3.5" aria-hidden="true" />}
                    <Armchair className="h-5 w-5" aria-hidden="true" />
                    <span className="figures text-sm font-semibold">{seat.seatNumber}</span>
                  </button>
                );
              })}
            </div>
          )}
        </div>

        {/* Mounted before any message arrives, so the resolved count is
            actually announced rather than missed by assistive tech. */}
        <p role="status" aria-live="polite" className="sr-only">
          {statusMessage}
        </p>
      </div>
    </Card>
  );
};
