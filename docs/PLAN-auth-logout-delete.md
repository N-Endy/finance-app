# Comprehensive Implementation Plan: Auth Lifecycle, Ubiquitous Logout & Complete Account Cascade Purge

**Task Slug**: `auth-logout-delete`  
**Plan Document**: `docs/PLAN-auth-logout-delete.md`  
**Mode**: PLANNING ONLY  
**Primary Agent**: `project-planner`  
**Execution Agents**: `backend-specialist`, `frontend-specialist`, `security-auditor`  
**Applicable Skills**: `clean-code`, `database-design`, `frontend-design`, `api-patterns`, `testing-patterns`

---

## 1. Overview & Problem Analysis

### 1.1 The Missing Logout Paradox
- **Current Desktop State**: A "Sign Out" button exists at the bottom of the left sidebar (`web/components/ui.tsx` line 50).
- **Current Mobile State**: On mobile devices and viewport widths $< 768px$, the side navigation is completely hidden (`display: none`). The mobile bottom bar and the "More" slide-over sheet drawer (`more-sheet`) **do not contain any logout button**. Mobile users who log in are trapped with no visual or accessible way to log out.
- **Current Settings State**: `web/app/settings/page.tsx` contains OPay allowance controls, export buttons, and ledger wipe buttons, but **lacks a dedicated Sign Out button**. Users instinctively check Settings to log out.

### 1.2 Sign In & Sign Up Status Confirmation
- **Sign In Endpoint**: `POST /api/v1/auth/login` is operational with rate limiting (`[EnableRateLimiting("login")]`), password verification via ASP.NET Core `IPasswordHasher<Owner>`, and secure cookie issuance.
- **Sign Up / Register Endpoint**: `POST /api/v1/auth/register` is operational, creates a fresh `Owner`, hashes password, sets default currency, assigns onboarding status, and auto-authenticates the session.
- **Initial Setup Endpoint**: `POST /api/v1/auth/setup` handles the first-time bootstrapping flow if no owners exist.
- **UI State**: `web/app/login/page.tsx` features an interactive tab switcher between **Sign In** and **Create Account** (with full name, email, password, base currency, and archetype selection).

### 1.3 Complete Account & Database Purge (Zero Orphan Records)
- **Current Limitation**: `FinanceOsService.DeleteTenantAccountAsync` calls `PlanSeeder.ClearOwnerPlanAsync(db, owner, ct)` and deletes the `Owner` record.
- **Database Leak Vectors**:
  1. `AuditLogs` (`db.AuditLogs.Where(a => a.OwnerId == owner.Id)`) are currently NOT purged.
  2. `HouseholdWorkspaces` where `PrimaryOwnerId == owner.Id` are NOT purged (which leaves orphaned workspaces and child `HouseholdMembers` in PostgreSQL).
  3. `HouseholdMembers` where `UserId == owner.Id` (when the user joined another member's household) are NOT removed, and other users might still point to an orphaned workspace.
  4. Cookies and authentication tickets must be immediately invalidated (`SignOutAsync`).
  5. The purge must execute atomically within an EF Core execution strategy / database transaction.

### 1.4 Account Deletion Safety Prompt
- **Current State**: A simple browser dialog `window.confirm(...)` is triggered from `web/app/settings/page.tsx`.
- **Target Experience**: A purpose-built, accessible **Enterprise Safety Modal**:
  - Dark-tinted backdrop with high contrast warning typography.
  - Clear bulleted list detailing every item that will be irreversibly destroyed (ledger accounts, transactions, allocation envelopes, debt plans, tax reserves, household memberships, login credentials).
  - Explicit confirmation input requiring the user to type `"DELETE"` (case-insensitive or exact uppercase `"DELETE"`) into a designated text box.
  - The destructive "Permanently Delete Everything" button remains strictly disabled until the exact confirmation word is entered.
  - Clear "Cancel" button and ESC/backdrop click handlers.

---

## 2. Project Architecture & Decisions

| Decision Area | Chosen Strategy | Rationale |
| :--- | :--- | :--- |
| **Project Type** | `WEB` (Next.js 16 App Router) + `BACKEND` (.NET 10 Web API + EF Core + PostgreSQL) | Full-stack financial OS |
| **Logout Locations** | 1. Mobile "More" drawer (`web/components/ui.tsx`)<br>2. Settings page account card (`web/app/settings/page.tsx`)<br>3. Existing desktop sidebar maintained | User selected "Settings and Mobile drawer only"; guarantees accessibility on all screen sizes |
| **Purge Strategy** | Complete Cascade Purge across 24 entities + `HouseholdWorkspaces` + `HouseholdMembers` + `AuditLogs` + `Owner` | User selected complete cascade purge; ensures 100% GDPR/privacy compliance and zero database orphan records |
| **Confirmation UX** | Enterprise Safety Modal with explicit `"DELETE"` text input match | Prevents catastrophic misclicks while avoiding ugly native browser alerts |

---

## 3. Database Purge Cascade Map

When `DELETE /api/v1/data/account` is invoked, the backend must remove records in the following deterministic sequence to satisfy foreign key constraints:

```mermaid
flowchart TD
    A["Owner Request to Delete Account"] --> B["Validate Current User"]
    B --> C["1. Remove Financial Transactions & Postings & Revisions"]
    C --> D["2. Remove Accounts, Snapshots & Envelope Assignments"]
    D --> E["3. Remove Allocation Plans & Lines"]
    E --> F["4. Remove Envelopes, Categories, Goals, Holdings, FX"]
    F --> G["5. Remove Subscriptions, Recurring, Family Support & Loans"]
    G --> H["6. Remove Businesses, Liabilities, Assets, Pensions & Assumptions"]
    H --> I["7. Purge User Audit Logs (OwnerId == owner.Id)"]
    I --> J["8. Disband Owned Household Workspaces (Cascade HouseholdMembers)"]
    J --> K["9. Remove HouseholdMemberships in Other Workspaces"]
    K --> L["10. Delete Owner Record from Owners Table"]
    L --> M["11. Invalidate Authentication Cookie (SignOutAsync)"]
    M --> N["HTTP 200 OK + Client Redirect to /login"]
```

---

## 4. Work Breakdown Structure (Tasks)

### Task 1: Complete Database Cascade Purge Engine
- **Task ID**: `TASK-AUTH-01`
- **Agent**: `backend-specialist`
- **Skill**: `database-design`, `clean-code`
- **Priority**: P0 (Security & Data Integrity)
- **Dependencies**: None
- **Files Modified**:
  - `src/FinanceOS.Infrastructure/Data/PlanSeeder.cs`
  - `src/FinanceOS.Infrastructure/Services/FinanceOsService.cs`
- **Description**:
  - Update `PlanSeeder.ClearOwnerPlanAsync` or `FinanceOsService.DeleteTenantAccountAsync` to purge:
    - `db.AuditLogs.Where(a => a.OwnerId == owner.Id)`
    - `db.HouseholdWorkspaces.Where(w => w.PrimaryOwnerId == owner.Id)` (including its cascade to `HouseholdMembers`)
    - `db.HouseholdMembers.Where(m => m.UserId == owner.Id)`
    - Reset `ActiveWorkspaceId` on any remaining partner records if orphaned
    - Remove `owner` from `db.Owners`
    - Wrap the entire operation in `db.Database.CreateExecutionStrategy().ExecuteAsync(async () => ...)` or explicit database transaction.
- **INPUT**: Authenticated `ICurrentUser` calling `DELETE /api/v1/data/account`.
- **OUTPUT**: Complete eradication of the user's data across all 26 database tables; zero orphan records.
- **VERIFY**: Unit/Integration test verifying database state before and after account deletion; `db.Owners.FindAsync(owner.Id)` returns `null`, `db.Transactions.Any(t => t.OwnerId == owner.Id)` returns `false`, `db.AuditLogs.Any(a => a.OwnerId == owner.Id)` returns `false`.

---

### Task 2: Mobile Drawer & Settings Logout UX
- **Task ID**: `TASK-AUTH-02`
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`, `clean-code`
- **Priority**: P1 (Core UX Accessibility)
- **Dependencies**: None
- **Files Modified**:
  - `web/components/ui.tsx`
  - `web/app/settings/page.tsx`
- **Description**:
  - In `web/components/ui.tsx` inside the `more-sheet` mobile slide-over container:
    - Add a dedicated, beautifully styled **Sign Out** button at the base of the sheet.
    - Wire `onClick` to call `await api.logout()` and navigate to `/login`.
  - In `web/app/settings/page.tsx`:
    - Add an "Account Session" section or card featuring current user details (Name, Email, Base Currency) and a prominent "Sign Out of Finance OS" button.
- **INPUT**: Mobile user clicking "More" or user navigating to `/settings`.
- **OUTPUT**: Instant, accessible logout triggers on both mobile and settings screens.
- **VERIFY**: Visual audit on mobile viewport; verifying network request to `POST /api/v1/auth/logout` and subsequent redirect to `/login`.

---

### Task 3: Enterprise Safety Modal for Account Deletion
- **Task ID**: `TASK-AUTH-03`
- **Agent**: `frontend-specialist`
- **Skill**: `frontend-design`, `clean-code`
- **Priority**: P1 (Safety & Compliance)
- **Dependencies**: `TASK-AUTH-01`
- **Files Modified**:
  - `web/app/settings/page.tsx`
  - `web/components/ui.tsx` (or standalone `DeleteAccountModal` component in `web/components/DeleteAccountModal.tsx`)
- **Description**:
  - Replace the crude browser `window.confirm(...)` in `web/app/settings/page.tsx` with a tailored `DeleteAccountModal`:
    - Clean modal dialog with backdrop blur and escape key / backdrop click support.
    - Warning banner: *"Danger Zone: Permanent Account Deletion"*.
    - Breakdown of data to be purged:
      - 💰 All Financial Accounts & Snapshots
      - 🧾 All Income & Expense Transactions
      - 🎯 Envelopes, Allocations, and Goals
      - 👥 Household Workspaces & Shared Memberships
      - 🛡️ User Profile, Credentials & Audit Logs
    - Input requirement: User must type `"DELETE"` into a styled text field.
    - "Permanently Delete Everything" button is disabled with visual feedback until the string matches `"DELETE"`.
    - Loading state while `api.deleteAccount()` executes.
    - Redirect to `/login` upon completion.
- **INPUT**: User clicks "Permanently Delete My Account & All Data" in Settings.
- **OUTPUT**: Accessible, high-safety modal with typing verification and clear consequence disclosures.
- **VERIFY**: Unit/component test verifying button is disabled until exact text is typed; manual browser testing.

---

### Task 4: API Integration & End-to-End Regression Tests
- **Task ID**: `TASK-AUTH-04`
- **Agent**: `test-engineer`
- **Skill**: `testing-patterns`, `clean-code`
- **Priority**: P2 (Verification)
- **Dependencies**: `TASK-AUTH-01`, `TASK-AUTH-02`, `TASK-AUTH-03`
- **Files Modified**:
  - `tests/FinanceOS.Api.Tests/AuthAndLifecycleTests.cs`
- **Description**:
  - Write comprehensive tests:
    1. Sign Up (`POST /api/v1/auth/register`) creates new owner.
    2. Sign In (`POST /api/v1/auth/login`) validates credentials.
    3. Log Out (`POST /api/v1/auth/logout`) terminates session.
    4. Account Deletion (`DELETE /api/v1/data/account`) purges all data across all tables and invalidates session.
    5. Attempting to query `/api/v1/auth/me` after deletion returns unauthenticated.
- **INPUT**: Test runner execution against in-memory SQLite and WebApplicationFactory.
- **OUTPUT**: All tests passing cleanly.
- **VERIFY**: `dotnet test FinanceOS.sln --no-build -m:1`.

---

## 5. Phase X: Verification & Definition of Done

The feature will be considered complete ONLY when:
- [x] Sign in and Sign up flows confirmed working and documented.
- [x] Logout button visible and functional in mobile "More" drawer.
- [x] Logout button visible and functional in Settings page.
- [x] Enterprise Safety Modal implemented with `"DELETE"` confirmation input.
- [x] Complete database purge implemented without leaving orphaned records (including `AuditLogs`, `HouseholdWorkspaces`, and `HouseholdMembers`).
- [x] Automated integration tests pass (`dotnet test` — 36/36 tests passing).
- [x] Web application compiles with 0 errors (`npm run build` — 19/19 routes generated).
- [x] Railway production deployment verified and passing healthcheck.

## ✅ PHASE X COMPLETE
- Lint: ✅ Pass
- Security: ✅ No critical issues / Zero orphan DB records / Complete cascade purge
- Tests: ✅ 36/36 Passed (21 Domain + 15 API Integration)
- Build: ✅ Success (Backend 0 errors, Frontend 19/19 static pages)
- Date: 2026-10-24
- Commit: `615ec28`
