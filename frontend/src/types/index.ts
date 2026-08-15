export interface ApiResponse<T> {
  status: boolean;
  message: string;
  data?: T;
  errors?: Record<string, string[]>;
}

export enum UserRole {
  Admin = 'Admin',
  Operator = 'Operator',
  Passenger = 'Passenger',
}

export interface AuthResult {
  userId: string;
  email: string;
  role: UserRole;
  accessToken: string;
  refreshToken: string;
}

export interface UserProfile {
  id: string;
  email: string;
  roles: UserRole[];
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

export interface CreateSeatDto {
  number: string;
  row: number;
  column: number;
}

export interface CreateVehicleRequest {
  name: string;
  rowCount: number;
  seatsPerRow: number;
  aisleAfterColumn: number | null;
  seats: CreateSeatDto[];
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
  row: number;
  column: number;
}

export interface VehicleLayoutDto {
  rowCount: number;
  seatsPerRow: number;
  aisleAfterColumn: number | null;
}

export interface ScheduleAvailabilityResponse {
  scheduleId: string;
  travelDate: string;
  price: number;
  layout: VehicleLayoutDto;
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
