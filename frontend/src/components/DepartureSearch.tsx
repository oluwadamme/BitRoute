import React from 'react';
import { ArrowRight } from 'lucide-react';
import { RouteDto, ScheduleDto } from '../types';
import { Card } from './ui/Card';
import { Field, controlStyles } from './ui/Field';
import { formatDepartureTime, formatFare, formatSegment, shortRef, todayIso } from '../lib/format';

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
  // No fabricated stand-ins here: a route with no stops loaded means there is
  // nothing yet to put on the sliders, not an excuse to invent a journey.
  const stops = selectedRoute?.stops ?? [];
  const hasJourney = stops.length >= 2;

  const matchingSchedules = selectedRoute
    ? schedules.filter((schedule) => schedule.routeId === selectedRoute.id)
    : schedules;

  const legCount = alightingIndex - boardingIndex;

  const handleBoardingChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const next = Number(event.target.value);
    if (next < alightingIndex) onSegmentChange(next, alightingIndex);
  };

  const handleAlightingChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const next = Number(event.target.value);
    if (next > boardingIndex) onSegmentChange(boardingIndex, next);
  };

  return (
    <Card
      title="Plan your journey"
      subtitle="Pick your route and departure, then where you'll get on and off."
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
          <p className="rounded-ticket border border-dashed border-rule-strong px-4 py-8 text-center text-sm text-ink-muted">
            {!selectedRoute
              ? routes.length === 0
                ? 'No routes available yet.'
                : 'Choose a route to see its stops and fares.'
              : "This route doesn't have enough stops to plan a journey yet."}
          </p>
        ) : (
          <div className="rounded-ticket border border-rule px-4 pb-4 pt-3">
            {/* Origin -> destination timetable row: stop names prominent */}
            <div className="flex items-center justify-between gap-3">
              <span className="board truncate text-lg text-ink md:text-xl">{stops[boardingIndex]}</span>
              <ArrowRight className="h-5 w-5 shrink-0 text-signal" aria-hidden="true" />
              <span className="board truncate text-right text-lg text-ink md:text-xl">
                {stops[alightingIndex]}
              </span>
            </div>
            <p className="stencil mt-1 text-ink-faint">{formatSegment(boardingIndex, alightingIndex)}</p>

            <fieldset className="mt-4 space-y-4">
              <legend className="stencil mb-1 text-ink-muted">Adjust your journey</legend>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field label="Board at" hint={stops[boardingIndex]}>
                  {(ids) => (
                    <input
                      {...ids}
                      type="range"
                      min={0}
                      max={stops.length - 2}
                      value={boardingIndex}
                      aria-valuetext={stops[boardingIndex]}
                      onChange={handleBoardingChange}
                      className="w-full cursor-pointer accent-signal"
                    />
                  )}
                </Field>

                <Field label="Alight at" hint={stops[alightingIndex]}>
                  {(ids) => (
                    <input
                      {...ids}
                      type="range"
                      min={1}
                      max={stops.length - 1}
                      value={alightingIndex}
                      aria-valuetext={stops[alightingIndex]}
                      onChange={handleAlightingChange}
                      className="w-full cursor-pointer accent-signal"
                    />
                  )}
                </Field>
              </div>
            </fieldset>

            <div className="perforation mt-4 flex items-center justify-between pt-4">
              <span className="stencil text-ink-muted">
                {legCount} leg{legCount === 1 ? '' : 's'}
              </span>
              {estimatedPrice === null ? (
                <span className="board text-xl text-ink-muted">
                  <span aria-hidden="true">&mdash;</span>
                  <span className="sr-only">Fare not available yet</span>
                </span>
              ) : (
                <span className="board text-xl text-signal-deep">{formatFare(estimatedPrice)}</span>
              )}
            </div>
          </div>
        )}
      </div>
    </Card>
  );
};
