# The refresh-token system, explained simply

This document explains how BitRoute keeps users logged in securely. It is written to be
readable without a security background. The design is **rotation with reuse detection**,
the approach recommended by the OAuth 2.0 Security Best Current Practice and used by
providers like Auth0 and Okta.

## The problem: staying logged in is dangerous

When you log into BitRoute with your email and password, the server gives you an
**access token**. Think of it as a wristband at a water park: the staff at every ride
just glances at it and lets you through. Nobody re-checks your ID at every slide.
That is what makes it fast.

But wristbands can be stolen. If someone photographs your wristband, they can walk
around pretending to be you. So there is a rule: **wristbands expire after 60 minutes**
(configurable via `Jwt__AccessTokenMinutes`). A thief only gets one hour of mischief.

That creates a new problem. If the wristband dies every hour, do you have to walk back
to the front gate and show your ID (log in with your password) every hour? Nobody wants
to type their password 10 times a day.

## The fix: a second kind of ticket

The park gives you two things when you enter:

- The **wristband** (access token): fast, checked everywhere, dies in 60 minutes.
- A **golden ticket** (refresh token): kept hidden in your pocket. When your wristband
  expires, you go to a kiosk, show the golden ticket, and get a fresh wristband without
  showing your ID again. The golden ticket is good for 30 days
  (`Jwt__RefreshTokenDays`).

Wristband = short and disposable. Golden ticket = long-lived and precious.

If a golden ticket gets stolen, the thief can keep getting fresh wristbands for 30 days.
The danger has moved from the wristband to the ticket, so the rest of the system exists
to protect the golden ticket. There are five protections.

## Protection 1: every golden ticket works only once

When you use your golden ticket at the kiosk, the kiosk takes it, stamps it **USED**,
and hands you a brand new golden ticket along with your fresh wristband. You never use
the same golden ticket twice: ticket #1 gets you ticket #2, #2 gets you #3, and so on.

This is called **rotation**. In the code it is `Consume()` (stamp the old one) and
`IssueNextInSession()` (mint the next one) on the
[RefreshToken entity](../src/BitRoute.Domain/Entities/RefreshToken.cs).

Each new ticket is good for 30 days from that moment, so as long as you visit at least
once a month, your session slides forward forever. That is the **sliding window**.

## Protection 2: the ticket family, and catching thieves

All the tickets in a chain (#1, #2, #3, ...) belong to the same **family**. In the code
this is the `SessionId`: every ticket in the chain carries the same family id, like a
surname.

Suppose a thief secretly copies your ticket #2. You, innocent, use ticket #2 at the
kiosk and get ticket #3. Later the thief shows up with the copy of #2. The kiosk sees
that #2 is stamped USED and concludes: this ticket was copied, there is a thief in this
family.

The response is drastic on purpose: the kiosk cancels the **entire family**, including
the fresh #3 in your pocket. When a used ticket shows up, the kiosk cannot tell whether
the person holding the newest ticket is you or the thief (the thief may have used the
copy first, in which case the thief holds #3 and you hold the dud). The only safe move
is to cancel everything and make the real owner log in again with their password. One
minor annoyance for you, total shutdown for the thief.

In the code, a replayed ticket makes `Consume()` throw
[RefreshTokenReuseException](../src/BitRoute.Domain/Exceptions/RefreshTokenReuseException.cs),
which carries the `SessionId` so the caller knows which family to cancel, and
`RevokeSessionAsync` on the
[store](../src/BitRoute.Infrastructure/Identity/RedisRefreshTokenStore.cs) is the
cancel-the-family button.

## Protection 3: used tickets are kept, not shredded

Why not just shred a ticket once it is used? Because of the thief-catching trick above.
If ticket #2 were shredded after use, the thief's copy would simply be rejected as
"never heard of it". No alarm would fire, the family would survive, and a thief who
rotated first would keep their stolen ticket.

Instead, used tickets go into a USED drawer and stay there until the date they would
have naturally expired. Any replay within that window is recognized and raises the
alarm.

## Protection 4: the store never holds the actual ticket

Records live in **Redis**, a fast in-memory store. But the tickets themselves are never
stored. Each record is keyed by the ticket's **fingerprint**: a SHA-256 **hash** of the
raw value. A fingerprint identifies a ticket perfectly, but the ticket cannot be rebuilt
from it. If the whole store ever leaked, the attacker would hold a pile of fingerprints,
none of them usable to get a wristband.

This is safe because raw tokens are 256 bits of cryptographic randomness (created in
[RefreshTokenCrypto](../src/BitRoute.Infrastructure/Identity/RefreshTokenCrypto.cs)),
so they cannot be guessed the way weak passwords can. The raw value goes only to the
client.

Redis also cleans up after itself: every record is written with a **TTL** (time to
live) matching the ticket's expiry, so expired records vanish automatically.

## Protection 5: two people at once (the race condition)

The sneakiest attack: you and the thief present ticket #2 at two different kiosks at
literally the same instant. Kiosk A checks the drawer: not used, fine. Kiosk B checks
at the same moment: not used, fine. Both hand out fresh tickets, and the thief slips
through, because both checks happened before either stamp landed. This
check-then-act gap is a classic **race condition** (time-of-check to time-of-use).

The fix is Redis's **SET NX** ("set if not exists"), an **atomic** operation: the check
and the write happen as one indivisible motion, so there is no gap. Both kiosks try to
place the USED marker; Redis guarantees exactly one wins, and the loser is treated as a
replay, which fires the family alarm.

This is the same class of problem as two passengers grabbing the same seat at the same
instant, which is the heart of BitRoute. Here SET NX closes the gap; on the booking
path a Serializable transaction plus a database exclusion constraint do. Same disease,
same class of cure.

## Who does what

| Piece | Layer | Job |
| --- | --- | --- |
| [RefreshToken](../src/BitRoute.Domain/Entities/RefreshToken.cs) | Domain | The rulebook: validity, rotation, when to raise the reuse alarm. Pure logic, unit-tested. |
| [IRefreshTokenStore](../src/BitRoute.Domain/Interfaces/IRefreshTokenStore.cs) / [IRefreshTokenCrypto](../src/BitRoute.Domain/Interfaces/IRefreshTokenCrypto.cs) | Domain | The contracts the inner layers program against. |
| [RedisRefreshTokenStore](../src/BitRoute.Infrastructure/Identity/RedisRefreshTokenStore.cs) | Infrastructure | The kiosk and filing cabinet: persistence, TTLs, the atomic USED marker, family revocation. |
| [RefreshTokenCrypto](../src/BitRoute.Infrastructure/Identity/RefreshTokenCrypto.cs) | Infrastructure | Makes raw tickets (crypto-random) and fingerprints (SHA-256). |
| Auth service (Phase 2, Application layer) | Application | The clerk: hash the presented ticket, consume it, catch the reuse alarm and revoke the family, or issue the next ticket plus a fresh wristband. |

## Redis key layout

```
refresh-token:{hash}     -> JSON document of one token, TTL = token expiry
refresh-consumed:{hash}  -> USED marker (SET NX target), TTL = token expiry
refresh-session:{id}     -> set of token hashes in the family, TTL slides with the newest token
```

## A full timeline

1. July 7: Ada logs in with her password. She gets a wristband (60 min) and golden
   ticket T1 (family F, expires Aug 6).
2. 1pm: wristband dead. She presents T1. T1 is stamped USED, she gets a new wristband
   plus T2 (family F, expires Aug 6, 1pm).
3. Weeks pass: T2 becomes T3, then T4, ... family F slides along.
4. A thief copies T7 and uses it first, getting T8. Ada then presents her T7: used
   ticket alarm. Family F is cancelled entirely, including the thief's T8. Ada logs in
   with her password and starts fresh family G.

## Tests that pin this behavior

- [RefreshTokenTests](../tests/BitRoute.Domain.Tests/Entities/RefreshTokenTests.cs):
  the domain rules (rotation keeps the family, reuse throws, revoke is idempotent,
  expiry).
- [RedisRefreshTokenStoreTests](../tests/BitRoute.Infrastructure.Tests/Identity/RedisRefreshTokenStoreTests.cs):
  the store against real Redis via Testcontainers, including firing concurrent consume
  attempts at one token and asserting exactly one wins.
