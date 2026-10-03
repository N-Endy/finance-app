# PLAN-auto-balance-dynamic-engine.md: Automated Ledger Balance Engine & Dynamic Decoupling

**Target**: Personal Financial Operating System (Finance OS)  
**Date**: October 3, 2026  
**Status**: DRAFT (Planning Phase - No Code Written)  
**Agent**: `project-planner`  
**Skills**: `clean-code`, `plan-writing`, `brainstorming`, `database-design`, `api-patterns`

---

## 1. Goal Description & Scope

Address fundamental operational automation and design agility gaps in Finance OS:

1. **Automated Real-Time Balance Readjustment**:
   - Currently, adding, editing, voiding, or importing transactions records `LedgerTransaction` and `Posting` rows, but never readjusts account balances or Net Worth because the system only queries static `BalanceSnapshot` rows.
   - **Goal**: Implement a **Real-Time Ledger Balance Engine** that dynamically computes current working balances (`Base Confirmed Snapshot + Sum of Subsequent Postings`), instantly updating Account screens, Net Worth, Spendable Cash, and Reconciliation without manual re-entry.

2. **Full Dynamic Decoupling (Eliminating Hardcoded Slugs & Assumptions)**:
   - Hardcoded slugs across backend and frontend limit adaptability (e.g. `LiquidCashAsync` hardcodes `["stanbic", "kuda", "opay", "cash", "access"]`, `UnknownFactsAsync` hardcodes 3 specific accounts, `HealthAsync` only monitors `opay` and `access`, `MoneyMapAsync` hardcodes old salary figures and accounts, and `BudgetAsync` crashes if income slug is not `"salary"`).
   - **Goal**: Decouple business logic to query live database entities (`FinancialAccount`, `IncomeSource`, `AllocationPlan`, `Envelope`, `Business`) so new banks, cards, or ventures operate seamlessly.

3. **Smart Category-to-Envelope Auto-Linking & Merchant Memory**:
   - Transactions recorded without an explicit `EnvelopeId` (such as bank statement imports or quick captures) are currently omitted from budget envelope calculations even when their category belongs to an envelope.
   - **Goal**: Auto-inherit `EnvelopeId` from `Category.EnvelopeId` and remember merchant-to-category associations across statement imports.

---

## 2. User Alignments & Socratic Gate Decisions

Following Socratic Gate consultation, the user confirmed the following architectural decisions:

| Area | Decision | Rationale |
| :--- | :--- | :--- |
| **Balance Readjustment** | Real-Time Ledger Balance Engine | Dynamic computation ensures transactions immediately reflect in account balances, Net Worth, and spendable pool, while preserving snapshot auditability. |
| **Dynamism & Decoupling** | Full Dynamic Decoupling | All accounts, liquid cash roles, spending allowances, credit cycles, and the Money Map (`/money`) become entity-driven rather than slug-dependent. |
| **Envelope Assignment** | Smart Classification & Auto-Link | Automatically infers envelopes from categories and utilizes merchant memory from ledger history to eliminate manual categorization friction. |

---

## 3. Technical Architecture & Design

### 3.1 Real-Time Ledger Balance Engine (`src/FinanceOS.Infrastructure`)

1. **Effective Account Balance Algorithm**:
   - For any account $A$:
     - Retrieve the latest confirmed baseline snapshot $S$ (or opening snapshot).
     - Query all non-voided postings where `AccountId == A.Id` on or after $S.AsOf$ (or all postings if no snapshot exists).
     - Inflows (Income, Deposit, Transfer-In, Business Revenue) add to balance.
     - Outflows (Expense, Fee, Transfer-Out, Business Expense) subtract from balance.
     - `EffectiveBalanceMinor = (S?.AmountMinor ?? 0) + NetPostingsMinor`.
   - Provenance tracking:
     - If $NetPostingsMinor == 0$ and $S$ exists: retains $S.Provenance$ (e.g. `Confirmed`).
     - If postings exist since snapshot: marked as `Confirmed` with note indicating real-time ledger calculation.
2. **Integration Touchpoints**:
   - `AccountsAsync`: Populate `AccountDto.LatestBalance` using `GetEffectiveAccountBalanceAsync(account)`.
   - `BuildNetWorthAsync`: Sum effective balances across all active accounts included in net worth.
   - `LiquidCashAsync`: Sum effective balances across all liquid operating accounts.
   - `ReconcileAsync`: Compare bank statement snapshot against effective ledger balance, computing exact discrepancy.
3. **Statement Import Auto-Snapshot**:
   - In `BankStatementParserService`, extract the closing balance from statement header/footer.
   - When importing statements, automatically record/update a `BalanceSnapshot` with `Source = "statement"`, locking in the bank's confirmed balance.

### 3.2 Dynamic Decoupling Subsystem

1. **Schema Enhancements on `FinancialAccount`**:
   - `MonthlyAllowanceMinor` (nullable long): Configurable spending ceiling for any wallet/card (replaces hardcoded OPay allowance).
   - `BillingCycleDay` (nullable int): Day of month for statement closing/repayment (replaces hardcoded Access card logic).
   - `CreditLimitMinor` (nullable long): For credit cards and overdraft facilities.
2. **Dynamic Money Map (`/money`)**:
   - Replace the static array in `MoneyMapAsync` with dynamic graph generation:
     - **Income Nodes**: Generated from active `IncomeSource`s.
     - **Account Nodes**: Generated from active `FinancialAccount`s with live effective balances.
     - **Envelope Nodes**: Generated from active `Envelope`s linked to the primary allocation plan.
     - **Goal Nodes**: Generated from active `Goal`s with current balances.
     - **Edges / Connections**: Derived from `AllocationPlanLine` relationships (Income $\rightarrow$ Account $\rightarrow$ Envelope/Goal).
3. **Dynamic Budget Plan (`BudgetAsync`)**:
   - Remove `p.IncomeSource!.Slug == "salary"`. Query all active allocation plans for the user and aggregate envelope budget targets.
4. **Dynamic Assistant (`AskAsync`)**:
   - Replace hardcoded `stanbic`, `opay`, `matchpredictor` string checks with dynamic entity resolution:
     - Match query against any user account name or slug.
     - Match query against any user business name or slug.
     - Provide contextual ledger balances for any matched entity.

### 3.3 Smart Category-to-Envelope Auto-linking & Merchant Memory

1. **Category Inheritance**:
   - In `CreateTransactionAsync`: If `request.EnvelopeId` is null and `category.EnvelopeId` exists, set `tx.EnvelopeId = category.EnvelopeId`.
   - In `BudgetAsync`: Include transactions where `t.EnvelopeId == line.EnvelopeId || (t.EnvelopeId == null && t.Category!.EnvelopeId == line.EnvelopeId)`.
2. **Merchant Memory Classifier**:
   - In `QuickEntryParser` and `BankStatementParserService`, look up recent transactions matching the raw narration or merchant string.
   - Auto-suggest or auto-assign the most frequent historical `CategoryId` and `EnvelopeId`.

---

## 4. Phased Task Breakdown

### Phase 1: Real-Time Ledger Balance Engine (P1)
- **Task ID**: `TASK-BAL-01`
- **Agent**: `backend-specialist`
- **Skills**: `clean-code`, `database-design`, `api-patterns`
- **Files**:
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
  - `src/FinanceOS.Domain/Services/FinancialEngine.cs`
- **INPUT**: Accounts and net worth calculate solely from static `BalanceSnapshot`s.
- **OUTPUT**:
  - `GetEffectiveAccountBalanceAsync` computing real-time balance from snapshot + postings.
  - Integration into `AccountsAsync`, `BuildNetWorthAsync`, and `LiquidCashAsync`.
  - Transaction creation, edit, and void operations immediately reflect in account balances and net worth.
- **VERIFY**: Run `dotnet test FinanceOS.sln`. Verify creating a transaction decrements/increments the account balance and net worth immediately without adding a manual snapshot.

---

### Phase 2: Category-to-Envelope Auto-Linking & Merchant Memory (P1)
- **Task ID**: `TASK-ENV-01`
- **Agent**: `backend-specialist`
- **Skills**: `clean-code`, `api-patterns`
- **Files**:
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
  - `src/FinanceOS.Infrastructure/Services/BankStatementParserService.cs`
- **INPUT**: Transactions without explicit envelope IDs are ignored by budget envelope tracking.
- **OUTPUT**:
  - Automatic inheritance of `Category.EnvelopeId` during transaction creation.
  - `BudgetAsync` query updated to include category-linked transactions.
  - Historical merchant narration lookup for auto-categorization during statement ingestion.
- **VERIFY**: Record an expense under category "Groceries" (which belongs to "Food" envelope) without specifying envelope ID. Check `GET /api/v1/budget` $\rightarrow$ "Food" envelope spent reflects the transaction.

---

### Phase 3: Dynamic Decoupling of Accounts, Health & Budget (P1)
- **Task ID**: `TASK-DEC-01`
- **Agent**: `backend-specialist`
- **Skills**: `clean-code`, `database-design`
- **Files**:
  - `src/FinanceOS.Domain/Entities/IdentityAndLedger.cs`
  - `src/FinanceOS.Infrastructure/Data/FinanceDbContext.cs`
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
- **INPUT**: Hardcoded slug checks for `"opay"`, `"access"`, `"stanbic"`, and `"salary"`.
- **OUTPUT**:
  - Non-destructive DB migration adding `MonthlyAllowanceMinor`, `CreditLimitMinor`, and `BillingCycleDay` to `FinancialAccount`.
  - `HealthAsync` checking `MonthlyAllowanceMinor` on any account rather than `account.Slug == "opay"`.
  - `BudgetAsync` reading across all active allocation plans rather than filtering by `"salary"`.
  - `LiquidCashAsync` using `AccountRole` instead of hardcoded slug list.
- **VERIFY**: Add a new arbitrary bank account (e.g. "Zenith Bank") with an allowance. Verify it is included in Liquid Cash and monitored by health rules without code changes.

---

### Phase 4: Dynamic Money Map & Assistant Engine (P2)
- **Task ID**: `TASK-DEC-02`
- **Agent**: `backend-specialist` & `frontend-specialist`
- **Skills**: `clean-code`, `frontend-design`, `api-patterns`
- **Files**:
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
  - `web/app/money/page.tsx`
- **INPUT**: Static, hardcoded Money Map graph in `MoneyMapAsync` and hardcoded NLP matches in `AskAsync`.
- **OUTPUT**:
  - `MoneyMapAsync` constructing nodes dynamically from user's active income sources, accounts, allocation plans, and goals.
  - `AskAsync` matching queries against any active account or business name.
- **VERIFY**: Visit `/money` on web. Verify graph nodes dynamically match database accounts and active income allocations.

---

### Phase 5: Frontend Real-Time Balance Refresh & Indicator (P2)
- **Task ID**: `TASK-FE-01`
- **Agent**: `frontend-specialist`
- **Skills**: `frontend-design`, `clean-code`
- **Files**:
  - `web/app/accounts/page.tsx`
  - `web/app/page.tsx`
  - `web/components/ui.tsx`
- **INPUT**: UI displaying balances with static snapshot dates.
- **OUTPUT**:
  - Clear provenance badges distinguishing between "Verified Bank Snapshot" and "Live Ledger Balance".
  - Quick-reconcile trigger showing any difference between statement and ledger.
- **VERIFY**: Open `/accounts`, log a transaction on `/transactions`, navigate back to `/accounts` $\rightarrow$ balance updates immediately without manual reload.

---

## 5. Verification Checklist (Phase X)

### Automated Checks
- [ ] Backend: `dotnet build FinanceOS.sln --no-restore` compiles with 0 errors and 0 warnings.
- [ ] Backend: `dotnet test FinanceOS.sln` passes 100% of unit & domain tests.
- [ ] Frontend: `npm run build` and `npx tsc --noEmit` pass with 0 errors.

### Manual UX & Accounting Verification
- [ ] **Transaction Balance Readjustment**:
  - Create transaction: Note Stanbic balance before ₦100,000; log ₦15,000 expense $\rightarrow$ balance immediately displays ₦85,000; Net Worth drops by ₦15,000.
  - Void transaction: Balance immediately restores to ₦100,000; Net Worth restores.
- [ ] **New Account Dynamism**:
  - Create/register a new account (e.g. "Kuda Savings" or "FirstBank Checking").
  - Confirm it appears in Liquid Cash, Net Worth, and the Money Map (`/money`) without restarting the server.
- [ ] **Envelope Auto-Resolution**:
  - Ingest bank statement with narration "OPay - Kilimanjaro" $\rightarrow$ classifies under "Food", auto-associates with "Food" envelope, updates budget spent amount.

---

## 6. Rollback & Safety Plan

- **Database Safety**: Schema additions are strictly additive (`ADD COLUMN IF NOT EXISTS`). No destructive table alterations or drops.
- **Audit Trails**: Every automated balance readjustment maintains complete auditability through the immutable ledger posting history.
- **Git Checkpoint**: All changes staged on a dedicated feature commit with instant rollback capability.
