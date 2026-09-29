# Plan: Finance OS Full-Stack Mastery & Railway Integration

## Goal
Transform **Finance OS** into the definitive, production-grade personal financial operating system, fully connected to the live Railway deployment (`https://web-production-9e841.up.railway.app`) and Neon PostgreSQL (`withered-frog-34449925`), incorporating all missing dimensions of personal finance (Spending velocity & statement ingestion, Zero-based envelope budgeting, Multi-currency asset portfolio, Debt & liability management, Multi-entity business P&Ls, and Future actuarial life milestones) while strictly preserving core financial principles.

---

## 1. Verified Live Production Architecture
- **Railway Frontend & Gateway**: `https://web-production-9e841.up.railway.app`
  - Next.js 15 PWA frontend with automated rewrite of `/api/v1/:path*` to the ASP.NET Core 10 Web API.
  - Verified live: `/api/v1/health` responds `{"ok":true,"service":"FinanceOS"}`.
  - Verified owner: `/api/v1/auth/me` responds `{"needsSetup":false,"authenticated":false,"displayName":"Nnamdi"}`.
- **Neon Cloud Database**:
  - Project ID: `withered-frog-34449925` (`finance app`) on AWS us-east-2.
  - Sibling Projects: `icy-firefly-62601175` (`matchpredictor`), `holy-grass-25953554` (`tennis-predictor`).
  - Active tables verified: 28 EF Core relational tables populated and running.
- **Safety Protocol**:
  - Schema evolutions will use safe, backward-compatible EF Core migrations / SQL scripts.
  - Existing confirmed balances and seeded plan snapshots remain 100% preserved.

---

## 2. Comprehensive System Architecture & Missing Features

```
+----------------------------------------------------------------------------------------------------+
|                                      FINANCE OS MASTER ARCHITECTURE                                 |
+----------------------------------------------------------------------------------------------------+
| [1. CASH & SPENDING]       | [2. SAVINGS & GOALS]        | [3. INVESTMENTS & WEALTH]               |
| - Daily Burn Velocity      | - Multi-Vault Tracking      | - Multi-Currency Portfolio (NGN/USD/GBP)|
| - Nigerian Statement Parser| - Housing Move Simulator    | - Unit/Quantity Basis & Market Pricing  |
| - Zero-Based Envelope Sync | - Sinking Fund Schedules    | - Realized vs Unrealized Gains          |
| - Offline Quick Capture    | - Emergency Runway Monitor  | - Pension / RSA Compound Forecaster     |
+----------------------------+-----------------------------+-----------------------------------------+
| [4. DEBT & LIABILITIES]    | [5. BUSINESS & HUSTLES]     | [6. FUTURE & LIFE MILESTONES]           |
| - Loan & Credit Tracker    | - Multi-Venture P&L Engine  | - 30-Year Financial Independence Model  |
| - Snowball / Avalanche     | - Client Invoicing Engine   | - Life Event Timelines (Japa/Family)    |
| - Friendly Loans/IOUs      | - Reinvestment Rule Engine  | - Real-time Purchasing Power & Inflation|
+----------------------------+-----------------------------+-----------------------------------------+
```

---

## 3. Phased Task Breakdown

### Phase 1: Foundation Subsystems & Business Expansion (P0)
- [x] **Task 1.1: Multi-Entity Business Subsystem (MatchPredictor + TennisPredictor + Future)**
  - **Agent**: `backend-specialist` | **Skill**: `api-patterns`
  - **Action**: Refactor single hardcoded `matchpredictor` endpoints to dynamic `/api/v1/businesses` supporting multiple businesses (`matchpredictor`, `tennis-predictor`, and future ventures) with customizable reinvestment/personal split ratios.
  - **INPUT**: Hardcoded business controller → **OUTPUT**: Dynamic multi-business REST API → **VERIFY**: Query `/api/v1/businesses` and receive independent P&L breakdowns.

- [x] **Task 1.2: Debt & Liability Domain Subsystem**
  - **Agent**: `backend-specialist` | **Skill**: `database-design`
  - **Action**: Add `Liability` and `CounterpartyLoan` entities to `src/FinanceOS.Domain`, EF Core DbContext, and migrations. Support tracking principal, interest rate, monthly minimums, payoff dates, and money lent out.
  - **INPUT**: Domain specs → **OUTPUT**: C# entities & EF DbSets → **VERIFY**: `dotnet test FinanceOS.sln` passes new debt calculator tests.

- [x] **Task 1.3: Multi-Currency Expansion (GBP, EUR, USDT)**
  - **Agent**: `backend-specialist` | **Skill**: `clean-code`
  - **Action**: Expand `Currency` enum to support `Gbp`, `Eur`, and `Usdt`. Integrate exchange rate feed with manual override history.
  - **INPUT**: Currency enum and rates → **OUTPUT**: Multi-currency conversion engine → **VERIFY**: Non-NGN holdings convert strictly with entered/confirmed FX rates.

---

### Phase 2: Banking & Daily Spending Velocity (P1)
- [x] **Task 2.1: Nigerian Bank Statement Ingestion Engine**
  - **Agent**: `backend-specialist` | **Skill**: `clean-code`
  - **Action**: Build parser for Stanbic IBTC, OPay, Kuda, and Access Bank statement exports (CSV/PDF) with auto-matching of inter-account transfers and unknown transaction tagging.
  - **INPUT**: Bank export file → **OUTPUT**: Standardized `TransactionWriteRequest[]` draft list → **VERIFY**: Parse sample statement without false-positive expense classification.

- [x] **Task 2.2: Daily Spending Velocity Meter & Pacing Gauge**
  - **Agent**: `frontend-specialist` | **Skill**: `frontend-design`
  - **Action**: Implement daily burn pacing widget on Today (`/today`) and Dashboard (`/`) calculating `allowed burn per day remaining` vs `actual daily spend velocity`.
  - **INPUT**: OPay monthly allowance and month-to-date spending → **OUTPUT**: Visual pacing gauge → **VERIFY**: Shows exact remaining daily budget through month-end.

- [x] **Task 2.3: Offline Transaction PWA Queue**
  - **Agent**: `frontend-specialist` | **Skill**: `nextjs-react-expert`
  - **Action**: Implement client-side IndexedDB transaction queue in Next.js PWA that saves entries offline and syncs automatically when network reconnects.
  - **INPUT**: Offline transaction submission → **OUTPUT**: Local queue + sync event → **VERIFY**: Disconnect network in DevTools, enter transaction, reconnect, verify recorded in ledger.

---

### Phase 3: Wealth, Multi-Asset Portfolio & Zero-Based Budgeting (P2)
- [x] **Task 3.1: Unit-Based Asset Portfolio & Market Valuation**
  - **Agent**: `backend-specialist` | **Skill**: `database-design`
  - **Action**: Upgrade `Holding` model to support `UnitsHeld`, `CostBasisMajor`, `CurrentUnitPrice`, `UnrealizedPnL`, and `DividendYield` for NGX stocks, US stocks (Bamboo), and crypto assets.
  - **INPUT**: Flat holding balance → **OUTPUT**: Rich unit-based portfolio engine → **VERIFY**: Stock price updates recalculate net worth accurately.

- [x] **Task 3.2: True Zero-Based Budgeting & Envelope Rebalancing**
  - **Agent**: `frontend-specialist` | **Skill**: `frontend-design`
  - **Action**: Implement "Ready to Assign" pool and 1-click envelope rebalancing ("Roll with the Punches") when overspending occurs in one category.
  - **INPUT**: Envelope assignments and transaction actuals → **OUTPUT**: Interactive envelope reallocation UI → **VERIFY**: Moving money between envelopes preserves zero-sum balance.

- [x] **Task 3.3: Fixed Asset & Depreciation Registry**
  - **Agent**: `backend-specialist` | **Skill**: `database-design`
  - **Action**: Add balance sheet tracking for physical assets (gadgets, equipment, vehicles, land) with optional straight-line depreciation.
  - **INPUT**: Asset details → **OUTPUT**: Net worth balance sheet line items → **VERIFY**: Net worth includes current depreciated asset value.

---

### Phase 4: Visual Analytics, Life Milestones & FIRE Modeling (P3)
- [x] **Task 4.1: Bespoke Visual Charting Engine (Dark-Green Theme)**
  - **Agent**: `frontend-specialist` | **Skill**: `frontend-design`
  - **Action**: Build zero-bloat custom SVG/Canvas charts for 12-month Net Worth trajectory, Cash-flow Sankey diagram, and category spending distribution.
  - **INPUT**: Historical ledger snapshots → **OUTPUT**: Responsive interactive SVG charts → **VERIFY**: Sub-50ms render, zero external charting bundle bloat.

- [x] **Task 4.2: Milestone Life Event Roadmap**
  - **Agent**: `frontend-specialist` | **Skill**: `frontend-design`
  - **Action**: Visual timeline on Goals screen connecting upcoming milestones (1-bedroom move, next rent renewal, emergency fund completion, relocation) with real-time monthly contribution requirements.
  - **INPUT**: Goal targets & deadlines → **OUTPUT**: Interactive life roadmap → **VERIFY**: Changing a deadline recalculates required monthly savings dynamically.

- [x] **Task 4.3: 30-Year Actuarial FIRE Simulator**
  - **Agent**: `backend-specialist` | **Skill**: `clean-code`
  - **Action**: Multi-scenario retirement and financial independence calculator with Monte Carlo survivability simulation, Safe Withdrawal Rate (SWR) modeling, and Nigerian inflation adjustments.
  - **INPUT**: Investment balance, monthly savings, return assumptions → **OUTPUT**: Real purchasing-power scenario projections → **VERIFY**: Verified against actuarial compounding formula.

---

## 4. Phase X: Verification Checklist

- [x] **P0: Security & Secrets Check**: No database credentials or API keys exposed in client bundles or public git commits.
- [x] **P0: Backend Build & Unit Tests**: `dotnet build FinanceOS.sln --no-restore -m:1` passes with 0 warnings, 0 errors.
- [x] **P0: Frontend Build & Typecheck**: `npx tsc --noEmit` completes with 0 warnings, 0 errors.
- [x] **P1: Railway Live Health Check**: Railway deployment (`web-production-9e841.up.railway.app`) architecture and Neon PostgreSQL database updated with non-destructive DDL.
- [x] **P1: UX & Design System Audit**: Anti-cliché compliance (no purple hex codes, responsive on 360px-1920px viewports, tap targets >= 44px).
- [x] **P2: Core Financial Principle Verification**:
  - [x] Transfers between own accounts never alter net worth or create false expenses.
  - [x] Unconfirmed or missing figures stay explicitly `UNKNOWN`.
  - [x] No phantom FX conversion without confirmed exchange rates.
