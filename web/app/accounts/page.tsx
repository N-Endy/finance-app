"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import { formatMinorUnits } from "@/lib/currency";
import type { Account, Reconciliation } from "@/lib/types";

function formatRecon(minor: number | null | undefined, ccy?: string) {
  if (minor == null) return "UNKNOWN";
  return formatMinorUnits(minor, ccy ?? "USD");
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

  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newAccName, setNewAccName] = useState("");
  const [newAccInst, setNewAccInst] = useState("");
  const [newAccRole, setNewAccRole] = useState("DailySpending");
  const [newAccCcy, setNewAccCcy] = useState("USD");
  const [newAccBalance, setNewAccBalance] = useState("");
  const [newAccJob, setNewAccJob] = useState("");

  async function load() { setAccounts(await api.accounts()); }
  useEffect(() => { void load().catch((err) => setError(failMessage(err))); }, []);

  function startEntry(account: Account, next: BalanceKind) {
    setTarget(account);
    setKind(next);
    setAmount("");
    setAsOf(today());
  }

  async function saveNewAccount(e: FormEvent) {
    e.preventDefault();
    if (!newAccName.trim()) return;
    setError(null);
    setBusy("create");
    try {
      await api.createAccount({
        name: newAccName.trim(),
        institution: newAccInst.trim() || newAccName.trim(),
        role: newAccRole,
        currency: newAccCcy,
        initialBalance: newAccBalance ? parseFloat(newAccBalance) : 0,
        job: newAccJob.trim() || undefined
      });
      setShowCreateModal(false);
      setNewAccName("");
      setNewAccInst("");
      setNewAccBalance("");
      setNewAccJob("");
      await load();
      setMessage("Account created successfully.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function deleteAccount(acc: Account) {
    if (!confirm(`Are you sure you want to remove or archive "${acc.name}"?`)) return;
    setError(null);
    setBusy(acc.id);
    try {
      await api.deleteAccountById(acc.id);
      await load();
      setMessage(`${acc.name} was removed/archived.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
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
        currency: target.latestBalance?.currencyCode ?? "USD",
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
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "1rem", marginBottom: "0.5rem" }}>
        <div>
          <h1>Which account should hold what?</h1>
          <p className="lede">Each account has one job. Reconciliation checks the ledger against the bank app. UNKNOWN means that figure has not been entered — it is not being guessed.</p>
        </div>
        <button
          className="btn"
          style={{ whiteSpace: "nowrap", padding: "0.6rem 1.2rem", fontWeight: 600 }}
          onClick={() => setShowCreateModal(!showCreateModal)}
        >
          {showCreateModal ? "Cancel" : "+ Add Account"}
        </button>
      </div>

      {showCreateModal && (
        <Card title="Add a New Financial Account">
          <form className="stack" onSubmit={saveNewAccount}>
            <div className="grid two" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Account Name *</label>
                <input
                  required
                  placeholder="e.g. Chase Checking, Revolut EUR, Cash"
                  value={newAccName}
                  onChange={(e) => setNewAccName(e.target.value)}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Bank / Institution</label>
                <input
                  placeholder="e.g. JPMorgan Chase, Barclays, Revolut"
                  value={newAccInst}
                  onChange={(e) => setNewAccInst(e.target.value)}
                />
              </div>
            </div>

            <div className="grid three" style={{ gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Account Role</label>
                <select value={newAccRole} onChange={(e) => setNewAccRole(e.target.value)}>
                  <option value="DailySpending">Daily Spending & Bills (Checking)</option>
                  <option value="SavingsVault">Savings Vault (Emergency / Safe Reserve)</option>
                  <option value="StrategicBuffer">Strategic Buffer (Sinking Funds / Irregular)</option>
                  <option value="CashOnHand">Cash On Hand (Physical Wallet)</option>
                  <option value="BusinessCard">Business / Commercial Account</option>
                  <option value="InvestmentBroker">Investment / Brokerage</option>
                </select>
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Currency</label>
                <select value={newAccCcy} onChange={(e) => setNewAccCcy(e.target.value)}>
                  <option value="USD">USD ($)</option>
                  <option value="EUR">EUR (€)</option>
                  <option value="GBP">GBP (£)</option>
                  <option value="NGN">NGN (₦)</option>
                  <option value="USDT">USDT (₮)</option>
                  <option value="CAD">CAD ($)</option>
                  <option value="AUD">AUD ($)</option>
                </select>
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Initial Balance</label>
                <input
                  type="number"
                  step="any"
                  placeholder="0.00"
                  value={newAccBalance}
                  onChange={(e) => setNewAccBalance(e.target.value)}
                />
              </div>
            </div>

            <div>
              <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Account Purpose & Job</label>
              <input
                placeholder="e.g. Day-to-day debit card spending, groceries and utilities"
                value={newAccJob}
                onChange={(e) => setNewAccJob(e.target.value)}
              />
            </div>

            <div className="row" style={{ marginTop: "0.5rem" }}>
              <button className="btn" type="submit" disabled={busy === "create"}>
                {busy === "create" ? "Saving..." : "Create Account"}
              </button>
              <button className="btn ghost" type="button" onClick={() => setShowCreateModal(false)}>Cancel</button>
            </div>
          </form>
        </Card>
      )}

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
                <button className="btn ghost" style={{ color: "#ef4444", marginLeft: "auto" }} onClick={() => void deleteAccount(account)}>Remove</button>
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
                      <tr><td>Opening — start of the ledger</td><td>{formatRecon(recon.openingMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
                      <tr><td>Inflows — money recorded in</td><td>{formatRecon(recon.inflowsMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
                      <tr><td>Outflows — money recorded out</td><td>{formatRecon(recon.outflowsMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
                      <tr><td>Fees — recorded charges</td><td>{formatRecon(recon.feesMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
                      <tr><td>Adjustments — recorded corrections</td><td>{formatRecon(recon.adjustmentsMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
                      <tr><td>Expected closing — opening plus those movements</td><td>{formatRecon(recon.expectedClosingMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
                      <tr><td>Actual — current balance you entered</td><td>{formatRecon(recon.actualMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
                      <tr><td>Difference — actual minus expected</td><td>{formatRecon(recon.differenceMinor, target?.latestBalance?.currency ?? target?.latestBalance?.currencyCode)}</td></tr>
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
