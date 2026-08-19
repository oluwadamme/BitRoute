/**
 * Turns a flat list of seats into a seating plan — or admits it can't.
 *
 * The server is now the authority: every seat carries `row` and `column`, and
 * the vehicle reports `{ rowCount, seatsPerRow, aisleAfterColumn }`. Columns are
 * 1-based and count the aisle as a position of its own, so a 2+2 coach is five
 * columns wide with seats at 1, 2, 4, 5 and nothing at 3.
 *
 * Label parsing survives only as a fallback for an API older than the seat
 * positions — the SPA and the API are separate containers and can be out of
 * step during a rolling deploy. It is not speculative dead code, but it is the
 * lesser path: it can only guess where the aisle runs.
 *
 * Nothing here trusts the order seats arrive in.
 */

import type { SeatAvailabilityDto, VehicleLayoutDto } from '../types';

/** One position in a row: either a seat, or a spot with no seat on file. */
export type SeatCell =
  | { kind: 'seat'; column: number; seat: SeatAvailabilityDto }
  | { kind: 'blank'; column: number };

/** A row of the plan, already split either side of the aisle. */
export interface SeatPlanRow {
  row: number;
  /** Cells before the aisle. Never empty. */
  left: SeatCell[];
  /** Cells after the aisle. Empty when the vehicle has no aisle. */
  right: SeatCell[];
}

/** Why a vehicle could not be drawn as a plan. Copy lives in the component. */
export type SimpleLayoutReason =
  | 'no-seats'
  | 'positions-unavailable'
  | 'positions-collide';

export type SeatLayout =
  | {
      kind: 'plan';
      /** Rows front to back. Row 1 is the front of the vehicle. */
      rows: SeatPlanRow[];
      /** Seat-bearing columns, left to right. Excludes the aisle column. */
      columns: number[];
      hasAisle: boolean;
      /** True when positions came from the server rather than from labels. */
      fromServer: boolean;
    }
  | {
      kind: 'simple';
      /** Seats in the order they arrived; no spatial claim is made. */
      seats: SeatAvailabilityDto[];
      reason: SimpleLayoutReason;
    };

interface SeatPosition {
  row: number;
  column: number;
}

/** Row digits then a single column letter — "1A", "12C". Nothing else counts. */
const POSITIONAL_LABEL = /^(\d{1,2})([A-Z])$/;

const isPositiveInt = (value: unknown): value is number =>
  typeof value === 'number' && Number.isInteger(value) && value > 0;

/**
 * Positions straight off the seats, which is where the server puts them.
 * Returns null if any seat is missing a usable pair, so a partial plan can
 * never place some passengers and silently drop the rest.
 */
const positionsFromSeats = (
  seats: SeatAvailabilityDto[]
): Map<string, SeatPosition> | null => {
  const positions = new Map<string, SeatPosition>();

  for (const seat of seats) {
    if (!isPositiveInt(seat.row) || !isPositiveInt(seat.column)) return null;
    positions.set(seat.seatId, { row: seat.row, column: seat.column });
  }

  return positions;
};

/**
 * Fallback for an API that predates seat positions. Letters become 1-based
 * ordinals, so "A" is column 1. There is no aisle information in a label, so
 * the caller infers one.
 */
const positionsFromLabels = (
  seats: SeatAvailabilityDto[]
): Map<string, SeatPosition> | null => {
  const positions = new Map<string, SeatPosition>();

  for (const seat of seats) {
    const match = POSITIONAL_LABEL.exec(seat.seatNumber.trim());
    if (!match) return null;

    positions.set(seat.seatId, {
      row: Number(match[1]),
      column: match[2].charCodeAt(0) - 64,
    });
  }

  return positions;
};

/**
 * How many columns sit left of the aisle when nothing tells us.
 *
 * Four or more across is a 2 + rest coach; three across is the 2 + 1 minibus.
 * One or two across carries no inferable aisle at all — a pair of seats gives
 * no evidence about which side the gangway runs, so none is drawn.
 */
const inferLeftColumnCount = (columnCount: number): number =>
  columnCount >= 3 ? 2 : columnCount;

/**
 * Builds the seating plan for a vehicle.
 *
 * @param seats Availability exactly as the API returned it, in any order.
 * @param layout The vehicle's cabin shape. When present and consistent with the
 *   seats it wins, because it states where the aisle runs instead of guessing.
 */
export const buildSeatLayout = (
  seats: SeatAvailabilityDto[],
  layout?: VehicleLayoutDto | null
): SeatLayout => {
  if (seats.length === 0) {
    return { kind: 'simple', seats, reason: 'no-seats' };
  }

  const serverPositions = positionsFromSeats(seats);
  const positions = serverPositions ?? positionsFromLabels(seats);
  if (!positions) {
    return { kind: 'simple', seats, reason: 'positions-unavailable' };
  }

  // Two seats claiming one square means the data is inconsistent, and there is
  // no honest way to choose which passenger sits there.
  const occupied = new Set<string>();
  for (const { row, column } of positions.values()) {
    const key = `${row}|${column}`;
    if (occupied.has(key)) {
      return { kind: 'simple', seats, reason: 'positions-collide' };
    }
    occupied.add(key);
  }

  const usedColumns = [...new Set([...positions.values()].map((p) => p.column))].sort(
    (a, b) => a - b
  );
  const widest = usedColumns[usedColumns.length - 1];

  /*
   * Only honour the declared cabin shape if it can actually hold the seats we
   * were given. A layout narrower than the widest occupied column would clip
   * passengers off the right-hand side of the plan.
   */
  const declaredUsable =
    !!serverPositions &&
    !!layout &&
    isPositiveInt(layout.seatsPerRow) &&
    layout.seatsPerRow >= widest;

  let left: number[];
  let right: number[];

  if (declaredUsable && layout) {
    const aisle = layout.aisleAfterColumn;
    const allColumns = Array.from({ length: layout.seatsPerRow }, (_, i) => i + 1);

    if (isPositiveInt(aisle) && aisle < layout.seatsPerRow) {
      // The aisle occupies a column of its own, so it is skipped rather than
      // drawn as an empty seat: with aisleAfterColumn 2, column 3 IS the gangway.
      left = allColumns.filter((column) => column <= aisle);
      right = allColumns.filter((column) => column > aisle + 1);
    } else {
      left = allColumns;
      right = [];
    }
  } else {
    const splitAt = inferLeftColumnCount(usedColumns.length);
    left = usedColumns.slice(0, splitAt);
    right = usedColumns.slice(splitAt);
  }

  const seatAt = new Map<string, SeatAvailabilityDto>();
  for (const seat of seats) {
    const position = positions.get(seat.seatId) as SeatPosition;
    seatAt.set(`${position.row}|${position.column}`, seat);
  }

  const toCell = (row: number, column: number): SeatCell => {
    const seat = seatAt.get(`${row}|${column}`);
    return seat ? { kind: 'seat', column, seat } : { kind: 'blank', column };
  };

  const rowNumbers = [...new Set([...positions.values()].map((p) => p.row))].sort(
    (a, b) => a - b
  );

  const rows: SeatPlanRow[] = rowNumbers.map((row) => ({
    row,
    left: left.map((column) => toCell(row, column)),
    right: right.map((column) => toCell(row, column)),
  }));

  return {
    kind: 'plan',
    rows,
    columns: [...left, ...right],
    hasAisle: right.length > 0,
    fromServer: !!serverPositions,
  };
};
