"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { AllocationLine, BudgetItem, CalendarItem, Envelope, FamilyRow, IncomePreview, Rule } from "@/lib/types";

function major(value: number | null | undefined) {
  return value == null ? "" : String(value);
}

export default function BudgetPage() {
  const [items, setItems] = useState<BudgetItem[]>([]);
  const [envelopes, setEnvelopes] = useState<Envelope[]>([]);
  const [salary, setSalary] = useState<AllocationLine[]>([]);
  const [family, setFamily] = useState<FamilyRow[]>([]);
  const [calendar, setCalendar] = useState<CalendarItem[]>([]);
  const [rules, setRules] = useState<Rule[]>([]);
  const [preview, setPreview] = useState<IncomePreview | null>(null);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [familyDrafts, setFamilyDrafts] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [showRebalance, setShowRebalance] = useState(false);
  const [fromEnvelopeId, setFromEnvelopeId] = useState("");
  const [toEnvelopeId, setToEnvelopeId] = useState("");
  const [rebalanceAmount, setRebalanceAmount] = useState("");
  const [rebalanceNotes, setRebalanceNotes] = useState("");
  const today = new Date();
  const date = today.toISOString().slice(0, 10);

  async function load() {
    const [budgetItems, envs, salaryLines, familyRows, calendarItems, planRules] = await Promise.all([
      api.budget(),
      api.envelopes(),
      api.plan("salary"),
      api.family(),
      api.calendar(today.getFullYear(), today.getMonth() + 1),
      api.rules()
    ]);
    setItems(budgetItems);
    setEnvelopes(envs);
    setSalary(salaryLines);
    setFamily(familyRows);
    setCalendar(calendarItems);
    setRules(planRules);
    setDrafts(Object.fromEntries(budgetItems.filter((item) => item.category !== "Betting").map((item) => [item.category, major(item.budget.major)])));
    setFamilyDrafts(Object.fromEntries(familyRows.filter((row) => row.kind === "Recurring").map((row) => [row.id, major(row.amount.major)])));
    if (!fromEnvelopeId && envs.length > 0) setFromEnvelopeId(envs[0].id);
    if (!toEnvelopeId && envs.length > 1) setToEnvelopeId(envs[1].id);
  }

  async function handleRebalance(event: FormEvent) {
    event.preventDefault();
    if (!fromEnvelopeId || !toEnvelopeId || !rebalanceAmount) {
      setError("Please pick both source and target envelopes and enter an amount.");
      return;
    }
    setError(null);
    setMessage(null);
    setBusy("rebalance");
    try {
      await api.rebalanceBudget({
        fromEnvelopeId,
        toEnvelopeId,
        amount: Number(rebalanceAmount),
        notes: rebalanceNotes || "Roll with the punches rebalance"
      });
      await load();
      setShowRebalance(false);
      setRebalanceAmount("");
      setRebalanceNotes("");
      setMessage("Rebalanced envelopes. Zero-sum balance preserved.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  useEffect(() => { void load().catch((err) => setError(failMessage(err))); }, []);

  async function saveBudget(event: FormEvent, category: string) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy(category);
    try {
      await api.updateBudget(category, { amount: Number(drafts[category]) });
      await load();
      setMessage(`${category} saved.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function saveFamily(event: FormEvent, row: FamilyRow) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy(row.id);
    try {
      await api.updateFamily(row.id, { amount: Number(familyDrafts[row.id]), purpose: row.purpose });
      await load();
      setMessage(`${row.recipient} saved.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function previewIncome(source: "salary" | "secondary") {
    setError(null);
    setBusy(source);
    try {
      setPreview(await api.incomePreview(source, date));
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function confirmIncome() {
    if (!preview) return;
    const source = preview.planName.includes("Secondary") ? "secondary" : "salary";
    setError(null);
    setMessage(null);
    setBusy("confirm");
    try {
      await api.incomeConfirm(source, date);
      setPreview(null);
      await load();
      setMessage("Recorded planned movements.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <Shell>
      <h1>Am I following this month&apos;s plan?</h1>
      <p className="lede">Budget, actual, and remaining for each category. Transfers are not spending.</p>
      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      <div className="row" style={{ gap: 8 }}>
        <button className="btn" disabled={busy === "salary"} onClick={() => void previewIncome("salary")}>Preview salary waterfall</button>
        <button className="btn ghost" disabled={busy === "secondary"} onClick={() => void previewIncome("secondary")}>Preview ₦400k waterfall</button>
        <button className="btn ghost" onClick={() => setShowRebalance((v) => !v)}>
          {showRebalance ? "Hide Rebalance" : "Roll with the punches (Rebalance envelopes)"}
        </button>
      </div>

      {showRebalance && (
        <Reveal watch={showRebalance}>
          <Card title="Roll with the punches (Envelope rebalance)">
            <p className="sentence">
              Move money from an envelope with surplus or buffer to cover an overspent envelope. Total account balances remain completely unchanged.
            </p>
            <form className="stack" onSubmit={handleRebalance} style={{ marginTop: 12 }}>
              <div className="grid two">
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>
                    Move money from:
                  </label>
                  <select value={fromEnvelopeId} onChange={(e) => setFromEnvelopeId(e.target.value)}>
                    {envelopes.map((e) => <option key={e.id} value={e.id}>{e.name} ({e.class})</option>)}
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>
                    To envelope:
                  </label>
                  <select value={toEnvelopeId} onChange={(e) => setToEnvelopeId(e.target.value)}>
                    {envelopes.filter((e) => e.id !== fromEnvelopeId).map((e) => <option key={e.id} value={e.id}>{e.name} ({e.class})</option>)}
                  </select>
                </div>
              </div>
              <div className="grid two">
                <input
                  value={rebalanceAmount}
                  onChange={(e) => setRebalanceAmount(e.target.value)}
                  placeholder="Amount to move (₦)"
                  inputMode="decimal"
                />
                <input
                  value={rebalanceNotes}
                  onChange={(e) => setRebalanceNotes(e.target.value)}
                  placeholder="Reason / notes (optional)"
                />
              </div>
              <button className="btn" type="submit" disabled={busy === "rebalance"}>
                {busy === "rebalance" ? "Rebalancing…" : "Execute Zero-Sum Rebalance"}
              </button>
            </form>
          </Card>
        </Reveal>
      )}
      {preview && (
        <Reveal watch={preview.planName}>
          <Card title={preview.planName}>
            <p className="sentence">{preview.sentence}</p>
            <ul>{preview.lines.map((line) => <li key={line.label}>{line.label}: {line.amount.formatted}</li>)}</ul>
            {preview.canConfirm && <button className="btn" disabled={busy === "confirm"} onClick={() => void confirmIncome()}>Confirm and record planned movements</button>}
          </Card>
        </Reveal>
      )}

      <div className="grid two">
        {items.map((item) => (
          <Card key={item.category} title={item.category}>
            <span className={`badge ${item.status.toLowerCase()}`}>{item.status}</span>
            <p className="sentence">{item.sentence}</p>
            <p className="lede">Actual {item.actual.formatted} · remaining {item.remaining.formatted} · {item.percentUsed}</p>
            {item.category !== "Betting" && (
              <form className="row" onSubmit={(event) => saveBudget(event, item.category)}>
                <input
                  value={drafts[item.category] ?? ""}
                  onChange={(e) => setDrafts((current) => ({ ...current, [item.category]: e.target.value }))}
                  inputMode="decimal"
                  aria-label={`${item.category} budget`}
                />
                <button className="btn" type="submit" disabled={busy === item.category}>Save</button>
              </form>
            )}
          </Card>
        ))}
      </div>

      <Card title="Salary allocation">
        <table className="table">
          <tbody>{salary.map((line) => <tr key={line.id}><td>{line.name}</td><td>{line.amount?.formatted ?? "UNKNOWN"}</td><td>{line.rule}</td></tr>)}</tbody>
        </table>
      </Card>

      <Card title="Family commitments vs one-off support">
        {family.map((row) => (
          <div key={row.id} className="stack" style={{ marginBottom: 12 }}>
            <p className="sentence">{row.recipient} · {row.kind} · {row.amount.formatted} · {row.purpose}</p>
            {row.kind === "Recurring" && (
              <form className="row" onSubmit={(event) => saveFamily(event, row)}>
                <input
                  value={familyDrafts[row.id] ?? ""}
                  onChange={(e) => setFamilyDrafts((current) => ({ ...current, [row.id]: e.target.value }))}
                  inputMode="decimal"
                  aria-label={`${row.recipient} amount`}
                />
                <button className="btn" type="submit" disabled={busy === row.id}>Save</button>
              </form>
            )}
          </div>
        ))}
        <p className="lede">Changing a family amount updates the plan. It does not move money between accounts.</p>
      </Card>

      <Card title="This month on the calendar">
        {calendar.map((item) => (
          <p key={item.date + item.title}>{item.date} — {item.title} {item.amount?.formatted ?? ""}</p>
        ))}
      </Card>

      <Card title="Rules">
        {rules.map((rule) => <p key={rule.id}><strong>{rule.title}.</strong> {rule.action} {rule.control}</p>)}
      </Card>
    </Shell>
  );
}
