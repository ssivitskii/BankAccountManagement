# Bank Account Management

A production-oriented ASP.NET Core backend and Angular client for authenticated bank-account management. Customers can manage only their own accounts, while administrators can provision users and operate across account ownership boundaries. PostgreSQL persistence, explicit transactions, row locking, and integration tests make the important money paths reproducible locally.

## Features

- Customer registration and login with hashed passwords and signed JWT bearer tokens.
- Responsive Angular dashboard with account creation, bounded account and ledger pagination, deposits, withdrawals, and transfers.
- Admin and Customer roles with ownership authorization.
- Ownership-scoped, keyset-paginated account listing plus account creation, details, balance, deposit, withdrawal, atomic transfer, and keyset-paginated ledger history.
- Transfer-scoped idempotency with request fingerprints, replay detection, and payload-conflict rejection.
- Bounded account statements as JSON or invariant CSV with opening and closing balances.
- PostgreSQL persistence through EF Core migrations and database constraints.
- Atomic balance mutations with `SELECT ... FOR UPDATE` to prevent double spending.
- RFC-compatible Problem Details, authentication rate limiting, request validation, audit-oriented structured logging, Development-only Swagger, and liveness/readiness checks.
- Unit tests plus end-to-end HTTP tests against a Testcontainers PostgreSQL instance.

## Tech Stack

C# · .NET 9 · ASP.NET Core Web API · Entity Framework Core 9 · PostgreSQL/Npgsql · JWT Bearer · Swagger/OpenAPI · Angular 22 · TypeScript · RxJS · Vitest · xUnit · WebApplicationFactory · Testcontainers · Docker Compose

## Architecture

- `Banking.Domain` — persistence-independent `User`, `Account`, `Money`, `Transfer`, and linked `Operation` model.
- `Banking.Application` — async use cases, ownership checks, repository ports, authentication, and transaction boundary.
- `Banking.Infrastructure` — EF Core repositories/configurations/migration, PostgreSQL transaction and JWT/password adapters.
- `Banking.Api` — HTTP DTOs, controllers, authentication middleware, Problem Details, Swagger, and health endpoints.
- `frontend` — standalone Angular application, typed API client, session auth, guarded routes, and responsive banking UI.

Dependency direction points inward: the Domain references no EF Core or ASP.NET Core package, and HTTP responses are dedicated DTOs rather than domain entities.

## Project Structure

```text
BankAccountManagement/
├── src/
│   ├── Banking.Domain/
│   ├── Banking.Application/
│   ├── Banking.Infrastructure/
│   └── Banking.Api/
├── tests/
│   ├── Banking.UnitTests/
│   └── Banking.IntegrationTests/
├── frontend/
├── Dockerfile
├── docker-compose.yml
└── .env.example
```

## Getting Started

For a direct `dotnet run`, provide configuration through environment variables and run a reachable PostgreSQL instance:

```bash
export ConnectionStrings__Banking='Host=localhost;Port=5432;Database=banking;Username=banking;Password=local-password'
export Jwt__Issuer='BankAccountManagement'
export Jwt__Audience='BankAccountManagement.Client'
export Jwt__SigningKey="$(openssl rand -base64 32)"
export ASPNETCORE_URLS='http://localhost:8080'
export ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/Banking.Api
```

The API applies committed EF Core migrations at startup. With the explicit Development environment above, Swagger is available at `http://localhost:8080/swagger` when that port is selected; it is not exposed in Production.

In a second terminal, start the Angular development server:

```bash
cd frontend
npm install
npm start
```

Open `http://localhost:4200`. The development server proxies `/api` and `/health` to `http://localhost:8080`, so the backend does not need a broad CORS policy. The access token and user metadata are kept in `sessionStorage` for the current browser tab and are cleared on sign-out, token expiry, or an API `401` response. If a transfer result is ambiguous, only its user ID, account IDs, amount, request fingerprint, and idempotency key (never the token or credentials) remain in user-scoped `localStorage` until the exact request is safely retried to a definitive outcome. That recovery metadata intentionally survives tab closure and sign-out so the same user can recover safely after signing in again; on a shared browser it is a privacy tradeoff, although another user cannot restore or submit it.

Administrators also see a **Provision a user** panel. A successful customer or administrator provisioning request automatically copies the returned user ID into the account form's Owner ID field; customers never see or invoke this action.

## Build

```bash
dotnet restore BankAccountManagement.slnx
dotnet build BankAccountManagement.slnx -c Release --no-restore
cd frontend
npm install
npm run build
```

## Run

See Getting Started for a direct process, or use Docker Compose below.

## Docker Start

```bash
cp .env.example .env
# Replace every placeholder in .env; use a random JWT key of at least 32 bytes.
docker compose up --build
```

The Compose service runs with the ASP.NET Core Production environment, so Swagger is not exposed. Check `http://localhost:8080/health/live` (or the configured `API_PORT`) and use the API or Angular development client described above. Stop and remove containers with `docker compose down`; add `--volumes` only when you intentionally want to delete local database data.

## Configuration

- `ConnectionStrings__Banking` — PostgreSQL connection string.
- `API_PORT` — loopback host port exposed by Docker Compose; defaults to `8080`.
- `Jwt__Issuer`, `Jwt__Audience` — expected JWT issuer and audience.
- `Jwt__SigningKey` — local secret, at least 32 UTF-8 bytes; never commit a real value.
- `Jwt__LifetimeMinutes` — positive access-token lifetime.
- `BootstrapAdmin__Username` and `BootstrapAdmin__Password` — optional pair used to create the first admin when absent.
- `AuthRateLimit__PermitLimit` and `AuthRateLimit__WindowSeconds` — fixed-window limit applied only to login and registration.

Tracked configuration contains no usable database or JWT secret. `.env` is ignored; `.env.example` contains placeholders only.

## Examples

### Authentication

```bash
curl -i http://localhost:8080/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"username":"alice","password":"local-password"}'

curl -i http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"alice","password":"local-password"}'
```

Copy `accessToken` from the response and send it as `Authorization: Bearer TOKEN`.

### Account Operations

List the first page of accounts visible to the current actor (owned accounts for a Customer, all accounts for an Admin):

```bash
curl -H 'Authorization: Bearer TOKEN' \
  'http://localhost:8080/api/accounts?limit=20'
```

The response is `{ "items": [...], "nextCursor": "..." }`. Pass the opaque `nextCursor` back unchanged to load the next page; `limit` must be between 1 and 100.

```bash
curl -i http://localhost:8080/api/accounts \
  -H 'Authorization: Bearer TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{"number":"PORTFOLIO-0001","initialBalance":100.00}'

curl -i http://localhost:8080/api/accounts/ACCOUNT_ID/deposit \
  -H 'Authorization: Bearer TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{"amount":25.00}'

curl -i http://localhost:8080/api/accounts/ACCOUNT_ID/withdraw \
  -H 'Authorization: Bearer TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{"amount":20.00}'

curl -H 'Authorization: Bearer TOKEN' http://localhost:8080/api/accounts/ACCOUNT_ID/operations
```

History pages contain `items` and an opaque `nextCursor`; pass it back with `limit` (1–100):

```bash
curl -H 'Authorization: Bearer TOKEN' \
  'http://localhost:8080/api/accounts/ACCOUNT_ID/operations?limit=25&cursor=NEXT_CURSOR'
```

Create an atomic transfer with a caller-generated idempotency key:

```bash
curl -i http://localhost:8080/api/transfers \
  -H 'Authorization: Bearer TOKEN' \
  -H 'Idempotency-Key: 9d1188cf-8d23-46f8-a25b-e452035c237c' \
  -H 'Content-Type: application/json' \
  -d '{"fromAccountId":"SOURCE_ID","toAccountId":"DESTINATION_ID","amount":25.00}'
```

The first successful request returns `201`; an identical replay returns `200` with the same transfer ID, while reuse with a different payload returns `409`. Idempotency applies to transfers only—deposit and withdrawal endpoints do not promise API-wide exactly-once processing.

Statements use an inclusive `from`, exclusive `to`, a maximum 366-day range, and at most 10,000 operations:

```bash
curl -H 'Authorization: Bearer TOKEN' \
  'http://localhost:8080/api/accounts/ACCOUNT_ID/statement?from=2026-01-01T00:00:00Z&to=2026-02-01T00:00:00Z'

curl -OJ -H 'Authorization: Bearer TOKEN' \
  'http://localhost:8080/api/accounts/ACCOUNT_ID/statement.csv?from=2026-01-01T00:00:00Z&to=2026-02-01T00:00:00Z'
```

`src/Banking.Api/Banking.Api.http` contains equivalent IDE-friendly requests.

## Tests

Docker must be available for integration tests because Testcontainers starts an isolated PostgreSQL container.

```bash
dotnet test tests/Banking.UnitTests -c Release
dotnet test tests/Banking.IntegrationTests -c Release
cd frontend
npm test
```

Integration coverage includes authentication semantics and isolated rate limiting, ownership-scoped account pagination, money boundaries, account creation and duplicates, deposits, withdrawals, validation, insufficient funds, concurrent withdrawals, idempotent transfers with linked ledger entries, equal-timestamp ledger pagination, Development-only Swagger, and JSON/CSV statements. Frontend tests cover typed HTTP contracts, authentication navigation and errors, tab-scoped auth persistence, bearer/401 handling, API error extraction, account-page append/stale guards, durable transfer recovery, money boundaries, and admin-only provisioning.

## Design Decisions

- Passwords use ASP.NET Core's `IPasswordHasher<User>`; raw credentials exist only in request bodies and are never logged or stored.
- JWT validation checks issuer, audience, signature, lifetime, and the HMAC-SHA256 algorithm.
- A PostgreSQL row lock serializes concurrent balance changes. Account mutation and operation insertion share one EF transaction, so they commit or roll back together.
- Transfers reserve the hashed `(actor, scope, idempotency key)` in the same transaction, lock both accounts in UUID order to avoid A↔B deadlocks, and insert one transfer with exactly one debit and one credit ledger entry.
- Statements read account balance and later ledger rows in one repeatable-read snapshot, then reconstruct opening and closing balances from signed operations.
- Money is limited to `$9,000,000,000,000.00`, keeping every cent value within JavaScript's exact integer range for browser/API round trips.
- Expected domain/application failures map centrally to 400/401/403/404/409 Problem Details responses.
- Built-in structured logging and two health endpoints provide useful local observability without adding a telemetry stack the project does not need.

## Limitations / Future Improvements

The API intentionally uses short-lived access tokens without refresh tokens, transfer-only idempotency, bounded keyset pagination, and a single implicit currency. Account numbers are portfolio identifiers rather than validated IBANs. Authenticated account creation currently has no per-user quota or throttle; add both before an Internet-facing deployment. Production deployment would also require managed secret storage, TLS termination, signing-key rotation, distributed rate-limit storage, immutable audit retention, and an operational backup policy.
