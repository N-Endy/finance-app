"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
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

  useEffect(() => {
    void Promise.all([
      api.settings().then(setSettings),
      api.rules().then(setRules),
      api.violations().then(setViolations),
      api.subscriptions().then(setSubs),
      api.alerts().then(setAlerts)
    ]);
  }, []);

  async function save(event: FormEvent) {
    event.preventDefault();
    await api.updateSettings({ opayAllowance: Number(allowance) });
    setSettings(await api.settings());
    setMessage("OPay allowance saved. Overspend alerts can now use this single number.");
  }

  return (
    <Shell>
      <h1>Settings and accountability</h1>
      <p className="lede">Configure the plan. Export your data. Delete and reseed if you want a clean snapshot. Bank passwords are never stored.</p>

      {settings && (
        <Card title="OPay allowance and alert bands">
          <p className="sentence">Current allowance: {settings.opayAllowance.formatted}. Band used by the plan: ₦60,000–₦80,000.</p>
          <form className="row" onSubmit={save}>
            <input value={allowance} onChange={(e) => setAllowance(e.target.value)} />
            <button className="btn" type="submit">Save allowance</button>
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
        <form className="stack" onSubmit={async (event) => { event.preventDefault(); const result = await api.importCsv(csv); setMessage(`Imported ${result.imported} rows.`); }}>
          <textarea rows={6} value={csv} onChange={(e) => setCsv(e.target.value)} />
          <button className="btn" type="submit">Import CSV</button>
        </form>
      </Card>

      <Card title="Delete and reseed">
        <button className="btn warn" onClick={() => api.deleteAll().then(() => setMessage("Data reset to the plan snapshot."))}>Delete my ledger and restore the plan snapshot</button>
      </Card>
      {message && <p className="sentence">{message}</p>}
    </Shell>
  );
}
