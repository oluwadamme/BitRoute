import React, { useId, useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { RouteDto, ScheduleDto } from '../types';
import { Card } from './ui/Card';
import { Field, controlStyles } from './ui/Field';
import { formatDepartureTime, formatFare, shortRef, todayIso } from '../lib/format';

interface DepartureSearchProps {
  routes: RouteDto[];
  selectedRoute: RouteDto | null;
  onSelectRoute: (route: RouteDto) => void;
  schedules: ScheduleDto[];
  selectedSchedule: ScheduleDto | null;
  onSelectSchedule: (schedule: ScheduleDto) => void;
  travelDate: string;
  onDateChange: (date: string) => void;
  boardingIndex: number;
  alightingIndex: number;
  onSegmentChange: (boarding: number, alighting: number) => void;
  /** Kobo, or null when the server hasn't quoted a fare yet. */
  estimatedPrice: number | null;
  /** Earliest selectable travel date. Defaults to today when omitted. */
  minDate?: string;
}

export const DepartureSearch: React.FC<DepartureSearchProps> = ({
  routes,
  selectedRoute,
  onSelectRoute,
  schedules,
  selectedSchedule,
  onSelectSchedule,
  travelDate,
  onDateChange,
  boardingIndex,
  alightingIndex,
  onSegmentChange,
  estimatedPrice,
  minDate,
}) => {
  const promptId = useId();

  // No fabricated stand-ins here: a route with no stops loaded means there is
  // nothing yet to draw a route line from, not an excuse to invent a journey.
  const stops = selectedRoute?.stops ?? [];
  const lastIndex = stops.length - 1;
  const hasJourney = stops.length >= 2;

  const matchingSchedules = selectedRoute
    ? schedules.filter((schedule) => schedule.routeId === selectedRoute.id)
    : schedules;

  /**
   * Half-finished selections live here, because the prop contract can only
   * express a complete journey: two integers, both always present. Tapping a
   * boarding stop clears the destination, and that in-between state must not be
   * pushed upstream — committing a boarding stop on its own would refetch
   * availability for a journey the passenger hasn't finished describing.
   */
  const [pendingBoarding, setPendingBoarding] = useState<number | null>(null);

  // Stops belong to a route, so an index held from the previous route is
  // meaningless the moment the route changes — it would silently point at a
  // different town. Reset during render rather than in an effect, so no frame
  // is ever painted with the stale stop highlighted.
  const routeId = selectedRoute?.id ?? null;
  const [trackedRouteId, setTrackedRouteId] = useState<string | null>(routeId);
  if (trackedRouteId !== routeId) {
    setTrackedRouteId(routeId);
    setPendingBoarding(null);
  }
  const carriedPending = trackedRouteId === routeId ? pendingBoarding : null;

  // The parent's indices are treated as a claim to verify, not a given. A route
  // whose stop list shrank underneath them would otherwise render blank stop
  // names and quote a journey nobody can take.
  const committedIsValid =
    hasJourney &&
    Number.isInteger(boardingIndex) &&
    Number.isInteger(alightingIndex) &&
    boardingIndex >= 0 &&
    alightingIndex <= lastIndex &&
    boardingIndex < alightingIndex;

  // A boarding stop must leave somewhere to travel to, so it can never be last.
  const pending =
    carriedPending !== null && carriedPending >= 0 && carriedPending < lastIndex
      ? carriedPending
      : null;

  const boarding: number | null = pending ?? (committedIsValid ? boardingIndex : null);
  const alighting: number | null =
    pending !== null ? null : committedIsValid ? alightingIndex : null;

  const boardingStop = boarding === null ? null : stops[boarding];
  const alightingStop = alighting === null ? null : stops[alighting];
  const legCount = boarding !== null && alighting !== null ? alighting - boarding : 0;
  const legWord = legCount === 1 ? 'stop' : 'stops';

  /** A tap at or before the boarding stop restarts the journey from there. */
  const isBoardingTap = (index: number) => boarding === null || index <= boarding;

  const handleStopTap = (index: number) => {
    if (boarding === null || index <= boarding) {
      // The last stop can never be a boarding stop: there'd be nowhere to ride to.
      if (index >= lastIndex) return;
      setPendingBoarding(index);
      return;
    }

    setPendingBoarding(null);
    onSegmentChange(boarding, index);
  };

  const describeStop = (index: number): string => {
    const name = stops[index];

    if (index === boarding) return `${name}, boarding stop`;
    if (index === alighting) return `${name}, stop where you get off`;

    if (isBoardingTap(index)) {
      if (index === lastIndex) return `${name}, cannot board here, it is the last stop`;
      return boarding === null
        ? `${name}, tap to board here`
        : `${name}, tap to board here instead`;
    }

    const onJourney = alighting !== null && index < alighting;
    return onJourney
      ? `${name}, on your journey, tap to get off here`
      : `${name}, tap to get off here`;
  };

  const prompt =
    boarding === null
      ? "Tap where you'll board"
      : alighting === null
        ? "Now tap where you'll get off"
        : 'Tap a stop to change your journey';

  // Spoken state, in a region that is mounted for the life of this card so a
  // message is never delivered to a region born at the same moment.
  const spokenState = (() => {
    if (!selectedRoute) {
      return routes.length === 0
        ? 'No routes available yet.'
        : 'Choose a route to see its stops and fares.';
    }
    if (!hasJourney) return "This route doesn't have enough stops to plan a journey yet.";
    if (boarding === null) return "Tap the stop where you'll board.";
    if (alighting === null) {
      return `You board at ${boardingStop}. Now tap a later stop to choose where you get off.`;
    }

    const fare =
      estimatedPrice === null
        ? "We haven't got a fare for this journey yet."
        : `Fare ${formatFare(estimatedPrice)}.`;
    return `You board at ${boardingStop} and get off at ${alightingStop}, ${legCount} ${legWord} later. ${fare}`;
  })();

  return (
    <Card
      title="Plan your journey"
      subtitle="Pick your route and departure, then tap where you'll get on and off."
    >
      <div className="stagger space-y-5">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <Field label="Route">
            {(ids) => (
              <select
                {...ids}
                className={controlStyles}
                value={selectedRoute?.id ?? ''}
                onChange={(event) => {
                  const found = routes.find((route) => route.id === event.target.value);
                  if (found) onSelectRoute(found);
                }}
              >
                {routes.length === 0 && <option value="">No routes available yet</option>}
                {routes.length > 0 && !selectedRoute && <option value="">Choose a route</option>}
                {routes.map((route) => (
                  <option key={route.id} value={route.id}>
                    {route.name}
                  </option>
                ))}
              </select>
            )}
          </Field>

          <Field
            label="Departure time"
            hint={matchingSchedules.length === 0 ? 'No departures for this route yet.' : undefined}
          >
            {(ids) => (
              <select
                {...ids}
                className={controlStyles}
                value={selectedSchedule?.id ?? ''}
                disabled={matchingSchedules.length === 0}
                onChange={(event) => {
                  const found = schedules.find((schedule) => schedule.id === event.target.value);
                  if (found) onSelectSchedule(found);
                }}
              >
                {matchingSchedules.length === 0 && (
                  <option value="">No departures for this route yet</option>
                )}
                {matchingSchedules.length > 0 && !selectedSchedule && (
                  <option value="">Choose a departure</option>
                )}
                {matchingSchedules.map((schedule) => (
                  <option key={schedule.id} value={schedule.id}>
                    {formatDepartureTime(schedule.departureTimeOfDay)} &middot; {shortRef(schedule.id)}
                  </option>
                ))}
              </select>
            )}
          </Field>

          <Field label="Travel date">
            {(ids) => (
              <input
                {...ids}
                type="date"
                min={minDate ?? todayIso()}
                className={controlStyles}
                value={travelDate}
                onChange={(event) => onDateChange(event.target.value)}
              />
            )}
          </Field>
        </div>

        {!hasJourney ? (
          <p className="rounded-ticket border border-dashed border-rule-strong px-4 py-8 text-center text-sm text-content-muted">
            {!selectedRoute
              ? routes.length === 0
                ? 'No routes available yet.'
                : 'Choose a route to see its stops and fares.'
              : "This route doesn't have enough stops to plan a journey yet."}
          </p>
        ) : (
          <div className="rounded-ticket border border-rule">
            {/* Timetable header: the chosen journey, stop names first. */}
            <div className="px-4 pb-3 pt-3">
              <p id={promptId} className="stencil text-content-muted">
                {prompt}
              </p>

              {boardingStop && (
                <div className="mt-2 flex items-center justify-between gap-3">
                  <span className="board min-w-0 truncate text-lg text-content md:text-xl">
                    {boardingStop}
                  </span>
                  <ArrowRight className="h-5 w-5 shrink-0 text-signal" aria-hidden="true" />
                  {alightingStop ? (
                    <span className="board min-w-0 truncate text-right text-lg text-content md:text-xl">
                      {alightingStop}
                    </span>
                  ) : (
                    <span className="board shrink-0 text-right text-lg text-content-faint md:text-xl">
                      Choose a stop
                    </span>
                  )}
                </div>
              )}
            </div>

            {/* The route line. Every stop in order, tapped rather than dragged. */}
            <div
              role="group"
              aria-labelledby={promptId}
              className="border-y border-rule bg-surface-sunk"
            >
              <ul>
                {stops.map((stop, index) => {
                  const isBoarding = index === boarding;
                  const isAlighting = index === alighting;
                  const isEndpoint = isBoarding || isAlighting;
                  const isBetween =
                    boarding !== null &&
                    alighting !== null &&
                    index > boarding &&
                    index < alighting;
                  const isOnJourney = isEndpoint || isBetween;

                  // The connector is split at the dot so each half can carry the
                  // state of the leg it belongs to.
                  const legAbove =
                    boarding !== null && alighting !== null && index > boarding && index <= alighting;
                  const legBelow =
                    boarding !== null && alighting !== null && index >= boarding && index < alighting;

                  const isDisabled = isBoardingTap(index) && index === lastIndex;

                  return (
                    <li key={`${index}-${stop}`}>
                      <button
                        type="button"
                        disabled={isDisabled}
                        aria-pressed={isEndpoint}
                        aria-label={describeStop(index)}
                        onClick={() => handleStopTap(index)}
                        className={[
                          // No vertical padding: the rail is `self-stretch`, so any
                          // would break the connector line between rows. Height
                          // comes from min-h, which clears the 44px touch target.
                          'flex w-full min-h-[48px] items-stretch gap-3 px-3 text-left transition-colors',
                          'cursor-pointer disabled:cursor-not-allowed',
                          isEndpoint
                            ? 'bg-signal-wash'
                            : isBetween
                              ? 'bg-signal-wash/50'
                              : 'bg-transparent',
                          isDisabled ? '' : 'hover:bg-surface-raised',
                        ].join(' ')}
                      >
                        <span
                          className="figures w-6 shrink-0 self-center text-[11px] text-content-faint"
                          aria-hidden="true"
                        >
                          {String(index + 1).padStart(2, '0')}
                        </span>

                        <span
                          className={[
                            'board min-w-0 flex-1 self-center truncate text-base md:text-lg',
                            isDisabled
                              ? 'text-content-faint'
                              : isOnJourney
                                ? 'text-content'
                                : 'text-content-muted',
                          ].join(' ')}
                        >
                          {stop}
                        </span>

                        {/* Decorative rail: the line reads as the route itself. */}
                        <span
                          className="relative w-8 shrink-0 self-stretch"
                          aria-hidden="true"
                        >
                          {index > 0 && (
                            <span
                              className={[
                                'absolute left-1/2 top-0 h-1/2 -translate-x-1/2',
                                legAbove
                                  ? 'w-0.5 bg-signal'
                                  : 'w-0 border-l border-dashed border-rule-strong',
                              ].join(' ')}
                            />
                          )}
                          {index < lastIndex && (
                            <span
                              className={[
                                'absolute bottom-0 left-1/2 h-1/2 -translate-x-1/2',
                                legBelow
                                  ? 'w-0.5 bg-signal'
                                  : 'w-0 border-l border-dashed border-rule-strong',
                              ].join(' ')}
                            />
                          )}
                          <span
                            className={[
                              'absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 rounded-full',
                              isEndpoint
                                ? 'h-3.5 w-3.5 bg-signal ring-2 ring-signal-wash'
                                : isBetween
                                  ? 'h-2 w-2 bg-signal'
                                  : 'h-2.5 w-2.5 border-2 border-rule-strong bg-surface-sunk',
                            ].join(' ')}
                          />
                        </span>

                        <span className="flex w-[5.5rem] shrink-0 items-center justify-end self-center whitespace-nowrap">
                          {isBoarding && (
                            <span className="stencil rounded-ticket bg-signal px-1.5 py-0.5 text-surface">
                              Board
                            </span>
                          )}
                          {isAlighting && (
                            <span className="stencil rounded-ticket border border-signal px-1.5 py-0.5 text-signal-deep">
                              Get off
                            </span>
                          )}
                          {isBetween && (
                            <span className="stencil text-[10px] text-content-muted">On the way</span>
                          )}
                          {isDisabled && (
                            <span className="stencil text-[10px] text-content-faint">Last stop</span>
                          )}
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ul>
            </div>

            {/* The tear line: journey above, fare below. */}
            <div className="px-4 pb-4 pt-1">
              <div className="perforation flex items-center justify-between gap-3 pt-4">
                {alighting === null ? (
                  <>
                    <span className="stencil text-content-muted">Fare needs both stops</span>
                    <span className="board text-xl text-content-muted">
                      <span aria-hidden="true">&mdash;</span>
                      <span className="sr-only">
                        Fare shows once you choose where you get off
                      </span>
                    </span>
                  </>
                ) : (
                  <>
                    <span className="stencil text-content-muted">
                      {legCount} {legWord}
                    </span>
                    {estimatedPrice === null ? (
                      <span className="board text-xl text-content-muted">
                        <span aria-hidden="true">&mdash;</span>
                        <span className="sr-only">Fare not available yet</span>
                      </span>
                    ) : (
                      <span className="board text-xl text-signal-deep">
                        {formatFare(estimatedPrice)}
                      </span>
                    )}
                  </>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      <p role="status" aria-live="polite" className="sr-only">
        {spokenState}
      </p>
    </Card>
  );
};
