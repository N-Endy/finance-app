"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { AlertItem, Rule, Settings, Subscription, Violation } from "@/lib/types";

export default function SettingsPage() {
  const [settings, setSettings] = useState<Settings | null>(null);
  const [rules, setRules] = useState<Rule[]>([]);
  const [violations, setViolations] = useState<Violation[]>([]);
  const [subs, setSubs] = useState<Subscription[]>([]);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [allowance, setAllowance] = useState("70000");
  const [csv, setCsv] = useState("Date,Account,Type,Category,Description,Amount,Fee,Currency\n");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    void Promise.all([
      api.settings().then(setSettings),
      api.rules().then(setRules),
      api.violations().then(setViolations),
      api.subscriptions().then(setSubs),
      api.alerts().then(setAlerts)
    ]).catch((err) => setError(failMessage(err)));
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

  async function importCsv(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy("import");
    try {
      const result = await api.importCsv(csv);
      setMessage(`Imported ${result.imported} rows.`);
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
      setMessage("Data reset to the plan snapshot.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <Shell>
      <h1>Settings</h1>
      <p className="lede">Allowance, alerts, export, and a reset back to the plan snapshot.</p>
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

      <Card title="Alerts, ranked">
        {alerts.map((alert) => <p key={alert.message}><strong>{alert.priority}.</strong> {alert.message}</p>)}
        {alerts.length === 0 && <p>No ranked alerts right now.</p>}
      </Card>

      <Card title="Subscriptions">
        {subs.map((sub) => <p key={sub.id}>{sub.name} · {sub.amount.formatted} · next {sub.nextBillingDate} · {sub.isBusiness ? "business" : "personal"}</p>)}
      </Card>

      <Card title="Rules">
        {rules.map((rule) => <p key={rule.id}><strong>{rule.title}.</strong> {rule.action} {rule.control}</p>)}
      </Card>

      <Card title="Open violations">
        {violations.filter((v) => v.isOpen).map((v) => <p key={v.id}>{v.date} · {v.rule}: {v.message}</p>)}
        {violations.filter((v) => v.isOpen).length === 0 && <p>No open violations.</p>}
      </Card>

      <Card title="Export">
        <div className="row">
          <a className="btn" href={api.exportUrl("csv")}>CSV</a>
          <a className="btn ghost" href={api.exportUrl("json")}>JSON</a>
          <a className="btn ghost" href={api.exportUrl("xlsx")}>Excel</a>
        </div>
      </Card>

      <Card title="CSV import">
        <form className="stack" onSubmit={importCsv}>
          <textarea rows={6} value={csv} onChange={(e) => setCsv(e.target.value)} />
          <button className="btn" type="submit" disabled={busy === "import"}>Import CSV</button>
        </form>
      </Card>

      <Card title="Delete and reseed">
        <button className="btn warn" disabled={busy === "delete"} onClick={() => void resetLedger()}>Delete my ledger and restore the plan snapshot</button>
      </Card>
    </Shell>
  );
}
