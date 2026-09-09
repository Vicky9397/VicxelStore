# api — .NET 8 modular monolith (spec repo plan 10.1)

ASP.NET Core Web API built as a Clean Architecture modular monolith. Each module
is a vertical slice (`Domain`, `Application`, `Infrastructure`, `Api`) that can be
extracted into its own service later without a rewrite.

## Structure

```
api/
  Dpm.sln
  Directory.Build.props        # net8.0, nullable, warnings-as-errors, analyzers
  src/
    Dpm.Api/                   # composition root: middleware, DI, auth, Swagger
    Dpm.BuildingBlocks/        # shared kernel: Result, Error, Money, events, behaviors
    Modules/Identity/          # Domain / Application / Infrastructure / Api
  tests/
    Dpm.UnitTests/             # domain invariants + application handlers
  docker/Dockerfile
```

## Dependency rule (enforced)

`Domain <- Application <- Infrastructure`, `Domain <- Application <- Api`. Domain
references nothing. No module references another module's Infrastructure; cross-
module integration goes through published interfaces and domain events only.

## Modules

| Module | State |
|--------|-------|
| BuildingBlocks | Result/Error, Money, Entity/AggregateRoot/ValueObject, domain events, outbox message, cross-module integration events, validation + logging pipeline behaviors |
| SharedApi | problem+json translation and the named authorization policies |
| Identity | Register, verify email, resend verification, login, refresh (rotating, reuse-detecting), logout, `/me` |
| Marketplace | Store creation and branding, KYC/tax/bank onboarding, staff verification, suspension and reinstatement |
| Catalog | Products, variants, versions, categories, tags, the draft-to-published lifecycle with moderation, and storefront read queries |
| Files | Resumable chunked upload with checksum verification, quarantine storage, and scan-gated availability |
| Orders | Cart, checkout quote and idempotent confirm, orders, licenses issued on capture |
| Payments | Provider abstraction, signature-verified webhooks, exactly-once inbox, idempotency store |
| Ledger | Append-only double-entry journal, sale posting, derived wallet balances, reconciliation |
| Downloads | Entitlement, quota and scan gates, short-lived signed URLs, download logging |
| Outbox | Dispatcher delivering domain events after commit, with retry and parking |

Remaining modules (Reviews, Notifications, Administration, Analytics, CMS,
Messaging) land in milestone order per spec `11B section 3`.

## The money path

A purchase moves through four modules, and the ordering is what keeps it safe:

1. **Checkout** re-prices the cart against the live catalog, resolves each
   line's commission, computes tax for the billing region, and places a Pending
   order. It requires an `Idempotency-Key`, so a retry returns the first result
   rather than placing a second order.
2. **Payments** creates the provider intent. Nothing is captured here; a client
   cannot assert that it paid.
3. A **signature-verified webhook** is the only path that captures. It is
   deduplicated by provider event id, and the capture commits in the same
   transaction as its outbox event.
4. The **outbox dispatcher** delivers that event to Orders, which issues one
   license per line, and to Ledger, which posts the balanced journal. Delivery
   is at-least-once and retried until it succeeds, so a capture is never left
   without its ledger row.

Every consumer on that path is replay-safe: a redelivered capture issues no
second license, posts no second journal entry, and announces nothing twice.

Balances are never stored. A seller's wallet is summed from `ledger.LedgerLines`
on every read, and the reconciliation query reports any transaction whose debits
and credits disagree, which must always be none.

### Payment providers

`IPaymentProvider` is the whole surface a gateway needs to implement. Two
adapters ship today: a sandbox provider that signs its own webhooks so the
verification, inbox and ledger path runs for real without a network call, and
manual bank transfer, which an administrator confirms.

A hosted gateway (Stripe or Razorpay) is the same interface plus live keys.
That adapter is deliberately not written blind: payment code that has never been
exercised against the real provider should not look production-ready.

### Cross-module wiring

The spec fixes the dependency direction as
`Identity -> Marketplace -> Catalog -> Files` and forbids reversing an arrow.
Each module publishes a `Contracts` assembly holding only interfaces and DTOs,
which the modules downstream of it reference; no module reads another module's
tables.

Because Files sits below Catalog, a scan outcome cannot travel back up as a
direct reference. It travels as the `FileScanned` integration event, defined in
the shared kernel that both already depend on, and Catalog projects it into its
own variant readiness table. The event carries the resulting clean-file total
rather than a delta, so replaying it under at-least-once delivery converges
instead of double-counting.

## Run

```bash
docker compose up -d                      # from the repo root
cd ../db && ./deploy/deploy.sh            # schema + reference seed
cd ../api && dotnet run --project src/Dpm.Api
```

Swagger UI is served at `/swagger` outside production. Health probes are
`/health/live` and `/health/ready`.

## Configuration

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:Database` | SQL Server connection string |
| `Jwt:Issuer` / `Jwt:Audience` | Token issuer and audience |
| `Jwt:AccessTokenMinutes` | Access token lifetime (15) |
| `Jwt:RefreshTokenDays` | Refresh token lifetime (14) |
| `Jwt:RsaPrivateKeyPem` | RS256 signing key; absent means an ephemeral dev key |
| `Smtp:Host` / `Smtp:Port` | Transactional email; empty means emails are logged, not sent |
| `App:WebBaseUrl` | Base URL used to build links in emails |
| `Cors:AllowedOrigins` | Allowed browser origins |

No secrets belong in the repository; supply them through environment variables
or the environment's secret store.

## Auth model

- Access token: JWT RS256, 15 minutes, carries `sub` (PublicId), email,
  `email_verified` and role claims.
- Refresh token: opaque 64-byte random value, 14 days, stored only as a SHA-256
  hash, delivered in an httpOnly `SameSite=Strict` cookie scoped to
  `/api/v1/auth`. Every refresh rotates the token within its family; presenting
  an already-rotated token revokes the whole family (theft detection).
- Authorization is default-deny: the fallback policy requires an authenticated
  user, and anonymous endpoints opt in explicitly.

## Tests

```bash
dotnet test
```

Covers domain invariants (lockout thresholds and backoff, refresh-token rotation
and reuse, Money precision and currency safety) plus the auth handler flows
(registration without account-existence disclosure, hash-only token storage,
verification TTL and single use, resend rate limiting, family revocation).
