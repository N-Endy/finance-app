"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { Settings, AuthStatus } from "@/lib/types";

export default function SettingsPage() {
  const [settings, setSettings] = useState<Settings | null>(null);
  const [user, setUser] = useState<AuthStatus | null>(null);
  const [allowance, setAllowance] = useState("70000");
  const [confirmClear, setConfirmClear] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState("");
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    void api.settings().then((row) => {
      setSettings(row);
      setAllowance(String(row.opayAllowance.major ?? 70000));
    }).catch((err) => setError(failMessage(err)));

    void api.me().then((me) => {
      setUser(me);
    }).catch(() => {});
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
      <p className="lede">Account session, OPay allowance, data export, and database lifecycle management.</p>
      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      <Card title="Account & Active Session">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <p className="sentence" style={{ fontSize: "1.1rem", fontWeight: 600, color: "var(--ink)", margin: 0 }}>
              {user?.displayName || "Account Owner"}
            </p>
            <p className="lede" style={{ margin: "4px 0 0", fontSize: "0.88rem" }}>
              {user?.email || "Authenticated"} · Base Currency: <strong>{user?.baseCurrency || "USD"}</strong>
            </p>
          </div>
          <button
            type="button"
            className="btn ghost"
            onClick={async () => {
              try {
                await api.logout();
              } finally {
                window.location.href = "/login";
              }
            }}
            style={{ borderColor: "rgba(255, 255, 255, 0.15)" }}
          >
            Sign Out
          </button>
        </div>
      </Card>

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
            onClick={() => {
              setDeleteConfirmText("");
              setDeleteError(null);
              setDeleteModalOpen(true);
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

      {deleteModalOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="delete-modal-title"
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(0, 0, 0, 0.85)",
            backdropFilter: "blur(6px)",
            WebkitBackdropFilter: "blur(6px)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1000,
            padding: "1.5rem"
          }}
          onClick={() => {
            if (!deleteBusy) {
              setDeleteModalOpen(false);
              setDeleteConfirmText("");
              setDeleteError(null);
            }
          }}
        >
          <div
            className="stack"
            style={{
              background: "var(--card, #0d1c16)",
              border: "1px solid rgba(240, 90, 74, 0.5)",
              borderRadius: "12px",
              padding: "2rem",
              maxWidth: "520px",
              width: "100%",
              boxShadow: "0 24px 48px rgba(0, 0, 0, 0.7)"
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
              <div
                style={{
                  width: "42px",
                  height: "42px",
                  borderRadius: "8px",
                  background: "rgba(240, 90, 74, 0.15)",
                  color: "var(--alert, #f05a4a)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "1.3rem",
                  fontWeight: 700
                }}
              >
                ⚠️
              </div>
              <div>
                <h3 id="delete-modal-title" style={{ margin: 0, fontSize: "1.25rem", color: "var(--alert, #f05a4a)" }}>
                  Permanently Delete Account
                </h3>
                <p className="lede" style={{ margin: 0, fontSize: "0.85rem" }}>
                  Irreversible Cascade Purge
                </p>
              </div>
            </div>

            <p className="sentence" style={{ color: "var(--ink)", lineHeight: 1.5, margin: "0.5rem 0" }}>
              This will permanently delete your account and immediately purge all data across the entire database:
            </p>

            <ul style={{ margin: 0, paddingLeft: "1.25rem", color: "var(--muted)", fontSize: "0.88rem", lineHeight: 1.6 }}>
              <li>All financial accounts, live balances, and snapshots</li>
              <li>Every income, expense, and transfer transaction</li>
              <li>All budget envelopes, allocation rules, and category splits</li>
              <li>Debt payoff plans, tax reserves, and financial goals</li>
              <li>Household workspaces owned by you (disbands joint view)</li>
              <li>Your security credentials, login identity, and audit logs</li>
            </ul>

            <div style={{ marginTop: "0.5rem" }}>
              <label style={{ display: "block", fontSize: "0.84rem", fontWeight: 600, color: "var(--ink)", marginBottom: "0.4rem" }}>
                To confirm, type <strong style={{ color: "var(--alert, #f05a4a)", letterSpacing: "0.05em" }}>DELETE</strong> below:
              </label>
              <input
                type="text"
                placeholder="Type DELETE"
                value={deleteConfirmText}
                onChange={(e) => setDeleteConfirmText(e.target.value)}
                disabled={deleteBusy}
                autoFocus
                style={{
                  width: "100%",
                  padding: "0.75rem 1rem",
                  borderRadius: "6px",
                  border: "1px solid rgba(255, 255, 255, 0.15)",
                  background: "rgba(0, 0, 0, 0.3)",
                  color: "#fff",
                  fontSize: "0.95rem"
                }}
              />
            </div>

            {deleteError && <p className="error" style={{ margin: 0 }}>{deleteError}</p>}

            <div className="row" style={{ marginTop: "1rem", justifyContent: "flex-end", gap: "0.75rem" }}>
              <button
                type="button"
                className="btn ghost"
                disabled={deleteBusy}
                onClick={() => {
                  setDeleteModalOpen(false);
                  setDeleteConfirmText("");
                  setDeleteError(null);
                }}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn warn"
                disabled={deleteConfirmText.trim().toUpperCase() !== "DELETE" || deleteBusy}
                onClick={async () => {
                  setDeleteBusy(true);
                  setDeleteError(null);
                  try {
                    await api.deleteAccount();
                    window.location.href = "/login";
                  } catch (err) {
                    setDeleteError(failMessage(err));
                    setDeleteBusy(false);
                  }
                }}
                style={{
                  opacity: deleteConfirmText.trim().toUpperCase() === "DELETE" && !deleteBusy ? 1 : 0.45,
                  cursor: deleteConfirmText.trim().toUpperCase() === "DELETE" && !deleteBusy ? "pointer" : "not-allowed"
                }}
              >
                {deleteBusy ? "Purging Account…" : "Delete Everything"}
              </button>
            </div>
          </div>
        </div>
      )}
    </Shell>
  );
}
