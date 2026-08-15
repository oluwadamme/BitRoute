import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Header } from './components/Header';
import { DepartureSearch } from './components/DepartureSearch';
import { SeatMap } from './components/SeatMap';
import { CheckoutModal } from './components/CheckoutModal';
import { TelemetryRadar } from './components/TelemetryRadar';
import { AuthModal } from './components/AuthModal';
import { MyBookingsTab } from './components/MyBookingsTab';
import { OperatorTab } from './components/OperatorTab';
import { Card } from './components/ui/Card';
import { Button } from './components/ui/Button';
import { IconButton } from './components/ui/IconButton';
import { NotificationBanner, Notification } from './components/ui/NotificationBanner';
import { api, isAbortError, toErrorMessage } from './services/api';
import {
  BookingDto,
  RouteDto,
  ScheduleDto,
  SeatAvailabilityDto,
  UserProfile,
  UserRole,
  VehicleLayoutDto,
} from './types';
import { formatDepartureTime, formatFare, todayIso } from './lib/format';
import { RefreshCw } from 'lucide-react';

type Tab = 'search' | 'my-bookings' | 'operator';

const getTabFromPath = (pathname: string): Tab => {
  const cleanPath = pathname.toLowerCase().replace(/\/+$/, '');
  if (cleanPath === '/bookings') return 'my-bookings';
  if (cleanPath === '/operator') return 'operator';
  return 'search';
};

const getAuthModeFromPath = (pathname: string): 'login' | 'register' | null => {
  const cleanPath = pathname.toLowerCase().replace(/\/+$/, '');
  if (cleanPath === '/login') return 'login';
  if (cleanPath === '/register') return 'register';
  return null;
};

const getPathForTab = (tab: Tab): string => {
  if (tab === 'my-bookings') return '/bookings';
  if (tab === 'operator') return '/operator';
  return '/search';
};

/** Identifies one availability query. Used to key the idempotency token below. */
const selectionKey = (
  scheduleId: string,
  travelDate: string,
  seatId: string,
  boardingIndex: number,
  alightingIndex: number
) => `${scheduleId}|${travelDate}|${seatId}|${boardingIndex}|${alightingIndex}`;

export const App: React.FC = () => {
  const initialPath = window.location.pathname;
  const initialAuthMode = getAuthModeFromPath(initialPath);

  const [activeTab, setActiveTabState] = useState<Tab>(() => getTabFromPath(initialPath));
  const [isAuthModalOpen, setIsAuthModalOpen] = useState(initialAuthMode !== null);
  const [authMode, setAuthMode] = useState<'login' | 'register'>(initialAuthMode || 'login');
  const [currentUser, setCurrentUser] = useState<UserProfile | null>(null);
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [isAuthChecking, setIsAuthChecking] = useState(true);

  const navigateTab = useCallback((tab: Tab) => {
    setActiveTabState(tab);
    const targetPath = getPathForTab(tab);
    if (window.location.pathname !== targetPath && window.location.pathname !== `/${authMode}`) {
      window.history.pushState(null, '', targetPath);
    }
  }, [authMode]);

  const openAuthModal = useCallback((mode: 'login' | 'register' = 'login') => {
    setAuthMode(mode);
    setIsAuthModalOpen(true);
    const targetPath = `/${mode}`;
    if (window.location.pathname !== targetPath) {
      window.history.pushState(null, '', targetPath);
    }
  }, []);

  const closeAuthModal = useCallback(() => {
    if (!currentUser) {
      openAuthModal('login');
      return;
    }
    setIsAuthModalOpen(false);
    const targetPath = getPathForTab(activeTab);
    if (window.location.pathname !== targetPath) {
      window.history.pushState(null, '', targetPath);
    }
  }, [activeTab, currentUser, openAuthModal]);

  useEffect(() => {
    const handlePopState = () => {
      const path = window.location.pathname;
      const mode = getAuthModeFromPath(path);
      if (mode) {
        setAuthMode(mode);
        setIsAuthModalOpen(true);
      } else {
        setIsAuthModalOpen(false);
        const tab = getTabFromPath(path);
        setActiveTabState(tab);
      }
    };

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  useEffect(() => {
    const urlParams = new URLSearchParams(window.location.search);
    const reference = urlParams.get('reference');
    const status = urlParams.get('status');

    if (reference && (status === 'success' || window.location.pathname === '/payment/callback')) {
      const handlePaymentReturn = async () => {
        try {
          await api.confirmBooking(reference);
          setNotification({
            message: 'Payment confirmed! Your seat booking is finalized.',
            type: 'success',
          });
          window.history.replaceState(null, '', '/bookings');
          setActiveTabState('my-bookings');
          setActiveBooking(null);
        } catch (err) {
          setNotification({
            message: toErrorMessage(err, 'Failed to confirm payment.'),
            type: 'error',
          });
        }
      };

      void handlePaymentReturn();
    }
  }, []);

  useEffect(() => {
    if (!isAuthChecking && currentUser === null) {
      openAuthModal(authMode || 'login');
    }
  }, [isAuthChecking, currentUser, authMode, openAuthModal]);

  const isOperatorUser = Boolean(
    currentUser?.roles?.some((r) => r === UserRole.Admin || r === UserRole.Operator)
  );

  useEffect(() => {
    if (activeTab === 'operator' && currentUser !== null && !isOperatorUser) {
      navigateTab('search');
    }
  }, [activeTab, currentUser, isOperatorUser, navigateTab]);

  const [routes, setRoutes] = useState<RouteDto[]>([]);
  const [selectedRoute, setSelectedRoute] = useState<RouteDto | null>(null);
  const [schedules, setSchedules] = useState<ScheduleDto[]>([]);
  const [selectedSchedule, setSelectedSchedule] = useState<ScheduleDto | null>(null);

  const [travelDate, setTravelDate] = useState<string>(() => {
    const tomorrow = new Date(Date.now() + 86_400_000);
    const local = new Date(tomorrow.getTime() - tomorrow.getTimezoneOffset() * 60_000);
    return local.toISOString().split('T')[0];
  });

  const [boardingIndex, setBoardingIndex] = useState(0);
  const [alightingIndex, setAlightingIndex] = useState(1);

  const [seats, setSeats] = useState<SeatAvailabilityDto[]>([]);

  /**
   * The vehicle's cabin shape, reported alongside availability. Null until a
   * departure has been quoted, and cleared whenever the seats are, so the plan
   * can never be drawn against a shape from a different vehicle.
   */
  const [seatLayout, setSeatLayout] = useState<VehicleLayoutDto | null>(null);
  const [selectedSeatId, setSelectedSeatId] = useState<string | null>(null);

  /**
   * Null until the server has quoted a fare. The previous build seeded this
   * with a hardcoded 220000 and rendered "₦2,200.00" before any request had
   * been made, which is a price the passenger might reasonably rely on.
   */
  const [estimatedPrice, setEstimatedPrice] = useState<number | null>(null);

  const [isLoadingAvailability, setIsLoadingAvailability] = useState(false);
  const [activeBooking, setActiveBooking] = useState<BookingDto | null>(null);
  const [isProcessingHold, setIsProcessingHold] = useState(false);
  const [isProcessingPayment, setIsProcessingPayment] = useState(false);
  const [notification, setNotification] = useState<Notification | null>(null);

  /** Aborts the in-flight availability request when a newer one supersedes it. */
  const availabilityAbortRef = useRef<AbortController | null>(null);

  /** Forces a refetch when the user asks for one explicitly. */
  const [refreshNonce, setRefreshNonce] = useState(0);

  /**
   * Holds one idempotency key per distinct booking intent.
   *
   * The key must stay stable across retries of the *same* logical hold, which
   * is the entire reason it exists. The previous build minted
   * `hold-${Date.now()}-${seatId}` inside the click handler, so a double-click
   * or a retry after a timeout produced two different keys and therefore two
   * separate holds — precisely the duplicate the key is meant to prevent. It
   * is regenerated only when the passenger's intent actually changes, or once
   * a hold has been successfully created.
   */
  const idempotencyRef = useRef<{ key: string; token: string } | null>(null);

  const idempotencyKeyFor = useCallback((key: string): string => {
    if (idempotencyRef.current?.key === key) return idempotencyRef.current.token;

    const token =
      typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : `${key}-${Math.random().toString(36).slice(2)}`;

    idempotencyRef.current = { key, token };
    return token;
  }, []);

  // Initial load: identity, routes and schedules.
  useEffect(() => {
    const controller = new AbortController();

    const loadInitialData = async () => {
      try {
        const user = await api.getCurrentUser(controller.signal);
        if (!controller.signal.aborted) setCurrentUser(user);
      } catch {
        // An unauthenticated visitor is an ordinary state, not an error.
        if (!controller.signal.aborted) setCurrentUser(null);
      } finally {
        if (!controller.signal.aborted) setIsAuthChecking(false);
      }

      try {
        const [fetchedRoutes, fetchedSchedules] = await Promise.all([
          api.getAllRoutes(controller.signal),
          api.getAllSchedules(controller.signal),
        ]);
        if (controller.signal.aborted) return;

        setRoutes(fetchedRoutes);
        setSchedules(fetchedSchedules);

        const initialRoute = fetchedRoutes[0] ?? null;
        setSelectedRoute(initialRoute);
        setSelectedSchedule(
          initialRoute
            ? fetchedSchedules.find((s) => s.routeId === initialRoute.id) ?? null
            : null
        );
      } catch (err) {
        if (controller.signal.aborted || isAbortError(err)) return;
        setNotification({
          message: toErrorMessage(
            err,
            "We couldn't load routes and departures. Check your connection and try again."
          ),
          type: 'error',
        });
      }
    };

    void loadInitialData();
    return () => controller.abort();
  }, []);

  const handleSelectRoute = useCallback(
    (route: RouteDto) => {
      setSelectedRoute(route);
      setSelectedSchedule(schedules.find((s) => s.routeId === route.id) ?? null);

      // A new route means new stops, so the old segment indices are meaningless.
      setBoardingIndex(0);
      setAlightingIndex(Math.min(1, Math.max(1, route.stops.length - 1)));
      setSelectedSeatId(null);
      setEstimatedPrice(null);
    },
    [schedules]
  );

  const handleSelectSchedule = useCallback((schedule: ScheduleDto) => {
    setSelectedSchedule(schedule);
    // Seat ids belong to a vehicle, and a different schedule may run a
    // different vehicle, so the current selection cannot carry over.
    setSelectedSeatId(null);
  }, []);

  const handleSegmentChange = useCallback((boarding: number, alighting: number) => {
    setBoardingIndex(boarding);
    setAlightingIndex(alighting);
  }, []);

  /**
   * Refetches availability whenever the query changes.
   *
   * Every previous call is aborted first. Without that, dragging a segment
   * slider fires a request per step and the responses resolve out of order, so
   * the map ends up showing whichever reply arrived last rather than the one
   * for the segment currently on screen.
   */
  useEffect(() => {
    if (activeTab !== 'search' || !selectedSchedule) return;

    availabilityAbortRef.current?.abort();
    const controller = new AbortController();
    availabilityAbortRef.current = controller;

    const loadAvailability = async () => {
      setIsLoadingAvailability(true);
      try {
        const res = await api.getScheduleAvailability(
          selectedSchedule.id,
          travelDate,
          boardingIndex,
          alightingIndex,
          controller.signal
        );
        if (controller.signal.aborted) return;

        setSeats(res.seats);
        setSeatLayout(res.layout ?? null);
        setEstimatedPrice(res.price);

        /*
         * Drop a selection the new segment has invalidated.
         *
         * Selecting seat 3 for [0,2) and then widening to [0,4) can leave the
         * seat occupied on the added legs. Previously the selection survived
         * and the Hold button stayed enabled, so the UI actively invited a
         * request the database was always going to reject.
         */
        setSelectedSeatId((current) => {
          if (!current) return current;
          const stillFree = res.seats.some(
            (seat) => seat.seatId === current && seat.isAvailable
          );
          if (stillFree) return current;

          setNotification({
            message:
              'That seat is already booked on your new journey, so we cleared it. Pick another seat.',
            type: 'error',
          });
          return null;
        });
      } catch (err) {
        if (controller.signal.aborted || isAbortError(err)) return;
        setSeats([]);
        setSeatLayout(null);
        setEstimatedPrice(null);
        setNotification({
          message: toErrorMessage(
            err,
            "We couldn't check which seats are free. Try again in a moment."
          ),
          type: 'error',
        });
      } finally {
        if (!controller.signal.aborted) setIsLoadingAvailability(false);
      }
    };

    void loadAvailability();
    return () => controller.abort();
  }, [selectedSchedule, travelDate, boardingIndex, alightingIndex, activeTab, refreshNonce]);

  const handleRefreshAvailability = useCallback(() => {
    setRefreshNonce((n) => n + 1);
  }, []);

  const handleHoldSeat = useCallback(async () => {
    if (!selectedSeatId || !selectedSchedule) return;

    // Surface the real requirement rather than letting the request 401.
    if (!currentUser) {
      setNotification({ message: 'Sign in to hold a seat.', type: 'error' });
      setIsAuthModalOpen(true);
      return;
    }

    setIsProcessingHold(true);
    setNotification(null);

    try {
      const idempotencyKey = idempotencyKeyFor(
        selectionKey(
          selectedSchedule.id,
          travelDate,
          selectedSeatId,
          boardingIndex,
          alightingIndex
        )
      );

      const booking = await api.holdSeat({
        passengerId: currentUser.id,
        scheduleId: selectedSchedule.id,
        travelDate,
        seatId: selectedSeatId,
        boardingIndex,
        alightingIndex,
        idempotencyKey,
      });

      setActiveBooking(booking);

      // The intent is satisfied, so the next hold is a genuinely new one.
      idempotencyRef.current = null;
    } catch (err) {
      setNotification({
        message: toErrorMessage(
          err,
          "We couldn't hold that seat. Someone may have just taken it, so pick another and try again."
        ),
        type: 'error',
      });
      // Refresh so the map reflects whoever won the seat.
      handleRefreshAvailability();
    } finally {
      setIsProcessingHold(false);
    }
  }, [
    selectedSeatId,
    selectedSchedule,
    currentUser,
    travelDate,
    boardingIndex,
    alightingIndex,
    idempotencyKeyFor,
    handleRefreshAvailability,
  ]);

  const handlePayWithPaystack = useCallback(async (bookingId: string) => {
    setPaymentError(null);
    setIsProcessingPayment(true);

    try {
      const paystackRes = await api.initializePaystackPayment(bookingId, currentUser?.email ?? '');
      window.location.href = paystackRes.authorizationUrl;
    } catch (err) {
      setPaymentError(toErrorMessage(err, "We couldn't open Paystack to take your payment. Try again."));
    } finally {
      setIsProcessingPayment(false);
    }
  }, [currentUser?.email]);

  const selectedSeat = seats.find((seat) => seat.seatId === selectedSeatId) ?? null;
  const stops = selectedRoute?.stops ?? [];
  const journeyLabel =
    stops[boardingIndex] && stops[alightingIndex]
      ? `${stops[boardingIndex]} → ${stops[alightingIndex]}`
      : null;

  return (
    <div className="min-h-screen flex flex-col">
      <Header
        activeHoldExpiry={activeBooking?.holdExpiry ?? null}
        activeTab={activeTab}
        onTabChange={navigateTab}
        currentUser={currentUser}
        onOpenAuth={() => openAuthModal('login')}
      />

      <main className="flex-1 w-full max-w-7xl mx-auto px-4 md:px-6 py-6 space-y-5">
        <NotificationBanner notification={notification} onDismiss={() => setNotification(null)} />

        {activeTab === 'search' && (
          <div
            id="panel-search"
            role="tabpanel"
            aria-labelledby="tab-search"
            tabIndex={-1}
            className="grid grid-cols-1 lg:grid-cols-3 gap-5 outline-none"
          >
            <div className="lg:col-span-2 space-y-5 stagger">
              <DepartureSearch
                routes={routes}
                selectedRoute={selectedRoute}
                onSelectRoute={handleSelectRoute}
                schedules={schedules}
                selectedSchedule={selectedSchedule}
                onSelectSchedule={handleSelectSchedule}
                travelDate={travelDate}
                onDateChange={setTravelDate}
                boardingIndex={boardingIndex}
                alightingIndex={alightingIndex}
                onSegmentChange={handleSegmentChange}
                estimatedPrice={estimatedPrice}
                minDate={todayIso()}
              />

              <SeatMap
                seats={seats}
                layout={seatLayout}
                selectedSeatId={selectedSeatId}
                onSelectSeat={setSelectedSeatId}
                isLoading={isLoadingAvailability}
              />
            </div>

            <div className="space-y-5 stagger">
              <Card
                as="aside"
                emphasis
                title="Your journey"
                action={
                  <IconButton
                    label="Check seat availability again"
                    onClick={handleRefreshAvailability}
                    disabled={isLoadingAvailability}
                    icon={
                      <RefreshCw
                        className={`w-4 h-4 ${isLoadingAvailability ? 'animate-spin' : ''}`}
                      />
                    }
                  />
                }
              >
                <dl className="space-y-3 text-sm">
                  <div className="flex items-baseline justify-between gap-3">
                    <dt className="stencil text-content-muted">Stops</dt>
                    <dd className="text-right font-semibold text-content">
                      {journeyLabel ?? <span className="text-content-muted">Not chosen yet</span>}
                    </dd>
                  </div>

                  <div className="flex items-baseline justify-between gap-3">
                    <dt className="stencil text-content-muted">Departs</dt>
                    <dd className="figures text-content">
                      {selectedSchedule ? (
                        formatDepartureTime(selectedSchedule.departureTimeOfDay)
                      ) : (
                          <span className="text-content-muted">
                          <span aria-hidden="true">&mdash;</span>
                          <span className="sr-only">Departure time not chosen yet</span>
                        </span>
                      )}
                    </dd>
                  </div>

                  <div className="flex items-baseline justify-between gap-3">
                    <dt className="stencil text-content-muted">Seat</dt>
                    <dd className="figures font-semibold text-content">
                      {selectedSeat?.seatNumber ?? (
                        <span className="text-content-muted font-sans">Not chosen yet</span>
                      )}
                    </dd>
                  </div>

                </dl>

                <div className="perforation my-5" />

                <div className="flex items-baseline justify-between gap-3">
                  <span className="board text-lg font-bold text-content">Fare</span>
                  <span className="board text-3xl font-bold text-signal-deep">
                    {estimatedPrice === null ? (
                      <span className="text-content-muted text-xl">
                        <span aria-hidden="true">&mdash;</span>
                        <span className="sr-only">Fare not available yet</span>
                      </span>
                    ) : (
                      formatFare(estimatedPrice)
                    )}
                  </span>
                </div>

                <Button
                  size="lg"
                  className="w-full mt-5"
                  disabled={!selectedSeatId || !selectedSchedule || isLoadingAvailability}
                  isLoading={isProcessingHold}
                  loadingLabel="Holding your seat"
                  onClick={handleHoldSeat}
                >
                  Hold this seat
                </Button>

                <p className="mt-2 text-xs text-content-muted text-center leading-snug">
                  We&rsquo;ll hold your seat while you pay. If the hold runs out first, the seat
                  goes back on sale.
                </p>
              </Card>

              {selectedSchedule && (
                <TelemetryRadar scheduleId={selectedSchedule.id} stops={stops} />
              )}
            </div>
          </div>
        )}

        {activeTab === 'my-bookings' && (
          <div id="panel-my-bookings" role="tabpanel" aria-labelledby="tab-my-bookings" tabIndex={-1} className="outline-none">
            <MyBookingsTab currentUser={currentUser} onPayBooking={setActiveBooking} />
          </div>
        )}

        {activeTab === 'operator' && (
          <div id="panel-operator" role="tabpanel" aria-labelledby="tab-operator" tabIndex={-1} className="outline-none">
            <OperatorTab currentUser={currentUser} />
          </div>
        )}
      </main>

      {activeBooking && (
        <CheckoutModal
          booking={activeBooking}
          onClose={() => {
            setActiveBooking(null);
            setPaymentError(null);
          }}
          onPayWithPaystack={handlePayWithPaystack}
          isProcessing={isProcessingPayment}
          paymentError={paymentError}
        />
      )}

      {isAuthModalOpen && (
        <AuthModal
          currentUser={currentUser}
          initialMode={authMode}
          onModeChange={(mode) => openAuthModal(mode)}
          onClose={closeAuthModal}
          onAuthSuccess={setCurrentUser}
        />
      )}
    </div>
  );
};
