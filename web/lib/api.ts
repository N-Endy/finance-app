const API = process.env.NEXT_PUBLIC_API_URL ?? "";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API}${path}`, {
      ...init,
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
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
  return response.json();
}

export const api = {
  me: () => request("/api/v1/auth/me"),
  setup: (body: object) => request("/api/v1/auth/setup", { method: "POST", body: JSON.stringify(body) }),
  login: (body: object) => request("/api/v1/auth/login", { method: "POST", body: JSON.stringify(body) }),
  logout: () => request("/api/v1/auth/logout", { method: "POST" }),
  dashboard: () => request("/api/v1/dashboard"),
  explain: (metric: string) => request(`/api/v1/explain/${metric}`),
  accounts: () => request("/api/v1/accounts"),
  reconciliation: (id: string) => request<import("@/lib/types").Reconciliation>(`/api/v1/accounts/${id}/reconciliation`),
  snapshot: (id: string, body: object) => request(`/api/v1/accounts/${id}/snapshots`, { method: "POST", body: JSON.stringify(body) }),
  assign: (id: string, body: object) => request(`/api/v1/accounts/${id}/assignments`, { method: "POST", body: JSON.stringify(body) }),
  envelopes: () => request("/api/v1/envelopes"),
  transactions: (query = "") => request(`/api/v1/transactions${query}`),
  preview: (body: object) => request("/api/v1/transactions/preview", { method: "POST", body: JSON.stringify(body) }),
  createTx: (body: object) => request("/api/v1/transactions", { method: "POST", body: JSON.stringify(body) }),
  voidTx: (id: string) => request(`/api/v1/transactions/${id}/void`, { method: "POST" }),
  plan: (source: string) => request(`/api/v1/allocation-plans/${source}`),
  incomePreview: (source: string, date: string) => request(`/api/v1/income-receipts/${source}/preview`, { method: "POST", body: JSON.stringify({ date }) }),
  incomeConfirm: (source: string, date: string) => request(`/api/v1/income-receipts/${source}/confirm`, { method: "POST", body: JSON.stringify({ date }) }),
  actualCharge: (body: object) => request("/api/v1/actual-charges", { method: "POST", body: JSON.stringify(body) }),
  moneyMap: () => request("/api/v1/money-map"),
  goals: () => request("/api/v1/goals"),
  convertHousing: () => request("/api/v1/goals/housing/convert-to-next-rent", { method: "POST" }),
  budget: () => request("/api/v1/budget"),
  holdings: () => request("/api/v1/holdings"),
  updateHolding: (id: string, body: object) => request(`/api/v1/holdings/${id}`, { method: "POST", body: JSON.stringify(body) }),
  rates: () => request("/api/v1/exchange-rates"),
  addRate: (body: object) => request("/api/v1/exchange-rates", { method: "POST", body: JSON.stringify(body) }),
  pension: () => request("/api/v1/pension"),
  updatePension: (body: object) => request("/api/v1/pension", { method: "PUT", body: JSON.stringify(body) }),
  retirement: () => request("/api/v1/retirement"),
  updateRetirement: (body: object) => request("/api/v1/retirement", { method: "PUT", body: JSON.stringify(body) }),
  business: () => request("/api/v1/business/matchpredictor"),
  split: (body: object) => request("/api/v1/business/matchpredictor/split", { method: "PUT", body: JSON.stringify(body) }),
  actions: () => request("/api/v1/actions"),
  resolveAction: (id: string, verb: "complete" | "skip" | "snooze") => request(`/api/v1/actions/${id}/${verb}`, { method: "POST" }),
  calendar: (year: number, month: number) => request(`/api/v1/calendar?year=${year}&month=${month}`),
  alerts: () => request("/api/v1/alerts"),
  subscriptions: () => request("/api/v1/subscriptions"),
  family: () => request("/api/v1/family-support"),
  updateFamily: (id: string, body: object) => request(`/api/v1/family-support/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  rules: () => request("/api/v1/rules"),
  violations: () => request("/api/v1/violations"),
  settings: () => request("/api/v1/settings"),
  updateSettings: (body: object) => request("/api/v1/settings", { method: "PUT", body: JSON.stringify(body) }),
  report: (name: string, from: string, to: string) => request(`/api/v1/reports/${name}?from=${from}&to=${to}`),
  ask: (message: string) => request("/api/v1/assistant", { method: "POST", body: JSON.stringify({ message }) }),
  importCsv: (csv: string) => request("/api/v1/imports/csv", { method: "POST", body: JSON.stringify({ csv }) }),
  deleteAll: () => request("/api/v1/data", { method: "DELETE" }),
  exportUrl: (format: string) => `${API}/api/v1/exports/${format}`
};
