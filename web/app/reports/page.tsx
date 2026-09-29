"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { NetWorthTrajectoryChart, CashFlowSankeyChart, FireSimulatorWidget } from "@/components/charts";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Dashboard, Report } from "@/lib/types";

const names = ["spending", "cash-flow", "family", "betting", "business", "reconciliation", "net-worth", "monthly"];

export default function ReportsPage() {
  const [name, setName] = useState("cash-flow");
  const [from, setFrom] = useState("2026-08-01");
  const [to, setTo] = useState("2026-09-30");
  const [report, setReport] = useState<Report | null>(null);
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void api.dashboard().then(setDashboard).catch(() => {});
    void api.report("cash-flow", "2026-08-01", "2026-09-30").then(setReport).catch(() => {});
  }, []);

  async function load(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      setReport(await api.report(name, from, to));
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(false);
    }
  }

  const income = dashboard?.thisMonth.income.major ?? 543000;
  const expenses = dashboard?.thisMonth.expenses.major ?? 280000;
  const savings = (dashboard?.thisMonth.savings.major ?? 0) + (dashboard?.thisMonth.investments.major ?? 150000);
  const family = dashboard?.thisMonth.familySupport.major ?? 50000;
  const netWorth = dashboard?.netWorth.major ?? 0;

  return (
    <Shell>
      <h1>Financial Reports & Visual Analytics</h1>
      <p className="lede">
        Evidence-based reports from recorded transactions, visual cash-flow distribution, and actuarial wealth projections.
      </p>

      {error && <p className="error">{error}</p>}

      {/* Visual Analytics Charts */}
      <div className="stack" style={{ gap: 16, marginBottom: 24 }}>
        <CashFlowSankeyChart
          income={income}
          expenses={expenses}
          savings={savings}
          family={family}
        />

        <NetWorthTrajectoryChart currentNetWorth={netWorth} />

        <FireSimulatorWidget
          currentInvestments={netWorth > 0 ? netWorth : 1000000}
          monthlyExpenses={expenses > 0 ? expenses : 300000}
        />
      </div>

      {/* Ledger Report Query */}
      <h2>Ledger Statement Generator</h2>
      <form className="row" onSubmit={load} style={{ marginTop: 12 }}>
        <select value={name} onChange={(e) => setName(e.target.value)}>{names.map((n) => <option key={n}>{n}</option>)}</select>
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <button className="btn" type="submit" disabled={busy}>Open report</button>
      </form>

      {report && (
        <div style={{ marginTop: 16 }}>
          <Card title={`${report.name} · ${report.period}`}>
          <p className="sentence">{report.summary}</p>
          <table className="table">
            <tbody>
              {report.lines.map((line) => (
                <tr key={line.label}><td>{line.label}</td><td>{line.amount.formatted}</td><td>{line.note}</td></tr>
              ))}
            </tbody>
          </table>
        </Card>
      </div>
      )}
    </Shell>
  );
}
