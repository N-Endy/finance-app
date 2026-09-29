"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView, Reveal } from "@/components/ui";
import { NetWorthTrajectoryChart, FireSimulatorWidget } from "@/components/charts";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Dashboard, ExchangeRate, FixedAsset, Holding, Pension, Retirement } from "@/lib/types";

export default function InvestmentsPage() {
  const [holdings, setHoldings] = useState<Holding[]>([]);
  const [fixedAssets, setFixedAssets] = useState<FixedAsset[]>([]);
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [pension, setPension] = useState<Pension | null>(null);
  const [retirement, setRetirement] = useState<Retirement | null>(null);
  const [todayRate, setTodayRate] = useState<ExchangeRate | null>(null);
  const [rate, setRate] = useState("");
  const [rsa, setRsa] = useState("");
  const [balances, setBalances] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  // Unit valuation modal / inline state
  const [editUnitSlug, setEditUnitSlug] = useState<string | null>(null);
  const [unitForm, setUnitForm] = useState({
    unitsHeld: "",
    costBasisMajor: "",
    currentUnitPrice: "",
    symbol: "",
    assetClass: "Stock"
  });

  // Fixed Asset Form
  const [showAddAsset, setShowAddAsset] = useState(false);
  const [assetForm, setAssetForm] = useState({
    name: "",
    category: "Gadget",
    purchasePrice: "",
    currentValuation: "",
    purchaseDate: new Date().toISOString().slice(0, 10),
    usefulLifeMonths: "36",
    salvageValue: "0",
    notes: "",
    includeInNetWorth: true
  });

  async function load() {
    const [rows, assets, dash, pen, ret] = await Promise.all([
      api.holdings(),
      api.fixedAssets(),
      api.dashboard(),
      api.pension(),
      api.retirement()
    ]);
    setHoldings(rows);
    setFixedAssets(assets);
    setDashboard(dash);
    setPension(pen);
    setRetirement(ret);
    setBalances(Object.fromEntries(rows.map((holding) => [holding.id, holding.amount.major == null ? "" : String(holding.amount.major)])));

    try {
      setTodayRate(await api.ensureTodaysRate());
      setError(null);
    } catch (err) {
      setTodayRate(null);
      setError(failMessage(err));
    }
  }

  useEffect(() => {
    void load().catch((err) => setError(failMessage(err)));
  }, []);

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

  function openUnitEdit(holding: Holding) {
    const slug = holding.name.toLowerCase().replace(/\s+/g, "-");
    setEditUnitSlug(slug);
    setUnitForm({
      unitsHeld: holding.unitsHeld != null ? String(holding.unitsHeld) : "",
      costBasisMajor: holding.costBasisMajor != null ? String(holding.costBasisMajor) : "",
      currentUnitPrice: holding.currentUnitPrice != null ? String(holding.currentUnitPrice) : "",
      symbol: holding.symbol ?? "",
      assetClass: holding.assetClass ?? "Stock"
    });
  }

  async function saveUnitValuation(event: FormEvent) {
    event.preventDefault();
    if (!editUnitSlug) return;
    setError(null);
    setMessage(null);
    setBusy("unit_save");
    try {
      await api.updateHoldingValuation(editUnitSlug, {
        unitsHeld: unitForm.unitsHeld ? Number(unitForm.unitsHeld) : null,
        costBasisMajor: unitForm.costBasisMajor ? Number(unitForm.costBasisMajor) : null,
        currentUnitPrice: unitForm.currentUnitPrice ? Number(unitForm.currentUnitPrice) : null,
        symbol: unitForm.symbol || null,
        assetClass: unitForm.assetClass || null,
        asOf: new Date().toISOString().slice(0, 10)
      });
      setEditUnitSlug(null);
      await load();
      setMessage("Unit valuation saved.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleAddAsset(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setBusy("add_asset");
    try {
      await api.createFixedAsset({
        name: assetForm.name.trim(),
        category: assetForm.category,
        purchasePrice: Number(assetForm.purchasePrice),
        currentValuation: Number(assetForm.currentValuation || assetForm.purchasePrice),
        currency: "NGN",
        purchaseDate: assetForm.purchaseDate,
        usefulLifeMonths: Number(assetForm.usefulLifeMonths || 36),
        salvageValue: Number(assetForm.salvageValue || 0),
        notes: assetForm.notes || null,
        includeInNetWorth: assetForm.includeInNetWorth
      });
      setShowAddAsset(false);
      setAssetForm({
        name: "",
        category: "Gadget",
        purchasePrice: "",
        currentValuation: "",
        purchaseDate: new Date().toISOString().slice(0, 10),
        usefulLifeMonths: "36",
        salvageValue: "0",
        notes: "",
        includeInNetWorth: true
      });
      await load();
      setMessage("Fixed asset recorded.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  const confirmedNetWorth = dashboard?.netWorth.major ?? 0;
  const currentInvestmentsTotal = holdings.reduce((sum, h) => sum + (h.amount.major ?? 0), 0);

  return (
    <Shell>
      <h1>Investments, Assets, and Retirement</h1>
      <p className="lede">
        Multi-asset unit portfolio (stocks, crypto, mutual funds), fixed assets with depreciation, and actuarial FIRE modeling.
      </p>

      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      {/* 12-Month Net Worth Trajectory Chart */}
      <NetWorthTrajectoryChart currentNetWorth={confirmedNetWorth} />

      {/* Holdings & Unit Valuations */}
      <h2 style={{ marginTop: 24, fontSize: "1.25rem" }}>Investment Holdings & Portfolios</h2>
      <div className="grid two" style={{ marginTop: 12 }}>
        {holdings.map((holding) => {
          const hasUnits = holding.unitsHeld != null && holding.currentUnitPrice != null;
          const pnl = holding.unrealizedPnLMajor;
          return (
            <Card key={holding.id} title={holding.name}>
              <MoneyView money={holding.amount} />
              <p className="sentence">{holding.purpose}. {holding.statusNote}</p>
              {holding.isExpectedReceivable && <span className="badge expected">expected receivable</span>}

              {hasUnits && (
                <div style={{
                  padding: "10px 12px",
                  borderRadius: 6,
                  backgroundColor: "rgba(255,255,255,0.03)",
                  border: "1px solid var(--border)",
                  fontSize: "0.85rem",
                  margin: "10px 0"
                }}>
                  <div className="grid two" style={{ gap: 8 }}>
                    <div>
                      <span style={{ color: "var(--text-muted)" }}>Units Held: </span>
                      <strong>{holding.unitsHeld?.toLocaleString()}</strong> {holding.symbol && `(${holding.symbol})`}
                    </div>
                    <div>
                      <span style={{ color: "var(--text-muted)" }}>Unit Price: </span>
                      <strong>₦{holding.currentUnitPrice?.toLocaleString()}</strong>
                    </div>
                    <div>
                      <span style={{ color: "var(--text-muted)" }}>Cost Basis: </span>
                      <strong>₦{holding.costBasisMajor?.toLocaleString()}</strong>
                    </div>
                    <div>
                      <span style={{ color: "var(--text-muted)" }}>Unrealized P&L: </span>
                      <strong style={{ color: (pnl ?? 0) >= 0 ? "#1ee087" : "#f05252" }}>
                        {(pnl ?? 0) >= 0 ? "+" : ""}₦{pnl?.toLocaleString()}
                      </strong>
                    </div>
                  </div>
                </div>
              )}

              <div className="row" style={{ marginTop: 10, gap: 8 }}>
                <button className="btn ghost" onClick={() => openUnitEdit(holding)}>
                  {hasUnits ? "Edit Unit Valuation" : "Configure Units/Symbol"}
                </button>
              </div>

              <form className="row" style={{ marginTop: 10 }} onSubmit={(event) => saveHolding(event, holding)}>
                <input
                  value={balances[holding.id] ?? ""}
                  onChange={(e) => setBalances((current) => ({ ...current, [holding.id]: e.target.value }))}
                  placeholder="Enter total balance"
                  inputMode="decimal"
                  aria-label={`${holding.name} current balance`}
                />
                <button className="btn" type="submit" disabled={busy === holding.id}>Save</button>
              </form>
            </Card>
          );
        })}
      </div>

      {/* Unit Valuation Modal / Form */}
      {editUnitSlug && (
        <Reveal watch={editUnitSlug}>
          <Card title="Unit-based market valuation">
            <p className="sentence">
              Configure units, cost basis, and current unit price for NGX stocks, US stocks (Bamboo), or crypto. Total holding balance will calculate automatically.
            </p>
            <form className="stack" onSubmit={saveUnitValuation} style={{ marginTop: 12 }}>
              <div className="grid two">
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Asset Class</label>
                  <select value={unitForm.assetClass} onChange={(e) => setUnitForm({ ...unitForm, assetClass: e.target.value })}>
                    <option value="Stock">Stock (NGX / US)</option>
                    <option value="Crypto">Crypto (BTC / ETH / USDT)</option>
                    <option value="MutualFund">Mutual Fund / MMF</option>
                    <option value="RealEstate">Real Estate</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Symbol (e.g. MTNN, AAPL, BTC)</label>
                  <input
                    value={unitForm.symbol}
                    onChange={(e) => setUnitForm({ ...unitForm, symbol: e.target.value })}
                    placeholder="e.g. BAMBOO:AAPL"
                  />
                </div>
              </div>

              <div className="grid three">
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Units Held</label>
                  <input
                    value={unitForm.unitsHeld}
                    onChange={(e) => setUnitForm({ ...unitForm, unitsHeld: e.target.value })}
                    placeholder="e.g. 50"
                    inputMode="decimal"
                  />
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Avg Cost / Unit (₦)</label>
                  <input
                    value={unitForm.costBasisMajor}
                    onChange={(e) => setUnitForm({ ...unitForm, costBasisMajor: e.target.value })}
                    placeholder="e.g. 2400"
                    inputMode="decimal"
                  />
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Current Price / Unit (₦)</label>
                  <input
                    value={unitForm.currentUnitPrice}
                    onChange={(e) => setUnitForm({ ...unitForm, currentUnitPrice: e.target.value })}
                    placeholder="e.g. 2950"
                    inputMode="decimal"
                  />
                </div>
              </div>

              <div className="row" style={{ gap: 8 }}>
                <button className="btn" type="submit" disabled={busy === "unit_save"}>Save unit valuation</button>
                <button className="btn ghost" type="button" onClick={() => setEditUnitSlug(null)}>Cancel</button>
              </div>
            </form>
          </Card>
        </Reveal>
      )}

      {/* Fixed Asset & Depreciation Registry */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 32, marginBottom: 12 }}>
        <h2 style={{ margin: 0, fontSize: "1.25rem" }}>Physical Fixed Assets & Depreciation</h2>
        <button className="btn ghost" onClick={() => setShowAddAsset((v) => !v)}>
          {showAddAsset ? "Close" : "+ Add physical asset"}
        </button>
      </div>

      {showAddAsset && (
        <Reveal watch={showAddAsset}>
          <Card title="Register physical fixed asset">
            <p className="sentence">
              Record gadgets (MacBook, iPhone), work equipment, vehicle, or land for balance sheet tracking and straight-line depreciation.
            </p>
            <form className="stack" onSubmit={handleAddAsset} style={{ marginTop: 12 }}>
              <div className="grid two">
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Asset Name</label>
                  <input
                    value={assetForm.name}
                    onChange={(e) => setAssetForm({ ...assetForm, name: e.target.value })}
                    placeholder="e.g. MacBook Pro M3 Max"
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Category</label>
                  <select value={assetForm.category} onChange={(e) => setAssetForm({ ...assetForm, category: e.target.value })}>
                    <option value="Gadget">Gadget / Tech</option>
                    <option value="Equipment">Work Equipment</option>
                    <option value="Vehicle">Vehicle</option>
                    <option value="Property">Land / Property</option>
                  </select>
                </div>
              </div>

              <div className="grid three">
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Purchase Price (₦)</label>
                  <input
                    value={assetForm.purchasePrice}
                    onChange={(e) => setAssetForm({ ...assetForm, purchasePrice: e.target.value })}
                    placeholder="e.g. 2500000"
                    inputMode="decimal"
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Current Book Valuation (₦)</label>
                  <input
                    value={assetForm.currentValuation}
                    onChange={(e) => setAssetForm({ ...assetForm, currentValuation: e.target.value })}
                    placeholder="e.g. 2100000"
                    inputMode="decimal"
                  />
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Purchase Date</label>
                  <input
                    type="date"
                    value={assetForm.purchaseDate}
                    onChange={(e) => setAssetForm({ ...assetForm, purchaseDate: e.target.value })}
                  />
                </div>
              </div>

              <div className="grid two">
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Useful Life (Months)</label>
                  <input
                    value={assetForm.usefulLifeMonths}
                    onChange={(e) => setAssetForm({ ...assetForm, usefulLifeMonths: e.target.value })}
                    placeholder="36"
                    inputMode="numeric"
                  />
                </div>
                <div>
                  <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>Estimated Salvage Value (₦)</label>
                  <input
                    value={assetForm.salvageValue}
                    onChange={(e) => setAssetForm({ ...assetForm, salvageValue: e.target.value })}
                    placeholder="0"
                    inputMode="decimal"
                  />
                </div>
              </div>

              <button className="btn" type="submit" disabled={busy === "add_asset"}>
                {busy === "add_asset" ? "Recording…" : "Save Fixed Asset"}
              </button>
            </form>
          </Card>
        </Reveal>
      )}

      {fixedAssets.length > 0 ? (
        <table className="table" style={{ marginTop: 12 }}>
          <thead>
            <tr>
              <th>Asset</th>
              <th>Category</th>
              <th>Purchase Date</th>
              <th>Cost Price</th>
              <th>Current Book Value</th>
              <th>Depreciation / Mo</th>
            </tr>
          </thead>
          <tbody>
            {fixedAssets.map((asset) => {
              const cost = asset.purchasePrice.major ?? 0;
              const salvage = asset.salvageValue.major ?? 0;
              const months = asset.usefulLifeMonths || 36;
              const monthlyDepreciation = Math.round((cost - salvage) / months);
              return (
                <tr key={asset.id}>
                  <td><strong>{asset.name}</strong></td>
                  <td><span className="badge">{asset.category}</span></td>
                  <td>{asset.purchaseDate}</td>
                  <td>{asset.purchasePrice.formatted}</td>
                  <td style={{ fontWeight: 700, color: "#1ee087" }}>{asset.currentValuation.formatted}</td>
                  <td>₦{monthlyDepreciation.toLocaleString()}/mo</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      ) : (
        <Card title="No physical fixed assets recorded">
          <p className="sentence">Record physical assets (laptops, equipment, vehicles) to track their current depreciated book value in your Confirmed Net Worth.</p>
        </Card>
      )}

      {/* FX Rates & Pension */}
      <h2 style={{ marginTop: 32, fontSize: "1.25rem" }}>FX Rates & Retirement</h2>
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

      {/* 30-Year Actuarial FIRE Simulator */}
      <FireSimulatorWidget currentInvestments={currentInvestmentsTotal} />

      {retirement && (
        <Card title="Retirement assumptions & scenarios">
          <p className="sentence">{retirement.sentence}</p>
          {retirement.scenarios.map((s) => <p key={s.name} className="lede">{s.name}: {s.sentence}</p>)}
        </Card>
      )}
    </Shell>
  );
}
