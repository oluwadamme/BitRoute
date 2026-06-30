# BitRoute

A backend for booking seats on fixed-route transport, built in C# / .NET. The defining feature is segment-based seat inventory: a single physical seat can be sold to several passengers as long as their journeys do not overlap. A passenger riding A to C and another riding C to E can share the same seat on the same departure.

## Table of contents

- [The problem](#the-problem)
- [The core invariant](#the-core-invariant)
- [System components](#system-components)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [Domain model](#domain-model)
- [Booking lifecycle](#booking-lifecycle)
- [Identity and auth](#identity-and-auth)
- [Payments](#payments)
- [Real-time telemetry](#real-time-telemetry)
- [Scheduled vs realtime booking](#scheduled-vs-realtime-booking)
- [Scalability and reliability](#scalability-and-reliability)
- [Testing](#testing)
- [API overview](#api-overview)
- [Getting started](#getting-started)
- [Roadmap](#roadmap)
- [What this project demonstrates](#what-this-project-demonstrates)

## The problem

A route is an ordered sequence of stops. Take a route A, B, C, D, E. Those four gaps (A-B, B-C, C-D, D-E) are legs. A vehicle running this route has a fixed number of seats, and each seat can carry a different passenger on each leg, provided nobody books overlapping legs on the same seat.

Give each stop an index along the route:

```
A = 0    B = 1    C = 2    D = 3    E = 4
```

A booking occupies a seat over the half-open interval `[boarding_index, alighting_index)`. A trip from A to C occupies `[0, 2)`. A trip from C to E occupies `[2, 4)`. Those two intervals do not overlap, so the same seat can serve both passengers.

```
Route:   A --------- B --------- C --------- D --------- E
Seat 1:  | Passenger 1 (A to C)  | Passenger 2 (C to E)  |
Seat 2:  | Passenger 3 (A to E)                          |
Seat 3:  | available  | Passenger 4 (B to D)  | available |
```

Seat 1 is the whole idea: one physical seat, two paying passengers, no conflict.

## The core invariant

Two bookings on the same seat conflict if and only if their intervals overlap:

```
ExistingStart < RequestedEnd  AND  ExistingEnd > RequestedStart
```

Everything in the system exists to protect this rule under concurrent load. Availability is the inverse: a seat is free for a journey from `i` to `j` when no existing active booking on that seat satisfies the overlap condition.

```sql
SELECT s.seat_id
FROM seats s
WHERE s.vehicle_id = @vehicleId
  AND NOT EXISTS (
    SELECT 1
    FROM seat_bookings b
    WHERE b.schedule_id   = @scheduleId
      AND b.travel_date   = @travelDate
      AND b.seat_id       = s.seat_id
      AND b.status        IN ('held', 'confirmed')
      AND b.boarding_index < @j
      AND b.alighting_index > @i
  );
```

### Enforcing it under concurrency

The race condition is real: two requests both read seat 1 as free for overlapping legs, and both try to insert. A plain unique constraint cannot catch this, because the conflicting intervals are not identical. BitRoute defends the invariant on two fronts.

**Serializable transactions.** The validate-then-insert sequence runs inside an explicit transaction at `IsolationLevel.Serializable`. PostgreSQL implements this with serializable snapshot isolation, which detects the conflict and aborts one of the two transactions, so the booking path needs a retry loop on serialization failures (SQLSTATE `40001`).

**A database exclusion constraint.** As a hard guarantee that does not depend on getting the transaction code perfect, a PostgreSQL exclusion constraint backed by `btree_gist` makes overlapping bookings physically impossible to store.

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE seat_bookings
ADD CONSTRAINT no_overlapping_legs
EXCLUDE USING gist (
  schedule_id WITH =,
  travel_date WITH =,
  seat_id     WITH =,
  int4range(boarding_index, alighting_index, '[)') WITH &&
) WHERE (status IN ('held', 'confirmed'));
```

Using both is deliberate. Serializable isolation keeps the application logic correct and explainable, and the exclusion constraint is the backstop that holds even if a future code change slips past the transaction boundary. Npgsql maps the range to `NpgsqlRange<int>` in EF Core.

## System components

### 1. Identity and auth domain

Stateless, cryptographically signed JWT access tokens secure the API. Access windows are short, paired with a Redis-backed refresh-token sliding lifecycle so sessions stay alive without thrashing the primary database. Passwords are hashed with ASP.NET Core Identity.

### 2. Multi-leg reservation domain

PostgreSQL with EF Core, using rich domain entities that run the overlap evaluation inside their own object boundaries rather than in anemic services. Transactional protection (Serializable isolation plus the exclusion constraint) prevents multi-threaded race conditions.

### 3. Asynchronous payment processing (Stripe)

A decoupled, server-to-server webhook listener. No sensitive PCI data is stored. Webhook payloads are verified against Stripe's cryptographic signature before anything is trusted, then a MediatR notification is published so the checkout is decoupled from the core application pipeline.

### 4. Real-time telemetry tracking

ASP.NET Core SignalR over WebSockets opens persistent duplex channels that stream live driver GPS data, used to compute active booking windows on the fly for real-time standby passengers.

## Tech stack

| Concern | Choice |
| --- | --- |
| Language / runtime | C# / ASP.NET Core |
| Database | PostgreSQL (with `btree_gist`) |
| ORM | EF Core (Npgsql) |
| Caching / token store | Redis |
| Identity | ASP.NET Core Identity (password hashing) + JWT |
| In-process events | MediatR |
| Payments | Stripe (webhooks, signature verification) |
| Real-time | ASP.NET Core SignalR (WebSockets) |
| Background jobs | Hangfire |
| Logging | Serilog (structured, correlation ids) |
| Testing | xUnit, WebApplicationFactory, Testcontainers |
| Local infra | Docker Compose |

## Architecture

The solution is split into four projects:

- **Api**: controllers, SignalR hubs, the Stripe webhook endpoint, authentication, error handling.
- **Application**: use cases, the availability and booking services, MediatR handlers, DTOs.
- **Domain**: entities and the overlap rules as behavior on the entities themselves.
- **Infrastructure**: EF Core, the range mapping and exclusion-constraint migration, the Redis token store, Hangfire jobs, the Stripe client.

The read path (availability) is cacheable and tolerant of staleness. The write path (booking) is strongly consistent and guarded by both the Serializable transaction and the database constraint. The API is stateless so it scales horizontally, with PostgreSQL as the single source of truth.

## Domain model

The relational chain is `Route -> Schedule -> ScheduleLeg -> SeatBooking`.

- **User**: account, roles (passenger, operator, admin).
- **Stop**: a named location with an order index along a route.
- **Route**: an ordered set of stops, the geography.
- **Schedule**: a departure of a route by a vehicle, carrying the departure time and recurrence.
- **ScheduleLeg**: an ordered leg between two consecutive stops on a schedule, with its index and fare.
- **Vehicle / Seat**: the physical seats a schedule's vehicle provides.
- **SeatBooking**: a user, a schedule, a travel date, a seat, a boarding index, an alighting index, a status, a price, and a hold expiry.
- **Payment**: a booking, a Stripe reference, an amount in minor units, a status, and an idempotency key.

Pricing for a journey is the sum of the `ScheduleLeg` fares for the legs traversed, so A to C costs the A-B fare plus the B-C fare. The travel date lives on the `SeatBooking`, which keeps the model light. If you later need per-departure state (a cancelled or delayed run, a swapped vehicle), materialize dated departure rows instead.

## Booking lifecycle

```
   create booking
        |
        v
     [ HELD ] --- hold expires ---> [ EXPIRED ]  (seat released)
        |
   payment confirmed (Stripe webhook -> MediatR)
        |
        v
  [ CONFIRMED ]
        |
   cancellation
        |
        v
  [ CANCELLED ]  (seat released)
```

A booking is created in `HELD` state, which reserves the seat and starts an expiry timer. The seat stays excluded from availability while held. A Hangfire sweep releases holds that expire before payment completes. A confirmed Stripe payment moves the booking to `CONFIRMED`. Status changes go through domain methods on the entity, which reject illegal transitions.

## Identity and auth

Passwords are hashed with ASP.NET Core Identity. Login issues a short-lived JWT access token carrying the user's claims, paired with a refresh token whose sliding expiration is tracked in Redis rather than the primary database. Each refresh extends the window and rotates the token, which limits the damage from a leaked token. Authorization is policy-based and ownership-aware: an operator manages only their own schedules, a passenger sees only their own bookings.

## Payments

Payment is tied to the hold, so a seat is never confirmed without money and never blocked indefinitely without payment.

```
create booking (HELD)
        |
        v
create Stripe PaymentIntent  --->  return client secret
        |
   user pays
        |
        v
Stripe webhook  --->  verify signature  --->  publish MediatR PaymentConfirmed
        |                                              |
   failure or timeout                                  v
        |                                     handler confirms booking
        v                                     and writes to the outbox
release hold (EXPIRED)
```

Two rules that matter: verify the Stripe signature before trusting the payload, and dedupe on Stripe's event id so a re-delivered webhook does not confirm twice. No card data touches the system. Money is stored as integer minor units, never a float. MediatR keeps the webhook handler thin and the booking confirmation decoupled from the HTTP request.

## Real-time telemetry

A SignalR hub holds persistent duplex connections. Driver clients push GPS pings, which the server maps onto the schedule's legs to know which leg a vehicle is currently on. From that, it computes live booking windows and pushes them to standby passengers watching a route, so a seat that frees up mid-journey can be offered in real time. This channel is intentionally separate from the transactional booking core: a dropped socket must never affect a confirmed booking.

## Scheduled vs realtime booking

There is one booking path, not two. The difference is timing: scheduled booking reserves a future-dated departure, realtime booking reserves one departing now, often informed by the live telemetry above. A recurring Hangfire job keeps the bookable date window open, and it is idempotent so re-runs never duplicate anything.

## Scalability and reliability

- **Caching**: availability reads are cached in Redis, keyed by schedule, travel date, and leg range, and invalidated on every booking write.
- **Rate limiting**: applied to the booking and payment endpoints.
- **Idempotency keys**: accepted on booking creation and enforced on Stripe webhooks.
- **Outbox pattern**: domain events such as "booking confirmed" are written in the same transaction as the booking, then dispatched by a worker. This survives a crash between the database commit and the email or notification.
- **Observability**: structured logging with Serilog, a correlation id per request, and optional OpenTelemetry traces and metrics.

## Testing

The overlap logic is pure, so it is unit-tested exhaustively: given a set of bookings and a requested journey, is the seat available or not. On top of that, integration tests run against real PostgreSQL using Testcontainers.

The most important test fires many parallel booking requests at the same seat and overlapping legs and asserts that exactly one succeeds. A passing concurrency test is the single most convincing piece of evidence that the inventory engine is correct.

## API overview

A representative slice of the endpoints. Shapes are indicative.

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/auth/register` | Create a passenger account |
| POST | `/auth/login` | Obtain access and refresh tokens |
| POST | `/auth/refresh` | Rotate the refresh token (Redis-backed) |
| POST | `/routes` | Create a route with ordered stops (operator) |
| POST | `/schedules` | Define a schedule, its legs, and vehicle (operator) |
| GET | `/schedules/{id}/availability?date={d}&from={i}&to={j}` | Seats available for a journey |
| POST | `/bookings` | Hold a seat for a journey (idempotent) |
| POST | `/bookings/{id}/pay` | Create a Stripe PaymentIntent for a held booking |
| POST | `/webhooks/stripe` | Confirm payment (Stripe callback) |
| GET | `/bookings/me` | List the caller's bookings |
| DELETE | `/bookings/{id}` | Cancel a booking and release the seat |
| WS | `/hubs/telemetry` | SignalR channel for live vehicle tracking |

## Getting started

Prerequisites: the .NET SDK, Docker, and Docker Compose.

```bash
# 1. Clone
git clone https://github.com/<your-username>/bitroute.git
cd bitroute

# 2. Start PostgreSQL and Redis
docker compose up -d

# 3. Apply migrations
dotnet ef database update --project src/BitRoute.Infrastructure --startup-project src/BitRoute.Api

# 4. Run the API
dotnet run --project src/BitRoute.Api

# 5. Open the API docs
# http://localhost:5000/swagger
```

Configuration (connection strings, JWT signing key, Stripe keys, Redis) is read from `appsettings.json` and environment variables. Do not commit secrets. For local Stripe webhooks, use the Stripe CLI to forward events to `/webhooks/stripe`.

## Roadmap

Built as a series of phases, each shippable on its own. Build a thin vertical slice through Phase 3 first, then layer the rest.

- **Phase 1, Clean Architecture setup**: solution layout via the .NET CLI, the four projects, and the dependency-direction check (Domain and Application reference nothing outward). Overlap rules embedded in the entities, not in anemic services.
  - [x] Solution scaffolded via the .NET CLI with the four projects (Api, Application, Domain, Infrastructure).
  - [x] Inward project references wired: Application and Infrastructure reference only Domain; Api references Application and Infrastructure as the composition root.
  - [x] Solution file populated, root `.gitignore` added, and build artifacts untracked.
  - [x] `Segment` value object owns the half-open overlap rule as behavior, not an anemic service.
  - [x] xUnit test project under `tests/` with exhaustive `Segment` overlap unit tests, including the half-open boundary case.
  - [x] Automated dependency-direction check (NetArchTest) that fails the build if a layer references outward.
- **Phase 2, decoupled identity**: ASP.NET Core Identity password hashing, JWT issuance with user claims, and the Redis-backed sliding refresh-token store.
  - Decisions: login by email; 60-minute access token and 30-day sliding refresh; refresh tokens are one-time-use with reuse detection that revokes the session family; passengers self-register while operators and admins are provisioned (no role accepted from the client).
  - [ ] Domain: `User` concept and `Role` enum (passenger, operator, admin), plus the Identity boundary decision (rich Domain user vs Infrastructure `IdentityUser`).
  - [ ] Domain: refresh-token model (token, expiry, session-family id, used flag) with reuse-detection rules as behavior, and auth domain exceptions.
  - [ ] Infrastructure: ASP.NET Core Identity on EF Core + PostgreSQL for password hashing, with the Identity migration.
  - [ ] Infrastructure: JWT generator (60-minute access token carrying user claims) behind a Domain-defined interface.
  - [ ] Infrastructure: Redis-backed refresh-token store with the 30-day sliding window, rotation, reuse detection, and family revocation.
  - [ ] Application: auth service orchestrating register, login, refresh, and logout, with record DTOs and FluentValidation.
  - [ ] Api: auth endpoints (`/auth/register`, `/auth/login`, `/auth/refresh`, logout), JWT bearer auth, policy-based authorization, and DI registration.
  - [ ] Api: map the new auth exceptions in `ExceptionMiddleware`.
  - [ ] Cross-cutting: admin seeding and the admin-only operator provisioning path.
  - [ ] Tests: login, refresh rotation, reuse detection revoking the family, and authorization failures.
- **Phase 3, high-concurrency overlap engine**: the `Route -> Schedule -> ScheduleLeg -> SeatBooking` schema, the leg-overlap query, Serializable transactions with a retry loop, and the exclusion constraint backstop. Plus the hold-and-expiry pattern.
- **Phase 4, webhook architecture**: the Stripe server-side integration, a signature-verified callback endpoint, idempotent event handling, and MediatR notifications on confirmation.
- **Phase 5, real-time WebSockets**: the SignalR hub, and mapping incoming GPS pings onto active leg windows.
- **Phase 6, testing quality gates**: unit tests over the overlap logic, Testcontainers integration tests, and the multi-threaded collision test that hits identical seat paths in parallel.

Cross-cutting work (availability caching, rate limiting, the outbox, observability, and idempotent schedule generation) lands alongside the phases it touches.

## What this project demonstrates

For anyone reviewing this as a portfolio piece, look here first:

1. **Concurrency-correct segment inventory**, defended by both Serializable transactions and a PostgreSQL exclusion constraint, proven by a passing parallel-booking test.
2. **Asynchronous, signature-verified Stripe payments** decoupled through MediatR, with idempotent webhooks and correct money handling.
3. **The reserve-then-confirm hold pattern**, including automatic release of expired holds.
4. **Real-time telemetry over SignalR**, kept cleanly separate from the transactional core.

These are real backend concerns rather than CRUD, and each one is the kind of thing that comes up in interviews for junior and internship .NET roles.

---

Author: [your name] · [GitHub] · [LinkedIn]