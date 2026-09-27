"use client";

import { useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import type { Goal } from "@/lib/types";

export default function GoalsPage() {
  const [goals, setGoals] = useState<Goal[]>([]);
  useEffect(() => { void api.goals().then(setGoals); }, []);

  return (
    <Shell>
      <h1>What am I building toward?</h1>
      <p className="lede">Progress uses confirmed balances when they exist. Last-known and expected figures stay labelled. The ₦500m retirement number is an aspiration, not a forecast.</p>
      <div className="grid two">
        {goals.map((goal) => {
          const current = goal.current.minor ?? 0;
          const target = goal.target.minor ?? 1;
          const pct = Math.max(0, Math.min(100, (current / target) * 100));
          return (
            <Card key={goal.id} title={goal.name}>
              <MoneyView money={goal.current} />
              <p className="sentence">{goal.progressSentence}</p>
              <div className="progress" style={{ marginTop: 12 }}><span style={{ width: `${goal.current.minor == null ? 0 : pct}%` }} /></div>
              <p className="lede">Target {goal.target.formatted} · monthly {goal.monthly.formatted}{goal.isAspiration ? " · aspiration" : ""}</p>
            </Card>
          );
        })}
      </div>
    </Shell>
  );
}
