"use client";

import { useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Business, Subscription } from "@/lib/types";

export default function BusinessPage() {
  const [data, setData] = useState<Business | null>(null);
  const [subs, setSubs] = useState<Subscription[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void Promise.all([api.business(), api.subscriptions()])
      .then(([business, subscriptions]) => {
        setData(business);
        setSubs(subscriptions);
      })
      .catch((err) => setError(failMessage(err)));
  }, []);

  if (error) return <Shell><p className="error">{error}</p></Shell>;
  if (!data) return <Shell><p>Loading MatchPredictor…</p></Shell>;

  return (
    <Shell>
      <h1>Is MatchPredictor making or losing money?</h1>
      <p className="lede">MatchPredictor costs and revenue, separate from personal spending.</p>
      <div className="grid three">
        <Card title="Revenue"><MoneyView money={data.revenue} large /></Card>
        <Card title="Expenses"><MoneyView money={data.expenses} large /></Card>
        <Card title="Net"><MoneyView money={data.net} large /></Card>
      </div>
      {data.lines.map((line) => (
        <Card key={line.category} title={line.category}>
          <MoneyView money={line.amount} />
        </Card>
      ))}
      <Card title="Subscriptions">
        {subs.map((sub) => <p key={sub.id}>{sub.name} · {sub.amount.formatted} · next {sub.nextBillingDate} · {sub.isBusiness ? "business" : "personal"}</p>)}
        {subs.length === 0 && <p>No subscriptions on the plan.</p>}
      </Card>
      <p className="lede">If revenue is recorded, the current split is {data.reinvestPercent}% reinvestment / {data.personalPercent}% personal wealth. Change it in Settings. It applies only to actual revenue.</p>
    </Shell>
  );
}
