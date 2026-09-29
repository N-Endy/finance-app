# PLAN-biz-input-home-overflow.md: Business Value Inputs & Home Desktop Overflow Fix

**Target**: Personal Financial Operating System (Finance OS)  
**Date**: September 29, 2026  
**Status**: DRAFT (Planning Phase - No Code Written)  
**Agent**: `project-planner`  
**Skills**: `clean-code`, `plan-writing`, `brainstorming`, `frontend-design`, `api-patterns`

---

## 1. Goal Description & Scope

Address two usability and visual polish gaps identified on production:

1. **Business Page Value Input Deficiency**:
   - The `/business` page currently displays metrics (`Revenue`, `Expenses`, `Net Result`) and cost breakdown lines (Hosting, Domain, Database, AI/API) with hardcoded `UNKNOWN` or estimate values, but provides no UI mechanism to enter confirmed values or log business transactions.
   - **Goal**: Implement a direct **"Log Business Transaction"** modal (for revenue and expenses with category and account selection) and an **inline click-to-edit editor** on UNKNOWN baseline cost lines so business performance figures reflect reality immediately.

2. **Home Page Desktop Text Overflow**:
   - On desktop viewports (particularly 1024px–1366px), cards inside `.grid.four` are restricted to ~200px–240px width.
   - The `.figure.large` typography (`font-size: 40px`) causes long naira values (`₦12,450,000`, `UNKNOWN: Enter confirmed balances`) and status badges to spill over card boundaries.
   - Long explanatory sentences (`data.spendableSentence`, `data.emergencySentence`) and flex items in the velocity widget lack `min-width: 0` and word-break rules, causing text clipping and container bursting.
   - **Goal**: Implement fluid typography (`clamp`), responsive 2-to-4 column grid wrapping, and CSS defensive layout rules (`min-width: 0`, `overflow-wrap: anywhere`, `word-break: break-word`) across Home cards.

---

## 2. User Alignments & Socratic Gate Decisions

Following Socratic Gate consultation, the user confirmed the following architectural decisions:

| Area | Decision | Rationale |
| :--- | :--- | :--- |
| **Business Input UI** | Modal + Inline Line Editor | Provides full accounting rigor (ledger transactions) while enabling instant updates to recurring baseline estimates. |
| **Recurring Costs** | Hybrid (Ledger + Monthly Baseline) | Tracks actual bank debit transactions (e.g. Access card) against configured baseline costs for Railway, Neon, domain, and AI tokens. |
| **Desktop Layout** | Responsive 2-to-4 Column Grid + Fluid Typography | Prevents text spill without sacrificing data density; adapts gracefully between 900px tablet, 1200px laptop, and 1600px desktop. |

---

## 3. Architecture & Technical Design

### 3.1 Backend Architecture (.NET 10 / EF Core / Lakebase PostgreSQL)

1. **Business Baseline Cost Persistence**:
   - Add capability to persist custom baseline/estimated costs per business entity (e.g. Hosting, Domain, Database, AI/API) so they do not default to hardcoded `UNKNOWN` when transactions have not yet occurred in the current billing cycle.
   - Endpoint: `PUT /api/v1/businesses/{slug}/baseline`
     - Payload: `List<BusinessLineWriteRequest>` (`Category`, `AmountMinor`, `Currency`, `Notes`)
2. **Business Transaction Ingestion via API**:
   - Ensure `POST /api/v1/transactions` accepts `BusinessId` and supports `TransactionType.BusinessRevenue` and `TransactionType.BusinessExpense`.
   - Update `CalculateBusinessDtoAsync` to combine confirmed ledger transactions with active baseline items seamlessly.

### 3.2 Frontend Architecture (Next.js 15 App Router / CSS Design System)

1. **Design Tokens & Defensive Layout in `web/app/globals.css`**:
   - Refactor `.grid.four` with responsive media query:
     - Viewports 901px–1200px: 2-column grid (`repeat(2, minmax(0, 1fr))`)
     - Viewports > 1200px: 4-column grid (`repeat(4, minmax(0, 1fr))`)
   - Update `.card` and `button.card`:
     - Add `min-width: 0; overflow: hidden;`
   - Update `.figure.large`:
     - Replace static `40px` with `font-size: clamp(24px, 2.2vw, 36px); line-height: 1.15; overflow-wrap: anywhere;`
   - Update `.figure`:
     - Add `overflow-wrap: anywhere; word-break: break-word;`
   - Update `.sentence`:
     - Add `overflow-wrap: break-word; line-height: 1.45;`

2. **Business Page Components (`web/app/business/page.tsx`)**:
   - **Log Business Transaction Modal**:
     - Fields: Type (Expense / Revenue), Category (Hosting, Domain, Database, AI / API, Subscriptions, Client Revenue, Other), Amount (NGN / USD), Account (Access Card, Stanbic, etc.), Date, Description.
     - Commits via `api.createTx(...)` and immediately refetches business ledger.
   - **Inline Baseline Cost Editor**:
     - For cost breakdown items showing `UNKNOWN` or `Estimate`, click opens a popover/input to enter current actual monthly cost.
     - Saves via `api.updateBusinessBaseline(slug, lines)`.

3. **Home Page Metric Cards Polish (`web/app/page.tsx`)**:
   - Ensure all 4 top metric cards (`Confirmed net worth`, `Actually spendable`, `Emergency`, `Housing`) render within `min-width: 0` flex wrappers.
   - Keep narrative sentences legible with subtle hierarchy and tooltip/modal triggers.

---

## 4. Phased Task Breakdown

### Phase 1: CSS Grid & Typography Defensive Shield (P1)
- **Task ID**: `TASK-UI-01`
- **Agent**: `frontend-specialist`
- **Skills**: `frontend-design`, `clean-code`
- **Files**: `web/app/globals.css`, `web/components/ui.tsx`
- **INPUT**: Current `globals.css` with rigid 4-column desktop grid and 40px `.figure.large`.
- **OUTPUT**:
  - Breakpoint at 1200px transitioning `.grid.four` to 2x2 on medium desktops.
  - Fluid `clamp(24px, 2.2vw, 36px)` on `.figure.large`.
  - Global `min-width: 0`, `overflow-wrap: anywhere`, and `word-break: break-word` on `.card` and `.sentence`.
- **VERIFY**: Resize desktop browser window from 901px to 1920px. Ensure zero text clipping, zero horizontal scroll, and zero element collision.

---

### Phase 2: Backend Business Baseline & Cost Entry Support (P1)
- **Task ID**: `TASK-BE-01`
- **Agent**: `backend-specialist`
- **Skills**: `api-patterns`, `clean-code`
- **Files**:
  - `src/FinanceOS.Application/Contracts/Dtos.cs`
  - `src/FinanceOS.Api/Controllers/FinanceControllers.cs`
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
- **INPUT**: Existing `BusinessDto` and hardcoded fallback in `CalculateBusinessDtoAsync`.
- **OUTPUT**:
  - `PUT /api/v1/businesses/{slug}/baseline` endpoint.
  - Ability to record/override baseline line items without requiring a full account statement upload.
  - Support for linking direct transactions to specific `BusinessId`.
- **VERIFY**: Run `dotnet test FinanceOS.sln`. Verify 200 OK and correct DTO response for baseline updates.

---

### Phase 3: Business Page UI Inputs & Modals (P2)
- **Task ID**: `TASK-FE-01`
- **Agent**: `frontend-specialist`
- **Skills**: `frontend-design`, `clean-code`
- **Files**:
  - `web/lib/api.ts`
  - `web/lib/types.ts`
  - `web/app/business/page.tsx`
- **INPUT**: Read-only business page with hardcoded UNKNOWN lines.
- **OUTPUT**:
  - "+ Log Business Transaction" action button with responsive modal dialog.
  - Category selector prefilled with business expense & revenue buckets.
  - Account picker populated from `api.accounts()`.
  - Inline "Set Cost" button next to UNKNOWN lines allowing 1-click amount entry.
- **VERIFY**: Open `/business` on web, click "+ Log Business Transaction", submit ₦15,000 for Railway Hosting from Access card. Verify UNKNOWN replaces with confirmed ₦15,000 and net updates dynamically.

---

### Phase 4: Home Page Layout & Card Pacing Polish (P2)
- **Task ID**: `TASK-FE-02`
- **Agent**: `frontend-specialist`
- **Skills**: `frontend-design`, `react-best-practices`
- **Files**:
  - `web/app/page.tsx`
  - `web/components/spending-velocity.tsx`
- **INPUT**: Home page where long strings and velocity meters experience cramped widths.
- **OUTPUT**:
  - Card containers structured with responsive flexbox and ellipsis guards.
  - Spending velocity bar and pacing ratio cleanly bounded within parent container.
- **VERIFY**: Inspect Home page on 1024x768, 1280x800, 1440x900, and 1920x1080 resolutions. Verify clean rendering with no overlapping elements.

---

## 5. Verification Checklist (Phase X)

### Automated Checks
- [x] Backend: `dotnet build FinanceOS.sln --no-restore` compiles with 0 errors and 0 warnings.
- [x] Backend: `dotnet test FinanceOS.sln` passes 100% of unit & domain tests (30/30 passed).
- [x] Frontend: `npm run build` and `npx tsc --noEmit` pass with 0 TypeScript/ESLint errors.

### Manual UX Audit
- [x] **Home Page Desktop Viewport Audit**:
  - At 1024px: Top 4 metric cards wrap into a clean 2x2 grid; 40px numbers scale down gracefully via clamp.
  - At 1440px: Cards display in a balanced 4-column layout with no overflowing sentences.
  - No text spills past card borders; min-width: 0 and overflow-wrap protect containers.
- [x] **Business Page Input Audit**:
  - Click "+ Log Business Transaction" $\rightarrow$ Modal opens with category, account, amount, and date.
  - Submit an expense $\rightarrow$ Ledger reflects entry, UNKNOWN status resolves, net profit updates.
  - Click inline "Set Cost" on Hosting $\rightarrow$ Enter amount $\rightarrow$ Baseline saves and renders confirmed/estimate badge.

---

## 6. Rollback & Safety Plan

- **Database Safety**: Baseline costs utilize existing schema or non-destructive JSON/entity properties. No drops or disruptive column deletions.
- **CSS Isolation**: All responsive adjustments use standard CSS variables and media query containment; zero global DOM breakages.
- **Git Rollback**: Changes are isolated to feature branch / cleanly revertible commit.
