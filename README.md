# Finance OS

A personal financial operating system for one owner. Every naira has a job. Bank balances are never treated as spendable cash. Missing facts stay UNKNOWN.

## Stack

- ASP.NET Core 10 + EF Core + PostgreSQL
- Next.js 15
- Docker Compose is provided. Local PostgreSQL also works.

## First run without Docker

Local development uses SQLite (`financeos.db` next to the API) so the app can start without Docker. Docker Compose still uses PostgreSQL.

```bash
cd src/FinanceOS.Api
dotnet run
```

```bash
cd web
npm install
npm run dev
```

Open http://localhost:3000. Create the owner on first run. The workbook plan is seeded as a labelled snapshot, not as today's confirmed balances.

API: http://localhost:5080  
OpenAPI: http://localhost:5080/openapi/v1.json

## Tests

```bash
dotnet test FinanceOS.sln
```

## Principles the software will not break

- Transfers between own accounts are not expenses and do not change net worth.
- Last-known, expected, and estimated figures stay labelled.
- No FX conversion until you enter a rate.
- No pension, revenue, or betting P&L is invented.
- Recommendations never move money at a bank.
