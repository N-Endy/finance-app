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
  const [balances, setBalances] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);

  async function load() {
    const rows = await api.holdings();
    setHoldings(rows);
    setPension(await api.pension());
    setRetirement(await api.retirement());
    setBalances(Object.fromEntries(rows.map((holding) => [holding.id, holding.amount.major == null ? "" : String(holding.amount.major)])));
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

  async function saveHolding(event: FormEvent, holding: Holding) {
    event.preventDefault();
    await api.updateHolding(holding.id, {
      amount: Number(balances[holding.id]),
      provenance: "Confirmed",
      asOf: new Date().toISOString().slice(0, 10)
    });
    await load();
    setMessage(`${holding.name} saved.`);
  }

  return (
    <Shell>
      <h1>Investments, pension, and retirement</h1>
      <p className="lede">USD stays in dollars until you enter an FX rate. Enter a current balance to confirm a holding.</p>
      {message && <p className="sentence">{message}</p>}
      {holdings.map((holding) => (
        <Card key={holding.id} title={holding.name}>
          <MoneyView money={holding.amount} />
          <p className="sentence">{holding.purpose}. {holding.statusNote}</p>
          {holding.isExpectedReceivable && <span className="badge expected">expected receivable</span>}
          <form className="row" style={{ marginTop: 12 }} onSubmit={(event) => saveHolding(event, holding)}>
            <input
              value={balances[holding.id] ?? ""}
              onChange={(e) => setBalances((current) => ({ ...current, [holding.id]: e.target.value }))}
              placeholder="Enter current balance"
              inputMode="decimal"
              aria-label={`${holding.name} current balance`}
            />
            <button className="btn" type="submit">Save</button>
          </form>
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
