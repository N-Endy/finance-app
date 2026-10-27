"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Account, Goal } from "@/lib/types";

export default function GoalsPage() {
  const [goals, setGoals] = useState<Goal[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [drafts, setDrafts] = useState<Record<string, { target: string; monthly: string }>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  // Create Goal State
  const [showAddGoal, setShowAddGoal] = useState(false);
  const [newGoalName, setNewGoalName] = useState("");
  const [newGoalKind, setNewGoalKind] = useState("SinkingFund");
  const [newGoalTarget, setNewGoalTarget] = useState("");
  const [newGoalMonthly, setNewGoalMonthly] = useState("");
  const [newGoalDeadline, setNewGoalDeadline] = useState("");
  const [newGoalFundingAccountId, setNewGoalFundingAccountId] = useState("");
  const [newGoalIsAspiration, setNewGoalIsAspiration] = useState(false);

  // Edit Goal State
  const [editingGoal, setEditingGoal] = useState<{
    id: string;
    name: string;
    kind: string;
    target: string;
    monthly: string;
    deadline: string;
    fundingAccountId: string;
    isAspiration: boolean;
  } | null>(null);

  async function load() {
    const [rows, accs] = await Promise.all([
      api.goals(),
      api.accounts().catch(() => [])
    ]);
    setGoals(rows);
    setAccounts(accs);
    setDrafts(Object.fromEntries(rows.map((goal) => [goal.id, {
      target: goal.target.major == null ? "" : String(goal.target.major),
      monthly: goal.monthly.major == null ? "" : String(goal.monthly.major)
    }])));
  }

  useEffect(() => { void load().catch((err) => setError(failMessage(err))); }, []);

  async function handleCreateGoal(e: FormEvent) {
    e.preventDefault();
    if (!newGoalName.trim()) return;
    setError(null);
    setMessage(null);
    setBusyId("create-goal");
    try {
      await api.createGoal({
        name: newGoalName.trim(),
        kind: newGoalKind,
        target: newGoalTarget ? parseFloat(newGoalTarget) : 0,
        monthly: newGoalMonthly ? parseFloat(newGoalMonthly) : 0,
        deadline: newGoalDeadline || undefined,
        fundingAccountId: newGoalFundingAccountId || undefined,
        isAspiration: newGoalIsAspiration
      });
      setShowAddGoal(false);
      setNewGoalName("");
      setNewGoalTarget("");
      setNewGoalMonthly("");
      setNewGoalDeadline("");
      setNewGoalFundingAccountId("");
      setNewGoalIsAspiration(false);
      await load();
      setMessage(`Goal "${newGoalName.trim()}" created successfully.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  function startEditGoal(goal: Goal) {
    setEditingGoal({
      id: goal.id,
      name: goal.name,
      kind: goal.kind,
      target: goal.target.major != null ? String(goal.target.major) : "",
      monthly: goal.monthly.major != null ? String(goal.monthly.major) : "",
      deadline: goal.deadline || "",
      fundingAccountId: goal.fundingAccountId || "",
      isAspiration: goal.isAspiration
    });
  }

  async function handleSaveEditGoal(e: FormEvent) {
    e.preventDefault();
    if (!editingGoal || !editingGoal.name.trim()) return;
    setError(null);
    setMessage(null);
    setBusyId("edit-goal");
    try {
      await api.updateGoal(editingGoal.id, {
        name: editingGoal.name.trim(),
        kind: editingGoal.kind,
        target: editingGoal.target ? parseFloat(editingGoal.target) : 0,
        monthly: editingGoal.monthly ? parseFloat(editingGoal.monthly) : 0,
        deadline: editingGoal.deadline || undefined,
        fundingAccountId: editingGoal.fundingAccountId || undefined,
        isAspiration: editingGoal.isAspiration
      });
      setEditingGoal(null);
      await load();
      setMessage(`Goal "${editingGoal.name}" updated successfully.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  async function handleDeleteGoal(goal: Goal) {
    if (!confirm(`Are you sure you want to remove the "${goal.name}" goal?`)) return;
    setError(null);
    setMessage(null);
    setBusyId(`delete-${goal.id}`);
    try {
      await api.deleteGoal(goal.id);
      await load();
      setMessage(`Goal "${goal.name}" was removed.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  async function saveInline(event: FormEvent, goal: Goal) {
    event.preventDefault();
    const draft = drafts[goal.id];
    setError(null);
    setMessage(null);
    setBusyId(goal.id);
    try {
      await api.updateGoal(goal.id, { target: Number(draft.target), monthly: Number(draft.monthly) });
      await load();
      setMessage(`${goal.name} saved.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <Shell>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "1rem", marginBottom: "0.5rem" }}>
        <div>
          <h1>What am I building toward?</h1>
          <p className="lede">Savings targets, sinking funds, and financial aspirations. Progress is tracked from confirmed balances.</p>
        </div>
        <button
          className="btn"
          style={{ whiteSpace: "nowrap", padding: "0.6rem 1.2rem", fontWeight: 600 }}
          onClick={() => { setShowAddGoal(!showAddGoal); setEditingGoal(null); }}
        >
          {showAddGoal ? "Cancel" : "+ Add Goal"}
        </button>
      </div>

      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      {/* Add Goal Modal Card */}
      {showAddGoal && (
        <Card title="Add a New Financial Goal or Sinking Fund">
          <form className="stack" onSubmit={handleCreateGoal}>
            <div className="grid two" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Goal Name *
                </label>
                <input
                  required
                  placeholder="e.g. 6-Month Emergency Buffer, New Vehicle, Vacation in Tokyo"
                  value={newGoalName}
                  onChange={(e) => setNewGoalName(e.target.value)}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Goal Category / Type
                </label>
                <select value={newGoalKind} onChange={(e) => setNewGoalKind(e.target.value)}>
                  <option value="SinkingFund">Sinking Fund (Planned irregular expenditure)</option>
                  <option value="Emergency">Emergency Reserve (Safety buffer)</option>
                  <option value="Housing">Housing & Downpayment</option>
                  <option value="NextRent">Next Rent Cycle</option>
                  <option value="Children">Children & Education Fund</option>
                  <option value="Retirement">Long-Term Wealth & Retirement</option>
                  <option value="OperatingReserve">Business Operating Reserve</option>
                </select>
              </div>
            </div>

            <div className="grid two" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Target Amount *
                </label>
                <input
                  type="number"
                  step="any"
                  min="0"
                  required
                  placeholder="0.00"
                  value={newGoalTarget}
                  onChange={(e) => setNewGoalTarget(e.target.value)}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Monthly Contribution
                </label>
                <input
                  type="number"
                  step="any"
                  min="0"
                  placeholder="0.00"
                  value={newGoalMonthly}
                  onChange={(e) => setNewGoalMonthly(e.target.value)}
                />
              </div>
            </div>

            <div className="grid two" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Target Completion Deadline (Optional)
                </label>
                <input
                  type="date"
                  value={newGoalDeadline}
                  onChange={(e) => setNewGoalDeadline(e.target.value)}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Linked Funding Account (Optional)
                </label>
                <select
                  value={newGoalFundingAccountId}
                  onChange={(e) => setNewGoalFundingAccountId(e.target.value)}
                >
                  <option value="">General Net Worth (Unassigned)</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name} ({a.institution} · {a.latestBalance?.formatted ?? "No balance"})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div style={{ marginTop: "0.4rem" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.88rem", cursor: "pointer" }}>
                <input
                  type="checkbox"
                  checked={newGoalIsAspiration}
                  onChange={(e) => setNewGoalIsAspiration(e.target.checked)}
                />
                <span>This is a long-term dream or aspiration (e.g. Financial Freedom)</span>
              </label>
            </div>

            <div className="row" style={{ marginTop: "0.5rem" }}>
              <button className="btn" type="submit" disabled={busyId === "create-goal"}>
                {busyId === "create-goal" ? "Saving..." : "Create Goal"}
              </button>
              <button className="btn ghost" type="button" onClick={() => setShowAddGoal(false)}>
                Cancel
              </button>
            </div>
          </form>
        </Card>
      )}

      {/* Edit Goal Modal Card */}
      {editingGoal && (
        <Card title={`Edit Goal: ${editingGoal.name}`}>
          <form className="stack" onSubmit={handleSaveEditGoal}>
            <div className="grid two" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Goal Name *
                </label>
                <input
                  required
                  placeholder="e.g. 6-Month Emergency Buffer"
                  value={editingGoal.name}
                  onChange={(e) => setEditingGoal({ ...editingGoal, name: e.target.value })}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Goal Category / Type
                </label>
                <select
                  value={editingGoal.kind}
                  onChange={(e) => setEditingGoal({ ...editingGoal, kind: e.target.value })}
                >
                  <option value="SinkingFund">Sinking Fund (Planned irregular expenditure)</option>
                  <option value="Emergency">Emergency Reserve (Safety buffer)</option>
                  <option value="Housing">Housing & Downpayment</option>
                  <option value="NextRent">Next Rent Cycle</option>
                  <option value="Children">Children & Education Fund</option>
                  <option value="Retirement">Long-Term Wealth & Retirement</option>
                  <option value="OperatingReserve">Business Operating Reserve</option>
                </select>
              </div>
            </div>

            <div className="grid two" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Target Amount *
                </label>
                <input
                  type="number"
                  step="any"
                  min="0"
                  required
                  placeholder="0.00"
                  value={editingGoal.target}
                  onChange={(e) => setEditingGoal({ ...editingGoal, target: e.target.value })}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Monthly Contribution
                </label>
                <input
                  type="number"
                  step="any"
                  min="0"
                  placeholder="0.00"
                  value={editingGoal.monthly}
                  onChange={(e) => setEditingGoal({ ...editingGoal, monthly: e.target.value })}
                />
              </div>
            </div>

            <div className="grid two" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Target Completion Deadline (Optional)
                </label>
                <input
                  type="date"
                  value={editingGoal.deadline}
                  onChange={(e) => setEditingGoal({ ...editingGoal, deadline: e.target.value })}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>
                  Linked Funding Account (Optional)
                </label>
                <select
                  value={editingGoal.fundingAccountId}
                  onChange={(e) => setEditingGoal({ ...editingGoal, fundingAccountId: e.target.value })}
                >
                  <option value="">General Net Worth (Unassigned)</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name} ({a.institution} · {a.latestBalance?.formatted ?? "No balance"})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div style={{ marginTop: "0.4rem" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.88rem", cursor: "pointer" }}>
                <input
                  type="checkbox"
                  checked={editingGoal.isAspiration}
                  onChange={(e) => setEditingGoal({ ...editingGoal, isAspiration: e.target.checked })}
                />
                <span>This is a long-term dream or aspiration</span>
              </label>
            </div>

            <div className="row" style={{ marginTop: "0.5rem" }}>
              <button className="btn" type="submit" disabled={busyId === "edit-goal"}>
                {busyId === "edit-goal" ? "Saving..." : "Save Changes"}
              </button>
              <button className="btn ghost" type="button" onClick={() => setEditingGoal(null)}>
                Cancel
              </button>
            </div>
          </form>
        </Card>
      )}

      {/* Empty State when no goals exist */}
      {goals.length === 0 && !showAddGoal && (
        <Card title="No Financial Goals Configured Yet">
          <p className="sentence" style={{ fontSize: "1rem" }}>
            You haven&apos;t set up any savings targets, sinking funds, or long-term financial goals yet.
          </p>
          <p className="lede" style={{ marginTop: "0.4rem" }}>
            Finance OS tracks your progress in real time using confirmed balances from your linked bank accounts, vaults, or investment holdings.
          </p>
          <div style={{ marginTop: "1.25rem" }}>
            <button
              className="btn"
              type="button"
              style={{ fontWeight: 600, padding: "0.6rem 1.4rem" }}
              onClick={() => setShowAddGoal(true)}
            >
              + Create Your First Goal
            </button>
          </div>
        </Card>
      )}

      {/* Goals Cards Grid */}
      <div className="grid two">
        {goals.map((goal) => {
          const current = goal.current.minor ?? 0;
          const target = goal.target.minor ?? 1;
          const pct = Math.max(0, Math.min(100, (current / target) * 100));
          const draft = drafts[goal.id] ?? { target: "", monthly: "" };
          const linkedAccount = accounts.find((a) => a.id === goal.fundingAccountId);

          return (
            <Card key={goal.id} title={goal.name}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.6rem" }}>
                <div style={{ display: "flex", gap: "0.4rem", flexWrap: "wrap" }}>
                  <span className="badge" style={{ textTransform: "capitalize" }}>{goal.kind}</span>
                  {goal.isAspiration && <span className="badge" style={{ background: "rgba(245,158,11,0.2)", color: "#f59e0b" }}>Aspiration</span>}
                </div>
                {linkedAccount && (
                  <span style={{ fontSize: "0.76rem", color: "var(--muted, #8e9e96)" }}>
                    🏦 {linkedAccount.name}
                  </span>
                )}
              </div>

              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", marginBottom: "0.2rem" }}>
                <div>
                  <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)" }}>Current Saved</div>
                  <MoneyView money={goal.current} />
                </div>
                <div style={{ textAlign: "right" }}>
                  <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)" }}>Target</div>
                  <div style={{ fontSize: "1.1rem", fontWeight: 700 }}>{goal.target.formatted}</div>
                </div>
              </div>

              <p className="sentence" style={{ fontSize: "0.85rem", margin: "0.4rem 0" }}>
                {goal.progressSentence}
              </p>

              <div className="progress" style={{ marginTop: 8, height: "8px", borderRadius: "4px" }}>
                <span style={{ width: `${goal.current.minor == null ? 0 : pct}%`, background: pct >= 100 ? "#10b981" : undefined }} />
              </div>

              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.8rem", color: "var(--muted, #8e9e96)", marginTop: "0.4rem" }}>
                <span>{pct.toFixed(0)}% funded</span>
                {goal.deadline && <span>Target: {goal.deadline}</span>}
              </div>

              {goal.requiredMonthly && (
                <p className="lede" style={{ fontSize: "0.8rem", margin: "0.4rem 0" }}>
                  Pacing requirement: <strong>{goal.requiredMonthly.formatted}</strong>/month to hit deadline.
                </p>
              )}

              {/* Quick Inline Adjustment Form */}
              <form className="stack" style={{ marginTop: 12 }} onSubmit={(event) => saveInline(event, goal)}>
                <div className="grid two" style={{ gap: "0.5rem" }}>
                  <label style={{ fontSize: "0.78rem" }}>
                    Target
                    <input
                      value={draft.target}
                      onChange={(e) => setDrafts((currentDrafts) => ({ ...currentDrafts, [goal.id]: { ...draft, target: e.target.value } }))}
                      inputMode="decimal"
                    />
                  </label>
                  <label style={{ fontSize: "0.78rem" }}>
                    Monthly Plan
                    <input
                      value={draft.monthly}
                      onChange={(e) => setDrafts((currentDrafts) => ({ ...currentDrafts, [goal.id]: { ...draft, monthly: e.target.value } }))}
                      inputMode="decimal"
                    />
                  </label>
                </div>
                <button className="btn" type="submit" disabled={busyId === goal.id}>
                  {busyId === goal.id ? "Saving..." : "Quick Save"}
                </button>
              </form>

              {/* Card Actions Toolbar */}
              <div className="row" style={{ marginTop: "0.85rem", justifyContent: "space-between", borderTop: "1px solid rgba(255,255,255,0.06)", paddingTop: "0.5rem" }}>
                <button
                  type="button"
                  className="btn ghost"
                  style={{ fontSize: "0.82rem", padding: "0.35rem 0.65rem" }}
                  onClick={() => startEditGoal(goal)}
                >
                  ✏️ Edit Goal
                </button>
                <button
                  type="button"
                  className="btn ghost"
                  style={{ fontSize: "0.82rem", padding: "0.35rem 0.65rem", color: "#ef4444" }}
                  disabled={busyId === `delete-${goal.id}`}
                  onClick={() => void handleDeleteGoal(goal)}
                >
                  {busyId === `delete-${goal.id}` ? "Removing..." : "🗑️ Delete"}
                </button>
              </div>
            </Card>
          );
        })}
      </div>
    </Shell>
  );
}
