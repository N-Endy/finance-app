"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { AllocationLine, BudgetItem, CalendarItem, FamilyRow, IncomePreview } from "@/lib/types";

function major(value: number | null | undefined) {
  return value == null ? "" : String(value);
}

export default function BudgetPage() {
  const [items, setItems] = useState<BudgetItem[]>([]);
  const [salary, setSalary] = useState<AllocationLine[]>([]);
  const [family, setFamily] = useState<FamilyRow[]>([]);
  const [calendar, setCalendar] = useState<CalendarItem[]>([]);
  const [preview, setPreview] = useState<IncomePreview | null>(null);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [familyDrafts, setFamilyDrafts] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const today = new Date();
  const date = today.toISOString().slice(0, 10);

  async function load() {
    const [budgetItems, salaryLines, familyRows, calendarItems] = await Promise.all([
      api.budget(),
      api.plan("salary"),
      api.family(),
      api.calendar(today.getFullYear(), today.getMonth() + 1)
    ]);
    setItems(budgetItems);
    setSalary(salaryLines);
    setFamily(familyRows);
    setCalendar(calendarItems);
    setDrafts(Object.fromEntries(budgetItems.filter((item) => item.category !== "Betting").map((item) => [item.category, major(item.budget.major)])));
    setFamilyDrafts(Object.fromEntries(familyRows.filter((row) => row.kind === "Recurring").map((row) => [row.id, major(row.amount.major)])));
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

      <div className="row">
        <button className="btn" disabled={busy === "salary"} onClick={() => void previewIncome("salary")}>Preview salary waterfall</button>
        <button className="btn ghost" disabled={busy === "secondary"} onClick={() => void previewIncome("secondary")}>Preview ₦400k waterfall</button>
      </div>
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
    </Shell>
  );
}
