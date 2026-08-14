import React, { useEffect, useRef, useState } from 'react';
import { Clock, Plus, RefreshCw, Send, Settings } from 'lucide-react';
import { api, isAbortError, toErrorMessage } from '../services/api';
import { telemetryService } from '../services/telemetry';
import { formatDepartureTime, formatFare, shortRef } from '../lib/format';
import { CreateScheduleLegDto, RouteDto, ScheduleDto, VehicleDto } from '../types';
import { Card } from './ui/Card';
import { Button } from './ui/Button';
import { Badge } from './ui/Badge';
import { Field, controlStyles } from './ui/Field';
import { NotificationBanner, Notification } from './ui/NotificationBanner';

type TabKey = 'routes' | 'vehicles' | 'schedules' | 'telemetry';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'routes', label: 'Routes' },
  { key: 'vehicles', label: 'Vehicles' },
  { key: 'schedules', label: 'Schedules' },
  { key: 'telemetry', label: 'Telemetry' },
];

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

export const OperatorTab: React.FC = () => {
  const [routes, setRoutes] = useState<RouteDto[]>([]);
  const [vehicles, setVehicles] = useState<VehicleDto[]>([]);
  const [schedules, setSchedules] = useState<ScheduleDto[]>([]);
  const [isLoadingData, setIsLoadingData] = useState(true);
  const [reloadNonce, setReloadNonce] = useState(0);

  const [notification, setNotification] = useState<Notification | null>(null);
  const notify = (type: Notification['type'], message: string) => setNotification({ type, message });

  const [activeTab, setActiveTab] = useState<TabKey>('routes');
  const tabRefs = useRef<Record<TabKey, HTMLButtonElement | null>>({
    routes: null,
    vehicles: null,
    schedules: null,
    telemetry: null,
  });

  const focusTab = (key: TabKey) => {
    setActiveTab(key);
    tabRefs.current[key]?.focus();
  };

  const handleTabKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    let nextIndex: number | null = null;
    if (event.key === 'ArrowRight') nextIndex = (index + 1) % TABS.length;
    else if (event.key === 'ArrowLeft') nextIndex = (index - 1 + TABS.length) % TABS.length;
    else if (event.key === 'Home') nextIndex = 0;
    else if (event.key === 'End') nextIndex = TABS.length - 1;

    if (nextIndex !== null) {
      event.preventDefault();
      focusTab(TABS[nextIndex].key);
    }
  };

  // Create Route Form State
  const [routeName, setRouteName] = useState('');
  const [routeStops, setRouteStops] = useState('');
  const [routeNameError, setRouteNameError] = useState<string | null>(null);
  const [routeStopsError, setRouteStopsError] = useState<string | null>(null);
  const [isCreatingRoute, setIsCreatingRoute] = useState(false);
  const parsedRouteStops = parseCommaList(routeStops);

  // Create Vehicle Form State
  const [vehicleName, setVehicleName] = useState('');
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
      notify('success', `Route "${routeName.trim()}" created (ref ${shortRef(id)}).`);
      setRouteName('');
      setRouteStops('');
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

    setIsCreatingVehicle(true);
    try {
      const id = await api.createVehicle({ name: vehicleName.trim(), seats });
      notify('success', `Vehicle "${vehicleName.trim()}" created (ref ${shortRef(id)}).`);
      setVehicleName('');
      setVehicleSeats('');
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

          <div role="tablist" aria-label="Operator console sections" className="flex gap-1 bg-paper-sunk border border-rule rounded-ticket p-1">
            {TABS.map((tab, index) => {
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
                    selected ? 'bg-signal text-paper-raised' : 'text-ink-muted hover:text-ink'
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
            <h3 className="text-sm font-bold text-ink flex items-center gap-2">
              <Plus className="w-4 h-4 text-signal" aria-hidden="true" />
              Define a new route
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
                              <span aria-hidden="true" className="text-ink-faint text-xs">
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
            <h4 className="stencil text-ink-muted">Configured routes</h4>
            {isLoadingData && routes.length === 0 ? (
              <p className="flex items-center gap-2 text-xs text-ink-muted py-3">
                <span
                  aria-hidden="true"
                  className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0"
                />
                Loading routes…
              </p>
            ) : routes.length === 0 ? (
              <p className="text-xs text-ink-muted py-3">No routes yet. Create one above.</p>
            ) : (
              <ul className="ruled">
                {routes.map((r) => (
                  <li key={r.id} className="py-3 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                    <span className="font-semibold text-ink text-sm">{r.name}</span>
                    <span className="figures text-xs text-ink-muted">{r.stops.join(' → ')}</span>
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
            <h3 className="text-sm font-bold text-ink flex items-center gap-2">
              <Plus className="w-4 h-4 text-signal" aria-hidden="true" />
              Define a new vehicle
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
            <Button type="submit" size="sm" isLoading={isCreatingVehicle} loadingLabel="Creating vehicle">
              Create vehicle
            </Button>
          </form>

          <div className="space-y-2">
            <h4 className="stencil text-ink-muted">Configured vehicles</h4>
            {isLoadingData && vehicles.length === 0 ? (
              <p className="flex items-center gap-2 text-xs text-ink-muted py-3">
                <span
                  aria-hidden="true"
                  className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0"
                />
                Loading vehicles…
              </p>
            ) : vehicles.length === 0 ? (
              <p className="text-xs text-ink-muted py-3">No vehicles yet. Create one above.</p>
            ) : (
              <ul className="ruled">
                {vehicles.map((v) => (
                  <li key={v.id} className="py-3 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                    <span className="font-semibold text-ink text-sm">{v.name}</span>
                    <span className="figures text-xs text-ink-muted">
                      {v.seats.length} seat{v.seats.length === 1 ? '' : 's'}: {v.seats.join(', ')}
                    </span>
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
          <form onSubmit={handleCreateSchedule} className="stock p-4 space-y-4">
            <h3 className="text-sm font-bold text-ink flex items-center gap-2">
              <Plus className="w-4 h-4 text-signal" aria-hidden="true" />
              Define a new schedule
            </h3>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
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
                <legend className="stencil text-ink-muted mb-1">Leg fares</legend>
                <p className="text-xs text-ink-faint -mt-1 mb-2">
                  Enter fares in naira. They&rsquo;re stored as kobo.
                </p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {selectedSchedRoute.stops.slice(0, -1).map((stop, index) => {
                    const nextStop = selectedSchedRoute.stops[index + 1];
                    return (
                      <Field key={index} label={`Leg ${index}: ${stop} → ${nextStop}`} error={legFareErrors[index] ?? null}>
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

          <div className="space-y-2">
            <h4 className="stencil text-ink-muted">Configured schedules</h4>
            {isLoadingData && schedules.length === 0 ? (
              <p className="flex items-center gap-2 text-xs text-ink-muted py-3">
                <span
                  aria-hidden="true"
                  className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin shrink-0"
                />
                Loading schedules…
              </p>
            ) : schedules.length === 0 ? (
              <p className="text-xs text-ink-muted py-3">No schedules yet. Create one above.</p>
            ) : (
              <ul className="ruled">
                {schedules.map((s) => {
                  const fares = s.legs.map((l) => l.fare);
                  const minFare = fares.length ? Math.min(...fares) : null;
                  const maxFare = fares.length ? Math.max(...fares) : null;
                  return (
                    <li key={s.id} className="py-3 space-y-1.5">
                      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                        <span className="board text-base text-ink">{formatDepartureTime(s.departureTimeOfDay)}</span>
                        <span className="stencil text-ink-faint">Ref {shortRef(s.id)}</span>
                      </div>
                      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-muted figures">
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
            <h3 className="text-sm font-bold text-ink flex items-center gap-2">
              <Send className="w-4 h-4 text-signal" aria-hidden="true" />
              Simulate a driver GPS ping
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
        </div>
      </div>
    </Card>
  );
};
