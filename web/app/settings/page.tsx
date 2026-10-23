"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Settings } from "@/lib/types";

export default function SettingsPage() {
  const [settings, setSettings] = useState<Settings | null>(null);
  const [allowance, setAllowance] = useState("70000");
  const [confirmClear, setConfirmClear] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    void api.settings().then((row) => {
      setSettings(row);
      setAllowance(String(row.opayAllowance.major ?? 70000));
    }).catch((err) => setError(failMessage(err)));
  }, []);

  async function save(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy("allowance");
    try {
      await api.updateSettings({ opayAllowance: Number(allowance) });
      setSettings(await api.settings());
      setMessage("OPay allowance saved. Overspend alerts can now use this single number.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function resetLedger() {
    setError(null);
    setMessage(null);
    setBusy("delete");
    try {
      await api.deleteAll();
      setConfirmClear(false);
      setMessage("Entered figures cleared. The plan (accounts, jobs, allocation lines) is kept.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <Shell>
      <h1>Settings</h1>
      <p className="lede">OPay allowance, export, and clearing entered figures while keeping the plan.</p>
      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      {settings && (
        <Card title="OPay allowance and alert bands">
          <p className="sentence">Current allowance: {settings.opayAllowance.formatted}. Band used by the plan: ₦60,000–₦80,000.</p>
          <form className="row" onSubmit={save}>
            <input value={allowance} onChange={(e) => setAllowance(e.target.value)} />
            <button className="btn" type="submit" disabled={busy === "allowance"}>Save allowance</button>
          </form>
          <p className="lede">Watch {settings.watchPercent}% · warning {settings.warningPercent}% · over {settings.overBudgetPercent}% · MatchPredictor split {settings.businessReinvestPercent}/{settings.businessPersonalPercent}</p>
        </Card>
      )}

      <Card title="Export & Data Portability">
        <p className="sentence">Download your complete financial records in multiple formats for your own records or portability.</p>
        <div className="row" style={{ marginTop: 8 }}>
          <a className="btn" href={api.exportUrl("csv")}>CSV Summary</a>
          <a className="btn ghost" href={api.exportUrl("json")}>JSON Summary</a>
          <a className="btn ghost" href={api.exportUrl("xlsx")}>Excel</a>
          <a className="btn ghost" href={api.exportDataUrl()} download="financeos-ledger-export.json">Full Ledger Export (JSON)</a>
        </div>
      </Card>

      <Card title="Account Lifecycle & Privacy">
        <p className="sentence">
          GDPR & Privacy Controls: Permanently purge your account, accounts, transactions, and envelopes from the database.
        </p>
        <div style={{ marginTop: 8 }}>
          <button
            className="btn warn"
            type="button"
            onClick={async () => {
              if (window.confirm("Are you ABSOLUTELY sure you want to permanently delete your account and all associated ledger data? This cannot be undone.")) {
                try {
                  await api.deleteAccount();
                  window.location.href = "/login";
                } catch (err) {
                  setError(failMessage(err));
                }
              }
            }}
          >
            Permanently Delete My Account & All Data
          </button>
        </div>
      </Card>

      <Card title="Clear entered figures">
        {!confirmClear ? (
          <button className="btn warn" disabled={busy === "delete"} onClick={() => setConfirmClear(true)}>Clear entered figures and keep the plan</button>
        ) : (
          <div className="stack">
            <p className="sentence">This removes balances, holdings, transactions, and FX. The plan stays.</p>
            <div className="row">
              <button className="btn warn" disabled={busy === "delete"} onClick={() => void resetLedger()}>Confirm clear</button>
              <button className="btn ghost" disabled={busy === "delete"} onClick={() => setConfirmClear(false)}>Cancel</button>
            </div>
          </div>
        )}
        <p className="lede">Removes balances, holdings, transactions, and FX you entered. Accounts, jobs, and allocation plan lines stay. You fill the figures again.</p>
      </Card>
    </Shell>
  );
}
