# VicxelStore

Multi-vendor digital product marketplace (spec codename: DPM). Creators sell
downloadable digital products with verified sellers, secure delivery, licensing,
automated tax and commission handling, an auditable double-entry ledger, and
seller payouts.

The full specification lives in the companion repository
`VicxelStore-Project-Docs`; this repository implements it.

## Monorepo layout

The specification (`10-repository-plans.md`) describes three repositories
(`dpm-api`, `dpm-db`, `dpm-ui`). By explicit owner decision this project uses a
**single repository** holding all three layers as top-level folders. The
internal structure of each layer follows the per-repository plan unchanged.

| Folder | Spec repo | Contents |
|--------|-----------|----------|
| `db/`  | `dpm-db`  | SQL Server schema migrations, programmability (procs/functions/views), reference + demo seed, deploy scripts |
| `api/` | `dpm-api` | .NET 8 ASP.NET Core Web API — Clean Architecture modular monolith |
| `ui/`  | `dpm-ui`  | React 18 + TypeScript (strict) SPA built with Vite |

> Recorded assumption (per spec `11 §14`): the three-repo plan is collapsed into
> one repo; folder boundaries carry the same isolation rules the repo boundaries
> did. CI runs each layer as an independent job.

## Technology (locked by spec `README §1`)

- **API:** .NET 8, ASP.NET Core, Clean Architecture modular monolith, MediatR
  CQRS, FluentValidation, EF Core 8 (writes) + Dapper (hot reads), ASP.NET Core
  Identity-style auth with JWT (15 min) + rotating refresh tokens (14 days,
  reuse detection), Hangfire, Redis, S3-compatible object storage, Swashbuckle.
- **DB:** SQL Server 2022. Money is `DECIMAL(19,4)` + ISO-4217 currency code,
  never float. Internal `BIGINT` ids; external `UNIQUEIDENTIFIER` `PublicId`.
  All timestamps UTC (`DATETIME2(3)`).
- **UI:** React 18 + TypeScript strict, Vite, React Router v6 data routers,
  TanStack Query, Zustand, React Hook Form + Zod, React-Bootstrap, AG Grid,
  react-i18next (`en`, `ta`).

## Getting started

Prerequisites: Docker, .NET 8 SDK, Node 20+.

```bash
# infrastructure (SQL Server, Redis, MinIO, MailHog, ClamAV)
docker compose up -d

# database: apply migrations, programmability and reference seed
cd db && ./deploy/deploy.sh --demo

# api
cd api && dotnet run --project src/Dpm.Api

# ui
cd ui && npm install && npm run dev
```

The API serves Swagger UI at `/swagger` in non-production environments.

## Development conventions (binding, from spec `11B`)

- Database-first per feature: schema, migration, seed, repository, domain,
  application, api, ui — in that order.
- Strict module dependency direction: `Domain <- Application <- Infrastructure/Api`.
  No module touches another module's tables or infrastructure; integration is
  via published interfaces and domain events only.
- All money movement goes through the append-only double-entry ledger; balances
  are derived, never stored as an editable column.
- APIs expose only `PublicId` GUIDs; internal `BIGINT` ids never leave the API.
- State-mutating payment/order/payout endpoints require an `Idempotency-Key`.
- Trunk-based development; Conventional Commits; no direct commits to `main`.
- No emojis in code, comments, commit messages, or identifiers.

## Milestones

| Milestone | Scope | Status |
|-----------|-------|--------|
| M0 | Monorepo scaffolding, CI, docker-compose | Done |
| M1 | Identity end-to-end: register, verify, login, refresh, `/me` + UI auth | Done (email delivery logs to MailHog in dev) |
| M2 | Stores, files (upload + scan), catalog publish with moderation | Pending |
| M3 | Cart, checkout (one provider), orders, licenses, ledger, downloads | Pending |
| M4 | Wallet, hold expiry, payouts, reviews, email notifications | Pending |
| M5 | Admin portal, reports, reconciliation | Pending |
| M6 | Hardening: rate limits, security headers, perf, E2E | Pending |
