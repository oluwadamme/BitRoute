# Production Defense-in-Depth & System Resilience Architecture

This document details BitRoute's production hardening layer, incorporating ASP.NET Core Rate Limiting policies, HTTP gateway resilience handlers, Redis sliding refresh-token rotation, and Docker Compose orchestration.

---

## 1. Explained for a 12-Year-Old

Imagine a high-security theme park:

1. **The Bouncer at the Gate (Rate Limiter)**: If a single person tries to run through the ticket gate 100 times in one second, the bouncer steps in, stops them, and hands them a polite note saying *"Too fast! Please wait a minute before trying again."* (`HTTP 429 Too Many Requests`).
2. **The Patient Delivery Truck (HTTP Resilience)**: When our system calls Paystack to check a payment, what if the phone line crackles for a split second? Instead of giving up immediately, our delivery truck tries 3 times with exponential patience (retries), and if the phone line is completely down, it trips a safety switch (`Circuit Breaker`) so workers don't waste time waiting!
3. **The One-Time Keycard (Redis Token Rotation)**: When you log in, you get a special keycard (`Refresh Token`). Every time you use it to get a new pass, the system destroys the old keycard and gives you a brand new one. If a thief steals your old keycard and tries to use it, the system detects the theft and immediately revokes all keycards in your family!
4. **The Container Village (Docker Compose)**: All our workers (API, Database, Redis, Web Server) live in tidy, isolated houses (`Docker Containers`). The API house won't open its doors until the Database house reports that it's healthy and ready (`pg_isready`)!

---

## 2. Deep-Dive Interview Defense Guide

### Technical Explanation

### 1. ASP.NET Core Rate Limiting Policies
BitRoute implements defense-in-depth API rate limiting using ASP.NET Core 8+ built-in rate limiters configured in `Program.cs`:
- **`AuthPolicy`**: 10 requests / minute (IP-based) on `/auth/login`, `/auth/register`, and `/auth/refresh` to prevent credential stuffing and brute-force attacks.
- **`HoldPolicy`**: 30 requests / minute on `/bookings` and `/bookings/{id}/pay` to prevent automated seat inventory hoarding.
- **`PublicSearchPolicy`**: 60 requests / minute on `/schedules/search` to protect read queries.

**Custom Rejection Envelope**: Standard ASP.NET Core rate limiting returns a blank 429 status code. BitRoute overrides `OnRejected` to write a consistent `ApiResponse.Error("Rate limit exceeded. Please try again later.")` JSON envelope.

### 2. HTTP Gateway Resilience (`Microsoft.Extensions.Http.Resilience`)
Third-party HTTP calls (e.g. Paystack API) are inherently network-unreliable. BitRoute attaches `.AddStandardResilienceHandler()` to `IPaystackService`:
- **Retry Strategy**: 3 attempts with exponential backoff and jitter.
- **Circuit Breaker**: Trips if >50% of requests fail over a 30s sampling window, preventing cascading thread starvation.
- **Timeout Strategy**: Enforces a strict 10-second per-attempt timeout.

### 3. Redis Sliding Refresh-Token Rotation & Reuse Detection
- Access tokens expire in 60 minutes.
- Refresh tokens carry a 30-day sliding window stored in Redis.
- **Rotation**: Consuming a refresh token invalidates it and issues a new token in the same `SessionFamilyId`.
- **Reuse Detection**: If an already-consumed refresh token is presented, BitRoute detects a potential token theft attack, revokes the entire `SessionFamilyId` in Redis, and forces full re-authentication.

### 4. Multi-Container Orchestration (`docker-compose.yml`)
Four production services are orchestrated with strict healthcheck dependencies:
- `postgres` (PostgreSQL 17-alpine with `pg_isready` check).
- `redis` (Redis 7-alpine with `redis-cli ping` check).
- `api` (.NET 10 Web API dependent on `postgres` and `redis` being `service_healthy`).
- `web` (Vite 6 + Nginx Alpine frontend static web server).

---

## 3. Trade-Off Analysis & Why We Chose This Method

| Approach | Pros | Cons | Why BitRoute Chose / Rejected |
|---|---|---|---|
| **No Rate Limiting** | Zero latency overhead; simple code. | Vulnerable to Denial-of-Service (DoS), brute force, and inventory scraping. | ❌ **Rejected**: Unacceptable security risk. |
| **Unbounded HTTP Retries** | Tries forever until external API responds. | Causes thread starvation and request queue buildup if external service is down. | ❌ **Rejected**: Causes cascading application failure. |
| **Database-Stored Refresh Tokens** | Simple EF Core query. | Requires heavy DB write load on every token refresh; slow expiration cleanup. | ❌ **Rejected**: High database IOPS overhead. |
| **ASP.NET Rate Limiting + Resilience Handlers + Redis Tokens** | Defense-in-depth protection; zero DB thrashing; automatic HTTP fault tolerance; production security. | Requires Redis infrastructure; slight configuration complexity. | ✅ **CHOSEN**: Production standard for cloud microservices. |

---

## 4. Mermaid System Flowchart

```mermaid
flowchart TD
    CLIENT[HTTP Client / Attacker] -->|1. Incoming HTTP Request| RL{Rate Limiter: AuthPolicy / HoldPolicy}
    RL -->|Rate Exceeded (>10/min)| 429[Write ApiResponse.Error 429 Too Many Requests]
    RL -->|Within Limit| API[BitRoute API Endpoint]
    
    API -->|Call Paystack API| RESILIENCE[Polly Standard Resilience Handler]
    RESILIENCE -->|Attempt 1 Fail| BACKOFF[Exponential Backoff + Jitter] --> RESILIENCE
    RESILIENCE -->|Circuit Open| CB_FAIL[Fail Fast: CircuitBreakerOpenException]
    RESILIENCE -->|Success| PS_GATEWAY[Paystack API Server]
    
    API -->|Auth Refresh Request| REDIS[(Redis Refresh Token Store)]
    REDIS -->|Check Token State| STATE{Token Valid?}
    STATE -->|Already Used!| REVOKE[Revoke Session Family & Reject]
    STATE -->|Valid| ROTATE[Rotate Token & Extend 30-Day Window]
```

---

## 5. Production Code Samples

### Rate Limiter Configuration & Custom 429 Envelope
From `src/BitRoute.Api/Program.cs`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var response = ApiResponse.Error("Rate limit exceeded. Please try again later.");
        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };

    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("HoldPolicy", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});
```

### HTTP Gateway Resilience Registration
From `src/BitRoute.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddHttpClient<IPaystackService, PaystackService>()
    .AddStandardResilienceHandler();
```

### Docker Compose Healthcheck Configuration
From `docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:17-alpine
    container_name: bitroute-postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U bitroute -d bitroute"]
      interval: 5s
      timeout: 3s
      retries: 10

  redis:
    image: redis:7-alpine
    container_name: bitroute-redis
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 10

  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: bitroute-api
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
```
