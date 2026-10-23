# PLAN-universal-finance-os.md: Transforming Finance OS into a Universal Multi-Tenant Product

**Target**: Universal Financial Operating System (Finance OS for Everyone)  
**Date**: October 23, 2026  
**Status**: DRAFT (Planning Phase - No Code Written)  
**Agent**: `project-planner`  
**Skills**: `clean-code`, `plan-writing`, `brainstorming`, `architecture`, `app-builder`  
**Project Type**: FULL-STACK (ASP.NET Core Web API + Next.js Web App)

---

## 1. Executive Summary & Vision

Finance OS was originally built as a personal financial operating system tailored specifically to one person (Nnamdi), with Nigerian banking assumptions (Stanbic, OPay, Kuda, Cowrywise), single-owner database architecture (`db.Owners.SingleOrDefaultAsync()`), and hardcoded seed values.

The goal is to **transform Finance OS into a universal, multi-tenant product that anyone in the world can sign up for and use**—whether they are a salary earner in London, a freelancer in New York, a solopreneur in Lagos, or a couple managing household finances in Toronto.

---

## 2. Core Architectural Gaps & Required Restructuring

| Layer | Current Single-User Implementation | Required Universal Product Architecture |
| :--- | :--- | :--- |
| **Identity & Tenancy** | Single `Owner` in DB; `SetupAsync` locks after 1 user; `RequireOwner()` calls `SingleOrDefaultAsync()` | **Multi-Tenant Scoped Tenancy**: Full auth (signup, login, password reset, OAuth); `ICurrentUser` scoped per request via JWT/Session cookie; EF Core Global Query Filter on all tables (`e => e.OwnerId == _tenantId`). |
| **Seeding & Onboarding** | Hardcoded `PlanSeeder` with personal bank accounts, personal salary, MatchPredictor, and family support | **Interactive Onboarding Wizard**: Step-by-step questionnaire choosing base currency, financial model (Zero-Based, 50/30/20, Wealth Accumulation, Solopreneur), and setting up initial accounts and income sources. |
| **Currency & Formatting** | NGN-first assumptions, hardcoded `₦` symbols and `Money.NgnFromMajor()` in multiple domain calculations | **Universal Base Currency Engine**: User selects primary currency (USD, EUR, GBP, NGN, CAD, AUD, etc.); all figures format dynamically according to user locale and chosen currency. |
| **Bank Ingestion** | Fixed Nigerian bank parsers (Stanbic, OPay, Kuda, Access) with hardcoded narration rules | **Universal CSV Mapper & Global Ingestion**: Interactive column mapper allowing users to import statements from *any* bank globally, plus pre-built parsers for top global & regional banks. |
| **Business / Solopreneur** | Slugs and defaults tied to `"matchpredictor"` | **Multi-Business Engine**: Any user can add $N$ businesses/ventures with custom revenue splits, baseline operating costs, and runway projection. |
| **Collaboration** | 1 user only | **Workspaces & Collaboration**: Ability to invite a partner, spouse, or accountant with view/edit permissions. |
| **Data Privacy & GDPR** | No data export/delete controls for individual tenants | **Tenant Lifecycle Management**: One-click data export (JSON/CSV) and complete account deletion with cascade wiping. |

---

## 3. Product Architecture Blueprint

```mermaid
graph TD
    subgraph Client Layer
        Web["Next.js Responsive Web App"]
        Mobile["Mobile Web / PWA"]
    end

    subgraph API & Gateway Layer
        AuthMdw["Tenant & Auth Middleware (JWT / Session)"]
        ScopedContext["Scoped User Context (UserId, BaseCurrency, Locale)"]
    end

    subgraph Business Logic Engines
        Ledger["Automated Ledger Balance Engine"]
        Onboarding["Onboarding & Template Engine"]
        BudgetEngine["Universal Allocation & Envelopes Engine"]
        StatementEngine["Pluggable Statement & CSV Mapping Engine"]
        AssistantEngine["Contextual Financial Assistant Engine"]
    end

    subgraph Data & Storage Layer
        GlobalFilter["EF Core Global Query Filter (Tenant Isolation)"]
        Postgres[(Neon PostgreSQL - Multi-Tenant)]
    end

    Client Layer --> AuthMdw
    AuthMdw --> ScopedContext
    ScopedContext --> Business Logic Engines
    Business Logic Engines --> GlobalFilter
    GlobalFilter --> Postgres
```

---

## 4. Phased Task Breakdown

### Phase 0: Multi-Tenant Identity & Scoped Security (P0)
- **Task ID**: `TASK-TENANT-01`
- **Agent**: `backend-specialist` & `security-auditor`
- **Skills**: `clean-code`, `database-design`, `architecture`
- **Files**:
  - `src/FinanceOS.Application/Contracts/Identity.cs`
  - `src/FinanceOS.Infrastructure/Data/FinanceDbContext.cs`
  - `src/FinanceOS.Infrastructure/Services/CurrentUser.cs`
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
  - `src/FinanceOS.Api/Controllers/FinanceControllers.cs`
- **INPUT**: Single-user system where `RequireOwner` returns `db.Owners.SingleOrDefaultAsync()` and `SetupAsync` blocks subsequent registrations.
- **OUTPUT**:
  - `ICurrentUser` interface extracting `UserId` and `BaseCurrency` from authenticated HTTP context claims.
  - EF Core Global Query Filter applied to all entity sets matching `OwnerId == _currentUser.UserId`.
  - Self-serve registration endpoint `POST /api/v1/auth/register` and login with secure session cookie / JWT.
  - Multi-user isolation tests verifying User A cannot see or mutate User B's accounts, transactions, or net worth.
- **VERIFY**: Run integration tests creating two separate tenants; verify total data isolation between tenants across all API endpoints.

---

### Phase 1: Interactive Onboarding & Financial Archetype Templates (P0)
- **Task ID**: `TASK-ONBOARD-01`
- **Agent**: `backend-specialist` & `frontend-specialist`
- **Skills**: `clean-code`, `frontend-design`, `api-patterns`
- **Files**:
  - `src/FinanceOS.Infrastructure/Data/TemplateEngine.cs`
  - `src/FinanceOS.Application/Contracts/Onboarding.cs`
  - `src/FinanceOS.Api/Controllers/OnboardingController.cs`
  - `web/app/onboarding/page.tsx`
  - `web/lib/templates.ts`
- **INPUT**: New accounts seeded with Nnamdi's personal accounts, envelopes, family members, and MatchPredictor business.
- **OUTPUT**:
  - Clean slate for new signups.
  - **Template Library**:
    1. **Zero-Based Budgeting (ZBB)**: Income $\rightarrow$ Fixed Bills $\rightarrow$ Envelopes $\rightarrow$ Savings Goal $\rightarrow$ Unallocated = 0.
    2. **50/30/20 Rule**: 50% Needs, 30% Wants, 20% Financial Goals.
    3. **Wealth Builder / FIRE**: Core expenses + High-savings rate allocation into investment brokers.
    4. **Solopreneur / Freelancer**: Personal checking + Business operating account + 30% Tax Vault + Retained Earnings.
    5. **Blank Canvas**: 0 accounts, 0 envelopes; user builds their own.
  - Wizard UI: Prompts User for (1) Base Currency, (2) Archetype Template, (3) Initial Accounts, (4) Income Schedule.
- **VERIFY**: Sign up as a new user, select "50/30/20" with "USD", verify accounts and envelopes initialize with USD and chosen archetype without any Nigerian bank accounts or personal references.

---

### Phase 2: Universal Base Currency & Multi-Currency Engine (P1)
- **Task ID**: `TASK-CURR-01`
- **Agent**: `backend-specialist` & `frontend-specialist`
- **Skills**: `clean-code`, `api-patterns`, `frontend-design`
- **Files**:
  - `src/FinanceOS.Domain/Entities/IdentityAndLedger.cs`
  - `src/FinanceOS.Domain/Services/FinancialEngine.cs`
  - `web/lib/format.ts`
  - `web/components/ui.tsx`
- **INPUT**: Currency formatting hardcoded to NGN ₦ with fixed conversion to USD.
- **OUTPUT**:
  - `Owner.BaseCurrency` stored per user (e.g. `USD`, `EUR`, `GBP`, `CAD`, `NGN`, `AUD`, `ZAR`, etc.).
  - Backend `Money.Format(Currency currency)` dynamically formatting according to ISO-4217 standard.
  - Frontend `formatMoney(money, baseCurrency)` using browser's `Intl.NumberFormat(locale, { style: 'currency', currency })`.
  - Net Worth and Spendable cash calculations dynamically convert all foreign holdings to the user's base currency using live exchange rates.
- **VERIFY**: Switch base currency to `EUR` or `USD`; all dashboard widgets, accounts, budgets, and explain screens format properly in `€` or `$`.

---

### Phase 3: Universal CSV Statement Ingestion & Column Mapper (P1)
- **Task ID**: `TASK-INGEST-01`
- **Agent**: `backend-specialist` & `frontend-specialist`
- **Skills**: `clean-code`, `api-patterns`, `frontend-design`
- **Files**:
  - `src/FinanceOS.Infrastructure/Services/BankStatementParserService.cs`
  - `web/app/transactions/page.tsx`
  - `web/components/csv-mapper-modal.tsx`
- **INPUT**: Statement parser limited to hardcoded Nigerian bank formats (`opay`, `stanbic`, `kuda`, `access`).
- **OUTPUT**:
  - Universal CSV Parser: Reads any delimiter-separated file (CSV, TSV).
  - Interactive Column Mapper modal: User previews first 5 rows, maps columns (`Date`, `Description`, `Debit`, `Credit` or `Amount` with sign), selects date format (`YYYY-MM-DD`, `DD/MM/YYYY`, `MM/DD/YYYY`).
  - Saved Bank Profiles: User saves mapping as a reusable profile (e.g., "Chase Checking", "Revolut Business", "GTBank Savings").
  - Retain built-in profiles for top global banks (Chase, Bank of America, Revolut, Monzo, Wise) and regional banks.
- **VERIFY**: Upload an arbitrary CSV from any bank in the world; map columns interactively; confirm transactions import accurately into the ledger with duplicate detection.

---

### Phase 4: Multi-Venture / Solopreneur Generalization (P2)
- **Task ID**: `TASK-BIZ-01`
- **Agent**: `backend-specialist`
- **Skills**: `clean-code`, `database-design`
- **Files**:
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
  - `web/app/business/page.tsx`
- **INPUT**: Business pages defaulting to or hardcoding `"matchpredictor"`.
- **OUTPUT**:
  - Fully dynamic multi-business directory: Users can create any number of businesses/projects.
  - Dynamic profit splits, baseline expenses, and revenue tracking per business.
  - Zero hardcoded references to MatchPredictor.
- **VERIFY**: Create two distinct businesses (e.g. "Design Studio" and "SaaS App"); each tracks independent revenues, baselines, and profit splits without code changes.

---

### Phase 5: Privacy, Data Portability & Tenant Lifecycle (P2)
- **Task ID**: `TASK-PRIV-01`
- **Agent**: `security-auditor` & `backend-specialist`
- **Skills**: `clean-code`, `vulnerability-scanner`
- **Files**:
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
  - `web/app/settings/page.tsx`
- **INPUT**: Single-user reset button wiping the whole database.
- **OUTPUT**:
  - Tenant Data Export: Full export of tenant's entire financial ledger in JSON and CSV format (`POST /api/v1/data/export`).
  - Tenant Self-Delete: Full GDPR-compliant purge of the tenant's data (`DELETE /api/v1/data/account`) leaving other tenants completely unaffected.
- **VERIFY**: Trigger tenant export and tenant delete; verify only the calling tenant's records are removed from the database.

---

## 5. Verification Checklist (Phase X)

### Automated Tests
- [ ] Backend: `dotnet build FinanceOS.sln --no-restore` compiles with 0 errors and 0 warnings.
- [ ] Backend: Multi-tenant isolation test suite passes 100% (User A cannot access User B data).
- [ ] Backend: Universal currency formatting tests pass for USD, EUR, GBP, NGN, and CAD.
- [ ] Frontend: `npm run build` and `tsc --noEmit` pass with 0 errors.

### User Experience Verification
- [ ] **Public Signup**: New user navigates to `/login` $\rightarrow$ clicks "Sign Up" $\rightarrow$ completes registration.
- [ ] **Onboarding**: User chooses currency (e.g. USD) and archetype (e.g. Zero-Based Budgeting) $\rightarrow$ arrives on a pristine dashboard configured in USD.
- [ ] **Custom Ingestion**: User uploads statement CSV from non-Nigerian bank $\rightarrow$ maps columns interactively $\rightarrow$ transactions commit and adjust balances immediately.
- [ ] **Multi-Business**: User creates custom business $\rightarrow$ logs business transactions $\rightarrow$ calculates revenue and runway.

---

## 6. Implementation Strategy & Safety

- **Non-Destructive Evolution**: Existing personal data for Nnamdi remains fully preserved as Tenant #1.
- **Additive Migrations**: New columns (`BaseCurrency`, `Locale`, `OnboardingCompleted`) added with sensible defaults.
- **Zero Downtime**: Staged release deployed to Railway with continuous backward compatibility.
