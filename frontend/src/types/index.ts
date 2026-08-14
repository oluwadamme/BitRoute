export interface ApiResponse<T> {
  status: boolean;
  message: string;
  data?: T;
  errors?: Record<string, string[]>;
}

export interface AuthResult {
  userId: string;
  email: string;
  role: string;
  accessToken: string;
  refreshToken: string;
}

export interface UserProfile {
  id: string;
  email: string;
  roles: string[];
}

export interface RouteDto {
  id: string;
  name: string;
  stops: string[];
}

export interface CreateRouteRequest {
  name: string;
  stops: string[];
}

export interface VehicleDto {
  id: string;
  name: string;
  seats: string[];
}

export interface CreateVehicleRequest {
  name: string;
  seats: string[];
}

export interface ScheduleLegDto {
  id: string;
  startStopIndex: number;
  endStopIndex: number;
  fare: number;
}

export interface CreateScheduleLegDto {
  startStopIndex: number;
  endStopIndex: number;
  fare: number;
}

export interface ScheduleDto {
  id: string;
  routeId: string;
  vehicleId: string;
  departureTimeOfDay: string;
  legs: ScheduleLegDto[];
}

export interface CreateScheduleRequest {
  routeId: string;
  vehicleId: string;
  departureTimeOfDay: string;
  legs: CreateScheduleLegDto[];
}

export interface SeatAvailabilityDto {
  seatId: string;
  seatNumber: string;
  isAvailable: boolean;
}

export interface ScheduleAvailabilityResponse {
  scheduleId: string;
  travelDate: string;
  price: number;
  seats: SeatAvailabilityDto[];
}

export interface BookingDto {
  id: string;
  passengerId: string;
  scheduleId: string;
  travelDate: string;
  seatId: string;
  boardingIndex: number;
  alightingIndex: number;
  status: 'Held' | 'Confirmed' | 'Expired' | 'Cancelled';
  price: number;
  holdExpiry: string | null;
}

export interface PaystackInitializeResponse {
  authorizationUrl: string;
  accessCode: string;
  reference: string;
}

export interface TelemetryPayload {
  scheduleId: string;
  latitude: number;
  longitude: number;
  currentLegIndex: number;
  timestamp: string;
}
