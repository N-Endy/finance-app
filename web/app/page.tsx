"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Shell, Card, MoneyView, ExplainModal } from "@/components/ui";
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

      {data.unknowns.length > 0 && (
        <Card title="Still to enter">
          <ul>{data.unknowns.map((item) => <li key={item}>{item}</li>)}</ul>
        </Card>
      )}

      <div className="grid four" style={{ marginTop: 16 }}>
        <button className="card map-node" onClick={() => setMetric("net-worth")}>
          <h3>Confirmed net worth</h3>
          <MoneyView money={data.netWorth} large />
        </button>
        <button className="card map-node" onClick={() => setMetric("spendable")}>
          <h3>Actually spendable</h3>
          <MoneyView money={data.spendable} large />
          <p className="sentence">{data.spendableSentence}</p>
        </button>
        <Card title="Emergency">
          <MoneyView money={data.emergencyFund} />
          <p className="sentence">{data.emergencySentence}</p>
        </Card>
        <button className="card map-node" onClick={() => setMetric("housing-gap")}>
          <h3>Housing</h3>
          <MoneyView money={data.housingFund} />
          <p className="sentence">{data.housingSentence}</p>
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
          <p className="sentence">Career/business: {data.thisMonth.careerBusiness.formatted}. Betting: {data.thisMonth.betting.formatted}. Transfers: {data.thisMonth.transfers.formatted} — those are not expenses.</p>
        </Card>
      </div>

      <div style={{ marginTop: 16 }}>
        <AskLedger />
      </div>

      {metric && <ExplainModal metric={metric} onClose={() => setMetric(null)} />}
    </Shell>
  );
}
