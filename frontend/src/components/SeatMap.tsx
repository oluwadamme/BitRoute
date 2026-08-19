import React, { useMemo } from 'react';
import { Check, DoorOpen, Lock } from 'lucide-react';
import { SeatAvailabilityDto, VehicleLayoutDto } from '../types';
import { buildSeatLayout, SeatCell, SimpleLayoutReason } from '../lib/seatLayout';
import { Card } from './ui/Card';

interface SeatMapProps {
  seats: SeatAvailabilityDto[];
  /** The vehicle's cabin shape, as reported with availability. */
  layout: VehicleLayoutDto | null;
  selectedSeatId: string | null;
  onSelectSeat: (seatId: string) => void;
  isLoading: boolean;
}

/**
 * The seating plan, drawn the way a passenger looks at a vehicle: the front at
 * the top, rows running back, the gangway down the middle. A seat's position
 * on screen is a claim about where someone will actually sit, so it is only
 * drawn when the data supports it — `buildSeatLayout` decides that, and when
 * it can't, this falls back to a plain list of seat numbers with a note saying
 * why rather than inventing a shape for the vehicle.
 *
 * State is never carried by colour alone: free seats are outlined, the chosen
 * one is filled and ticked, and a seat already sold on this journey is hatched,
 * locked and genuinely `disabled`.
 */

/** Why a vehicle is shown as a plain list. Sentence case, no internals. */
const simpleNotes: Record<SimpleLayoutReason, string> = {
  'no-seats': '',
  'positions-unavailable':
    "We don't have a seating plan on file for this vehicle, so its seats are listed by number rather than by where they sit on board.",
  'positions-collide':
    "This vehicle's seat positions don't line up into a plan, so its seats are listed by number rather than by where they sit on board.",
};

const seatStateClasses = (isSelected: boolean, isAvailable: boolean): string => {
  if (isSelected) return 'border-signal-deep bg-signal text-surface cursor-pointer shadow-stub';
  if (isAvailable) {
    return 'border-rule-strong bg-surface-raised text-content cursor-pointer hover:border-signal hover:bg-signal-wash hover:text-signal';
  }
  return 'hatched border-rule bg-surface-sunk text-content-faint cursor-not-allowed';
};

interface SeatButtonProps {
  seat: SeatAvailabilityDto;
  /** Full spoken label. Says where the seat is, never whether it's by a window. */
  label: string;
  isSelected: boolean;
  onSelect: (seatId: string) => void;
}

/** 48×48 with an 8px gap, so every seat clears the 44×44 target on its own. */
const SeatButton: React.FC<SeatButtonProps> = ({ seat, label, isSelected, onSelect }) => {
  const isAvailable = seat.isAvailable;

  return (
    <button
      type="button"
      disabled={!isAvailable}
      aria-pressed={isSelected}
      aria-label={label}
      onClick={() => onSelect(seat.seatId)}
      className={[
        'flex h-12 w-12 shrink-0 flex-col items-center justify-center gap-1 rounded-ticket border transition-colors',
        seatStateClasses(isSelected, isAvailable),
      ].join(' ')}
    >
      {/* Every state gets its own glyph, so the fill colour is never the only tell. */}
      {isSelected ? (
        <Check className="h-3 w-3" aria-hidden="true" />
      ) : isAvailable ? (
        // Headrest, so an empty seat still reads as a seat.
        <span aria-hidden="true" className="h-[3px] w-5 rounded-full bg-current opacity-50" />
      ) : (
        <Lock className="h-3 w-3" aria-hidden="true" />
      )}
      <span className="figures text-xs font-semibold leading-none">{seat.seatNumber}</span>
    </button>
  );
};

/** A position the vehicle has no seat recorded for. Purely a spacer. */
const BlankCell: React.FC = () => (
  <span
    aria-hidden="true"
    className="h-12 w-12 shrink-0 rounded-ticket border border-dashed border-rule"
  />
);

export const SeatMap: React.FC<SeatMapProps> = ({
  seats,
  layout: vehicleLayout,
  selectedSeatId,
  onSelectSeat,
  isLoading,
}) => {
  const layout = useMemo(
    () => buildSeatLayout(seats, vehicleLayout),
    [seats, vehicleLayout]
  );
  const availableCount = seats.filter((seat) => seat.isAvailable).length;

  const statusMessage = isLoading
    ? 'Checking which seats are free…'
    : seats.length === 0
    ? 'No seats to show yet. Choose a route, date and departure first.'
    : `${seats.length} seat${seats.length === 1 ? '' : 's'} shown, ${availableCount} available${
        layout.kind === 'plan'
          ? ` across ${layout.rows.length} row${layout.rows.length === 1 ? '' : 's'}`
          : ''
      }`;

  const renderCell = (cell: SeatCell, row: number): React.ReactNode => {
    if (cell.kind === 'blank') return <BlankCell key={`${row}-${cell.column}`} />;

    const isAvailable = cell.seat.isAvailable;
    /*
     * Named by the label printed on the physical seat, which is what the button
     * shows and what a passenger will look for on board. The row is added for
     * orientation, since the grid that conveys it visually is aria-hidden.
     */
    const where = `Seat ${cell.seat.seatNumber}, row ${row}`;

    return (
      <SeatButton
        key={cell.seat.seatId}
        seat={cell.seat}
        isSelected={selectedSeatId === cell.seat.seatId}
        onSelect={onSelectSeat}
        label={`${where}, ${isAvailable ? 'available' : 'already booked for this journey'}`}
      />
    );
  };

  return (
    <Card
      title="Choose your seat"
      subtitle={
        layout.kind === 'plan' && seats.length > 0
          ? 'Front of the bus is at the top. Seats already booked for this part of the journey are locked.'
          : 'Seats already booked for this part of the journey are locked.'
      }
    >
      <div className="space-y-5">
        <ul className="stencil flex flex-wrap items-center gap-x-5 gap-y-2 text-content-muted">
          <li className="flex items-center gap-1.5">
            <span
              aria-hidden="true"
              className="flex h-4 w-4 items-center justify-center rounded-ticket border border-rule-strong bg-surface-raised"
            >
              <span className="h-[2px] w-2 rounded-full bg-content opacity-50" />
            </span>
            Available
          </li>
          <li className="flex items-center gap-1.5">
            <span
              aria-hidden="true"
              className="flex h-4 w-4 items-center justify-center rounded-ticket border border-signal-deep bg-signal text-surface"
            >
              <Check className="h-2.5 w-2.5" />
            </span>
            Selected
          </li>
          <li className="flex items-center gap-1.5">
            <span
              aria-hidden="true"
              className="hatched flex h-4 w-4 items-center justify-center rounded-ticket border border-rule bg-surface-sunk"
            >
              <Lock className="h-2.5 w-2.5 text-content-faint" />
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
              <p className="text-sm text-content-muted">Checking which seats are free…</p>
            </div>
          ) : seats.length === 0 ? (
            <p className="rounded-ticket border border-dashed border-rule-strong px-4 py-10 text-center text-sm text-content-muted">
              No seats to show yet. Choose a route, date and departure first.
            </p>
          ) : layout.kind === 'plan' ? (
            /* Scrolls inside itself on a narrow screen rather than dragging the
               whole page sideways. The negative margin lets it use the card's
               full width before it starts scrolling. */
            <div className="-mx-5 overflow-x-auto px-5 pb-1">
              <div
                role="group"
                aria-label="Seat plan, row 1 at the front of the bus"
                className="mx-auto w-max rounded-t-[2rem] rounded-b-[0.75rem] border-2 border-rule-strong bg-surface-sunk px-3 pb-3 pt-3"
              >
                {/* The vehicle shell is a drawing, not information — the group's
                    name already says which end is the front. */}
                <div
                  aria-hidden="true"
                  className="mb-3 flex items-center justify-between gap-6 border-b border-dashed border-rule pb-3"
                >
                  <span className="flex items-center gap-2 text-content-faint">
                    <span className="flex h-8 w-8 items-center justify-center rounded-full border-2 border-rule-strong">
                      <span className="h-[3px] w-4 rounded-full bg-rule-strong" />
                    </span>
                    <span className="stencil">Driver</span>
                  </span>
                  <span className="stencil text-content-muted">Front</span>
                  <span className="flex items-center gap-2 text-content-faint">
                    <span className="stencil">Door</span>
                    <DoorOpen className="h-5 w-5" />
                  </span>
                </div>

                <div className="flex flex-col gap-2">
                  {layout.rows.map((row) => (
                    <div key={row.row} className="flex items-stretch justify-center gap-2">
                      <span
                        aria-hidden="true"
                        className="figures flex w-5 shrink-0 items-center justify-end text-[10px] text-content-faint"
                      >
                        {row.row}
                      </span>

                      <div className="flex items-center gap-2">
                        {row.left.map((cell) => renderCell(cell, row.row))}
                      </div>

                      {layout.hasAisle && (
                        <span aria-hidden="true" className="flex w-7 shrink-0 justify-center">
                          <span className="w-px bg-rule" />
                        </span>
                      )}

                      {row.right.length > 0 && (
                        <div className="flex items-center gap-2">
                          {row.right.map((cell) => renderCell(cell, row.row))}
                        </div>
                      )}
                    </div>
                  ))}
                </div>

                <div
                  aria-hidden="true"
                  className="stencil mt-3 border-t border-dashed border-rule pt-2 text-center text-content-faint"
                >
                  Rear
                </div>
              </div>
            </div>
          ) : (
            <div className="space-y-3">
              <p className="rounded-ticket border border-dashed border-rule-strong bg-surface-sunk px-3 py-2.5 text-xs leading-relaxed text-content-muted">
                {simpleNotes[layout.reason]}
              </p>
              <div
                role="group"
                aria-label="Seats on this vehicle"
                className="grid grid-cols-4 justify-items-center gap-3 sm:grid-cols-5 md:grid-cols-6"
              >
                {layout.seats.map((seat) => (
                  <SeatButton
                    key={seat.seatId}
                    seat={seat}
                    isSelected={selectedSeatId === seat.seatId}
                    onSelect={onSelectSeat}
                    label={`Seat ${seat.seatNumber}, ${
                      seat.isAvailable ? 'available' : 'already booked for this journey'
                    }`}
                  />
                ))}
              </div>
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
