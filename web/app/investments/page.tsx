"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { ExchangeRate, Holding, Pension, Retirement } from "@/lib/types";

export default function InvestmentsPage() {
  const [holdings, setHoldings] = useState<Holding[]>([]);
  const [pension, setPension] = useState<Pension | null>(null);
  const [retirement, setRetirement] = useState<Retirement | null>(null);
  const [todayRate, setTodayRate] = useState<ExchangeRate | null>(null);
  const [rate, setRate] = useState("");
  const [rsa, setRsa] = useState("");
  const [balances, setBalances] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  async function load() {
    const rows = await api.holdings();
    setHoldings(rows);
    setPension(await api.pension());
    setRetirement(await api.retirement());
    setBalances(Object.fromEntries(rows.map((holding) => [holding.id, holding.amount.major == null ? "" : String(holding.amount.major)])));
    try {
      setTodayRate(await api.ensureTodaysRate());
      setError(null);
    } catch (err) {
      setTodayRate(null);
      setError(failMessage(err));
    }
  }
  useEffect(() => { void load().catch((err) => setError(failMessage(err))); }, []);

  async function saveRate(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy("rate");
    try {
      await api.addRate({ from: "USD", to: "NGN", rate: Number(rate), asOf: new Date().toISOString().slice(0, 10), source: "manual" });
      setRate("");
      setTodayRate(await api.ensureTodaysRate());
      await load();
      setMessage("Rate saved.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function savePension(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy("rsa");
    try {
      await api.updatePension({ balance: Number(rsa), employee: null, employer: null, retirementAge: null });
      await load();
      setMessage("RSA balance saved.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function saveHolding(event: FormEvent, holding: Holding) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy(holding.id);
    try {
      await api.updateHolding(holding.id, {
        amount: Number(balances[holding.id]),
        provenance: "Confirmed",
        asOf: new Date().toISOString().slice(0, 10)
      });
      await load();
      setMessage(`${holding.name} saved.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <Shell>
      <h1>Investments, pension, and retirement</h1>
      <p className="lede">USD converts with today&apos;s published USD/NGN rate. If the feed is down, conversion stays UNKNOWN. Risevest and Bamboo naira charges are still entered by you — those are bank charges, not this rate.</p>
      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}
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
            <button className="btn" type="submit" disabled={busy === holding.id}>Save</button>
          </form>
        </Card>
      ))}
      <Card title="FX rate">
        {todayRate
          ? <p className="sentence">Today&apos;s USD/NGN: {todayRate.rate.toLocaleString("en-NG", { maximumFractionDigits: 4 })} as of {todayRate.asOf} ({todayRate.source}).</p>
          : <p className="sentence">UNKNOWN. Today&apos;s published USD/NGN rate has not been recorded.</p>}
        <form className="row" onSubmit={saveRate}>
          <input value={rate} onChange={(e) => setRate(e.target.value)} placeholder="Override with a rate you observed" />
          <button className="btn" type="submit" disabled={busy === "rate"}>Save rate</button>
        </form>
      </Card>
      {pension && (
        <Card title="Pension / RSA">
          <MoneyView money={pension.balance} />
          <p className="sentence">{pension.sentence}</p>
          <form className="row" onSubmit={savePension}>
            <input value={rsa} onChange={(e) => setRsa(e.target.value)} placeholder="RSA balance from a statement" />
            <button className="btn" type="submit" disabled={busy === "rsa"}>Save RSA balance</button>
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
