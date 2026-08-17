# Backend Clean Architecture & System Design Patterns

This document details BitRoute's backend design architecture, layer boundaries, dependency inversion enforcement, Domain-Driven Design (DDD) invariants, MediatR event pipelines, and dependency injection lifecycle management.

---

## 1. Explained for a 12-Year-Old

Imagine a high-security bank building:

1. **The Vault Rules (Domain Layer)**: Inside the absolute center of the bank is a steel vault. The vault contains the core rules of math and physics (e.g., *"A seat cannot hold two people at once"*). The vault doesn't care if the bank uses computers, paper, or carrier pigeons—its rules never change!
2. **The Tellers & Managers (Application Layer)**: These workers sit outside the vault. When a customer wants a ticket, they check the vault rules, calculate prices, and ask for help. They don't touch the database directly; they ask the specialists.
3. **The Builders & Database Mechanics (Infrastructure Layer)**: These are the plumbers, electricians, and database specialists. They know how to talk to PostgreSQL databases and Redis caches.
4. **The Security Desk (API Layer)**: This is the front entrance where customers check in, show their passports (`JWT tokens`), and get checked by security guards (`Rate Limiters`).
5. **The Clean Rule**: The security desk and mechanics must follow the vault's rules, but the vault NEVER asks the mechanics for permission or advice! (`Dependency Inversion`).

---

## 2. Deep-Dive Interview Defense Guide

### Technical Explanation

### 1. Clean Architecture & Dependency Inversion Principles
BitRoute enforces strict **Clean Architecture** (Onion / Hexagonal Architecture):

$$\text{Domain} \impliedby \text{Application} \impliedby \text{Infrastructure} \impliedby \text{Api}$$

- **Domain (`BitRoute.Domain`)**: Pure C# domain entities (`SeatBooking`, `Schedule`, `User`), value objects (`Segment`), enums (`BookingStatus`), domain events (`SeatHoldExpiredEvent`), and domain exceptions. Zero external dependencies or ORM attributes.
- **Application (`BitRoute.Application`)**: Use cases, business workflow orchestration (`BookingService`, `AuthService`), MediatR requests/notifications (`PaymentConfirmedNotification`), DTOs, and interfaces (`IBookingRepository`, `IPaystackService`).
- **Infrastructure (`BitRoute.Infrastructure`)**: Technical implementations—EF Core DbContext, PostgreSQL `btree_gist` configurations, Redis token/cache stores, hosted `BackgroundService` workers (`OutboxProcessor`, `ExpiredHoldSweeper`), and `PaystackService`.
- **Api (`BitRoute.Api`)**: Composition root, ASP.NET Core controllers, SignalR telemetry hubs, JWT authentication handler, rate limiting middleware, and Exception Handling middleware.

**Automated Architecture Policy**: A NetArchTest suite (`tests/BitRoute.ArchitectureTests`) runs during builds to assert that `Domain` and `Application` never reference `Infrastructure` or `Api`.

### 2. Domain-Driven Design (DDD) & Aggregate Boundaries
- **Value Object Immature Invariant Protection**: `Segment` enforces `BoardingIndex < AlightingIndex` and encapsulates interval overlap math (`OverlapsWith`).
- **Entity State Transitions**: `SeatBooking.Confirm(utcNow)` and `SeatBooking.ExpireHold(utcNow)` validate illegal state transitions (e.g., an `Expired` booking cannot be `Confirmed`).

### 3. Dependency Injection Lifecycles
- **`Scoped`**: `ApplicationDbContext`, `IUnitOfWork`, `IBookingService`, `IBookingRepository` (aligned with HTTP request lifecycles).
- **`Singleton`**: `IConnectionMultiplexer` (Redis connection pool), rate limiter policies, `PaystackOptions`.
- **`Transient`**: Transient helpers and MediatR handlers.
- **Background Service Scoping**: Hosted services (`BackgroundService`) are registered as Singletons, but create explicit `IServiceScope` instances in `ExecuteAsync` to resolve Scoped repositories cleanly without memory leaks.

---

## 3. Trade-Off Analysis & Why We Chose This Method

| Approach | Pros | Cons | Why BitRoute Chose / Rejected |
|---|---|---|---|
| **Monolithic Anemic CRUD Services** | Fast initial speed; low file count. | Business rules scattered in controllers; database coupled to domain; impossible to unit test without database. | ❌ **Rejected**: Unmaintainable spaghetti architecture. |
| **Microservices from Day 1** | Independent scalability per service. | Distributed transaction complexity (Sagas), network latency, massive DevOps overhead. | ❌ **Overkill**: Premature optimization for initial system scope. |
| **Clean Architecture Monolith** | 100% unit-testable domain core; zero ORM lock-in; compile-time layer boundary defense; clean modularity. | More project files and interface boilerplate. | ✅ **CHOSEN**: Gold standard enterprise .NET architecture. |

---

## 4. Detailed Backend System Design Diagram

```mermaid
flowchart TD
    subgraph Layer 4: API & Composition Root (BitRoute.Api)
        CTRL[Controllers: Bookings, Auth, Schedules, Webhooks]
        HUB[SignalR TelemetryHub /hubs/telemetry]
        MID[ExceptionMiddleware & RateLimiter]
    end

    subgraph Layer 3: Application Use Cases (BitRoute.Application)
        MED[MediatR Mediator & In-Process Bus]
        BS[BookingService]
        AS[AuthService]
        HANDLERS[Event Handlers: PaymentConfirmedHandler, SeatHoldExpiredHandler]
    end

    subgraph Layer 1: Core Domain (BitRoute.Domain)
        ENTITIES[Domain Entities: SeatBooking, Schedule, User]
        VO[Value Object: Segment Overlap Math]
        EVENTS[Domain Events: SeatHoldExpiredEvent]
        EXCEPTIONS[Domain Exceptions: OverlappingBookingException]
    end

    subgraph Layer 2: Infrastructure (BitRoute.Infrastructure)
        EF[ApplicationDbContext: EF Core Npgsql]
        UOW[UnitOfWork: Serializable Transaction & Retry Runner]
        REDIS_STORE[RedisRefreshTokenStore & RedisCacheService]
        PAYSTACK_SRV[PaystackService + Resilience Handler]
        WORKER1[ExpiredHoldSweeper HostedService]
        WORKER2[OutboxProcessor HostedService]
    end

    subgraph Data Stores & External Systems
        PG[(PostgreSQL 17 DB + btree_gist Exclusion Constraint)]
        REDIS[(Redis 7 Cache & Token Store)]
        GATEWAY[Paystack API Server]
    end

    %% Dependency Direction Rules (Inward Only)
    CTRL & HUB & MID --> BS & AS & MED
    BS & AS & HANDLERS --> ENTITIES & VO & EVENTS & EXCEPTIONS
    EF & UOW & REDIS_STORE & PAYSTACK_SRV & WORKER1 & WORKER2 --> BS & AS & ENTITIES
    
    %% Infrastructure Calls
    EF & UOW & WORKER1 & WORKER2 --> PG
    REDIS_STORE --> REDIS
    PAYSTACK_SRV --> GATEWAY
```

---

## 5. Production Code Samples

### Automated Architecture Boundary Enforcement
From `tests/BitRoute.ArchitectureTests/ArchitectureTests.cs`:

```csharp
[Fact]
public void Domain_Should_Not_HaveDependencyOnOtherProjects()
{
    var result = Types.InAssembly(typeof(BitRoute.Domain.Entities.SeatBooking).Assembly)
        .Should()
        .NotHaveDependenciesOn("BitRoute.Application", "BitRoute.Infrastructure", "BitRoute.Api")
        .GetResult();

    Assert.True(result.IsSuccessful, "Domain layer must not depend on external projects.");
}

[Fact]
public void Application_Should_Not_DependOnInfrastructureOrApi()
{
    var result = Types.InAssembly(typeof(BitRoute.Application.Booking.IBookingService).Assembly)
        .Should()
        .NotHaveDependenciesOn("BitRoute.Infrastructure", "BitRoute.Api")
        .GetResult();

    Assert.True(result.IsSuccessful, "Application layer must not depend on Infrastructure or Api.");
}
```

### Domain Aggregate Invariant Protection
From `src/BitRoute.Domain/Entities/SeatBooking.cs`:

```csharp
public sealed class SeatBooking
{
    public Guid Id { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTime HoldExpiresAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }

    public void Confirm(DateTime utcNow)
    {
        if (Status != BookingStatus.Held)
        {
            throw new InvalidBookingStatusException(
                $"Cannot confirm booking in state '{Status}'. Only 'Held' bookings can be confirmed.");
        }

        if (utcNow > HoldExpiresAtUtc)
        {
            throw new BookingHoldExpiredException($"Cannot confirm booking '{Id}' because the hold period has expired.");
        }

        Status = BookingStatus.Confirmed;
        ConfirmedAtUtc = utcNow;
    }
}
```
