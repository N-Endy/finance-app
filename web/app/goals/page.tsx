"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import type { Goal } from "@/lib/types";

export default function GoalsPage() {
  const [goals, setGoals] = useState<Goal[]>([]);
  const [drafts, setDrafts] = useState<Record<string, { target: string; monthly: string }>>({});
  const [message, setMessage] = useState<string | null>(null);

  async function load() {
    const rows = await api.goals();
    setGoals(rows);
    setDrafts(Object.fromEntries(rows.map((goal) => [goal.id, {
      target: goal.target.major == null ? "" : String(goal.target.major),
      monthly: goal.monthly.major == null ? "" : String(goal.monthly.major)
    }])));
  }

  useEffect(() => { void load(); }, []);

  async function save(event: FormEvent, goal: Goal) {
    event.preventDefault();
    const draft = drafts[goal.id];
    await api.updateGoal(goal.id, { target: Number(draft.target), monthly: Number(draft.monthly) });
    await load();
    setMessage(`${goal.name} saved.`);
  }

  return (
    <Shell>
      <h1>What am I building toward?</h1>
      <p className="lede">Targets and monthly contributions can be edited. Progress uses the latest labelled balance.</p>
      {message && <p className="sentence">{message}</p>}
      <div className="grid two">
        {goals.map((goal) => {
          const current = goal.current.minor ?? 0;
          const target = goal.target.minor ?? 1;
          const pct = Math.max(0, Math.min(100, (current / target) * 100));
          const draft = drafts[goal.id] ?? { target: "", monthly: "" };
          return (
            <Card key={goal.id} title={goal.name}>
              <MoneyView money={goal.current} />
              <p className="sentence">{goal.progressSentence}</p>
              <div className="progress" style={{ marginTop: 12 }}><span style={{ width: `${goal.current.minor == null ? 0 : pct}%` }} /></div>
              <form className="stack" style={{ marginTop: 12 }} onSubmit={(event) => save(event, goal)}>
                <label>
                  Target
                  <input
                    value={draft.target}
                    onChange={(e) => setDrafts((currentDrafts) => ({ ...currentDrafts, [goal.id]: { ...draft, target: e.target.value } }))}
                    inputMode="decimal"
                  />
                </label>
                <label>
                  Monthly contribution
                  <input
                    value={draft.monthly}
                    onChange={(e) => setDrafts((currentDrafts) => ({ ...currentDrafts, [goal.id]: { ...draft, monthly: e.target.value } }))}
                    inputMode="decimal"
                  />
                </label>
                <button className="btn" type="submit">Save</button>
              </form>
              {goal.isAspiration && <p className="lede">Aspiration</p>}
            </Card>
          );
        })}
      </div>
    </Shell>
  );
}
