"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Account, Business, Subscription } from "@/lib/types";

const CATEGORY_PRESETS = [
  "Hosting",
  "Domain",
  "Database",
  "AI / API",
  "Subscriptions",
  "Tools & Software",
  "Marketing & Ads",
  "Client Revenue",
  "Other"
];

export default function BusinessPage() {
  const [businesses, setBusinesses] = useState<Business[]>([]);
  const [selectedSlug, setSelectedSlug] = useState<string>("matchpredictor");
  const [data, setData] = useState<Business | null>(null);
  const [subs, setSubs] = useState<Subscription[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Forms visibility
  const [showAddForm, setShowAddForm] = useState(false);
  const [showTxModal, setShowTxModal] = useState(false);
  const [newBiz, setNewBiz] = useState({ name: "", slug: "", description: "", reinvest: 70, personal: 30 });
  const [splitEdit, setSplitEdit] = useState({ reinvest: 70, personal: 30 });

  // Direct Transaction Form
  const [txForm, setTxForm] = useState({
    type: "Expense",
    category: "Hosting",
    customCategory: "",
    amount: "",
    currency: "NGN",
    accountId: "",
    date: new Date().toISOString().slice(0, 10),
    description: "",
    notes: ""
  });

  // Inline Baseline Cost Editor
  const [editingLine, setEditingLine] = useState<{
    category: string;
    amount: string;
    provenance: string;
    notes: string;
  } | null>(null);

  async function load(currentSlug = selectedSlug) {
    try {
      const [allBiz, subscriptions, accs] = await Promise.all([
        api.businesses().catch(() => []),
        api.subscriptions().catch(() => []),
        api.accounts().catch(() => [])
      ]);

      const list = allBiz.length > 0 ? allBiz : [await api.business("matchpredictor")];
      setBusinesses(list);
      setSubs(subscriptions);
      setAccounts(accs);

      if (!txForm.accountId && accs.length > 0) {
        setTxForm((prev) => ({ ...prev, accountId: accs[0].id }));
      }

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

  async function handleLogTransaction(e: FormEvent) {
    e.preventDefault();
    if (!data?.slug || !txForm.amount || !txForm.accountId) return;
    setError(null);
    setMessage(null);
    setBusy(true);

    const chosenCat = txForm.category === "Other" && txForm.customCategory.trim()
      ? txForm.customCategory.trim()
      : txForm.category;

    try {
      const updated = await api.logBusinessTx(data.slug, {
        type: txForm.type,
        category: chosenCat,
        amount: Number(txForm.amount),
        currency: txForm.currency,
        accountId: txForm.accountId,
        date: txForm.date,
        description: txForm.description.trim() || undefined,
        notes: txForm.notes.trim() || undefined
      });
      setData(updated);
      setShowTxModal(false);
      setTxForm((prev) => ({
        ...prev,
        amount: "",
        description: "",
        notes: "",
        customCategory: ""
      }));
      setMessage(`Logged ${txForm.type.toLowerCase()} of ₦${Number(txForm.amount).toLocaleString()} for ${chosenCat}.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleSaveBaselineLine(e: FormEvent) {
    e.preventDefault();
    if (!data?.slug || !editingLine) return;
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      const updated = await api.updateBusinessBaselineLine(data.slug, {
        category: editingLine.category,
        amountMajor: Number(editingLine.amount) || 0,
        currency: "NGN",
        provenance: editingLine.provenance || "Confirmed",
        notes: editingLine.notes.trim() || undefined
      });
      setData(updated);
      setEditingLine(null);
      setMessage(`Updated baseline cost for ${editingLine.category}.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Shell>
      <div className="row" style={{ justifyContent: "space-between", alignItems: "flex-start", gap: 16 }}>
        <div>
          <h1>Business & Venture P&L</h1>
          <p className="lede">Venture costs and revenue kept separate from personal spending. Reinvestment stays in business accounts.</p>
        </div>
        <div className="row" style={{ gap: 8 }}>
          <button
            className="btn"
            onClick={() => {
              setShowTxModal((v) => !v);
              setShowAddForm(false);
              setEditingLine(null);
            }}
          >
            {showTxModal ? "Close" : "+ Log Transaction"}
          </button>
          <button
            className="btn ghost"
            onClick={() => {
              setShowAddForm((v) => !v);
              setShowTxModal(false);
              setEditingLine(null);
            }}
          >
            {showAddForm ? "Cancel" : "+ Register Business"}
          </button>
        </div>
      </div>

      {message && <p className="sentence" style={{ color: "var(--accent)" }}>{message}</p>}
      {error && <p className="error">{error}</p>}

      {/* Log Transaction Modal / Form */}
      {showTxModal && data && (
        <Card title={`Log Business Transaction for ${data.name}`}>
          <form className="stack" onSubmit={handleLogTransaction}>
            <div className="row" style={{ gap: 8, marginBottom: 4 }}>
              <button
                type="button"
                className={`btn ${txForm.type === "Expense" ? "" : "ghost"}`}
                style={{ flex: 1, minHeight: 40 }}
                onClick={() => setTxForm({ ...txForm, type: "Expense" })}
              >
                Business Expense
              </button>
              <button
                type="button"
                className={`btn ${txForm.type === "Revenue" ? "" : "ghost"}`}
                style={{ flex: 1, minHeight: 40 }}
                onClick={() => setTxForm({ ...txForm, type: "Revenue" })}
              >
                Business Revenue
              </button>
            </div>

            <div className="grid two">
              <label>
                Category
                <select
                  value={txForm.category}
                  onChange={(e) => setTxForm({ ...txForm, category: e.target.value })}
                  required
                >
                  {CATEGORY_PRESETS.map((cat) => (
                    <option key={cat} value={cat}>{cat}</option>
                  ))}
                </select>
              </label>

              {txForm.category === "Other" && (
                <label>
                  Custom Category Name
                  <input
                    value={txForm.customCategory}
                    onChange={(e) => setTxForm({ ...txForm, customCategory: e.target.value })}
                    placeholder="e.g. Legal Fees, Freelancers"
                    required
                  />
                </label>
              )}

              <label>
                Amount (Naira)
                <input
                  type="number"
                  step="any"
                  min="0.01"
                  value={txForm.amount}
                  onChange={(e) => setTxForm({ ...txForm, amount: e.target.value })}
                  placeholder="e.g. 15000"
                  required
                />
              </label>

              <label>
                Payment / Receiving Account
                <select
                  value={txForm.accountId}
                  onChange={(e) => setTxForm({ ...txForm, accountId: e.target.value })}
                  required
                >
                  {accounts.map((acc) => (
                    <option key={acc.id} value={acc.id}>
                      {acc.name} ({acc.institution})
                    </option>
                  ))}
                </select>
              </label>

              <label>
                Date
                <input
                  type="date"
                  value={txForm.date}
                  onChange={(e) => setTxForm({ ...txForm, date: e.target.value })}
                  required
                />
              </label>

              <label>
                Description
                <input
                  value={txForm.description}
                  onChange={(e) => setTxForm({ ...txForm, description: e.target.value })}
                  placeholder={`e.g. ${data.name} ${txForm.category} charge`}
                />
              </label>
            </div>

            <div className="row" style={{ justifyContent: "flex-end", marginTop: 8, gap: 10 }}>
              <button
                type="button"
                className="btn ghost"
                onClick={() => setShowTxModal(false)}
                disabled={busy}
              >
                Cancel
              </button>
              <button className="btn" type="submit" disabled={busy || !txForm.amount}>
                {busy ? "Recording…" : `Record ${txForm.type}`}
              </button>
            </div>
          </form>
        </Card>
      )}

      {/* Register Business Form */}
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

      {/* Inline Baseline Cost Editor Modal / Card */}
      {editingLine && data && (
        <Card title={`Configure Baseline Cost: ${editingLine.category}`}>
          <form className="stack" onSubmit={handleSaveBaselineLine}>
            <p className="sentence">
              Set your monthly baseline estimate or confirmed recurring cost for <strong>{editingLine.category}</strong>. This replaces UNKNOWN until a bank transaction occurs.
            </p>
            <div className="grid two">
              <label>
                Monthly Amount (Naira)
                <input
                  type="number"
                  step="any"
                  min="0"
                  value={editingLine.amount}
                  onChange={(e) => setEditingLine({ ...editingLine, amount: e.target.value })}
                  placeholder="e.g. 15000"
                  required
                />
              </label>
              <label>
                Status Badge
                <select
                  value={editingLine.provenance}
                  onChange={(e) => setEditingLine({ ...editingLine, provenance: e.target.value })}
                >
                  <option value="Confirmed">Confirmed (Exact rate known)</option>
                  <option value="Estimate">Estimate (Approximate budget)</option>
                </select>
              </label>
            </div>
            <label>
              Notes / Provider Details
              <input
                value={editingLine.notes}
                onChange={(e) => setEditingLine({ ...editingLine, notes: e.target.value })}
                placeholder="e.g. Railway Hobby container + egress, Namecheap annual divided by 12"
              />
            </label>
            <div className="row" style={{ justifyContent: "flex-end", gap: 10 }}>
              <button
                type="button"
                className="btn ghost"
                onClick={() => setEditingLine(null)}
                disabled={busy}
              >
                Cancel
              </button>
              <button className="btn" type="submit" disabled={busy}>
                {busy ? "Saving…" : "Save Baseline"}
              </button>
            </div>
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
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                <span style={{ fontSize: "12px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.08em" }}>
                  Active Operational Lines
                </span>
                <span style={{ fontSize: "12px", color: "var(--muted)" }}>
                  Click &apos;Set Cost&apos; to resolve UNKNOWN
                </span>
              </div>

              {data.lines.map((line) => {
                const isUnknown = line.amount.provenance === "unknown" || line.amount.provenanceLabel === "unknown" || line.amount.minor === null;
                return (
                  <div
                    key={line.category}
                    style={{
                      marginBottom: 12,
                      paddingBottom: 10,
                      borderBottom: "1px solid var(--line)",
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "flex-start",
                      gap: 12
                    }}
                  >
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <p className="sentence" style={{ fontWeight: 600 }}>{line.category}</p>
                      <MoneyView money={line.amount} />
                    </div>
                    <button
                      type="button"
                      className="btn ghost"
                      style={{ padding: "4px 10px", minHeight: "34px", fontSize: "12px", whiteSpace: "nowrap" }}
                      onClick={() => setEditingLine({
                        category: line.category,
                        amount: line.amount.minor ? (line.amount.minor / 100).toString() : "",
                        provenance: isUnknown ? "Confirmed" : "Estimate",
                        notes: line.amount.needed ?? ""
                      })}
                    >
                      {isUnknown ? "+ Set Cost" : "Edit"}
                    </button>
                  </div>
                );
              })}
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
