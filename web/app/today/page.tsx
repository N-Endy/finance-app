"use client";

import { useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { SpendingVelocityWidget } from "@/components/spending-velocity";
import { OfflineIndicator } from "@/components/offline-indicator";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { ActionItem, AlertItem, Violation, CashFlowForecast } from "@/lib/types";

const confirmation = {
  complete: "Marked as done.",
  skip: "Skipped.",
  snooze: "Snoozed until tomorrow."
} as const;

export default function TodayPage() {
  const [items, setItems] = useState<ActionItem[]>([]);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [violations, setViolations] = useState<Violation[]>([]);
  const [forecast, setForecast] = useState<CashFlowForecast | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  async function load() {
    try {
      const [actions, ranked, openViolations, forecastDto] = await Promise.all([
        api.actions(),
        api.alerts(),
        api.violations(),
        api.cashFlowForecast().catch(() => null)
      ]);
      setItems(actions);
      setAlerts(ranked);
      setViolations(openViolations);
      setForecast(forecastDto);
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

      {/* 30/60/90-Day Cash Flow Forecasting & Runway Radar */}
      {forecast && (
        <Card title="⚡ 30 / 60 / 90-Day Cash Flow Radar & Runway">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem", marginBottom: "1rem" }}>
            <div>
              <div style={{ fontSize: "0.8rem", color: "var(--muted, #8e9e96)", textTransform: "uppercase", letterSpacing: "0.05em", fontWeight: 600 }}>
                Liquid Cash Runway Status
              </div>
              <div style={{ fontSize: "1.4rem", fontWeight: 700, marginTop: "0.2rem" }}>
                {forecast.runwayStatus === "Infinite" ? (
                  <span style={{ color: "#10b981" }}>Net Positive Cash Flow (Surplus)</span>
                ) : (
                  <span>
                    ~{forecast.runwayMonths} Months{" "}
                    <span style={{ fontSize: "0.85rem", fontWeight: 400, color: "var(--muted, #8e9e96)" }}>
                      ({forecast.runwayDays} days of liquid runway)
                    </span>
                  </span>
                )}
              </div>
            </div>
            <div>
              <span style={{
                fontSize: "0.8rem",
                padding: "0.35rem 0.75rem",
                borderRadius: "20px",
                fontWeight: 700,
                background: forecast.runwayStatus === "Infinite" || forecast.runwayStatus === "Healthy"
                  ? "rgba(16,185,129,0.15)"
                  : forecast.runwayStatus === "Caution"
                    ? "rgba(245,158,11,0.15)"
                    : "rgba(239,68,68,0.15)",
                color: forecast.runwayStatus === "Infinite" || forecast.runwayStatus === "Healthy"
                  ? "#10b981"
                  : forecast.runwayStatus === "Caution"
                    ? "#f59e0b"
                    : "#ef4444"
              }}>
                {forecast.runwayStatus.toUpperCase()} RUNWAY
              </span>
            </div>
          </div>

          {/* 30 / 60 / 90 Forecast Milestone Cards */}
          <div className="grid three" style={{ gap: "0.75rem", marginBottom: "1rem" }}>
            <div style={{ padding: "0.85rem", background: "rgba(0,0,0,0.2)", borderRadius: "8px", border: "1px solid rgba(255,255,255,0.06)" }}>
              <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)", textTransform: "uppercase" }}>30-Day Outlook</div>
              <div style={{ fontSize: "1.15rem", fontWeight: 700, marginTop: "0.2rem" }}>
                {forecast.forecast30Days.projectedCash.formatted}
              </div>
              <div style={{ fontSize: "0.75rem", color: "var(--muted, #8e9e96)", marginTop: "0.2rem" }}>
                As of {new Date(forecast.forecast30Days.date).toLocaleDateString("en-US", { month: "short", day: "numeric" })}
              </div>
            </div>

            <div style={{ padding: "0.85rem", background: "rgba(0,0,0,0.2)", borderRadius: "8px", border: "1px solid rgba(255,255,255,0.06)" }}>
              <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)", textTransform: "uppercase" }}>60-Day Outlook</div>
              <div style={{ fontSize: "1.15rem", fontWeight: 700, marginTop: "0.2rem" }}>
                {forecast.forecast60Days.projectedCash.formatted}
              </div>
              <div style={{ fontSize: "0.75rem", color: "var(--muted, #8e9e96)", marginTop: "0.2rem" }}>
                As of {new Date(forecast.forecast60Days.date).toLocaleDateString("en-US", { month: "short", day: "numeric" })}
              </div>
            </div>

            <div style={{ padding: "0.85rem", background: "rgba(0,0,0,0.2)", borderRadius: "8px", border: "1px solid rgba(255,255,255,0.06)" }}>
              <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)", textTransform: "uppercase" }}>90-Day Outlook</div>
              <div style={{ fontSize: "1.15rem", fontWeight: 700, marginTop: "0.2rem" }}>
                {forecast.forecast90Days.projectedCash.formatted}
              </div>
              <div style={{ fontSize: "0.75rem", color: "var(--muted, #8e9e96)", marginTop: "0.2rem" }}>
                As of {new Date(forecast.forecast90Days.date).toLocaleDateString("en-US", { month: "short", day: "numeric" })}
              </div>
            </div>
          </div>

          {/* Strategic Insights */}
          <div style={{ fontSize: "0.85rem", display: "flex", flexDirection: "column", gap: "0.35rem", marginBottom: "0.8rem" }}>
            {forecast.strategicInsights.map((insight, idx) => (
              <div key={idx} style={{ color: "var(--foreground, #e5e7eb)" }}>
                💡 {insight}
              </div>
            ))}
          </div>

          {/* Upcoming Commitments Next 30 Days */}
          {forecast.upcomingCommitments.length > 0 && (
            <div style={{ marginTop: "0.75rem", paddingTop: "0.75rem", borderTop: "1px solid rgba(255,255,255,0.06)" }}>
              <div style={{ fontSize: "0.8rem", color: "var(--muted, #8e9e96)", marginBottom: "0.4rem" }}>
                Upcoming Scheduled Commitments:
              </div>
              <div style={{ display: "flex", flexWrap: "wrap", gap: "0.4rem" }}>
                {forecast.upcomingCommitments.map((c, i) => (
                  <span
                    key={i}
                    style={{
                      fontSize: "0.75rem",
                      padding: "0.25rem 0.5rem",
                      background: "rgba(255,255,255,0.05)",
                      borderRadius: "4px",
                      border: "1px solid rgba(255,255,255,0.08)"
                    }}
                  >
                    <strong>{new Date(c.date).toLocaleDateString("en-US", { month: "short", day: "numeric" })}</strong>: {c.description} ({c.amount.formatted})
                  </span>
                ))}
              </div>
            </div>
          )}
        </Card>
      )}

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
