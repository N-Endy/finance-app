"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Account, Reconciliation } from "@/lib/types";

function naira(minor: number | null | undefined) {
  if (minor == null) return "UNKNOWN";
  return `₦${(minor / 100).toLocaleString("en-NG", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

type BalanceKind = "opening" | "current";

export default function AccountsPage() {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [recon, setRecon] = useState<Reconciliation | null>(null);
  const [amount, setAmount] = useState("");
  const [asOf, setAsOf] = useState(today);
  const [target, setTarget] = useState<Account | null>(null);
  const [kind, setKind] = useState<BalanceKind>("current");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  async function load() { setAccounts(await api.accounts()); }
  useEffect(() => { void load().catch((err) => setError(failMessage(err))); }, []);

  function startEntry(account: Account, next: BalanceKind) {
    setTarget(account);
    setKind(next);
    setAmount("");
    setAsOf(today());
  }

  async function saveSnapshot(event: FormEvent) {
    event.preventDefault();
    if (!target) return;
    setError(null);
    setMessage(null);
    setBusy("snapshot");
    try {
      await api.snapshot(target.id, {
        amount: Number(amount),
        currency: "NGN",
        asOf,
        provenance: "Confirmed",
        notes: kind === "opening" ? "Opening balance entered on Accounts." : "Entered in the Accounts screen.",
        source: kind === "opening" ? "opening" : "manual"
      });
      const accountId = target.id;
      setAmount("");
      setTarget(null);
      await load();
      if (recon?.accountId === accountId) {
        setRecon(await api.reconciliation(accountId));
      }
      setMessage(kind === "opening" ? `${target.name} opening balance saved.` : `${target.name} current balance saved.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function showReconciliation(id: string) {
    setError(null);
    setBusy(id);
    try {
      setRecon(await api.reconciliation(id));
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <Shell>
      <h1>Which account should hold what?</h1>
      <p className="lede">Each account has one job. Reconciliation checks the ledger against the bank app. UNKNOWN means that figure has not been entered — it is not being guessed.</p>
      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}
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
                <button className="btn ghost" disabled={busy === account.id} onClick={() => void showReconciliation(account.id)}>Show reconciliation</button>
                <button className="btn ghost" onClick={() => startEntry(account, "opening")}>Enter opening balance</button>
                <button className="btn ghost" onClick={() => startEntry(account, "current")}>Enter current balance</button>
              </div>
            </Card>
            {target?.id === account.id && (
              <Reveal watch={`${target.id}-${kind}`}>
                <Card title={kind === "opening" ? `Opening balance for ${target.name}` : `Confirm ${target.name}`}>
                  <p className="lede">{kind === "opening"
                    ? "What this account held when you started the ledger, from a statement or the bank app on that date."
                    : "What the bank app shows now. This is the actual closing figure."}</p>
                  <form className="stack" onSubmit={saveSnapshot}>
                    <input value={amount} onChange={(e) => setAmount(e.target.value)} placeholder={kind === "opening" ? "Opening balance" : "Current balance"} />
                    <input type="date" value={asOf} onChange={(e) => setAsOf(e.target.value)} />
                    <button className="btn" type="submit" disabled={busy === "snapshot"}>Save as confirmed</button>
                  </form>
                </Card>
              </Reveal>
            )}
            {recon?.accountId === account.id && (
              <Reveal watch={recon.accountId + recon.status + String(recon.openingMinor) + String(recon.actualMinor)}>
                <Card title={`${recon.accountName} reconciliation`}>
                  <span className={`badge ${recon.status.toLowerCase()}`}>{recon.status}</span>
                  <p className="sentence">{recon.sentence}</p>
                  <p className="lede">This is a check, not a balance. Expected closing is opening plus recorded movements. Difference is actual minus expected.</p>
                  <table className="table">
                    <tbody>
                      <tr><td>Opening — start of the ledger</td><td>{naira(recon.openingMinor)}</td></tr>
                      <tr><td>Inflows — money recorded in</td><td>{naira(recon.inflowsMinor)}</td></tr>
                      <tr><td>Outflows — money recorded out</td><td>{naira(recon.outflowsMinor)}</td></tr>
                      <tr><td>Fees — recorded charges</td><td>{naira(recon.feesMinor)}</td></tr>
                      <tr><td>Adjustments — recorded corrections</td><td>{naira(recon.adjustmentsMinor)}</td></tr>
                      <tr><td>Expected closing — opening plus those movements</td><td>{naira(recon.expectedClosingMinor)}</td></tr>
                      <tr><td>Actual — current balance you entered</td><td>{naira(recon.actualMinor)}</td></tr>
                      <tr><td>Difference — actual minus expected</td><td>{naira(recon.differenceMinor)}</td></tr>
                    </tbody>
                  </table>
                  {recon.status === "Incomplete" && (
                    <div className="row" style={{ marginTop: 12 }}>
                      <button className="btn ghost" onClick={() => startEntry(account, "opening")}>Enter opening balance</button>
                      <button className="btn ghost" onClick={() => startEntry(account, "current")}>Enter current balance</button>
                    </div>
                  )}
                </Card>
              </Reveal>
            )}
          </div>
        ))}
      </div>
    </Shell>
  );
}
