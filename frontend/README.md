# Northstar Banking UI

Standalone Angular 22 client for the Bank Account Management API.

## Development

Start the API on `http://localhost:8080`, then run:

```bash
npm install
npm start
```

Open `http://localhost:4200`. The Angular development proxy forwards `/api` and `/health` to the local ASP.NET Core service.

## Checks

```bash
npm test
npm run build
```

Authentication data is stored in browser `sessionStorage`, so it is scoped to the current tab and cleared on sign-out, token expiry, or an API `401` response. An ambiguously completed transfer keeps only its user ID, account IDs, amount, request fingerprint, and idempotency key in user-scoped `localStorage` until an exact retry reaches a definitive result; no token or credentials are copied into that recovery record. The record intentionally survives tab closure and sign-out for same-user recovery. That is a privacy tradeoff on a shared browser, but cross-user or malformed records are ignored.

Account lists use bounded keyset pages (`items` plus an opaque `nextCursor`, with a 1–100 item limit). Money values are capped at `$9,000,000,000,000.00` so cent values remain safe across JSON and JavaScript round trips.
