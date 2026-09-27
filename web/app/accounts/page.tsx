"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import type { Account, Reconciliation } from "@/lib/types";

function naira(minor: number | null | undefined) {
  if (minor == null) return "UNKNOWN";
  return `₦${(minor / 100).toLocaleString("en-NG", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export default function AccountsPage() {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [recon, setRecon] = useState<Reconciliation | null>(null);
  const [amount, setAmount] = useState("");
  const [target, setTarget] = useState<Account | null>(null);

  async function load() { setAccounts(await api.accounts()); }
  useEffect(() => { void load(); }, []);

  async function saveSnapshot(event: FormEvent) {
    event.preventDefault();
    if (!target) return;
    await api.snapshot(target.id, {
      amount: Number(amount),
      currency: "NGN",
      asOf: new Date().toISOString().slice(0, 10),
      provenance: "Confirmed",
      notes: "Entered in the Accounts screen."
    });
    setAmount("");
    await load();
  }

  return (
    <Shell>
      <h1>Which account should hold what?</h1>
      <p className="lede">Each account has one job. A green status is never painted when the balance is UNKNOWN.</p>
      <div className="stack">
        {accounts.map((account) => (
          <div key={account.id}>
            <Card title={account.name}>
              <div className="row">
                <span className={`badge ${account.health.toLowerCase()}`}>{account.health}</span>
                <span className="badge">{account.reconciliationStatus}</span>
              </div>
              {account.latestBalance
                ? <MoneyView money={account.latestBalance} />
                : <p className="sentence">UNKNOWN. Enter the current balance.</p>}
              <p className="sentence">{account.healthSentence}</p>
              <p className="lede">{account.job} Do not put: {account.doNotPutHere}</p>
              <div className="row">
                <button className="btn ghost" onClick={() => api.reconciliation(account.id).then(setRecon)}>Show reconciliation</button>
                <button className="btn ghost" onClick={() => setTarget(account)}>Enter current balance</button>
              </div>
            </Card>
            {target?.id === account.id && (
              <Reveal watch={target.id}>
                <Card title={`Confirm ${target.name}`}>
                  <form className="stack" onSubmit={saveSnapshot}>
                    <input value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="Current balance" />
                    <button className="btn" type="submit">Save as confirmed</button>
                  </form>
                </Card>
              </Reveal>
            )}
            {recon?.accountId === account.id && (
              <Reveal watch={recon.accountId + recon.status}>
                <Card title={`${recon.accountName} reconciliation`}>
                  <span className={`badge ${recon.status.toLowerCase()}`}>{recon.status}</span>
                  <p className="sentence">{recon.sentence}</p>
                  <table className="table">
                    <tbody>
                      <tr><td>Opening</td><td>{naira(recon.openingMinor)}</td></tr>
                      <tr><td>Inflows</td><td>{naira(recon.inflowsMinor)}</td></tr>
                      <tr><td>Outflows</td><td>{naira(recon.outflowsMinor)}</td></tr>
                      <tr><td>Fees</td><td>{naira(recon.feesMinor)}</td></tr>
                      <tr><td>Adjustments</td><td>{naira(recon.adjustmentsMinor)}</td></tr>
                      <tr><td>Expected closing</td><td>{naira(recon.expectedClosingMinor)}</td></tr>
                      <tr><td>Actual</td><td>{naira(recon.actualMinor)}</td></tr>
                      <tr><td>Difference</td><td>{naira(recon.differenceMinor)}</td></tr>
                    </tbody>
                  </table>
                </Card>
              </Reveal>
            )}
          </div>
        ))}
      </div>
    </Shell>
  );
}
