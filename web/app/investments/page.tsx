"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import type { Holding, Pension, Retirement } from "@/lib/types";

export default function InvestmentsPage() {
  const [holdings, setHoldings] = useState<Holding[]>([]);
  const [pension, setPension] = useState<Pension | null>(null);
  const [retirement, setRetirement] = useState<Retirement | null>(null);
  const [rate, setRate] = useState("");
  const [rsa, setRsa] = useState("");

  async function load() {
    setHoldings(await api.holdings());
    setPension(await api.pension());
    setRetirement(await api.retirement());
  }
  useEffect(() => { void load(); }, []);

  async function saveRate(event: FormEvent) {
    event.preventDefault();
    await api.addRate({ from: "USD", to: "NGN", rate: Number(rate), asOf: new Date().toISOString().slice(0, 10), source: "manual" });
    setRate("");
    await load();
  }

  async function savePension(event: FormEvent) {
    event.preventDefault();
    await api.updatePension({ balance: Number(rsa), employee: null, employer: null, retirementAge: null });
    await load();
  }

  return (
    <Shell>
      <h1>Investments, pension, and retirement assumptions</h1>
      <p className="lede">USD stays in dollars until you type an FX rate. Pension stays empty until you enter an RSA statement. Retirement scenarios are assumptions, not predictions.</p>
      {holdings.map((holding) => (
        <Card key={holding.id} title={holding.name}>
          <MoneyView money={holding.amount} />
          <p className="sentence">{holding.purpose}. {holding.statusNote}</p>
          {holding.isExpectedReceivable && <span className="badge expected">expected receivable</span>}
        </Card>
      ))}
      <Card title="FX rate">
        <form className="row" onSubmit={saveRate}>
          <input value={rate} onChange={(e) => setRate(e.target.value)} placeholder="USD to NGN rate you observed" />
          <button className="btn" type="submit">Save rate</button>
        </form>
      </Card>
      {pension && (
        <Card title="Pension / RSA">
          <MoneyView money={pension.balance} />
          <p className="sentence">{pension.sentence}</p>
          <form className="row" onSubmit={savePension}>
            <input value={rsa} onChange={(e) => setRsa(e.target.value)} placeholder="RSA balance from a statement" />
            <button className="btn" type="submit">Save RSA balance</button>
          </form>
        </Card>
      )}
      {retirement && (
        <Card title="Retirement calculator">
          <p className="sentence">{retirement.sentence}</p>
          {retirement.scenarios.map((s) => <p key={s.name} className="lede">{s.name}: {s.sentence}</p>)}
        </Card>
      )}
    </Shell>
  );
}
