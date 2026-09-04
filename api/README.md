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

Remaining modules (Orders, Payments, Ledger, Downloads, Reviews, Notifications,
Administration) land in milestone order per spec `11B section 3`.

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
