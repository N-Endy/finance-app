"use client";

import { FormEvent, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Report } from "@/lib/types";

const names = ["spending", "cash-flow", "family", "betting", "business", "reconciliation", "net-worth", "monthly"];

export default function ReportsPage() {
  const [name, setName] = useState("spending");
  const [from, setFrom] = useState("2026-08-01");
  const [to, setTo] = useState("2026-09-30");
  const [report, setReport] = useState<Report | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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

  return (
    <Shell>
      <h1>What happened over time?</h1>
      <p className="lede">Reports from recorded transactions and labelled balances.</p>
      {error && <p className="error">{error}</p>}
      <form className="row" onSubmit={load}>
        <select value={name} onChange={(e) => setName(e.target.value)}>{names.map((n) => <option key={n}>{n}</option>)}</select>
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <button className="btn" type="submit" disabled={busy}>Open report</button>
      </form>
      {report && (
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
      )}
    </Shell>
  );
}
