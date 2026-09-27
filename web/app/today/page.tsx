"use client";

import { useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
import type { ActionItem } from "@/lib/types";

export default function TodayPage() {
  const [items, setItems] = useState<ActionItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    try { setItems(await api.actions()); } catch (err) { setError(err instanceof Error ? err.message : "Error"); }
  }
  useEffect(() => { void load(); }, []);

  return (
    <Shell>
      <h1>What should I do today?</h1>
      <p className="lede">Today&apos;s recommended actions. Completing one records that you handled it.</p>
      {error && <p className="error">{error}</p>}
      <div className="stack">
        {items.map((item) => (
          <Card key={item.id} title={item.title}>
            <p className="sentence">{item.detail}</p>
            <div className="row" style={{ marginTop: 12 }}>
              <button className="btn" onClick={() => api.resolveAction(item.id, "complete").then(load)}>Complete</button>
              <button className="btn ghost" onClick={() => api.resolveAction(item.id, "skip").then(load)}>Skip</button>
              <button className="btn ghost" onClick={() => api.resolveAction(item.id, "snooze").then(load)}>Snooze</button>
            </div>
          </Card>
        ))}
        {items.length === 0 && <p>No pending actions for today.</p>}
      </div>
    </Shell>
  );
}
