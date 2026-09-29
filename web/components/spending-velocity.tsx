"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Card, MoneyView } from "@/components/ui";
import type { SpendingVelocity } from "@/lib/types";

export function SpendingVelocityWidget({ onLoaded }: { onLoaded?: (data: SpendingVelocity) => void }) {
  const [data, setData] = useState<SpendingVelocity | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void api.spendingVelocity()
      .then((res) => {
        setData(res);
        onLoaded?.(res);
      })
      .catch((err: Error) => setError(err.message));
  }, [onLoaded]);

  if (error) return null;
  if (!data) return <Card title="Daily spending velocity"><p>Calculating pacing…</p></Card>;

  const ratio = Math.min(2.0, Math.max(0, data.pacingRatio));
  const progressPercent = Math.min(100, Math.round((ratio / 1.5) * 100));

  const statusStyles = {
    UnderBudget: { bg: "rgba(30, 224, 135, 0.12)", border: "#1ee087", text: "#1ee087", label: "Pacing under budget" },
    OnTrack: { bg: "rgba(42, 233, 151, 0.12)", border: "#2ae997", text: "#2ae997", label: "Pacing on target" },
    BurningFast: { bg: "rgba(240, 180, 41, 0.15)", border: "#f0b429", text: "#f0b429", label: "Burning too fast" },
    Exhausted: { bg: "rgba(240, 82, 82, 0.15)", border: "#f05252", text: "#f05252", label: "Pool exhausted" }
  }[data.pacingStatus] || { bg: "rgba(255,255,255,0.05)", border: "var(--border)", text: "inherit", label: data.pacingStatus };

  return (
    <Card title="Daily spending velocity">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 12, flexWrap: "wrap", marginBottom: 12 }}>
        <div>
          <span style={{
            fontSize: "0.75rem",
            textTransform: "uppercase",
            letterSpacing: "0.08em",
            color: "var(--text-muted)",
            display: "block",
            marginBottom: 2
          }}>
            Allowed Burn / Day Remaining
          </span>
          <MoneyView money={data.allowedBurnPerDayRemaining} large />
        </div>

        <div style={{
          padding: "4px 10px",
          borderRadius: 999,
          backgroundColor: statusStyles.bg,
          border: `1px solid ${statusStyles.border}`,
          color: statusStyles.text,
          fontSize: "0.8rem",
          fontWeight: 600,
          alignSelf: "center"
        }}>
          {statusStyles.label}
        </div>
      </div>

      {/* SVG Pacing Bar */}
      <div style={{ margin: "14px 0 10px 0" }}>
        <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.75rem", color: "var(--text-muted)", marginBottom: 4 }}>
          <span>Actual burn: {data.actualDailySpendVelocity.formatted}/day</span>
          <span>Target: {data.allowedBurnPerDayRemaining.formatted}/day</span>
        </div>
        <div style={{
          position: "relative",
          width: "100%",
          height: 10,
          backgroundColor: "rgba(255,255,255,0.08)",
          borderRadius: 6,
          overflow: "hidden"
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            bottom: 0,
            width: `${progressPercent}%`,
            backgroundColor: statusStyles.border,
            borderRadius: 6,
            transition: "width 0.4s ease"
          }} />
          {/* Target marker at 66% (which is 1.0x ratio on 1.5 scale) */}
          <div style={{
            position: "absolute",
            left: "66.6%",
            top: 0,
            bottom: 0,
            width: 2,
            backgroundColor: "#ffffff",
            opacity: 0.6
          }} title="Allowed Pace Target (1.0x)" />
        </div>
      </div>

      <p className="sentence" style={{ marginTop: 10 }}>{data.pacingSentence}</p>

      <div className="grid three" style={{ marginTop: 14, fontSize: "0.85rem", gap: 10 }}>
        <div style={{ padding: "8px 12px", background: "rgba(255,255,255,0.02)", borderRadius: 6, border: "1px solid var(--border)" }}>
          <div style={{ color: "var(--text-muted)", fontSize: "0.75rem" }}>Days left in month</div>
          <div style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--text)", marginTop: 2 }}>{data.daysRemainingInMonth} days</div>
        </div>
        <div style={{ padding: "8px 12px", background: "rgba(255,255,255,0.02)", borderRadius: 6, border: "1px solid var(--border)" }}>
          <div style={{ color: "var(--text-muted)", fontSize: "0.75rem" }}>Spendable pool remaining</div>
          <div style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--text)", marginTop: 2 }}>{data.spendablePool.formatted}</div>
        </div>
        <div style={{ padding: "8px 12px", background: "rgba(255,255,255,0.02)", borderRadius: 6, border: "1px solid var(--border)" }}>
          <div style={{ color: "var(--text-muted)", fontSize: "0.75rem" }}>Projected month-end</div>
          <div style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--text)", marginTop: 2 }}>{data.projectedMonthEndSpend.formatted}</div>
        </div>
      </div>
    </Card>
  );
}
