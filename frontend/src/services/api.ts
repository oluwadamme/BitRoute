import axios from 'axios';
import {
  ApiResponse,
  AuthResult,
  BookingDto,
  CreateRouteRequest,
  CreateScheduleRequest,
  CreateVehicleRequest,
  PaystackInitializeResponse,
  RouteDto,
  ScheduleAvailabilityResponse,
  ScheduleDto,
  TelemetryPayload,
  UserProfile,
  VehicleDto,
} from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5001';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

let accessToken: string | null = localStorage.getItem('bitroute_access_token');
let refreshToken: string | null = localStorage.getItem('bitroute_refresh_token');

export const setAuthTokens = (tokens: { accessToken: string; refreshToken: string } | null) => {
  if (tokens) {
    accessToken = tokens.accessToken;
    refreshToken = tokens.refreshToken;
    localStorage.setItem('bitroute_access_token', tokens.accessToken);
    localStorage.setItem('bitroute_refresh_token', tokens.refreshToken);
  } else {
    accessToken = null;
    refreshToken = null;
    localStorage.removeItem('bitroute_access_token');
    localStorage.removeItem('bitroute_refresh_token');
  }
};

apiClient.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  const method = config.method?.toUpperCase() || 'GET';
  console.warn(`[API Request] ${method} ${config.baseURL || ''}${config.url || ''}`, {
    params: config.params,
    data: config.data,
  });
  return config;
});

/**
 * Tracks the single in-flight refresh call.
 *
 * The app fires several authenticated requests concurrently on mount. Without
 * this, an expired access token makes every one of them start its own refresh.
 * Because the backend rotates refresh tokens, the first call invalidates the
 * token the others are still holding, so all but one fail and the user is
 * signed out on load. Every 401 now awaits the same promise instead.
 */
let refreshInFlight: Promise<string | null> | null = null;

const refreshAccessToken = (): Promise<string | null> => {
  if (refreshInFlight) return refreshInFlight;

  const tokenAtRequestTime = refreshToken;
  if (!tokenAtRequestTime) return Promise.resolve(null);

  refreshInFlight = axios
    .post<ApiResponse<AuthResult>>(`${API_BASE_URL}/auth/refresh`, {
      refreshToken: tokenAtRequestTime,
    })
    .then((res) => {
      if (res.data.status && res.data.data) {
        setAuthTokens({
          accessToken: res.data.data.accessToken,
          refreshToken: res.data.data.refreshToken,
        });
        return res.data.data.accessToken;
      }
      setAuthTokens(null);
      return null;
    })
    .catch(() => {
      setAuthTokens(null);
      return null;
    })
    .finally(() => {
      refreshInFlight = null;
    });

  return refreshInFlight;
};

// Automatic token refresh interceptor on 401 with request logging
apiClient.interceptors.response.use(
  (response) => {
    const method = response.config.method?.toUpperCase() || 'GET';
    console.warn(`[API Response ${response.status}] ${method} ${response.config.url}`, response.data);
    return response;
  },
  async (error) => {
    const originalRequest = error.config;

    // A cancelled request is an intentional abort, never an auth failure.
    if (axios.isCancel(error)) {
      console.warn(`[API Aborted] ${originalRequest?.method?.toUpperCase() || 'GET'} ${originalRequest?.url}`);
      return Promise.reject(error);
    }

    if (error.response) {
      console.error(
        `[API Error ${error.response.status}] ${originalRequest?.method?.toUpperCase() || 'GET'} ${originalRequest?.url}`,
        error.response.data
      );
    } else {
      console.error(
        `[API Network Error] ${originalRequest?.method?.toUpperCase() || 'GET'} ${originalRequest?.url}`,
        error.message
      );
    }

    if (error.response?.status === 401 && originalRequest && !originalRequest._retry && refreshToken) {
      originalRequest._retry = true;
      console.warn(`[API Auth] 401 Unauthorized — attempting refresh for ${originalRequest.url}`);

      const newAccessToken = await refreshAccessToken();
      if (newAccessToken) {
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        return apiClient(originalRequest);
      }
    }

    return Promise.reject(error);
  }
);

export const api = {
  // 1. Auth Endpoints
  register: async (payload: { email: string; password: string; fullName: string }): Promise<AuthResult> => {
    const res = await apiClient.post<ApiResponse<AuthResult>>('/auth/register', payload);
    if (res.data.data) setAuthTokens(res.data.data);
    return res.data.data!;
  },

  login: async (payload: { email: string; password: string }): Promise<AuthResult> => {
    const res = await apiClient.post<ApiResponse<AuthResult>>('/auth/login', payload);
    if (res.data.data) setAuthTokens(res.data.data);
    return res.data.data!;
  },

  refresh: async (token: string): Promise<AuthResult> => {
    const res = await apiClient.post<ApiResponse<AuthResult>>('/auth/refresh', { refreshToken: token });
    if (res.data.data) setAuthTokens(res.data.data);
    return res.data.data!;
  },

  logout: async (): Promise<void> => {
    if (refreshToken) {
      try {
        await apiClient.post('/auth/logout', { refreshToken });
      } catch {
        // ignore logout network errors
      }
    }
    setAuthTokens(null);
  },

  getCurrentUser: async (signal?: AbortSignal): Promise<UserProfile> => {
    const res = await apiClient.get<ApiResponse<UserProfile>>('/auth/me', { signal });
    return res.data.data!;
  },

  /** Admin-only. Returns the provisioned operator, shaped like any other user. */
  provisionOperator: async (payload: {
    email: string;
    password: string;
    fullName: string;
  }): Promise<UserProfile> => {
    const res = await apiClient.post<ApiResponse<UserProfile>>('/auth/operators', payload);
    return res.data.data!;
  },

  // 2. Routes Endpoints
  createRoute: async (payload: CreateRouteRequest): Promise<string> => {
    const res = await apiClient.post<ApiResponse<string>>('/routes', payload);
    return res.data.data!;
  },

  getRoute: async (id: string): Promise<RouteDto> => {
    const res = await apiClient.get<ApiResponse<RouteDto>>(`/routes/${id}`);
    return res.data.data!;
  },

  getAllRoutes: async (signal?: AbortSignal): Promise<RouteDto[]> => {
    const res = await apiClient.get<ApiResponse<RouteDto[]>>('/routes', { signal });
    return res.data.data || [];
  },

  // 3. Vehicles Endpoints
  createVehicle: async (payload: CreateVehicleRequest): Promise<string> => {
    const res = await apiClient.post<ApiResponse<string>>('/vehicles', payload);
    return res.data.data!;
  },

  getVehicle: async (id: string): Promise<VehicleDto> => {
    const res = await apiClient.get<ApiResponse<VehicleDto>>(`/vehicles/${id}`);
    return res.data.data!;
  },

  getAllVehicles: async (signal?: AbortSignal): Promise<VehicleDto[]> => {
    const res = await apiClient.get<ApiResponse<VehicleDto[]>>('/vehicles', { signal });
    return res.data.data || [];
  },

  // 4. Schedules Endpoints
  createSchedule: async (payload: CreateScheduleRequest): Promise<string> => {
    const res = await apiClient.post<ApiResponse<string>>('/schedules', payload);
    return res.data.data!;
  },

  getSchedule: async (id: string): Promise<ScheduleDto> => {
    const res = await apiClient.get<ApiResponse<ScheduleDto>>(`/schedules/${id}`);
    return res.data.data!;
  },

  getAllSchedules: async (signal?: AbortSignal): Promise<ScheduleDto[]> => {
    const res = await apiClient.get<ApiResponse<ScheduleDto[]>>('/schedules', { signal });
    return res.data.data || [];
  },

  /**
   * Accepts an AbortSignal because the seat map refetches on every segment,
   * date, and schedule change. Without cancellation the responses resolve out
   * of order and the last one to *arrive* wins rather than the last one
   * *issued*, which can leave the map showing availability for a segment the
   * passenger already moved off.
   */
  getScheduleAvailability: async (
    scheduleId: string,
    travelDate: string,
    boardingIndex: number,
    alightingIndex: number,
    signal?: AbortSignal
  ): Promise<ScheduleAvailabilityResponse> => {
    const res = await apiClient.get<ApiResponse<ScheduleAvailabilityResponse>>(
      `/schedules/${scheduleId}/availability`,
      {
        params: { travelDate, boardingIndex, alightingIndex },
        signal,
      }
    );
    return res.data.data!;
  },

  getLatestTelemetry: async (id: string, signal?: AbortSignal): Promise<TelemetryPayload | null> => {
    const res = await apiClient.get<ApiResponse<TelemetryPayload>>(`/schedules/${id}/telemetry/latest`, {
      signal,
    });
    return res.data.data || null;
  },

  // 5. Bookings Endpoints
  holdSeat: async (payload: {
    passengerId: string;
    scheduleId: string;
    travelDate: string;
    seatId: string;
    boardingIndex: number;
    alightingIndex: number;
    idempotencyKey: string;
  }): Promise<BookingDto> => {
    const res = await apiClient.post<ApiResponse<BookingDto>>('/bookings/hold', payload);
    return res.data.data!;
  },

  confirmBooking: async (bookingId: string): Promise<void> => {
    await apiClient.post(`/bookings/${bookingId}/confirm`);
  },

  initializePaystackPayment: async (bookingId: string, email: string): Promise<PaystackInitializeResponse> => {
    const res = await apiClient.post<ApiResponse<PaystackInitializeResponse>>(
      `/bookings/${bookingId}/pay`,
      { email }
    );
    return res.data.data!;
  },

  getBooking: async (bookingId: string, userId: string, userRole: string): Promise<BookingDto> => {
    const res = await apiClient.get<ApiResponse<BookingDto>>(`/bookings/${bookingId}`, {
      params: { userId, userRole },
    });
    return res.data.data!;
  },

  getMyBookings: async (passengerId: string, signal?: AbortSignal): Promise<BookingDto[]> => {
    const res = await apiClient.get<ApiResponse<BookingDto[]>>('/bookings/me', {
      params: { passengerId },
      signal,
    });
    return res.data.data || [];
  },

  cancelBooking: async (bookingId: string, userId: string, userRole: string): Promise<void> => {
    await apiClient.delete(`/bookings/${bookingId}`, {
      params: { userId, userRole },
    });
  },
};

/** True when a rejection came from an intentional abort rather than a real failure. */
export const isAbortError = (err: unknown): boolean =>
  axios.isCancel(err) || (err instanceof Error && err.name === 'CanceledError');

/**
 * Pulls the most specific message available out of an API rejection, falling
 * back through the envelope's `message`, then the transport error, then a
 * caller-supplied default. Replaces the `err.response?.data?.message || ...`
 * chain that was duplicated across every component.
 */
export const toErrorMessage = (err: unknown, fallback: string): string => {
  if (axios.isAxiosError(err)) {
    const envelope = err.response?.data as ApiResponse<unknown> | undefined;
    const fieldErrors = envelope?.errors;

    if (fieldErrors) {
      const firstField = Object.values(fieldErrors).find((messages) => messages?.length);
      if (firstField?.[0]) return firstField[0];
    }

    if (envelope?.message) return envelope.message;
    if (err.message) return err.message;
  }

  if (err instanceof Error && err.message) return err.message;
  return fallback;
};
