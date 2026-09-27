"use client";

import { useEffect, useState } from "react";
import { Shell, Card, MoneyView, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import type { AllocationLine, BudgetItem, CalendarItem, FamilyRow, IncomePreview } from "@/lib/types";

export default function BudgetPage() {
  const [items, setItems] = useState<BudgetItem[]>([]);
  const [salary, setSalary] = useState<AllocationLine[]>([]);
  const [family, setFamily] = useState<FamilyRow[]>([]);
  const [calendar, setCalendar] = useState<CalendarItem[]>([]);
  const [preview, setPreview] = useState<IncomePreview | null>(null);
  const today = new Date();
  const date = today.toISOString().slice(0, 10);

  useEffect(() => {
    void Promise.all([
      api.budget().then(setItems),
      api.plan("salary").then(setSalary),
      api.family().then(setFamily),
      api.calendar(today.getFullYear(), today.getMonth() + 1).then(setCalendar)
    ]);
  }, []);

  return (
    <Shell>
      <h1>Am I following this month&apos;s plan?</h1>
      <p className="lede">Budget, actual, remaining, and a sentence. Transfers are not spending. Family one-offs stay one-offs.</p>

      <div className="row">
        <button className="btn" onClick={() => api.incomePreview("salary", date).then(setPreview)}>Preview salary waterfall</button>
        <button className="btn ghost" onClick={() => api.incomePreview("secondary", date).then(setPreview)}>Preview ₦400k waterfall</button>
      </div>
      {preview && (
        <Reveal watch={preview.planName}>
          <Card title={preview.planName}>
            <p className="sentence">{preview.sentence}</p>
            <ul>{preview.lines.map((line) => <li key={line.label}>{line.label}: {line.amount.formatted}</li>)}</ul>
            {preview.canConfirm && <button className="btn" onClick={() => api.incomeConfirm(preview.planName.includes("Secondary") ? "secondary" : "salary", date).then(() => setPreview(null))}>Confirm and record planned movements</button>}
          </Card>
        </Reveal>
      )}

      <div className="grid two">
        {items.map((item) => (
          <Card key={item.category} title={item.category}>
            <span className={`badge ${item.status.toLowerCase()}`}>{item.status}</span>
            <p className="sentence">{item.sentence}</p>
            <p className="lede">Budget {item.budget.formatted} · actual {item.actual.formatted} · remaining {item.remaining.formatted} · {item.percentUsed}</p>
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
          <p key={row.id} className="sentence">{row.recipient} · {row.kind} · {row.amount.formatted} · {row.purpose}</p>
        ))}
      </Card>

      <Card title="This month on the calendar">
        {calendar.map((item) => (
          <p key={item.date + item.title}>{item.date} — {item.title} {item.amount?.formatted ?? ""}</p>
        ))}
      </Card>
    </Shell>
  );
}
