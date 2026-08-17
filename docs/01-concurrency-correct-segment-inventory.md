# Concurrency-Correct Segment Inventory

This document explains how BitRoute handles segment-based seat booking under high concurrent load without double-booking or wasting seat capacity.

---

## 1. Explained for a 12-Year-Old

Imagine a bus travelling from **Lagos (Stop 0)** to **Ibadan (Stop 1)** to **Ilorin (Stop 2)** to **Abuja (Stop 3)**.

- **Passenger A** buys a ticket on **Seat 1** from **Lagos to Ibadan** (Stop 0 to 1).
- **Passenger B** buys a ticket on **Seat 1** from **Ibadan to Abuja** (Stop 1 to 3).

Can Passenger A and Passenger B share Seat 1? **Yes!** Because when Passenger A gets off at Ibadan, the seat becomes empty just in time for Passenger B to sit down.

Now imagine **Passenger C** tries to buy **Seat 1** from **Lagos to Ilorin** (Stop 0 to 2) at the exact same millisecond that Passenger A buys his ticket.
If both tickets were sold, Passenger A and Passenger C would collide between Lagos and Ibadan and try to sit on each other's laps!

**BitRoute's job** is to act like a super-fast, super-smart bus conductor who lets Passenger A and B share the seat, but stops Passenger C from double-booking Seat 1 at the exact same split-second.

---

## 2. Deep-Dive Interview Defense Guide

### Technical Explanation

BitRoute's inventory model represents route stops as integer indices ($0, 1, 2, 3, \dots$). A passenger journey is defined by a half-open interval $[i, j)$, where $i = \text{BoardingIndex}$ and $j = \text{AlightingIndex}$.

Two bookings on the same physical seat and schedule conflict if and only if their intervals overlap:
$$\text{ExistingStart} < \text{RequestedEnd} \quad \text{AND} \quad \text{ExistingEnd} > \text{RequestedStart}$$

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
      AND b.boarding_index < @requestedEnd
      AND b.alighting_index > @requestedStart
  );
```

### The Race Condition Under Concurrency

Under high multi-threaded traffic, two concurrent requests ($T_1$ and $T_2$) for overlapping legs both read the seat as available simultaneously in read committed isolation. Both requests proceed to execute `INSERT INTO seat_bookings`. A standard database `UNIQUE(schedule_id, seat_id, travel_date)` constraint **fails** to catch this collision because the numerical interval values (e.g. $[0, 2)$ and $[1, 3)$) are non-identical.

### Defense-in-Depth Solution

BitRoute employs a dual-defense architecture:

1. **Serializable Isolation with Retry Loop**:
   Application transactions execute under `IsolationLevel.Serializable`. PostgreSQL's Serializable Snapshot Isolation (SSI) tracks read/write dependencies (SIREAD locks) and detects read-write conflicts, aborting the colliding transaction with SQLSTATE `40001` (`serialization_failure`). BitRoute's `UnitOfWork` catches `40001` and executes exponential backoff retries.

2. **PostgreSQL Exclusion Constraint (`btree_gist`)**:
   As a hard database backstop that guarantees zero double-bookings even if an application bug bypasses transaction isolation, we define a PostgreSQL range exclusion constraint using PostgreSQL's GiST index and `int4range`:

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

---

## 3. Trade-Off Analysis & Why We Chose This Method

| Approach | Pros | Cons | Why BitRoute Chose / Rejected |
|---|---|---|---|
| **Pessimistic Row Locking (`SELECT FOR UPDATE`)** | Simple to implement, avoids transaction retries. | Massive database lock contention; serializes all requests trying to book *any* seat on the vehicle. | ❌ **Rejected**: Kills throughput on popular departures. |
| **Pre-Materialized Leg Seat Rows** | Plain `UNIQUE(schedule_id, seat_id, leg_index)` constraint work. | Explosive database row growth (e.g., 50 seats $\times$ 20 legs = 1,000 rows per departure); rigid schema. | ❌ **Rejected**: Heavy storage penalty and inefficient querying. |
| **Serializable Transactions Only** | Clean application abstraction. | If developer slips or bypasses transaction scope, data corruption occurs. | ⚠️ **Partial Solution**: Used, but paired with DB exclusion constraint. |
| **Serializable + `btree_gist` Exclusion Constraint** | 100% mathematical guarantee against double-booking; high concurrency read performance; clean interval math. | Requires PostgreSQL `btree_gist` extension; requires serialization retry loop logic. | ✅ **CHOSEN**: Maximum safety, high concurrency, and production elegance. |

---

## 4. Mermaid System Flowchart

```mermaid
flowchart TD
    REQ[Client Request: Book Seat 1 [0, 2)] --> UOW[UnitOfWork: Begin Serializable Transaction]
    UOW --> CHK[Query Seat Availability: Overlap Check]
    CHK --> AVAIL{Seat Free?}
    AVAIL -->|No| ERR[Return 409 Conflict: Seat Occupied]
    AVAIL -->|Yes| INS[Insert SeatBooking: HELD]
    INS --> COMMIT[Commit Transaction]
    COMMIT --> EXCL{PostgreSQL GiST Overlap Check}
    EXCL -->|Overlap Violation| ABORT[SQLSTATE 40001: Serialization Failure]
    ABORT --> RETRY{Retries < 3?}
    RETRY -->|Yes| DELAY[Exponential Backoff Delay] --> UOW
    RETRY -->|No| FAIL[Throw ConcurrencyException]
    EXCL -->|Pass| SUCCESS[Return 201 Created: Booking HELD]
```

---

## 5. Production Code Samples

### Domain Value Object: Half-Open Overlap Check
From `src/BitRoute.Domain/ValueObjects/Segment.cs`:

```csharp
public sealed record Segment
{
    public int BoardingIndex { get; }
    public int AlightingIndex { get; }

    public Segment(int boardingIndex, int alightingIndex)
    {
        if (boardingIndex >= alightingIndex)
            throw new InvalidSegmentException("Boarding index must be strictly less than alighting index.");

        BoardingIndex = boardingIndex;
        AlightingIndex = alightingIndex;
    }

    public bool OverlapsWith(Segment other)
    {
        // Half-open interval overlap: [A, B) overlaps [C, D) iff A < D AND B > C
        return BoardingIndex < other.AlightingIndex && AlightingIndex > other.BoardingIndex;
    }
}
```

### Infrastructure Retry Loop: Catching SQLSTATE `40001`
From `src/BitRoute.Infrastructure/Persistence/UnitOfWork.cs`:

```csharp
public async Task<T> ExecuteInTransactionAsync<T>(
    Func<Task<T>> action,
    IsolationLevel isolationLevel = IsolationLevel.Serializable,
    CancellationToken cancellationToken = default)
{
    const int maxRetries = 3;
    var retryDelay = TimeSpan.FromMilliseconds(50);

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            var result = await action();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException ex) when (IsSerializationFailure(ex) && attempt < maxRetries)
        {
            await transaction.RollbackAsync(cancellationToken);
            await Task.Delay(retryDelay, cancellationToken);
            retryDelay *= 2; // Exponential backoff
        }
    }
    throw new ConcurrencyException("Could not complete operation due to concurrent updates.");
}
```
