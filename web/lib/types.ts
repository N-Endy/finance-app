export type Money = {
  minor: number | null;
  major: number | null;
  currency?: string;
  currencyCode?: string;
  formatted: string;
  provenance?: string;
  provenanceLabel?: string;
  asOf?: string | null;
  needed?: string | null;
};

export type Reconciliation = {
  accountId: string;
  accountName: string;
  openingMinor: number | null;
  inflowsMinor: number;
  outflowsMinor: number;
  feesMinor: number;
  adjustmentsMinor: number;
  expectedClosingMinor: number | null;
  actualMinor: number | null;
  differenceMinor: number | null;
  status: string;
  actualAsOf?: string | null;
  actualProvenance?: string | null;
  sentence: string;
};

export type AuthStatus = { needsSetup: boolean; authenticated: boolean; displayName?: string | null };

export type Dashboard = {
  netWorth: Money;
  liquidCash: Money;
  investments: Money;
  emergencyFund: Money;
  emergencySentence: string;
  housingFund: Money;
  housingSentence: string;
  monthlyIncome: Money;
  monthlySpending: Money;
  monthlySavings: Money;
  monthlyInvestment: Money;
  savingsRateSentence?: string | null;
  spendable: Money;
  spendableSentence: string;
  thisMonth: {
    income: Money; expenses: Money; savings: Money; investments: Money;
    familySupport: Money; careerBusiness: Money; betting: Money; transfers: Money;
  };
  actionRequired: string[];
  unknowns: { text: string; href: string }[];
};

export type Explain = { metric: string; sentence: string; formula: string; lines: { label: string; amount: Money; note?: string | null }[]; result: Money };
export type Account = { id: string; slug: string; name: string; institution: string; role: string; job: string; doNotPutHere: string; health: string; healthSentence: string; latestBalance?: Money | null; reconciliationStatus: string };
export type Transaction = { id: string; date: string; type: string; accountId: string; accountName: string; amount: Money; fee: Money; category: string; description: string; isBusiness: boolean; isTransfer: boolean; isVoided: boolean; isBetting: boolean };
export type Preview = { proposed?: Record<string, unknown> | null; summary: string; questions: string[]; balanceImpacts: string[]; budgetImpacts: string[]; canCommit: boolean };
export type Goal = { id: string; slug: string; name: string; kind: string; target: Money; current: Money; progressSentence: string; monthly: Money; isAspiration: boolean };
export type BudgetItem = { category: string; budget: Money; actual: Money; remaining: Money; percentUsed: string; status: string; sentence: string };
export type Holding = { id: string; name: string; amount: Money; liquidity: string; purpose: string; statusNote: string; isExpectedReceivable: boolean };
export type ExchangeRate = { from: string; to: string; rate: number; asOf: string; source: string };
export type MoneyMapNode = { id: string; label: string; kind: string; amount?: Money | null; purpose: string; whenToUse: string; ifSpent: string; children: string[] };
export type ActionItem = { id: string; forDate: string; title: string; detail: string; state: string };
export type AlertItem = { priority: string; message: string };
export type CalendarItem = { date: string; title: string; kind: string; amount?: Money | null };
export type Subscription = { id: string; name: string; amount: Money; frequency: string; nextBillingDate: string; category: string; isBusiness: boolean; isActive: boolean };
export type Business = { name: string; revenue: Money; expenses: Money; net: Money; reinvestPercent: number; personalPercent: number; lines: { category: string; amount: Money }[] };
export type Report = { name: string; period: string; summary: string; lines: { label: string; amount: Money; note?: string | null }[] };
export type Assistant = { question: string; answer: string; evidence: { label: string; amount: Money; note?: string | null }[]; usedEstimate: boolean; missingFacts: string[] };
export type Settings = { displayName: string; opayAllowance: Money; watchPercent: number; warningPercent: number; overBudgetPercent: number; businessReinvestPercent: number; businessPersonalPercent: number; familyLines: { id: string; name: string; recurringAmount?: Money | null; purpose: string; isRecurring: boolean }[]; hasMoved: boolean };
export type Rule = { id: string; code: string; title: string; action: string; control: string; isActive: boolean };
export type Violation = { id: string; rule: string; message: string; date: string; isOpen: boolean };
export type Pension = { balance: Money; sentence: string };
export type Retirement = { sentence: string; scenarios: { name: string; sentence: string; futureValue?: Money | null }[] };
export type FamilyRow = { id: string; recipient: string; date: string; amount: Money; kind: string; purpose: string };
export type AllocationLine = { id: string; sortOrder: number; name: string; kind: string; amount?: Money | null; rule: string };
export type IncomePreview = { planName: string; lines: { label: string; amount: Money; note?: string | null }[]; remainder?: Money | null; sentence: string; canConfirm: boolean };
