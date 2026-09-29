"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Business, Subscription } from "@/lib/types";

export default function BusinessPage() {
  const [businesses, setBusinesses] = useState<Business[]>([]);
  const [selectedSlug, setSelectedSlug] = useState<string>("matchpredictor");
  const [data, setData] = useState<Business | null>(null);
  const [subs, setSubs] = useState<Subscription[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);
  const [newBiz, setNewBiz] = useState({ name: "", slug: "", description: "", reinvest: 70, personal: 30 });
  const [splitEdit, setSplitEdit] = useState({ reinvest: 70, personal: 30 });

  async function load(currentSlug = selectedSlug) {
    try {
      const [allBiz, subscriptions] = await Promise.all([
        api.businesses().catch(() => []),
        api.subscriptions().catch(() => [])
      ]);

      const list = allBiz.length > 0 ? allBiz : [await api.business("matchpredictor")];
      setBusinesses(list);
      setSubs(subscriptions);

      const activeSlug = list.some((b) => b.slug === currentSlug) ? currentSlug : (list[0]?.slug ?? "matchpredictor");
      setSelectedSlug(activeSlug);

      const current = await api.business(activeSlug);
      setData(current);
      setSplitEdit({ reinvest: current.reinvestPercent, personal: current.personalPercent });
    } catch (err) {
      setError(failMessage(err));
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function handleSelectBusiness(slug: string) {
    setSelectedSlug(slug);
    setError(null);
    setMessage(null);
    try {
      const biz = await api.business(slug);
      setData(biz);
      setSplitEdit({ reinvest: biz.reinvestPercent, personal: biz.personalPercent });
    } catch (err) {
      setError(failMessage(err));
    }
  }

  async function handleCreateBusiness(e: FormEvent) {
    e.preventDefault();
    if (!newBiz.name.trim()) return;
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      const created = await api.createBusiness({
        name: newBiz.name,
        slug: newBiz.slug || undefined,
        description: newBiz.description || undefined,
        reinvestPercent: Number(newBiz.reinvest),
        personalPercent: Number(newBiz.personal)
      });
      setShowAddForm(false);
      setNewBiz({ name: "", slug: "", description: "", reinvest: 70, personal: 30 });
      setMessage(`Business ${created.name} registered.`);
      await load(created.slug ?? created.name.toLowerCase().replace(/ /g, "-"));
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleSaveSplit(e: FormEvent) {
    e.preventDefault();
    if (!data?.slug) return;
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      await api.splitBusiness(data.slug, {
        reinvestPercent: Number(splitEdit.reinvest),
        personalPercent: Number(splitEdit.personal)
      });
      setMessage("Split configuration saved.");
      await load(data.slug);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Shell>
      <div className="row" style={{ justifyContent: "space-between", alignItems: "flex-start" }}>
        <div>
          <h1>Business & Venture P&L</h1>
          <p className="lede">Venture costs and revenue kept separate from personal spending. Reinvestment stays in business accounts.</p>
        </div>
        <button className="btn ghost" onClick={() => setShowAddForm((v) => !v)}>
          {showAddForm ? "Cancel" : "+ Register Business"}
        </button>
      </div>

      {message && <p className="sentence" style={{ color: "var(--accent)" }}>{message}</p>}
      {error && <p className="error">{error}</p>}

      {showAddForm && (
        <Card title="Register New Business or Venture">
          <form className="stack" onSubmit={handleCreateBusiness}>
            <label>
              Business Name
              <input
                value={newBiz.name}
                onChange={(e) => setNewBiz({ ...newBiz, name: e.target.value })}
                placeholder="e.g. TennisPredictor, Freelance Consulting"
                required
              />
            </label>
            <label>
              Short Description / Purpose
              <input
                value={newBiz.description}
                onChange={(e) => setNewBiz({ ...newBiz, description: e.target.value })}
                placeholder="e.g. Algorithmic sports analytics engine"
              />
            </label>
            <div className="grid two">
              <label>
                Reinvestment % (e.g. 70)
                <input
                  type="number"
                  min="0"
                  max="100"
                  value={newBiz.reinvest}
                  onChange={(e) => {
                    const r = Number(e.target.value);
                    setNewBiz({ ...newBiz, reinvest: r, personal: 100 - r });
                  }}
                />
              </label>
              <label>
                Personal Wealth % (e.g. 30)
                <input
                  type="number"
                  min="0"
                  max="100"
                  value={newBiz.personal}
                  onChange={(e) => {
                    const p = Number(e.target.value);
                    setNewBiz({ ...newBiz, personal: p, reinvest: 100 - p });
                  }}
                />
              </label>
            </div>
            <button className="btn" type="submit" disabled={busy}>Register Business</button>
          </form>
        </Card>
      )}

      {businesses.length > 1 && (
        <div className="row" style={{ marginTop: 12, marginBottom: 8 }}>
          {businesses.map((biz) => (
            <button
              key={biz.slug ?? biz.name}
              className={`btn ${selectedSlug === biz.slug ? "" : "ghost"}`}
              onClick={() => handleSelectBusiness(biz.slug ?? biz.name.toLowerCase())}
            >
              {biz.name}
            </button>
          ))}
        </div>
      )}

      {data ? (
        <>
          <div className="grid three" style={{ marginTop: 16 }}>
            <Card title={`${data.name} Revenue`}>
              <MoneyView money={data.revenue} large />
            </Card>
            <Card title={`${data.name} Expenses`}>
              <MoneyView money={data.expenses} large />
            </Card>
            <Card title="Net Result">
              <MoneyView money={data.net} large />
            </Card>
          </div>

          <div className="grid two" style={{ marginTop: 16 }}>
            <Card title="Cost & Revenue Breakdown">
              {data.lines.map((line) => (
                <div key={line.category} style={{ marginBottom: 12 }}>
                  <p className="sentence" style={{ fontWeight: 600 }}>{line.category}</p>
                  <MoneyView money={line.amount} />
                </div>
              ))}
            </Card>

            <Card title="Revenue Split Rule">
              <p className="sentence">
                When revenue arrives, {data.reinvestPercent}% is retained for operational tools & infrastructure, and {data.personalPercent}% flows to personal wealth.
              </p>
              <form className="stack" style={{ marginTop: 16 }} onSubmit={handleSaveSplit}>
                <div className="row">
                  <label style={{ flex: 1 }}>
                    Reinvest %
                    <input
                      type="number"
                      min="0"
                      max="100"
                      value={splitEdit.reinvest}
                      onChange={(e) => {
                        const r = Number(e.target.value);
                        setSplitEdit({ reinvest: r, personal: 100 - r });
                      }}
                    />
                  </label>
                  <label style={{ flex: 1 }}>
                    Personal %
                    <input
                      type="number"
                      min="0"
                      max="100"
                      value={splitEdit.personal}
                      onChange={(e) => {
                        const p = Number(e.target.value);
                        setSplitEdit({ personal: p, reinvest: 100 - p });
                      }}
                    />
                  </label>
                </div>
                <button className="btn ghost" type="submit" disabled={busy}>Save Split</button>
              </form>
            </Card>
          </div>

          <Card title="Business Subscriptions & Services">
            {subs.filter((s) => s.isBusiness).map((sub) => (
              <p key={sub.id} className="sentence" style={{ marginBottom: 8 }}>
                <strong>{sub.name}</strong> · {sub.amount.formatted} ({sub.frequency}) · Next billing: {sub.nextBillingDate}
              </p>
            ))}
            {subs.filter((s) => s.isBusiness).length === 0 && (
              <p className="lede">No active business subscriptions recorded on Access card.</p>
            )}
          </Card>
        </>
      ) : (
        <p>Loading business ledger…</p>
      )}
    </Shell>
  );
}
