import * as signalR from '@microsoft/signalr';
import { TelemetryPayload } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5001';
const LOCATION_EVENT = 'ReceiveVehicleLocation';

/**
 * Thin transport wrapper around the telemetry hub.
 *
 * Telemetry is deliberately isolated from the booking path: every method here
 * resolves rather than throws on a dead connection, so a dropped socket or a
 * stalled GPS stream can never surface as a booking failure.
 */
export class TelemetryService {
  private connection: signalR.HubConnection | null = null;

  /**
   * De-duplicates concurrent connect() calls. React StrictMode double-invokes
   * effects in development, so without this the first call is still in
   * `Connecting` state when the second arrives, and `start()` throws.
   */
  private startPromise: Promise<void> | null = null;

  /** Groups currently joined, so a reconnect can restore them. */
  private joinedGroups = new Set<string>();

  public async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;
    if (this.startPromise) return this.startPromise;

    if (!this.connection) {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE_URL}/hubs/telemetry`, {
          skipNegotiation: false,
          transport:
            signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
        })
        .withAutomaticReconnect()
        .build();

      // Automatic reconnect rejoins the transport but not the hub groups, so
      // re-register them or the stream goes silent after a network blip.
      this.connection.onreconnected(() => {
        this.emitState(true);
        void this.rejoinGroups();
      });
      this.connection.onreconnecting(() => this.emitState(false));
      this.connection.onclose(() => this.emitState(false));
    }

    this.startPromise = this.connection
      .start()
      .then(() => {
        this.emitState(true);
      })
      .finally(() => {
        this.startPromise = null;
      });

    return this.startPromise;
  }

  private async rejoinGroups(): Promise<void> {
    for (const scheduleId of this.joinedGroups) {
      try {
        await this.connection?.invoke('JoinScheduleGroup', scheduleId);
      } catch {
        // A failed rejoin only costs telemetry updates, never a booking.
      }
    }
  }

  public async joinScheduleGroup(scheduleId: string): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;

    await this.connection.invoke('JoinScheduleGroup', scheduleId);
    this.joinedGroups.add(scheduleId);
  }

  /**
   * Leaves a group when a component stops caring about it. Without this, every
   * schedule switch left the previous group subscribed and the client kept
   * receiving pings for vehicles nobody was watching.
   */
  public async leaveScheduleGroup(scheduleId: string): Promise<void> {
    this.joinedGroups.delete(scheduleId);
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return;

    try {
      await this.connection.invoke('LeaveScheduleGroup', scheduleId);
    } catch {
      // The server drops the membership when the socket closes anyway.
    }
  }

  public async sendDriverLocation(
    scheduleId: string,
    latitude: number,
    longitude: number,
    currentLegIndex: number
  ): Promise<void> {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Telemetry stream is not connected.');
    }

    await this.connection.invoke(
      'SendDriverLocation',
      scheduleId,
      latitude,
      longitude,
      currentLegIndex
    );
  }

  /**
   * Subscribes to location pings and returns its own unsubscribe function.
   *
   * Returning the teardown is what makes this safe in an effect. The previous
   * version registered handlers with no way to remove them, so StrictMode's
   * double-invoke and every schedule change stacked another live listener on
   * the same connection.
   */
  public onLocationReceived(callback: (payload: TelemetryPayload) => void): () => void {
    const connection = this.connection;
    if (!connection) return () => {};

    connection.on(LOCATION_EVENT, callback);
    return () => connection.off(LOCATION_EVENT, callback);
  }

  /**
   * Subscribes to connection-state changes and returns an unsubscribe function.
   *
   * signalR's `onclose`/`onreconnecting` hooks cannot detach an individual
   * callback, and the connection outlives any single component. So the hub
   * callbacks are registered once against a local subscriber set that
   * components can add to and remove from freely.
   */
  public onConnectionStateChange(listener: (connected: boolean) => void): () => void {
    this.stateListeners.add(listener);
    listener(this.isConnected());

    return () => {
      this.stateListeners.delete(listener);
    };
  }

  private stateListeners = new Set<(connected: boolean) => void>();

  private emitState(connected: boolean): void {
    for (const listener of this.stateListeners) listener(connected);
  }

  public isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  public async disconnect(): Promise<void> {
    this.joinedGroups.clear();
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.emitState(false);
    }
  }
}

export const telemetryService = new TelemetryService();
