"use client";

import { useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { SpendingVelocityWidget } from "@/components/spending-velocity";
import { OfflineIndicator } from "@/components/offline-indicator";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { ActionItem, AlertItem, Violation } from "@/lib/types";

const confirmation = {
  complete: "Marked as done.",
  skip: "Skipped.",
  snooze: "Snoozed until tomorrow."
} as const;

export default function TodayPage() {
  const [items, setItems] = useState<ActionItem[]>([]);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [violations, setViolations] = useState<Violation[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  async function load() {
    try {
      const [actions, ranked, openViolations] = await Promise.all([
        api.actions(),
        api.alerts(),
        api.violations()
      ]);
      setItems(actions);
      setAlerts(ranked);
      setViolations(openViolations);
    } catch (err) {
      setError(failMessage(err));
    }
  }
  useEffect(() => { void load(); }, []);

  async function resolve(id: string, verb: "complete" | "skip" | "snooze") {
    setError(null);
    setBusyId(id);
    try {
      await api.resolveAction(id, verb);
      setItems((current) => current.filter((item) => item.id !== id));
      setMessage(confirmation[verb]);
      await load();
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  const openViolations = violations.filter((v) => v.isOpen);

  return (
    <Shell>
      <h1>What should I do today?</h1>
      <p className="lede">Today&apos;s recommended actions. Completing one records that you handled it.</p>
      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      <OfflineIndicator />
      <SpendingVelocityWidget />

      <Card title="Alerts, ranked">
        {alerts.map((alert) => <p key={alert.message}><strong>{alert.priority}.</strong> {alert.message}</p>)}
        {alerts.length === 0 && <p>No ranked alerts right now.</p>}
      </Card>

      <Card title="Open violations">
        {openViolations.map((v) => <p key={v.id}>{v.date} · {v.rule}: {v.message}</p>)}
        {openViolations.length === 0 && <p>No open violations.</p>}
      </Card>

      <div className="stack">
        {items.map((item) => (
          <Card key={item.id} title={item.title}>
            <p className="sentence">{item.detail}</p>
            <div className="row" style={{ marginTop: 12 }}>
              <button className="btn" disabled={busyId === item.id} onClick={() => void resolve(item.id, "complete")}>Complete</button>
              <button className="btn ghost" disabled={busyId === item.id} onClick={() => void resolve(item.id, "skip")}>Skip</button>
              <button className="btn ghost" disabled={busyId === item.id} onClick={() => void resolve(item.id, "snooze")}>Snooze</button>
            </div>
          </Card>
        ))}
        {items.length === 0 && <p>No pending actions for today.</p>}
      </div>
    </Shell>
  );
}
