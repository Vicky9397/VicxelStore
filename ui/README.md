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
| `/` | Landing page |
| `/search` | Browse with category and sort filters held in the query string |
| `/p/:slug` | Product detail with selectable license variants |
| `/s/:slug` | Store front |
| `/login`, `/register`, `/verify-email` | Implemented against the Identity module |
| `/account` | Buyer dashboard shell behind `RequireAuth` |
| `/seller/store` | Store creation and the KYC, tax and payout onboarding steps |
| `/seller/products` | The seller's catalog with submit, publish and unpublish |
| `/seller/products/new` | Draft creation with variants |
| `/seller/products/:productId` | Per-variant file upload and scan status |
| `/cart` | Cart with live totals and save-for-later |
| `/checkout` | Quote with regional tax, then confirm |
| `/checkout/return` | Post-payment landing that polls until the capture settles |
| `/account/purchases` | Order history |
| `/account/orders/:orderId` | Order detail with licenses and downloads |
| `/403`, `*` | Forbidden and not-found pages |

Remaining routes (wishlist, messaging, admin) follow the milestone order in
spec `11B section 4`.

## Checkout

The buyer gets a quote before committing, and the total they saw is sent back
with the confirmation, so a price that moved underneath them stops the charge
instead of surprising them. Each attempt carries a fresh idempotency key that is
reused across retries, so a dropped response cannot become a second order.

The return page does not claim success: capture is confirmed by the provider's
webhook, so the page polls until the order actually settles.

## File upload

`features/seller/api/chunkedUpload.ts` drives the resumable upload: it hashes
the file, declares it, sends only the parts the server still reports missing,
then completes. Because the server returns the outstanding parts on every
response, an interrupted upload resumes rather than restarting. Scanning is
asynchronous, so the uploader polls scan status and reports Pending, Clean or
Infected rather than claiming success at upload time.
