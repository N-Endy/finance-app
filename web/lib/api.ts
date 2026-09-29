import type {
  Account, ActionItem, AlertItem, AllocationLine, Assistant, AuthStatus, BudgetItem,
  Business, CalendarItem, CounterpartyLoan, Dashboard, Envelope, Explain, ExchangeRate, FamilyRow, FixedAsset, Goal, Holding, IncomePreview,
  Liability, MoneyMapNode, Pension, Preview, Reconciliation, Report, Retirement, Rule, Settings,
  SpendingVelocity, StatementParseResult, Subscription, Transaction, Violation
} from "@/lib/types";

const API = process.env.NEXT_PUBLIC_API_URL ?? "";

async function request<T = never>(path: string, init?: RequestInit): Promise<T> {
  const hasBody = init?.body != null && init.body !== "";
  let response: Response;
  try {
    response = await fetch(`${API}${path}`, {
      ...init,
      credentials: "include",
      headers: {
        ...(hasBody ? { "Content-Type": "application/json" } : {}),
        ...(init?.headers ?? {})
      }
    });
  } catch {
    throw new Error("The ledger is unreachable. Figures stay UNKNOWN until the API is back.");
  }
  if (response.status === 401) {
    if (typeof window !== "undefined" && !window.location.pathname.startsWith("/login")) {
      window.location.href = "/login";
    }
    throw new Error("Sign in required.");
  }
  if (!response.ok) {
    const body = await response.json().catch(() => ({ error: response.statusText }));
    throw new Error(body.error ?? "Request failed.");
  }
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  if (!text.trim()) return undefined as T;
  return JSON.parse(text) as T;
}

export const api = {
  me: () => request<AuthStatus>("/api/v1/auth/me"),
  setup: (body: object) => request("/api/v1/auth/setup", { method: "POST", body: JSON.stringify(body) }),
  login: (body: object) => request("/api/v1/auth/login", { method: "POST", body: JSON.stringify(body) }),
  logout: () => request("/api/v1/auth/logout", { method: "POST" }),
  dashboard: () => request<Dashboard>("/api/v1/dashboard"),
  explain: (metric: string) => request<Explain>(`/api/v1/explain/${metric}`),
  accounts: () => request<Account[]>("/api/v1/accounts"),
  reconciliation: (id: string) => request<Reconciliation>(`/api/v1/accounts/${id}/reconciliation`),
  snapshot: (id: string, body: object) => request(`/api/v1/accounts/${id}/snapshots`, { method: "POST", body: JSON.stringify(body) }),
  assign: (id: string, body: object) => request(`/api/v1/accounts/${id}/assignments`, { method: "POST", body: JSON.stringify(body) }),
  envelopes: () => request<Envelope[]>("/api/v1/envelopes"),
  transactions: (query = "") => request<Transaction[]>(`/api/v1/transactions${query}`),
  preview: (body: object) => request<Preview>("/api/v1/transactions/preview", { method: "POST", body: JSON.stringify(body) }),
  createTx: (body: object) => request("/api/v1/transactions", { method: "POST", body: JSON.stringify(body) }),
  voidTx: (id: string) => request(`/api/v1/transactions/${id}/void`, { method: "POST" }),
  plan: (source: string) => request<AllocationLine[]>(`/api/v1/allocation-plans/${source}`),
  incomePreview: (source: string, date: string) => request<IncomePreview>(`/api/v1/income-receipts/${source}/preview`, { method: "POST", body: JSON.stringify({ date }) }),
  incomeConfirm: (source: string, date: string) => request(`/api/v1/income-receipts/${source}/confirm`, { method: "POST", body: JSON.stringify({ date }) }),
  actualCharge: (body: object) => request("/api/v1/actual-charges", { method: "POST", body: JSON.stringify(body) }),
  moneyMap: () => request<MoneyMapNode[]>("/api/v1/money-map"),
  goals: () => request<Goal[]>("/api/v1/goals"),
  updateGoal: (id: string, body: object) => request(`/api/v1/goals/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  convertHousing: () => request("/api/v1/goals/housing/convert-to-next-rent", { method: "POST" }),
  budget: () => request<BudgetItem[]>("/api/v1/budget"),
  updateBudget: (category: string, body: object) => request(`/api/v1/budget/${encodeURIComponent(category)}`, { method: "PUT", body: JSON.stringify(body) }),
  holdings: () => request<Holding[]>("/api/v1/holdings"),
  updateHolding: (id: string, body: object) => request(`/api/v1/holdings/${id}`, { method: "POST", body: JSON.stringify(body) }),
  rates: () => request<ExchangeRate[]>("/api/v1/exchange-rates"),
  addRate: (body: object) => request("/api/v1/exchange-rates", { method: "POST", body: JSON.stringify(body) }),
  ensureTodaysRate: () => request<ExchangeRate>("/api/v1/exchange-rates/today", { method: "POST" }),
  pension: () => request<Pension>("/api/v1/pension"),
  updatePension: (body: object) => request("/api/v1/pension", { method: "PUT", body: JSON.stringify(body) }),
  retirement: () => request<Retirement>("/api/v1/retirement"),
  updateRetirement: (body: object) => request("/api/v1/retirement", { method: "PUT", body: JSON.stringify(body) }),
  business: (slug = "matchpredictor") => request<Business>(`/api/v1/businesses/${slug}`),
  businesses: () => request<Business[]>("/api/v1/businesses"),
  createBusiness: (body: object) => request<Business>("/api/v1/businesses", { method: "POST", body: JSON.stringify(body) }),
  split: (body: object) => request("/api/v1/business/matchpredictor/split", { method: "PUT", body: JSON.stringify(body) }),
  splitBusiness: (slug: string, body: object) => request(`/api/v1/businesses/${slug}/split`, { method: "PUT", body: JSON.stringify(body) }),
  liabilities: () => request<Liability[]>("/api/v1/liabilities"),
  createLiability: (body: object) => request<Liability>("/api/v1/liabilities", { method: "POST", body: JSON.stringify(body) }),
  updateLiability: (id: string, body: object) => request(`/api/v1/liabilities/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  counterpartyLoans: () => request<CounterpartyLoan[]>("/api/v1/counterparty-loans"),
  createCounterpartyLoan: (body: object) => request<CounterpartyLoan>("/api/v1/counterparty-loans", { method: "POST", body: JSON.stringify(body) }),
  updateCounterpartyLoan: (id: string, body: object) => request(`/api/v1/counterparty-loans/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  actions: () => request<ActionItem[]>("/api/v1/actions"),
  resolveAction: (id: string, verb: "complete" | "skip" | "snooze") => request(`/api/v1/actions/${id}/${verb}`, { method: "POST" }),
  calendar: (year: number, month: number) => request<CalendarItem[]>(`/api/v1/calendar?year=${year}&month=${month}`),
  alerts: () => request<AlertItem[]>("/api/v1/alerts"),
  subscriptions: () => request<Subscription[]>("/api/v1/subscriptions"),
  family: () => request<FamilyRow[]>("/api/v1/family-support"),
  updateFamily: (id: string, body: object) => request(`/api/v1/family-support/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  rules: () => request<Rule[]>("/api/v1/rules"),
  violations: () => request<Violation[]>("/api/v1/violations"),
  settings: () => request<Settings>("/api/v1/settings"),
  updateSettings: (body: object) => request("/api/v1/settings", { method: "PUT", body: JSON.stringify(body) }),
  report: (name: string, from: string, to: string) => request<Report>(`/api/v1/reports/${name}?from=${from}&to=${to}`),
  ask: (message: string) => request<Assistant>("/api/v1/assistant", { method: "POST", body: JSON.stringify({ message }) }),
  importCsv: (csv: string) => request<{ imported: number }>("/api/v1/imports/csv", { method: "POST", body: JSON.stringify({ csv }) }),
  spendingVelocity: () => request<SpendingVelocity>("/api/v1/spending-velocity"),
  parseStatement: (body: { content: string; bankFormat?: string; defaultAccountId?: string }) =>
    request<StatementParseResult>("/api/v1/statements/parse", { method: "POST", body: JSON.stringify(body) }),
  commitStatement: (transactions: object[]) =>
    request<{ committed: number }>("/api/v1/statements/commit", { method: "POST", body: JSON.stringify({ transactions }) }),
  fixedAssets: () => request<FixedAsset[]>("/api/v1/fixed-assets"),
  createFixedAsset: (body: object) => request<FixedAsset>("/api/v1/fixed-assets", { method: "POST", body: JSON.stringify(body) }),
  updateFixedAsset: (id: string, body: object) => request(`/api/v1/fixed-assets/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  updateHoldingValuation: (slug: string, body: object) => request<Holding>(`/api/v1/holdings/${slug}/valuation`, { method: "PUT", body: JSON.stringify(body) }),
  rebalanceBudget: (body: object) => request("/api/v1/budget/rebalance", { method: "POST", body: JSON.stringify(body) }),
  deleteAll: () => request("/api/v1/data", { method: "DELETE" }),
  exportUrl: (format: string) => `${API}/api/v1/exports/${format}`
};
