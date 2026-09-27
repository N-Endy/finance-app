"use client";

import { FormEvent, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
import type { Report } from "@/lib/types";

const names = ["spending", "cash-flow", "family", "betting", "business", "reconciliation", "net-worth", "monthly"];

export default function ReportsPage() {
  const [name, setName] = useState("spending");
  const [from, setFrom] = useState("2026-08-01");
  const [to, setTo] = useState("2026-09-30");
  const [report, setReport] = useState<Report | null>(null);

  async function load(event: FormEvent) {
    event.preventDefault();
    setReport(await api.report(name, from, to));
  }

  return (
    <Shell>
      <h1>What happened over time?</h1>
      <p className="lede">Every report is built from recorded transactions and labelled balances. Betting is awareness only.</p>
      <form className="row" onSubmit={load}>
        <select value={name} onChange={(e) => setName(e.target.value)}>{names.map((n) => <option key={n}>{n}</option>)}</select>
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <button className="btn" type="submit">Open report</button>
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
