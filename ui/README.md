# ui — React 18 + TypeScript SPA (spec repo plan 10.2, frontend architecture 07)

Vite-built single-page app. TypeScript runs in strict mode with
`noUncheckedIndexedAccess` and `exactOptionalPropertyTypes`; `any` is a lint
error.

## Structure

```
ui/src/
  app/            App shell, providers, router
  features/       Vertical slices mirroring API modules
    auth/         { api/ hooks/ pages/ store.ts }
    account/      Buyer dashboard shell
    catalog/      Storefront shell
  components/     ui/ forms/ layout/ — shared and feature-agnostic
  lib/            apiClient, queryClient, i18n, zodSchemas
  locales/        en.json, ta.json
  routes/         Route guards and error pages
  styles/         Design tokens and Bootstrap overrides
  test/           Vitest setup and MSW handlers
  types/          API contract types
```

No feature imports another feature's internals; sharing happens through
`components/` and `lib/`.

## Run

```bash
npm install
npm run dev        # http://localhost:5173, proxies /api to http://localhost:5080
```

| Script | Purpose |
|--------|---------|
| `npm run dev` | Vite dev server with API proxy |
| `npm run build` | Type-check the project references, then produce `dist/` |
| `npm run typecheck` | Type-check without emitting |
| `npm run lint` | ESLint with type-aware rules |
| `npm test` | Vitest with MSW-mocked network |

## Auth handling

- The access token lives in memory in `lib/apiClient`, never in `localStorage`,
  so an XSS payload cannot read it back later.
- The refresh token is an httpOnly `SameSite=Strict` cookie set by the API; the
  browser sends it only to `/api/v1/auth`.
- On a cold load the app calls the refresh endpoint once to rebuild the session;
  on a 401 the client attempts exactly one silent refresh, replays the request,
  and otherwise clears the session and routes to login.
- `authStore` (Zustand) holds session state only. Server data lives in the
  TanStack Query cache; the two never duplicate each other.

## Validation

Zod schemas in `lib/zodSchemas` mirror the API's FluentValidation rules, so both
sides agree on password length, the email-local-part rule and field limits.
Server-side `problem+json` field errors are merged into the same form error
surface as client-side validation.

## Internationalization

`react-i18next` with English and Tamil bundled. All user-facing strings resolve
through `t()`; the header carries a locale toggle.

## Pages

| Route | State |
|-------|-------|
| `/` | Storefront shell (catalog lands in M2) |
| `/login`, `/register`, `/verify-email` | Implemented against the Identity module |
| `/account` | Buyer dashboard shell behind `RequireAuth` |
| `/403`, `*` | Forbidden and not-found pages |

Remaining routes (search, product detail, cart, checkout, purchases, seller,
admin) follow the milestone order in spec `11B section 4`.
