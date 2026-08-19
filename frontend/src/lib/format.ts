/**
 * Fares cross the wire as integer kobo, never as a decimal or a float, so all
 * conversion to naira happens here at the display boundary and nowhere else.
 */

import type { BookingDto } from '../types';

const nairaWithKobo = new Intl.NumberFormat('en-NG', {
  style: 'currency',
  currency: 'NGN',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const nairaWhole = new Intl.NumberFormat('en-NG', {
  style: 'currency',
  currency: 'NGN',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

/**
 * Formats an integer kobo amount as naira.
 *
 * Pass `compact` where the exact kobo is noise, such as inside a button label.
 * Previously each call site inlined its own conversion and they disagreed on
 * whether to show kobo, so the same fare rendered two different ways on one
 * screen.
 */
export const formatFare = (kobo: number, compact = false): string => {
  const naira = kobo / 100;
  return compact && Number.isInteger(naira)
    ? nairaWhole.format(naira)
    : nairaWithKobo.format(naira);
};

/** Renders a half-open segment the way the domain writes it: [i, j). */
export const formatSegment = (boardingIndex: number, alightingIndex: number): string =>
  `[${boardingIndex}, ${alightingIndex})`;

/** Short booking reference for display. Full ids stay in aria labels and copy actions. */
export const shortRef = (id: string): string => id.slice(0, 8).toUpperCase();

/** mm:ss for a countdown. Clamps at zero so an elapsed hold never shows a negative. */
export const formatCountdown = (totalSeconds: number): string => {
  const safe = Math.max(0, totalSeconds);
  const minutes = Math.floor(safe / 60);
  const seconds = safe % 60;
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
};

/** Spoken form of a countdown, so a screen reader does not read "05:42" as digits. */
export const describeCountdown = (totalSeconds: number): string => {
  const safe = Math.max(0, totalSeconds);
  if (safe === 0) return 'Hold expired';

  const minutes = Math.floor(safe / 60);
  const seconds = safe % 60;
  const parts: string[] = [];
  if (minutes) parts.push(`${minutes} minute${minutes === 1 ? '' : 's'}`);
  if (seconds) parts.push(`${seconds} second${seconds === 1 ? '' : 's'}`);
  return `${parts.join(' ')} left to pay`;
};

/** Today's date as YYYY-MM-DD in the viewer's own timezone, for date input bounds. */
export const todayIso = (): string => {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return local.toISOString().split('T')[0];
};

/** Human-readable travel date, e.g. "Fri 15 Aug 2026". */
export const formatTravelDate = (iso: string): string => {
  const parsed = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(parsed.getTime())) return iso;

  return parsed.toLocaleDateString('en-NG', {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
};

/** Trims a "HH:mm:ss" departure time to "HH:mm" for display. */
export const formatDepartureTime = (timeOfDay: string): string =>
  /^\d{2}:\d{2}/.test(timeOfDay) ? timeOfDay.slice(0, 5) : timeOfDay;

/**
 * Passenger-facing wording for a booking status.
 *
 * The DTO's status values happen to read as English, so they were being
 * printed to screen verbatim. That put passenger-facing copy under the
 * server's control: a rename on the backend enum would land straight on a
 * passenger's screen with no review. The map is exhaustive over the union, so
 * a new status is a compile error here rather than a silent passthrough.
 */
const bookingStatusLabels: Record<BookingDto['status'], string> = {
  Held: 'Seat held',
  Confirmed: 'Confirmed',
  Expired: 'Hold expired',
  Cancelled: 'Cancelled',
};

export const bookingStatusLabel = (status: BookingDto['status']): string =>
  bookingStatusLabels[status];

/**
 * Human relative time, e.g. "just now" or "2 minutes ago".
 *
 * `now` is a parameter rather than a `Date.now()` call inside so a caller that
 * ticks a clock in state re-renders with a value that actually changes.
 * Anything unparseable or dated in the future reads as "just now" rather than
 * an "Invalid Date" or a negative age.
 */
export const formatTimeAgo = (iso: string, now: number = Date.now()): string => {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return 'just now';

  const seconds = Math.max(0, Math.floor((now - then) / 1000));
  if (seconds < 60) return 'just now';

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? '' : 's'} ago`;

  const hours = Math.floor(minutes / 60);
  return `${hours} hour${hours === 1 ? '' : 's'} ago`;
};
