import React, { useEffect, useRef, useState } from 'react';
import { BarChart3, Clock, History, Plus, RefreshCw, Send, Settings, UserPlus } from 'lucide-react';
import { api, isAbortError, toErrorMessage } from '../services/api';
import { telemetryService } from '../services/telemetry';
import { formatDepartureTime, formatFare, formatTimeAgo, shortRef, todayIso } from '../lib/format';
import { CreateScheduleLegDto, CreateSeatDto, RouteDto, ScheduleAnalyticsDto, ScheduleDto, TelemetryPayload, UserProfile, UserRole, VehicleDto } from '../types';
import { Card } from './ui/Card';
import { Button } from './ui/Button';
import { Badge } from './ui/Badge';
import { Field, controlStyles } from './ui/Field';
import { NotificationBanner, Notification } from './ui/NotificationBanner';

type TabKey = 'routes' | 'vehicles' | 'schedules' | 'telemetry' | 'operators';

interface OperatorTabProps {
  currentUser: UserProfile | null;
}

/** Splits a comma-separated field into trimmed, non-empty tokens. */
const parseCommaList = (value: string): string[] =>
  value
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);

type ParsedNumber = { value: number } | { error: string };

/** A leg fare must be a real, positive amount. Never invent a default. */
const parseFareNaira = (raw: string): ParsedNumber => {
  const trimmed = raw.trim();
  if (!trimmed) return { error: 'Enter a fare for this leg.' };
  const naira = Number(trimmed);
  if (!Number.isFinite(naira) || naira <= 0) return { error: 'Enter a fare greater than ₦0.' };
  return { value: naira };
};

const parseCoordinate = (raw: string, min: number, max: number, label: string): ParsedNumber => {
  const trimmed = raw.trim();
  if (!trimmed) return { error: `Enter a ${label}.` };
  const n = Number(trimmed);
  if (!Number.isFinite(n)) return { error: `${label[0].toUpperCase()}${label.slice(1)} must be a number.` };
  if (n < min || n > max) return { error: `${label[0].toUpperCase()}${label.slice(1)} must be between ${min} and ${max}.` };
  return { value: n };
};

const buildCreateSeatDtos = (
  seatLabels: string[],
  seatsPerRow: number,
  aisleAfterColumn: number | null
): CreateSeatDto[] => {
  const dtos: CreateSeatDto[] = [];
  const aisleCol = aisleAfterColumn && aisleAfterColumn > 0 ? aisleAfterColumn + 1 : null;

  const isPositional = seatLabels.length > 0 && seatLabels.every((label) => /^(\d+)([A-Z])$/i.test(label.trim()));

  if (isPositional) {
    for (const label of seatLabels) {
      const match = /^(\d+)([A-Z])$/i.exec(label.trim());
      if (match) {
        const row = parseInt(match[1], 10);
        const letterIdx = match[2].toUpperCase().charCodeAt(0) - 64;
        let col = letterIdx;
        if (aisleAfterColumn && letterIdx > aisleAfterColumn) {
          col = letterIdx + 1;
        }
        dtos.push({
          number: `${row}${match[2].toUpperCase()}`,
          row,
          column: col,
        });
      }
    }
  } else {
    let currRow = 1;
    let currCol = 1;
    for (const label of seatLabels) {
      if (aisleCol && currCol === aisleCol) {
        currCol++;
      }
      if (currCol > seatsPerRow) {
        currRow++;
        currCol = 1;
        if (aisleCol && currCol === aisleCol) {
          currCol++;
        }
      }
      dtos.push({
        number: label.trim(),
        row: currRow,
        column: currCol,
      });
      currCol++;
    }
  }

  return dtos;
};

export const OperatorTab: React.FC<OperatorTabProps> = ({ currentUser }) => {
  const isAdmin = Boolean(currentUser?.roles?.includes(UserRole.Admin));

  const visibleTabs: { key: TabKey; label: string }[] = [
    { key: 'routes', label: 'Routes' },
    { key: 'vehicles', label: 'Vehicles' },
    { key: 'schedules', label: 'Schedules' },
    { key: 'telemetry', label: 'Telemetry' },
    ...(isAdmin ? [{ key: 'operators' as TabKey, label: 'Onboard Operator' }] : []),
  ];
  const [routes, setRoutes] = useState<RouteDto[]>([]);
  const [vehicles, setVehicles] = useState<VehicleDto[]>([]);
  const [schedules, setSchedules] = useState<ScheduleDto[]>([]);
  const [isLoadingData, setIsLoadingData] = useState(true);
  const [reloadNonce, setReloadNonce] = useState(0);

  const [notification, setNotification] = useState<Notification | null>(null);
  const notify = (type: Notification['type'], message: string) => setNotification({ type, message });

  const [activeTab, setActiveTab] = useState<TabKey>('routes');
  const tabRefs = useRef<Partial<Record<TabKey, HTMLButtonElement | null>>>({});

  const focusTab = (key: TabKey) => {
    setActiveTab(key);
    tabRefs.current[key]?.focus();
  };

  const handleTabKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    let nextIndex: number | null = null;
    if (event.key === 'ArrowRight') nextIndex = (index + 1) % visibleTabs.length;
    else if (event.key === 'ArrowLeft') nextIndex = (index - 1 + visibleTabs.length) % visibleTabs.length;
    else if (event.key === 'Home') nextIndex = 0;
    else if (event.key === 'End') nextIndex = visibleTabs.length - 1;

    if (nextIndex !== null) {
      event.preventDefault();
      focusTab(visibleTabs[nextIndex].key);
    }
  };

  // Provision Operator Form State (Admin Only)
  const [opEmail, setOpEmail] = useState('');
  const [opPassword, setOpPassword] = useState('');
  const [opFullName, setOpFullName] = useState('');
  const [isProvisioningOp, setIsProvisioningOp] = useState(false);

  // Create Route Form State
  const [routeName, setRouteName] = useState('');
  const [routeStops, setRouteStops] = useState('');
  const [routeNameError, setRouteNameError] = useState<string | null>(null);
  const [routeStopsError, setRouteStopsError] = useState<string | null>(null);
  const [isCreatingRoute, setIsCreatingRoute] = useState(false);
  const parsedRouteStops = parseCommaList(routeStops);

  // Create Vehicle Form State
  const [vehicleName, setVehicleName] = useState('');
  const [vehicleRowCount, setVehicleRowCount] = useState('4');
  const [vehicleSeatsPerRow, setVehicleSeatsPerRow] = useState('5');
  const [vehicleAisleAfterCol, setVehicleAisleAfterCol] = useState('2');
  const [vehicleSeats, setVehicleSeats] = useState('');
  const [vehicleNameError, setVehicleNameError] = useState<string | null>(null);
  const [vehicleSeatsError, setVehicleSeatsError] = useState<string | null>(null);
  const [isCreatingVehicle, setIsCreatingVehicle] = useState(false);
  const parsedVehicleSeats = parseCommaList(vehicleSeats);

  // Create Schedule Form State
  const [schedRouteId, setSchedRouteId] = useState('');
  const [schedVehicleId, setSchedVehicleId] = useState('');
  const [schedDepartureTime, setSchedDepartureTime] = useState('08:00:00');
  const [legFaresInNaira, setLegFaresInNaira] = useState<Record<number, string>>({});
  const [scheduleRouteError, setScheduleRouteError] = useState<string | null>(null);
  const [scheduleVehicleError, setScheduleVehicleError] = useState<string | null>(null);
  const [legFareErrors, setLegFareErrors] = useState<Record<number, string>>({});
  const [isCreatingSchedule, setIsCreatingSchedule] = useState(false);

  // Telemetry Ping Simulator State
  const [simScheduleId, setSimScheduleId] = useState('');
  const [simLat, setSimLat] = useState('');
  const [simLng, setSimLng] = useState('');
  const [simLegIndex, setSimLegIndex] = useState('0');
  const [telemetryScheduleError, setTelemetryScheduleError] = useState<string | null>(null);
  const [telemetryLatError, setTelemetryLatError] = useState<string | null>(null);
  const [telemetryLngError, setTelemetryLngError] = useState<string | null>(null);
  const [telemetryLegIndexError, setTelemetryLegIndexError] = useState<string | null>(null);
  const [isSendingPing, setIsSendingPing] = useState(false);

  const selectedSchedRoute = routes.find((r) => r.id === schedRouteId) ?? null;
  const selectedSimSchedule = schedules.find((s) => s.id === simScheduleId) ?? null;
  const maxSimLegIndex = selectedSimSchedule ? Math.max(selectedSimSchedule.legs.length - 1, 0) : undefined;

  // Fleet Analytics & Telemetry History State
  const [analyticsTravelDate, setAnalyticsTravelDate] = useState(todayIso());
  const [scheduleAnalytics, setScheduleAnalytics] = useState<ScheduleAnalyticsDto | null>(null);
  const [telemetryHistory, setTelemetryHistory] = useState<TelemetryPayload[]>([]);
  const [isLoadingAnalytics, setIsLoadingAnalytics] = useState(false);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);

  useEffect(() => {
    if (!simScheduleId) return;
    const controller = new AbortController();

    const fetchTelemetryAndAnalytics = async () => {
      setIsLoadingAnalytics(true);
      setIsLoadingHistory(true);
      try {
        const [analytics, history] = await Promise.all([
          api.getScheduleAnalytics(simScheduleId, analyticsTravelDate, controller.signal).catch(() => null),
          api.getTelemetryHistory(simScheduleId, 50, controller.signal).catch(() => []),
        ]);
        if (controller.signal.aborted) return;
        setScheduleAnalytics(analytics);
        setTelemetryHistory(history);
      } finally {
        if (!controller.signal.aborted) {
          setIsLoadingAnalytics(false);
          setIsLoadingHistory(false);
        }
      }
    };

    void fetchTelemetryAndAnalytics();
    return () => controller.abort();
  }, [simScheduleId, analyticsTravelDate]);

  useEffect(() => {
    const controller = new AbortController();

    const load = async () => {
      setIsLoadingData(true);
      try {
        const [r, v, s] = await Promise.all([
          api.getAllRoutes(controller.signal),
          api.getAllVehicles(controller.signal),
          api.getAllSchedules(controller.signal),
        ]);
        if (controller.signal.aborted) return;

        setRoutes(r);
        setVehicles(v);
        setSchedules(s);

        // Default a selection only when nothing is chosen yet, so a reload
        // after creating a new record does not silently discard the
        // operator's current picks.
        setSchedRouteId((prev) => prev || r[0]?.id || '');
        setSchedVehicleId((prev) => prev || v[0]?.id || '');
        setSimScheduleId((prev) => prev || s[0]?.id || '');
      } catch (err) {
        // A superseded run (unmount, or another reload starting) aborts this
        // controller, which now genuinely cancels the in-flight requests
        // rather than just gating the state writes below.
        if (controller.signal.aborted || isAbortError(err)) return;
        notify('error', toErrorMessage(err, "We couldn't load the operator console. Reload to try again."));
      } finally {
        if (!controller.signal.aborted) setIsLoadingData(false);
      }
    };

    void load();
    // Reload button and every successful create bump reloadNonce, which
    // reruns this effect. React cleans up the previous run (aborting its
    // controller, cancelling its requests) before the new one starts, so
    // rapid clicks can't interleave out-of-order responses.
    return () => controller.abort();
     
  }, [reloadNonce]);

  const handleCreateRoute = async (e: React.FormEvent) => {
    e.preventDefault();

    const stops = parsedRouteStops;
    const nameError = routeName.trim() ? null : 'Enter a route name.';
    const stopsError = stops.length < 2 ? 'A route needs at least 2 ordered stops.' : null;
    setRouteNameError(nameError);
    setRouteStopsError(stopsError);
    if (nameError || stopsError) return;

    setIsCreatingRoute(true);
    try {
      const id = await api.createRoute({ name: routeName.trim(), stops });
      notify('success', `Route "${routeName.trim()}" created (ref ${shortRef(id)}). Switch to Schedules to configure it.`);
      setRouteName('');
      setRouteStops('');
      setSchedRouteId(id);
      setActiveTab('schedules');
      setReloadNonce((n) => n + 1);
    } catch (err) {
      notify('error', toErrorMessage(err, "We couldn't create that route. Try again."));
    } finally {
      setIsCreatingRoute(false);
    }
  };

  const handleCreateVehicle = async (e: React.FormEvent) => {
    e.preventDefault();

    const seats = parsedVehicleSeats;
    const nameError = vehicleName.trim() ? null : 'Enter a vehicle name.';
    const seatsError = seats.length === 0 ? 'A vehicle needs at least one seat.' : null;
    setVehicleNameError(nameError);
    setVehicleSeatsError(seatsError);
    if (nameError || seatsError) return;

    const parsedAisle = vehicleAisleAfterCol.trim() ? parseInt(vehicleAisleAfterCol.trim(), 10) : null;
    const aisleAfterColumn = parsedAisle && Number.isFinite(parsedAisle) && parsedAisle > 0 ? parsedAisle : null;

    let rowCountNum = parseInt(vehicleRowCount.trim(), 10) || 4;
    let seatsPerRowNum = parseInt(vehicleSeatsPerRow.trim(), 10) || 5;

    const dtos = buildCreateSeatDtos(seats, seatsPerRowNum, aisleAfterColumn);

    const maxRowInDtos = dtos.length > 0 ? Math.max(...dtos.map((d) => d.row)) : 1;
    const maxColInDtos = dtos.length > 0 ? Math.max(...dtos.map((d) => d.column)) : 1;

    if (rowCountNum < maxRowInDtos) rowCountNum = maxRowInDtos;
    if (seatsPerRowNum < maxColInDtos) seatsPerRowNum = maxColInDtos;

    const sanitizedAisle =
      aisleAfterColumn && aisleAfterColumn >= 1 && aisleAfterColumn < seatsPerRowNum ? aisleAfterColumn : null;

    setIsCreatingVehicle(true);
    try {
      const id = await api.createVehicle({
        name: vehicleName.trim(),
        rowCount: rowCountNum,
        seatsPerRow: seatsPerRowNum,
        aisleAfterColumn: sanitizedAisle,
        seats: dtos,
      });
      notify('success', `Vehicle "${vehicleName.trim()}" created (ref ${shortRef(id)}). Switch to Schedules to use it.`);
      setVehicleName('');
      setVehicleSeats('');
      setSchedVehicleId(id);
      setActiveTab('schedules');
      setReloadNonce((n) => n + 1);
    } catch (err) {
      notify('error', toErrorMessage(err, "We couldn't create that vehicle. Try again."));
    } finally {
      setIsCreatingVehicle(false);
    }
  };

  const handleCreateSchedule = async (e: React.FormEvent) => {
    e.preventDefault();

    const routeError = selectedSchedRoute ? null : 'Select a route.';
    const vehicleError = schedVehicleId ? null : 'Select a vehicle.';
    setScheduleRouteError(routeError);
    setScheduleVehicleError(vehicleError);
    if (routeError || vehicleError || !selectedSchedRoute) return;

    const numLegs = selectedSchedRoute.stops.length - 1;
    const nextLegFareErrors: Record<number, string> = {};
    const legs: CreateScheduleLegDto[] = [];

    for (let i = 0; i < numLegs; i++) {
      const parsed = parseFareNaira(legFaresInNaira[i] ?? '');
      if ('error' in parsed) {
        nextLegFareErrors[i] = parsed.error;
      } else {
        legs.push({ startStopIndex: i, endStopIndex: i + 1, fare: Math.round(parsed.value * 100) });
      }
    }

    setLegFareErrors(nextLegFareErrors);
    if (Object.keys(nextLegFareErrors).length > 0) {
      notify('error', 'Fix the highlighted leg fares before creating this schedule.');
      return;
    }

    setIsCreatingSchedule(true);
    try {
      const id = await api.createSchedule({
        routeId: schedRouteId,
        vehicleId: schedVehicleId,
        departureTimeOfDay: schedDepartureTime,
        legs,
      });
      notify('success', `Schedule created (ref ${shortRef(id)}).`);
      setLegFaresInNaira({});
      setLegFareErrors({});
      setReloadNonce((n) => n + 1);
    } catch (err) {
      notify('error', toErrorMessage(err, "We couldn't create that schedule. Try again."));
    } finally {
      setIsCreatingSchedule(false);
    }
  };

  const handleSendTelemetryPing = async (e: React.FormEvent) => {
    e.preventDefault();

    const scheduleError = simScheduleId ? null : 'Select a schedule.';
    const lat = parseCoordinate(simLat, -90, 90, 'latitude');
    const lng = parseCoordinate(simLng, -180, 180, 'longitude');

    const legIndexTrimmed = simLegIndex.trim();
    let legIndexError: string | null = null;
    if (!/^\d+$/.test(legIndexTrimmed)) {
      legIndexError = 'Leg index must be a whole number, 0 or more.';
    } else if (maxSimLegIndex !== undefined && Number(legIndexTrimmed) > maxSimLegIndex) {
      legIndexError = `This schedule only has legs 0–${maxSimLegIndex}.`;
    }

    setTelemetryScheduleError(scheduleError);
    setTelemetryLatError('error' in lat ? lat.error : null);
    setTelemetryLngError('error' in lng ? lng.error : null);
    setTelemetryLegIndexError(legIndexError);
    if (scheduleError || 'error' in lat || 'error' in lng || legIndexError) return;

    setIsSendingPing(true);
    try {
      await telemetryService.connect();
      await telemetryService.sendDriverLocation(
        simScheduleId,
        (lat as { value: number }).value,
        (lng as { value: number }).value,
        Number(legIndexTrimmed)
      );
      notify('success', `Telemetry ping broadcast for schedule ref ${shortRef(simScheduleId)}.`);
    } catch (err) {
      notify('error', toErrorMessage(err, "We couldn't broadcast that telemetry ping. Try again."));
    } finally {
      setIsSendingPing(false);
    }
  };

  const handleProvisionOperator = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isAdmin) return;

    if (!opEmail.trim() || !opPassword.trim() || !opFullName.trim()) {
      notify('error', 'Enter full name, email, and password to create an operator account.');
      return;
    }

    if (opPassword.length < 8) {
      notify('error', 'Password must be at least 8 characters long.');
      return;
    }

    setIsProvisioningOp(true);
    try {
      await api.provisionOperator({
        email: opEmail.trim(),
        password: opPassword,
        fullName: opFullName.trim(),
      });
      notify('success', `Operator account created for ${opEmail.trim()}.`);
      setOpEmail('');
      setOpPassword('');
      setOpFullName('');
    } catch (err) {
      notify('error', toErrorMessage(err, "We couldn't create that operator account. Check details and try again."));
    } finally {
      setIsProvisioningOp(false);
    }
  };

  return (
    <Card
      emphasis
      title={
        <span className="inline-flex items-center gap-2">
          <Settings className="w-5 h-5 text-signal" aria-hidden="true" />
          Operator console
        </span>
      }
      subtitle="Define routes, vehicles and schedules, and simulate driver telemetry."
      action={
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            disabled={isLoadingData}
            onClick={() => setReloadNonce((n) => n + 1)}
            icon={<RefreshCw className={`w-3.5 h-3.5 ${isLoadingData ? 'animate-spin' : ''}`} aria-hidden="true" />}
          >
            Reload
          </Button>

          <div role="tablist" aria-label="Operator console sections" className="flex gap-1 bg-surface-sunk border border-rule rounded-ticket p-1">
            {visibleTabs.map((tab, index) => {
              const selected = activeTab === tab.key;
              return (
                <button
                  key={tab.key}
                  ref={(el) => {
                    tabRefs.current[tab.key] = el;
                  }}
                  type="button"
                  role="tab"
                  id={`operator-tab-${tab.key}`}
                  aria-selected={selected}
                  aria-controls={`operator-panel-${tab.key}`}
                  tabIndex={selected ? 0 : -1}
                  onClick={() => setActiveTab(tab.key)}
                  onKeyDown={(event) => handleTabKeyDown(event, index)}
                  className={`px-3 py-1.5 rounded-ticket stencil transition-colors ${
                    selected ? 'bg-signal text-surface' : 'text-content-muted hover:text-content'
                  }`}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>
        </div>
      }
    >
      <div className="space-y-6" aria-busy={isLoadingData || undefined}>
        <NotificationBanner notification={notification} onDismiss={() => setNotification(null)} />

        {/* Routes Management */}
        <div
          role="tabpanel"
          id="operator-panel-routes"
          aria-labelledby="operator-tab-routes"
          tabIndex={0}
          hidden={activeTab !== 'routes'}
          className="space-y-6"
        >
          <form onSubmit={handleCreateRoute} className="stock p-4 space-y-4">
            <h3 className="text-sm font-bold text-content flex items-center gap-2">
              <Plus className="w-4 h-4 text-signal" aria-hidden="true" />
              Define a new route
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
              <Field label="Route name" error={routeNameError}>
                {(ids) => (
                  <input
                    {...ids}
                    type="text"
                    required
                    placeholder="e.g. Lagos–Abuja Express"
                    className={controlStyles}
                    value={routeName}
                    onChange={(e) => setRouteName(e.target.value)}
                  />
                )}
              </Field>
              <Field label="Ordered stops" hint="Comma-separated, in travel order." error={routeStopsError}>
                {(ids) => (
                  <>
                    <input
                      {...ids}
                      type="text"
                      required
                      placeholder="Lagos, Sagamu, Ibadan, Ilorin, Abuja"
                      className={controlStyles}
                      value={routeStops}
                      onChange={(e) => setRouteStops(e.target.value)}
                    />
                    {parsedRouteStops.length > 0 && (
                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        {parsedRouteStops.map((stop, i) => (
                          <React.Fragment key={`${stop}-${i}`}>
                            {i > 0 && (
                              <span aria-hidden="true" className="text-content-faint text-xs">
                                →
                              </span>
                            )}
                            <Badge variant="neutral">{stop}</Badge>
                          </React.Fragment>
                        ))}
                      </div>
                    )}
                  </>
                )}
              </Field>
            </div>
            <Button type="submit" size="sm" isLoading={isCreatingRoute} loadingLabel="Creating route">
              Create route
            </Button>
          </form>

          <div className="space-y-2">
            <h4 className="stencil text-content-muted">Configured routes</h4>
            {isLoadingData && routes.length === 0 ? (
              <p className="flex items-center gap-2 text-xs text-content-muted py-3">
                <span
                  aria-hidden="true"
                  className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0"
                />
                Loading routes…
              </p>
            ) : routes.length === 0 ? (
                <p className="text-xs text-content-muted py-3">No routes yet. Create one above.</p>
            ) : (
              <ul className="ruled">
                {routes.map((r) => (
                  <li key={r.id} className="py-3 flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
                    <div className="flex flex-col gap-0.5">
                      <span className="font-semibold text-content text-sm">{r.name}</span>
                      <span className="figures text-xs text-content-muted">{r.stops.join(' → ')}</span>
                    </div>
                    {!schedules.some((s) => s.routeId === r.id) && (
                      <Button 
                        type="button" 
                        variant="secondary" 
                        size="sm" 
                        className="shrink-0"
                        onClick={() => { setSchedRouteId(r.id); setActiveTab('schedules'); }}
                      >
                        Create schedule
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* Vehicles Management */}
        <div
          role="tabpanel"
          id="operator-panel-vehicles"
          aria-labelledby="operator-tab-vehicles"
          tabIndex={0}
          hidden={activeTab !== 'vehicles'}
          className="space-y-6"
        >
          <form onSubmit={handleCreateVehicle} className="stock p-4 space-y-4">
            <h3 className="text-sm font-bold text-content flex items-center gap-2">
              <Plus className="w-4 h-4 text-signal" aria-hidden="true" />
              Define a new vehicle
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
              <Field label="Vehicle name" error={vehicleNameError}>
                {(ids) => (
                  <input
                    {...ids}
                    type="text"
                    required
                    placeholder="e.g. Luxury Coach Alpha"
                    className={controlStyles}
                    value={vehicleName}
                    onChange={(e) => setVehicleName(e.target.value)}
                  />
                )}
              </Field>
              <Field label="Seat numbers" hint="Comma-separated." error={vehicleSeatsError}>
                {(ids) => (
                  <>
                    <input
                      {...ids}
                      type="text"
                      required
                      placeholder="1A, 1B, 2A, 2B, 3A, 3B"
                      className={controlStyles}
                      value={vehicleSeats}
                      onChange={(e) => setVehicleSeats(e.target.value)}
                    />
                    {parsedVehicleSeats.length > 0 && (
                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        {parsedVehicleSeats.map((seat, i) => (
                          <Badge key={`${seat}-${i}`} variant="neutral">
                            {seat}
                          </Badge>
                        ))}
                      </div>
                    )}
                  </>
                )}
              </Field>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 items-start">
              {/*
                Labels here are kept short and of similar length on purpose.
                They sit above their control, so a label long enough to wrap
                pushes its own input down and breaks alignment with the
                columns either side of it. Detail belongs in the hint, which
                renders below the control and cannot affect alignment.
              */}
              <Field label="Rows">
                {(ids) => (
                  <input
                    {...ids}
                    type="number"
                    min={1}
                    className={controlStyles}
                    value={vehicleRowCount}
                    onChange={(e) => setVehicleRowCount(e.target.value)}
                  />
                )}
              </Field>
              <Field label="Row width" hint="Counts the aisle as a column.">
                {(ids) => (
                  <input
                    {...ids}
                    type="number"
                    min={1}
                    className={controlStyles}
                    value={vehicleSeatsPerRow}
                    onChange={(e) => setVehicleSeatsPerRow(e.target.value)}
                  />
                )}
              </Field>
              <Field label="Aisle after" hint="Optional. 2 gives a 2+2 layout.">
                {(ids) => (
                  <input
                    {...ids}
                    type="number"
                    min={1}
                    placeholder="None"
                    className={controlStyles}
                    value={vehicleAisleAfterCol}
                    onChange={(e) => setVehicleAisleAfterCol(e.target.value)}
                  />
                )}
              </Field>
            </div>
            <Button type="submit" size="sm" isLoading={isCreatingVehicle} loadingLabel="Creating vehicle">
              Create vehicle
            </Button>
          </form>

          <div className="space-y-2">
            <h4 className="stencil text-content-muted">Configured vehicles</h4>
            {isLoadingData && vehicles.length === 0 ? (
              <p className="flex items-center gap-2 text-xs text-content-muted py-3">
                <span
                  aria-hidden="true"
                  className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0"
                />
                Loading vehicles…
              </p>
            ) : vehicles.length === 0 ? (
                <p className="text-xs text-content-muted py-3">No vehicles yet. Create one above.</p>
            ) : (
              <ul className="ruled">
                {vehicles.map((v) => (
                  <li key={v.id} className="py-3 flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
                    <div className="flex flex-col gap-0.5">
                      <span className="font-semibold text-content text-sm">{v.name}</span>
                      <span className="figures text-xs text-content-muted">
                        {v.seats.length} seat{v.seats.length === 1 ? '' : 's'}: {v.seats.join(', ')}
                      </span>
                    </div>
                    {!schedules.some((s) => s.vehicleId === v.id) && (
                      <Button 
                        type="button" 
                        variant="secondary" 
                        size="sm" 
                        className="shrink-0"
                        onClick={() => { setSchedVehicleId(v.id); setActiveTab('schedules'); }}
                      >
                        Create schedule
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* Schedules Management */}
        <div
          role="tabpanel"
          id="operator-panel-schedules"
          aria-labelledby="operator-tab-schedules"
          tabIndex={0}
          hidden={activeTab !== 'schedules'}
          className="space-y-6"
        >
          {routes.length === 0 || vehicles.length === 0 ? (
            <div className="stock p-6 text-center space-y-4">
              {isLoadingData ? (
                <p className="flex items-center justify-center gap-2 text-sm text-content-muted">
                  <span aria-hidden="true" className="w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0" />
                  Loading prerequisites…
                </p>
              ) : (
                <>
                  <p className="text-sm text-content-muted">
                    You need at least one route and one vehicle before you can create a schedule.
                  </p>
                  <div className="flex flex-wrap items-center justify-center gap-4">
                    {routes.length === 0 && (
                      <Button type="button" onClick={() => setActiveTab('routes')}>Create a route</Button>
                    )}
                    {vehicles.length === 0 && (
                      <Button type="button" onClick={() => setActiveTab('vehicles')}>Create a vehicle</Button>
                    )}
                  </div>
                </>
              )}
            </div>
          ) : (
          <form onSubmit={handleCreateSchedule} className="stock p-4 space-y-4">
            <h3 className="text-sm font-bold text-content flex items-center gap-2">
              <Plus className="w-4 h-4 text-signal" aria-hidden="true" />
              Define a new schedule
            </h3>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-start">
              <Field label="Route" error={scheduleRouteError}>
                {(ids) => (
                  <select
                    {...ids}
                    className={controlStyles}
                    value={schedRouteId}
                    onChange={(e) => setSchedRouteId(e.target.value)}
                  >
                    <option value="" disabled>
                      Select a route…
                    </option>
                    {routes.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.name}
                      </option>
                    ))}
                  </select>
                )}
              </Field>

              <Field label="Vehicle" error={scheduleVehicleError}>
                {(ids) => (
                  <select
                    {...ids}
                    className={controlStyles}
                    value={schedVehicleId}
                    onChange={(e) => setSchedVehicleId(e.target.value)}
                  >
                    <option value="" disabled>
                      Select a vehicle…
                    </option>
                    {vehicles.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.name} ({v.seats.length} seat{v.seats.length === 1 ? '' : 's'})
                      </option>
                    ))}
                  </select>
                )}
              </Field>

              <Field label="Departure time">
                {(ids) => (
                  <input
                    {...ids}
                    type="time"
                    step="1"
                    className={`${controlStyles} figures`}
                    value={schedDepartureTime}
                    onChange={(e) => setSchedDepartureTime(e.target.value)}
                  />
                )}
              </Field>
            </div>

            {/* Dynamic Leg Fares */}
            {selectedSchedRoute && selectedSchedRoute.stops.length >= 2 && (
              <fieldset className="space-y-3 pt-3 border-t border-rule">
                <legend className="stencil text-content-muted mb-1">Leg fares</legend>
                <p className="text-xs text-content-faint -mt-1 mb-2">
                  Enter fares in naira. They&rsquo;re stored as kobo.
                </p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
                  {selectedSchedRoute.stops.slice(0, -1).map((stop, index) => {
                    const nextStop = selectedSchedRoute.stops[index + 1];

                    /*
                     * The stop pair lives in the hint, not the label. As a label
                     * it read `Leg 0: Lagos → Sagamu`, whose length depends on
                     * the route's stop names — so one column's label wrapped to
                     * two lines while its neighbour's did not, pushing the two
                     * fare inputs to different heights. A short, fixed-width
                     * label keeps every row aligned whatever the stops are called.
                     */
                    return (
                      <Field
                        key={index}
                        label={`Leg ${index}`}
                        hint={`${stop} → ${nextStop}`}
                        error={legFareErrors[index] ?? null}
                      >
                        {(ids) => (
                          <input
                            {...ids}
                            type="number"
                            min="0.01"
                            step="0.01"
                            inputMode="decimal"
                            placeholder="e.g. 1000"
                            className={`${controlStyles} figures`}
                            value={legFaresInNaira[index] ?? ''}
                            onChange={(e) =>
                              setLegFaresInNaira({
                                ...legFaresInNaira,
                                [index]: e.target.value,
                              })
                            }
                          />
                        )}
                      </Field>
                    );
                  })}
                </div>
              </fieldset>
            )}

            <Button
              type="submit"
              size="sm"
              icon={<Clock className="w-3.5 h-3.5" aria-hidden="true" />}
              isLoading={isCreatingSchedule}
              loadingLabel="Creating schedule"
            >
              Create schedule
            </Button>
          </form>
          )}

          <div className="space-y-2">
            <h4 className="stencil text-content-muted">Configured schedules</h4>
            {isLoadingData && schedules.length === 0 ? (
              <p className="flex items-center gap-2 text-xs text-content-muted py-3">
                <span
                  aria-hidden="true"
                  className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0"
                />
                Loading schedules…
              </p>
            ) : schedules.length === 0 ? (
                <p className="text-xs text-content-muted py-3">No schedules yet. Create one above.</p>
            ) : (
              <ul className="ruled">
                {schedules.map((s) => {
                  const fares = s.legs.map((l) => l.fare);
                  const minFare = fares.length ? Math.min(...fares) : null;
                  const maxFare = fares.length ? Math.max(...fares) : null;
                  return (
                    <li key={s.id} className="py-3 space-y-1.5">
                      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                        <span className="board text-base text-content">{formatDepartureTime(s.departureTimeOfDay)}</span>
                        <span className="stencil text-content-faint">Ref {shortRef(s.id)}</span>
                      </div>
                      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-content-muted figures">
                        <span>Route {shortRef(s.routeId)}</span>
                        <span>Vehicle {shortRef(s.vehicleId)}</span>
                        <span>
                          {s.legs.length} leg{s.legs.length === 1 ? '' : 's'}
                        </span>
                        {minFare !== null && maxFare !== null && (
                          <span>{minFare === maxFare ? formatFare(minFare) : `${formatFare(minFare)}–${formatFare(maxFare)}`}</span>
                        )}
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        </div>

        {/* Telemetry Simulator */}
        <div
          role="tabpanel"
          id="operator-panel-telemetry"
          aria-labelledby="operator-tab-telemetry"
          tabIndex={0}
          hidden={activeTab !== 'telemetry'}
        >
          <form onSubmit={handleSendTelemetryPing} className="stock p-4 space-y-4">
            <h3 className="text-sm font-bold text-content flex items-center gap-2">
              <Send className="w-4 h-4 text-signal" aria-hidden="true" />
              Simulate a driver GPS ping
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
              <Field label="Schedule" error={telemetryScheduleError}>
                {(ids) => (
                  <select
                    {...ids}
                    className={controlStyles}
                    value={simScheduleId}
                    onChange={(e) => setSimScheduleId(e.target.value)}
                  >
                    <option value="" disabled>
                      Select a schedule…
                    </option>
                    {schedules.map((s) => (
                      <option key={s.id} value={s.id}>
                        {formatDepartureTime(s.departureTimeOfDay)} · Ref {shortRef(s.id)}
                      </option>
                    ))}
                  </select>
                )}
              </Field>
              <Field
                label="Current leg index"
                hint={
                  maxSimLegIndex !== undefined
                    ? `Which leg the vehicle is on now (0–${maxSimLegIndex}).`
                    : 'Which leg of the route the vehicle is currently on.'
                }
                error={telemetryLegIndexError}
              >
                {(ids) => (
                  <input
                    {...ids}
                    type="number"
                    min={0}
                    max={maxSimLegIndex}
                    inputMode="numeric"
                    className={`${controlStyles} figures`}
                    value={simLegIndex}
                    onChange={(e) => setSimLegIndex(e.target.value)}
                  />
                )}
              </Field>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">
              <Field label="Latitude" hint="-90 to 90." error={telemetryLatError}>
                {(ids) => (
                  <input
                    {...ids}
                    type="text"
                    inputMode="decimal"
                    placeholder="e.g. 6.5244"
                    className={`${controlStyles} figures`}
                    value={simLat}
                    onChange={(e) => setSimLat(e.target.value)}
                  />
                )}
              </Field>
              <Field label="Longitude" hint="-180 to 180." error={telemetryLngError}>
                {(ids) => (
                  <input
                    {...ids}
                    type="text"
                    inputMode="decimal"
                    placeholder="e.g. 3.3792"
                    className={`${controlStyles} figures`}
                    value={simLng}
                    onChange={(e) => setSimLng(e.target.value)}
                  />
                )}
              </Field>
            </div>
            <Button
              type="submit"
              size="sm"
              icon={<Send className="w-3.5 h-3.5" aria-hidden="true" />}
              isLoading={isSendingPing}
              loadingLabel="Broadcasting the GPS ping"
            >
              Broadcast GPS ping
            </Button>
          </form>

          {/* Schedule Occupancy & Revenue Analytics */}
          <div className="stock p-4 space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <h3 className="text-sm font-bold text-content flex items-center gap-2">
                <BarChart3 className="w-4 h-4 text-signal" aria-hidden="true" />
                Departure Occupancy & Revenue Analytics
              </h3>
              <div className="w-44">
                <Field label="Departure date">
                  {(ids) => (
                    <input
                      {...ids}
                      type="date"
                      className={`${controlStyles} figures py-1`}
                      value={analyticsTravelDate}
                      onChange={(e) => setAnalyticsTravelDate(e.target.value)}
                    />
                  )}
                </Field>
              </div>
            </div>

            {isLoadingAnalytics ? (
              <p className="flex items-center gap-2 text-xs text-content-muted py-2">
                <span className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0" aria-hidden="true" />
                Loading departure analytics…
              </p>
            ) : scheduleAnalytics ? (
              <div className="space-y-4">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div className="bg-surface-sunk border border-rule rounded-ticket p-3">
                    <span className="stencil text-content-faint">Confirmed Revenue</span>
                    <p className="figures text-lg font-bold text-signal mt-0.5">
                      {formatFare(scheduleAnalytics.totalRevenueKobo)}
                    </p>
                  </div>
                  <div className="bg-surface-sunk border border-rule rounded-ticket p-3">
                    <span className="stencil text-content-faint">Confirmed Passengers</span>
                    <p className="figures text-lg font-bold text-content mt-0.5">
                      {scheduleAnalytics.totalConfirmedBookings} / {scheduleAnalytics.totalCapacity}
                    </p>
                  </div>
                  <div className="bg-surface-sunk border border-rule rounded-ticket p-3">
                    <span className="stencil text-content-faint">Capacity Load</span>
                    <p className="figures text-lg font-bold text-stamp mt-0.5">
                      {scheduleAnalytics.totalCapacity > 0
                        ? Math.round((scheduleAnalytics.totalConfirmedBookings / scheduleAnalytics.totalCapacity) * 100)
                        : 0}%
                    </p>
                  </div>
                </div>

                <div className="space-y-2">
                  <h4 className="stencil text-content-muted">Leg-by-Leg Occupancy</h4>
                  <div className="space-y-2">
                    {scheduleAnalytics.legOccupancies.map((leg) => (
                      <div key={leg.legIndex} className="bg-surface-sunk border border-rule rounded-ticket p-3 space-y-1.5">
                        <div className="flex justify-between items-baseline text-xs">
                          <span className="font-semibold text-content">
                            Leg {leg.legIndex}: {leg.startStopName} → {leg.endStopName}
                          </span>
                          <span className="figures text-content-muted">
                            {leg.occupiedSeats} / {leg.totalSeats} seats ({leg.occupancyPercentage}%)
                          </span>
                        </div>
                        <div className="w-full bg-surface border border-rule rounded-full h-2 overflow-hidden">
                          <div
                            className="bg-signal h-full transition-all duration-300"
                            style={{ width: `${Math.min(100, Math.max(0, leg.occupancyPercentage))}%` }}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            ) : (
              <p className="text-xs text-content-muted py-2">Select a schedule above to view departure analytics.</p>
            )}
          </div>

          {/* Historical Telemetry Pings (Breadcrumb Trail) */}
          <div className="stock p-4 space-y-4">
            <h3 className="text-sm font-bold text-content flex items-center gap-2">
              <History className="w-4 h-4 text-signal" aria-hidden="true" />
              Historical GPS Breadcrumb Logs
            </h3>

            {isLoadingHistory ? (
              <p className="flex items-center gap-2 text-xs text-content-muted py-2">
                <span className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0" aria-hidden="true" />
                Loading breadcrumb logs…
              </p>
            ) : telemetryHistory.length === 0 ? (
              <p className="text-xs text-content-muted py-2">No historical pings recorded for this schedule yet.</p>
            ) : (
              <ul className="ruled max-h-60 overflow-y-auto">
                {telemetryHistory.map((ping, idx) => (
                  <li key={idx} className="py-2 flex flex-wrap items-center justify-between text-xs text-content-muted figures">
                    <div>
                      <span className="font-semibold text-content mr-2">Leg {ping.currentLegIndex}</span>
                      <span>Lat {ping.latitude.toFixed(4)}, Lng {ping.longitude.toFixed(4)}</span>
                    </div>
                    <span className="stencil text-content-faint">{formatTimeAgo(ping.timestamp)}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* Onboard Operator Management (Admin Only) */}
        {isAdmin && (
          <div
            role="tabpanel"
            id="operator-panel-operators"
            aria-labelledby="operator-tab-operators"
            tabIndex={0}
            hidden={activeTab !== 'operators'}
          >
            <form onSubmit={handleProvisionOperator} className="stock p-4 space-y-4">
              <h3 className="text-sm font-bold text-content flex items-center gap-2">
                <UserPlus className="w-4 h-4 text-signal" aria-hidden="true" />
                Onboard a new operator user
              </h3>
              <p className="text-xs text-content-muted">
                Admin privilege required. Provisioned operators gain access to manage routes, vehicles, and schedules.
              </p>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-start">
                <Field label="Full name">
                  {(ids) => (
                    <input
                      {...ids}
                      type="text"
                      required
                      placeholder="e.g. Samuel Okafor"
                      className={controlStyles}
                      value={opFullName}
                      onChange={(e) => setOpFullName(e.target.value)}
                    />
                  )}
                </Field>
                <Field label="Email address">
                  {(ids) => (
                    <input
                      {...ids}
                      type="email"
                      required
                      placeholder="operator@bitroute.com"
                      className={controlStyles}
                      value={opEmail}
                      onChange={(e) => setOpEmail(e.target.value)}
                    />
                  )}
                </Field>
                <Field label="Initial password" hint="At least 8 characters.">
                  {(ids) => (
                    <input
                      {...ids}
                      type="password"
                      required
                      placeholder="••••••••"
                      className={controlStyles}
                      value={opPassword}
                      onChange={(e) => setOpPassword(e.target.value)}
                    />
                  )}
                </Field>
              </div>
              <Button
                type="submit"
                size="sm"
                icon={<UserPlus className="w-3.5 h-3.5" aria-hidden="true" />}
                isLoading={isProvisioningOp}
                loadingLabel="Provisioning operator account"
              >
                Onboard operator
              </Button>
            </form>
          </div>
        )}
      </div>
    </Card>
  );
};
