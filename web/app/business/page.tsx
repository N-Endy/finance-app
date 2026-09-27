"use client";

import { useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import type { Business } from "@/lib/types";

export default function BusinessPage() {
  const [data, setData] = useState<Business | null>(null);
  useEffect(() => { void api.business().then(setData); }, []);
  if (!data) return <Shell><p>Loading MatchPredictor…</p></Shell>;

  return (
    <Shell>
      <h1>Is MatchPredictor making or losing money?</h1>
      <p className="lede">This ledger is separate from personal spending and from betting. Personal betting never appears here.</p>
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
      <p className="lede">If revenue is recorded, the current split is {data.reinvestPercent}% reinvestment / {data.personalPercent}% personal wealth. Change it in Settings. It applies only to actual revenue.</p>
    </Shell>
  );
}
