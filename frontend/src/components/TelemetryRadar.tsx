import React, { useEffect, useState } from 'react';
import { MapPin, Radio } from 'lucide-react';
import { telemetryService } from '../services/telemetry';
import { TelemetryPayload } from '../types';
import { formatTimeAgo } from '../lib/format';
import { Card } from './ui/Card';

interface TelemetryRadarProps {
  scheduleId: string;
  /** Stop names for the route this schedule runs, in board order. */
  stops: string[];
}

/** How often the "updated N minutes ago" line is recomputed. */
const AGE_TICK_MS = 30_000;

/**
 * Turns a leg index into a sentence a passenger can act on.
 *
 * Leg `i` runs from `stops[i]` to `stops[i + 1]`. The index arrives from the
 * driver's device, so it is never trusted to be in range for the stop list on
 * this screen: a stale or mismatched index falls back to a true but vague line
 * instead of rendering "Between Lagos and undefined".
 */
const describePosition = (legIndex: number, stops: string[]): string => {
  const from = Number.isInteger(legIndex) && legIndex >= 0 ? stops[legIndex] : undefined;
  const to = from !== undefined ? stops[legIndex + 1] : undefined;
  return from && to ? `Between ${from} and ${to}` : 'On its way along the route';
};

/**
 * Live position of the bus running one schedule, for the passenger booking a
 * seat on it.
 *
 * Telemetry is explicitly best-effort: a dead socket, a failed join, or a
 * connection that drops mid-journey must never read as an error to the
 * passenger watching this panel, only as "not currently tracking". Booking
 * failures get a banner; telemetry failures get silence.
 *
 * Coordinates deliberately never reach the screen. A passenger cannot do
 * anything with "6.5244, 3.3792" — they want to know which stops the bus is
 * between, so that is the only thing this renders.
 */
export const TelemetryRadar: React.FC<TelemetryRadarProps> = ({ scheduleId, stops }) => {
  const [telemetry, setTelemetry] = useState<TelemetryPayload | null>(null);
  const [isLive, setIsLive] = useState(false);

  /**
   * Clock for the relative "updated ..." line. Without it the wording freezes
   * at whatever it said when the last ping landed, so a stream that went quiet
   * ten minutes ago would still claim it updated just now.
   */
  const [now, setNow] = useState<number>(() => Date.now());

  useEffect(() => {
    let cancelled = false;
    setTelemetry(null);

    // Reflects the real socket state over time (including drops and
    // reconnects), unlike a one-shot flag set after the first connect.
    const unsubscribeState = telemetryService.onConnectionStateChange((connected) => {
      if (!cancelled) setIsLive(connected);
    });

    let unsubscribeLocation = () => {};

    const start = async () => {
      try {
        await telemetryService.connect();
        if (cancelled) return;

        await telemetryService.joinScheduleGroup(scheduleId);
        if (cancelled) return;

        unsubscribeLocation = telemetryService.onLocationReceived((data) => {
          if (!cancelled && data.scheduleId === scheduleId) setTelemetry(data);
        });
      } catch {
        // Connection-state listener above already reflects "not live"; there
        // is nothing further to surface here.
      }
    };

    void start();

    return () => {
      cancelled = true;
      unsubscribeState();
      unsubscribeLocation();
      void telemetryService.leaveScheduleGroup(scheduleId);
    };
  }, [scheduleId]);

  // Only runs while there is an age on screen to keep honest.
  useEffect(() => {
    if (!telemetry) return;

    setNow(Date.now());
    const interval = setInterval(() => setNow(Date.now()), AGE_TICK_MS);
    return () => clearInterval(interval);
  }, [telemetry]);

  return (
    <Card
      title={
        <span className="inline-flex items-center gap-2">
          <Radio className="w-4 h-4 text-signal" aria-hidden="true" />
          Where your bus is
        </span>
      }
      action={
        <div className="flex items-center gap-2">
          <span
            aria-hidden="true"
            className={`w-2 h-2 rounded-full shrink-0 ${isLive ? 'bg-stamp animate-tick' : 'bg-ink-faint'}`}
          />
          <span className="stencil text-ink-muted">{isLive ? 'Live' : 'Not currently tracking'}</span>
        </div>
      }
    >
      <div aria-live="polite">
        {telemetry ? (
          <div className="flex items-start gap-3 bg-paper-sunk rounded-ticket border border-rule p-4">
            <MapPin className="w-4 h-4 mt-0.5 text-signal shrink-0" aria-hidden="true" />
            <div className="min-w-0">
              <p className="board text-lg text-ink leading-snug">
                {describePosition(telemetry.currentLegIndex, stops)}
              </p>
              <p className="mt-1 text-xs text-ink-muted">
                Updated {formatTimeAgo(telemetry.timestamp, now)}
              </p>
            </div>
          </div>
        ) : (
          <div className="flex items-center gap-2 bg-paper-sunk rounded-ticket border border-rule p-4 text-xs text-ink-muted">
            <MapPin className="w-4 h-4 text-ink-faint shrink-0" aria-hidden="true" />
            <span>
              {isLive
                ? 'Waiting for the next position update…'
                : 'This bus isn’t sending its position right now.'}
            </span>
          </div>
        )}
      </div>
    </Card>
  );
};
