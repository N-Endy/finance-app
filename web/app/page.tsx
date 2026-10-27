"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Shell, Card, MoneyView, ExplainModal } from "@/components/ui";
import { SpendingVelocityWidget } from "@/components/spending-velocity";
import { OfflineIndicator } from "@/components/offline-indicator";
import { AskLedger } from "@/components/ask-ledger";
import { api } from "@/lib/api";
import type { Dashboard } from "@/lib/types";

export default function HomePage() {
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [metric, setMetric] = useState<string | null>(null);

  useEffect(() => {
    void api.dashboard().then(setData).catch((err: Error) => setError(err.message));
  }, []);

  if (error) return <Shell><p className="error">{error}</p></Shell>;
  if (!data) return <Shell><p>Loading…</p></Shell>;

  return (
    <Shell>
      <h1>How am I doing financially?</h1>
      <p className="lede">Net worth, spendable cash, and the jobs your money already has.</p>

      {data.hasLegacyDemoData && (
        <div style={{
          background: "linear-gradient(135deg, rgba(234, 179, 8, 0.12), rgba(234, 179, 8, 0.04))",
          border: "1px solid rgba(234, 179, 8, 0.35)",
          borderRadius: "10px",
          padding: "1rem 1.25rem",
          marginBottom: "1.25rem",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: "1rem"
        }}>
          <div>
            <strong style={{ color: "#fef08a", fontSize: "0.95rem" }}>Sample Demo Data Loaded</strong>
            <p style={{ margin: "0.25rem 0 0", color: "#fef9c3", fontSize: "0.85rem", opacity: 0.9 }}>
              Your workspace currently has demo accounts (Stanbic, OPay, Cowrywise) loaded. You can clear this anytime and configure your real bank accounts and currency.
            </p>
          </div>
          <button
            className="btn"
            style={{
              background: "#ca8a04",
              color: "#000",
              fontWeight: 700,
              padding: "0.55rem 1rem",
              borderRadius: "6px",
              cursor: "pointer",
              border: "none",
              fontSize: "0.85rem"
            }}
            onClick={async () => {
              if (confirm("Reset workspace and start fresh with your real accounts?")) {
                await api.resetWorkspace();
                window.location.href = "/onboarding";
              }
            }}
          >
            Start Fresh With My Real Accounts →
          </button>
        </div>
      )}

      <OfflineIndicator />

      {data.unknowns.length > 0 && (
        <Card title="Still to enter">
          <ul>{data.unknowns.map((item) => (
            <li key={item.href + item.text}><Link href={item.href}>{item.text}</Link></li>
          ))}</ul>
        </Card>
      )}

      <div style={{ marginTop: 16 }}>
        <SpendingVelocityWidget />
      </div>

      <div className="grid four" style={{ marginTop: 16 }}>
        <button className="card map-node" onClick={() => setMetric("net-worth")}>
          <h3>Confirmed net worth</h3>
          <MoneyView money={data.netWorth} large />
        </button>
        <button className="card map-node" onClick={() => setMetric("spendable")}>
          <h3>Actually spendable</h3>
          <MoneyView money={data.spendable} large />
          <p className="sentence" style={{ marginTop: 8 }}>{data.spendableSentence}</p>
        </button>
        <Card title="Emergency">
          <MoneyView money={data.emergencyFund} />
          <p className="sentence" style={{ marginTop: 8 }}>{data.emergencySentence}</p>
        </Card>
        <button className="card map-node" onClick={() => setMetric("housing-gap")}>
          <h3>Housing</h3>
          <MoneyView money={data.housingFund} />
          <p className="sentence" style={{ marginTop: 8 }}>{data.housingSentence}</p>
        </button>
      </div>

      <div className="grid four" style={{ marginTop: 16 }}>
        <Card title="This month income"><MoneyView money={data.thisMonth.income} /></Card>
        <Card title="Personal spending"><MoneyView money={data.thisMonth.expenses} /></Card>
        <Card title="Savings recorded"><MoneyView money={data.thisMonth.savings} /></Card>
        <Card title="Family support"><MoneyView money={data.thisMonth.familySupport} /></Card>
      </div>

      <div className="grid two" style={{ marginTop: 16 }}>
        <Card title="Action required">
          <p><Link href="/today">Open today&apos;s financial actions</Link></p>
          <ul>{data.actionRequired.map((item) => <li key={item}>{item}</li>)}</ul>
        </Card>
        <Card title="This month, in words">
          <p className="sentence">{data.savingsRateSentence}</p>
          <p className="sentence">
            Career/business: {data.thisMonth.careerBusiness.formatted}.
            {data.thisMonth.betting && (data.thisMonth.betting.minor ?? 0) > 0 ? ` Betting: ${data.thisMonth.betting.formatted}.` : ""}
            {" "}Transfers: {data.thisMonth.transfers.formatted} — those are movements of capital.
          </p>
        </Card>
      </div>

      <div style={{ marginTop: 16 }}>
        <AskLedger />
      </div>

      {metric && <ExplainModal metric={metric} onClose={() => setMetric(null)} />}
    </Shell>
  );
}
