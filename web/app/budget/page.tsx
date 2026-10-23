"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type {
  Account, AllocationLine, BudgetItem, CalendarItem, Envelope, FamilyRow, IncomePreview, Rule, UnassignedCash,
  BurnVelocityRadar, AiPaydayAllocationPlan
} from "@/lib/types";

function major(value: number | null | undefined) {
  return value == null ? "" : String(value);
}

export default function BudgetPage() {
  const [items, setItems] = useState<BudgetItem[]>([]);
  const [envelopes, setEnvelopes] = useState<Envelope[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [unassigned, setUnassigned] = useState<UnassignedCash | null>(null);
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

  const [showPaydayAssign, setShowPaydayAssign] = useState(false);
  const [paydayAmounts, setPaydayAmounts] = useState<Record<string, string>>({});
  const [selectedFundingAccount, setSelectedFundingAccount] = useState<string>("");
  const [radar, setRadar] = useState<BurnVelocityRadar | null>(null);
  const [aiPlan, setAiPlan] = useState<AiPaydayAllocationPlan | null>(null);

  const today = new Date();
  const date = today.toISOString().slice(0, 10);

  async function load() {
    const [budgetItems, envs, accs, unassignedDto, salaryLines, familyRows, calendarItems, planRules, radarDto] = await Promise.all([
      api.budget(),
      api.envelopes(),
      api.accounts(),
      api.unassignedCash().catch(() => null),
      api.plan("salary").catch(() => []),
      api.family().catch(() => []),
      api.calendar(today.getFullYear(), today.getMonth() + 1).catch(() => []),
      api.rules().catch(() => []),
      api.velocityRadar().catch(() => null)
    ]);
    setItems(budgetItems);
    setEnvelopes(envs);
    setAccounts(accs);
    setUnassigned(unassignedDto);
    setSalary(salaryLines);
    setFamily(familyRows);
    setCalendar(calendarItems);
    setRules(planRules);
    setRadar(radarDto);
    setDrafts(Object.fromEntries(budgetItems.filter((item) => item.category !== "Betting").map((item) => [item.category, major(item.budget.major)])));
    setFamilyDrafts(Object.fromEntries(familyRows.filter((row) => row.kind === "Recurring").map((row) => [row.id, major(row.amount.major)])));
    if (!fromEnvelopeId && envs.length > 0) setFromEnvelopeId(envs[0].id);
    if (!toEnvelopeId && envs.length > 1) setToEnvelopeId(envs[1].id);
    if (!selectedFundingAccount && accs.length > 0) {
      const positiveAcc = accs.find((a) => (a.latestBalance?.minor ?? 0) > 0) ?? accs[0];
      setSelectedFundingAccount(positiveAcc.id);
    }
  }

  async function handleAiRecommend() {
    setError(null);
    setMessage(null);
    setBusy("ai-alloc");
    try {
      const plan = await api.aiRecommendAllocations();
      setAiPlan(plan);
      const newAmounts: Record<string, string> = {};
      plan.recommendations.forEach((r) => {
        if (r.recommendedAmount > 0) {
          newAmounts[r.envelopeId] = r.recommendedAmount.toFixed(2);
        }
      });
      setPaydayAmounts(newAmounts);
      setShowPaydayAssign(true);
      setMessage(`AI Co-Pilot generated a zero-based allocation: ${plan.summaryRationale}`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleBatchAssign() {
    if (!selectedFundingAccount) {
      setError("Please select a funding account with available balance.");
      return;
    }
    const assignments = Object.entries(paydayAmounts)
      .map(([envId, amountStr]) => ({
        envelopeId: envId,
        amount: parseFloat(amountStr) || 0,
        accountId: selectedFundingAccount,
        asOf: date,
        notes: "Payday allocation"
      }))
      .filter((a) => a.amount > 0);

    if (assignments.length === 0) {
      setError("Please enter at least one envelope allocation amount.");
      return;
    }

    setError(null);
    setMessage(null);
    setBusy("payday-assign");

    try {
      await api.batchAssign(assignments);
      await load();
      setShowPaydayAssign(false);
      setPaydayAmounts({});
      setMessage(`Successfully assigned funds across ${assignments.length} envelopes.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
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
      <h1>Zero-Based Envelope Budgeting</h1>
      <p className="lede">Give every dollar, pound, or naira a job. Budget, actual, and remaining for each category.</p>
      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      {/* Unassigned Cash & Zero-Based Status Banner */}
      {unassigned && (
        <div style={{
          padding: "1.25rem 1.5rem",
          borderRadius: "10px",
          background: (unassigned.unassignedCash.minor ?? 0) > 0
            ? "rgba(16,185,129,0.12)"
            : unassigned.isFullyAssigned
              ? "rgba(255,255,255,0.04)"
              : "rgba(239,68,68,0.12)",
          border: (unassigned.unassignedCash.minor ?? 0) > 0
            ? "1px solid rgba(16,185,129,0.3)"
            : unassigned.isFullyAssigned
              ? "1px solid rgba(255,255,255,0.1)"
              : "1px solid rgba(239,68,68,0.3)",
          marginBottom: "1.25rem"
        }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
            <div>
              <div style={{ fontSize: "0.8rem", textTransform: "uppercase", letterSpacing: "0.05em", color: "var(--muted, #8e9e96)", fontWeight: 600 }}>
                Zero-Based Cash Allocation Status
              </div>
              <div style={{ fontSize: "1.5rem", fontWeight: 700, marginTop: "0.2rem" }}>
                {unassigned.unassignedCash.formatted}
                <span style={{ fontSize: "0.85rem", fontWeight: 400, color: "var(--muted, #8e9e96)", marginLeft: "0.5rem" }}>
                  unassigned to envelopes
                </span>
              </div>
              <p style={{ margin: "0.3rem 0 0", fontSize: "0.88rem", color: "var(--muted, #8e9e96)" }}>
                {unassigned.sentence}
              </p>
            </div>
            {(unassigned.unassignedCash.minor ?? 0) > 0 && (
              <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                <button
                  type="button"
                  className="btn"
                  disabled={busy === "ai-alloc"}
                  onClick={() => void handleAiRecommend()}
                  style={{
                    fontWeight: 600,
                    background: "rgba(16,185,129,0.2)",
                    border: "1px solid rgba(16,185,129,0.5)"
                  }}
                >
                  {busy === "ai-alloc" ? "Consulting AI Advisor..." : "✨ AI Auto-Fill Allocation"}
                </button>
                <button
                  type="button"
                  className="btn ghost"
                  onClick={() => setShowPaydayAssign((v) => !v)}
                  style={{ fontWeight: 600 }}
                >
                  {showPaydayAssign ? "Hide Tool" : "Manual Allocation"}
                </button>
              </div>
            )}
          </div>

          {showPaydayAssign && (
            <div style={{ marginTop: "1.25rem", paddingTop: "1.25rem", borderTop: "1px solid rgba(255,255,255,0.08)" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.5rem", marginBottom: "0.4rem" }}>
                <h3 style={{ fontSize: "1.05rem", fontWeight: 600, margin: 0 }}>
                  Payday Envelope Allocation (&quot;Give Every Money A Job&quot;)
                </h3>
                {aiPlan && (
                  <span style={{ fontSize: "0.75rem", background: "rgba(16,185,129,0.15)", color: "#10b981", padding: "0.2rem 0.5rem", borderRadius: "4px", fontWeight: 600 }}>
                    ✨ AI Co-Pilot Active
                  </span>
                )}
              </div>
              <p style={{ fontSize: "0.85rem", color: "var(--muted, #8e9e96)", margin: "0 0 1rem" }}>
                Distribute liquid funds across your active envelopes. Zero unassigned cash = perfectly balanced ledger.
              </p>

              {aiPlan && (
                <div style={{
                  padding: "0.85rem 1rem",
                  background: "rgba(16,185,129,0.08)",
                  border: "1px solid rgba(16,185,129,0.25)",
                  borderRadius: "6px",
                  marginBottom: "1rem",
                  fontSize: "0.85rem"
                }}>
                  <div style={{ fontWeight: 600, color: "#10b981", marginBottom: "0.2rem" }}>
                    Allocation Strategy Rationale:
                  </div>
                  <div style={{ color: "var(--foreground, #e5e7eb)" }}>
                    {aiPlan.summaryRationale}
                  </div>
                </div>
              )}

              <div style={{ marginBottom: "1rem", maxWidth: "340px" }}>
                <label style={{ fontSize: "0.8rem", color: "var(--muted, #8e9e96)", display: "block", marginBottom: "0.3rem" }}>
                  Funding Source Account:
                </label>
                <select
                  value={selectedFundingAccount}
                  onChange={(e) => setSelectedFundingAccount(e.target.value)}
                  style={{ width: "100%", padding: "0.6rem", borderRadius: "6px", background: "rgba(0,0,0,0.3)", color: "#fff", border: "1px solid rgba(255,255,255,0.15)" }}
                >
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name} ({a.latestBalance?.formatted ?? "No balance"})
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.75rem", marginBottom: "1rem" }}>
                {envelopes.map((env) => {
                  const rec = aiPlan?.recommendations.find((r) => r.envelopeId === env.id);
                  return (
                    <div key={env.id} style={{ background: "rgba(0,0,0,0.2)", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.06)" }}>
                      <div style={{ fontWeight: 600, fontSize: "0.88rem" }}>{env.name}</div>
                      <div style={{ fontSize: "0.75rem", color: "var(--muted, #8e9e96)", marginBottom: "0.4rem" }}>{env.purpose || env.class}</div>
                      <input
                        type="number"
                        step="0.01"
                        placeholder="0.00"
                        value={paydayAmounts[env.id] ?? ""}
                        onChange={(e) => setPaydayAmounts((prev) => ({ ...prev, [env.id]: e.target.value }))}
                        style={{ width: "100%", padding: "0.5rem", borderRadius: "4px", background: "rgba(255,255,255,0.05)", border: "1px solid rgba(255,255,255,0.1)", color: "#fff" }}
                      />
                      {rec && rec.recommendedAmount > 0 && (
                        <div style={{ marginTop: "0.35rem", fontSize: "0.72rem", color: "#10b981", lineHeight: 1.3 }}>
                          💡 {rec.rationale}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>

              <div style={{ display: "flex", gap: "0.75rem", alignItems: "center" }}>
                <button
                  type="button"
                  className="btn"
                  disabled={busy === "payday-assign"}
                  onClick={() => void handleBatchAssign()}
                  style={{ fontWeight: 600 }}
                >
                  {busy === "payday-assign" ? "Saving allocations..." : "Confirm & Assign to Envelopes"}
                </button>
                <button
                  type="button"
                  className="btn ghost"
                  onClick={() => { setPaydayAmounts({}); setShowPaydayAssign(false); setAiPlan(null); }}
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Burn Velocity Radar */}
      {radar && radar.items.length > 0 && (
        <Card title="⚡ Burn Velocity Radar (Pacing Early-Warning)">
          <p className="sentence" style={{ margin: "0 0 1rem", fontSize: "0.88rem" }}>
            {radar.overallAssessment} (Day {radar.daysElapsedInMonth} of {radar.totalDaysInMonth})
          </p>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "0.75rem", marginBottom: "1.25rem" }}>
            {radar.items.map((item) => {
              const isCrit = item.status === "Critical";
              const isCaut = item.status === "Caution";
              const badgeBg = isCrit ? "rgba(239,68,68,0.15)" : isCaut ? "rgba(245,158,11,0.15)" : "rgba(16,185,129,0.15)";
              const badgeColor = isCrit ? "#ef4444" : isCaut ? "#f59e0b" : "#10b981";
              const borderColor = isCrit ? "rgba(239,68,68,0.3)" : isCaut ? "rgba(245,158,11,0.3)" : "rgba(255,255,255,0.06)";

              return (
                <div
                  key={item.envelopeId}
                  style={{
                    background: "rgba(0,0,0,0.2)",
                    padding: "0.85rem",
                    borderRadius: "8px",
                    border: `1px solid ${borderColor}`
                  }}
                >
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "0.3rem" }}>
                    <div style={{ fontWeight: 600, fontSize: "0.92rem" }}>{item.envelopeName}</div>
                    <span style={{ fontSize: "0.7rem", fontWeight: 700, padding: "0.15rem 0.4rem", borderRadius: "4px", background: badgeBg, color: badgeColor }}>
                      {item.status.toUpperCase()}
                    </span>
                  </div>
                  <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)", marginBottom: "0.4rem" }}>
                    {item.remainingAmount.formatted} left / {item.budgetedAmount.formatted} assigned
                  </div>
                  <div style={{ fontSize: "0.8rem", lineHeight: 1.35, color: isCrit ? "#fca5a5" : isCaut ? "#fde68a" : "var(--muted, #8e9e96)" }}>
                    {item.message}
                  </div>
                </div>
              );
            })}
          </div>
        </Card>
      )}

      <div className="row" style={{ gap: 8 }}>
        {salary.length > 0 && (
          <button className="btn" disabled={busy === "salary"} onClick={() => void previewIncome("salary")}>
            Preview salary waterfall
          </button>
        )}
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
                  placeholder="Amount to move"
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

      {salary.length > 0 && (
        <Card title="Salary allocation waterfall">
          <table className="table">
            <tbody>{salary.map((line) => <tr key={line.id}><td>{line.name}</td><td>{line.amount?.formatted ?? "UNKNOWN"}</td><td>{line.rule}</td></tr>)}</tbody>
          </table>
        </Card>
      )}

      {family.length > 0 && (
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
      )}

      {calendar.length > 0 && (
        <Card title="This month on the calendar">
          {calendar.map((item) => (
            <p key={item.date + item.title}>{item.date} — {item.title} {item.amount?.formatted ?? ""}</p>
          ))}
        </Card>
      )}

      {rules.length > 0 && (
        <Card title="Rules">
          {rules.map((rule) => <p key={rule.id}><strong>{rule.title}.</strong> {rule.action} {rule.control}</p>)}
        </Card>
      )}
    </Shell>
  );
}
