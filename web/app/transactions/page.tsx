"use client";

import { ChangeEvent, FormEvent, useEffect, useState } from "react";
import { Shell, Card, Reveal } from "@/components/ui";
import { AskLedger } from "@/components/ask-ledger";
import { OfflineIndicator } from "@/components/offline-indicator";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import { savePendingTransaction, useOfflineQueue } from "@/lib/offline-queue";
import type { Account, ParsedTransactionDraft, Preview, StatementParseResult, Transaction } from "@/lib/types";

export default function TransactionsPage() {
  const [rows, setRows] = useState<Transaction[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [text, setText] = useState("Lunch 4300 from OPay");
  const [preview, setPreview] = useState<Preview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [showDetail, setShowDetail] = useState(false);
  const [activeTab, setActiveTab] = useState<"quick" | "statement">("quick");

  // Offline queue hook
  const { isOnline } = useOfflineQueue();

  // Statement Ingestion State
  const [statementText, setStatementText] = useState("");
  const [bankFormat, setBankFormat] = useState("auto");
  const [selectedAccountId, setSelectedAccountId] = useState("");
  const [parseResult, setParseResult] = useState<StatementParseResult | null>(null);
  const [selectedDraftIndices, setSelectedDraftIndices] = useState<Set<number>>(new Set());

  // Detailed entry state
  const [detail, setDetail] = useState({
    date: new Date().toISOString().slice(0, 10),
    type: "Expense",
    accountId: "",
    counterpartyAccountId: "",
    amount: "",
    description: ""
  });

  // Edit transaction state
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({
    date: "",
    type: "Expense",
    accountId: "",
    amount: "",
    description: ""
  });

  async function load() {
    const [txs, accs] = await Promise.all([
      api.transactions(),
      api.accounts()
    ]);
    setRows(txs);
    setAccounts(accs);
    if (!selectedAccountId && accs.length > 0) {
      setSelectedAccountId(accs[0].id);
    }
  }

  useEffect(() => {
    void load().catch((err) => setError(failMessage(err)));
  }, []);

  async function previewQuick(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy("preview");
    try {
      setPreview(await api.preview({ text }));
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function confirm() {
    if (!preview?.proposed || !preview.canCommit) return;
    setError(null);
    setMessage(null);
    setBusy("confirm");
    try {
      if (!isOnline) {
        await savePendingTransaction(preview.proposed, preview.summary);
        setPreview(null);
        setText("");
        setMessage("Saved to offline queue. Will sync automatically when connected.");
      } else {
        await api.createTx(preview.proposed);
        setPreview(null);
        setText("");
        await load();
        setMessage("Recorded.");
      }
    } catch (err) {
      // If network failed, fall back to offline queue
      try {
        await savePendingTransaction(preview.proposed, preview.summary);
        setPreview(null);
        setText("");
        setMessage("Network error: Transaction saved to offline queue. Will sync when online.");
      } catch {
        setError(failMessage(err));
      }
    } finally {
      setBusy(null);
    }
  }

  async function submitDetail(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy("detail");
    const payload = {
      date: detail.date,
      type: detail.type,
      accountId: detail.accountId,
      counterpartyAccountId: detail.counterpartyAccountId || null,
      amount: Number(detail.amount),
      fee: 0,
      currency: "NGN",
      description: detail.description,
      isBusiness: false,
      isRecurring: false
    };

    try {
      setPreview(await api.preview({ detailed: payload }));
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function voidRow(id: string) {
    setError(null);
    setMessage(null);
    setBusy(id);
    try {
      await api.voidTx(id);
      await load();
      setMessage("Voided.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  function startEdit(row: Transaction) {
    setEditingId(row.id);
    setEditForm({
      date: row.date,
      type: row.type,
      accountId: row.accountId ?? accounts[0]?.id ?? "",
      amount: row.amount.minor != null ? String(row.amount.minor / 100) : "",
      description: row.description
    });
  }

  async function saveEdit(id: string) {
    setError(null);
    setMessage(null);
    setBusy(`edit-${id}`);
    try {
      await api.updateTx(id, {
        date: editForm.date,
        type: editForm.type,
        accountId: editForm.accountId,
        amount: Number(editForm.amount),
        description: editForm.description
      });
      setEditingId(null);
      await load();
      setMessage("Transaction updated. Balances readjusted automatically.");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  // Handle statement file upload
  function handleFileUpload(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      const content = event.target?.result as string;
      if (content) {
        setStatementText(content);
        setMessage(`Loaded file: ${file.name} (${Math.round(file.size / 1024)} KB)`);
      }
    };
    reader.readAsText(file);
  }

  // Parse statement
  async function handleParseStatement(event: FormEvent) {
    event.preventDefault();
    if (!statementText.trim()) {
      setError("Please paste statement content or select a file first.");
      return;
    }

    setError(null);
    setMessage(null);
    setBusy("parse");
    try {
      const result = await api.parseStatement({
        content: statementText,
        bankFormat: bankFormat === "auto" ? undefined : bankFormat,
        defaultAccountId: selectedAccountId || undefined
      });

      setParseResult(result);
      // Select all non-duplicate items by default
      const defaultSelected = new Set<number>();
      result.items.forEach((item) => {
        if (!item.isDuplicate) {
          defaultSelected.add(item.tempIndex);
        }
      });
      setSelectedDraftIndices(defaultSelected);
      setMessage(`Parsed ${result.totalParsed} transactions (${result.transfersCount} transfers detected, ${result.duplicatesCount} potential duplicates flagged).`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  // Toggle selection
  function toggleSelect(index: number) {
    const next = new Set(selectedDraftIndices);
    if (next.has(index)) next.delete(index);
    else next.add(index);
    setSelectedDraftIndices(next);
  }

  function selectAll() {
    if (!parseResult) return;
    setSelectedDraftIndices(new Set(parseResult.items.map((i) => i.tempIndex)));
  }

  function deselectDuplicates() {
    if (!parseResult) return;
    const nonDups = new Set<number>();
    parseResult.items.forEach((i) => {
      if (!i.isDuplicate) nonDups.add(i.tempIndex);
    });
    setSelectedDraftIndices(nonDups);
  }

  // Commit selected statement transactions
  async function handleCommitStatement() {
    if (!parseResult || selectedDraftIndices.size === 0) return;

    const selectedItems = parseResult.items.filter((item) => selectedDraftIndices.has(item.tempIndex));
    const txPayloads = selectedItems.map((item) => ({
      date: item.date,
      type: item.type,
      accountId: item.accountId || selectedAccountId || accounts[0]?.id,
      counterpartyAccountId: item.counterpartyAccountId,
      amount: item.amount,
      fee: 0,
      currency: item.currency || "NGN",
      categoryId: item.categoryId,
      envelopeId: null,
      description: item.description,
      notes: item.rawNarration !== item.description ? item.rawNarration : null,
      source: "statement_import",
      tags: item.isTransfer ? "transfer,statement" : "statement",
      isBusiness: false,
      isRecurring: false
    }));

    setError(null);
    setMessage(null);
    setBusy("commit_statement");
    try {
      const res = await api.commitStatement(txPayloads);
      setMessage(`Successfully committed ${res.committed} transactions to your ledger.`);
      setParseResult(null);
      setStatementText("");
      await load();
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <Shell>
      <h1>Where did my money go?</h1>
      <p className="lede">Record transactions, import bank statements (Stanbic, OPay, Kuda, Access), and inspect ledger facts.</p>

      <OfflineIndicator />

      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      {/* Entry Mode Tabs */}
      <div className="row" style={{ gap: 8, marginBottom: 16 }}>
        <button
          className={`btn ${activeTab === "quick" ? "" : "ghost"}`}
          onClick={() => setActiveTab("quick")}
        >
          Single Entry
        </button>
        <button
          className={`btn ${activeTab === "statement" ? "" : "ghost"}`}
          onClick={() => setActiveTab("statement")}
        >
          Nigerian Bank Statement Ingestion
        </button>
      </div>

      {activeTab === "quick" && (
        <>
          <Card title="Quick entry">
            <form className="stack" onSubmit={previewQuick}>
              <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Lunch 4300 from OPay" />
              <button className="btn" type="submit" disabled={busy === "preview"}>Parse and preview</button>
            </form>
          </Card>

          <p><button className="btn ghost" onClick={() => setShowDetail((v) => !v)}>{showDetail ? "Hide" : "Show"} detailed entry</button></p>
          {showDetail && (
            <Reveal watch={showDetail}>
              <Card title="Detailed entry">
                <form className="stack" onSubmit={submitDetail}>
                  <input type="date" value={detail.date} onChange={(e) => setDetail({ ...detail, date: e.target.value })} />
                  <select value={detail.type} onChange={(e) => setDetail({ ...detail, type: e.target.value })}>
                    {["Income", "Expense", "Transfer", "Fee", "Savings", "Investment", "Refund", "Withdrawal", "Deposit", "Adjustment", "BusinessExpense", "BusinessRevenue"].map((t) => <option key={t}>{t}</option>)}
                  </select>
                  <select value={detail.accountId} onChange={(e) => setDetail({ ...detail, accountId: e.target.value })}>
                    <option value="">Account</option>
                    {accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                  </select>
                  {detail.type === "Transfer" && (
                    <select value={detail.counterpartyAccountId} onChange={(e) => setDetail({ ...detail, counterpartyAccountId: e.target.value })}>
                      <option value="">Destination account</option>
                      {accounts.filter((a) => a.id !== detail.accountId).map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                    </select>
                  )}
                  <input value={detail.amount} onChange={(e) => setDetail({ ...detail, amount: e.target.value })} placeholder="Amount" />
                  <input value={detail.description} onChange={(e) => setDetail({ ...detail, description: e.target.value })} placeholder="Description" />
                  <button className="btn" type="submit" disabled={busy === "detail"}>Preview detailed entry</button>
                </form>
              </Card>
            </Reveal>
          )}
        </>
      )}

      {activeTab === "statement" && (
        <Card title="Nigerian bank statement parser">
          <p className="sentence">
            Auto-detects exports from <strong>Stanbic IBTC, OPay, Kuda Bank, and Access Bank</strong>. Inter-account transfers are detected automatically and never counted as expenses.
          </p>

          <form className="stack" onSubmit={handleParseStatement} style={{ marginTop: 12 }}>
            <div className="grid two">
              <div>
                <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>
                  Bank Format
                </label>
                <select value={bankFormat} onChange={(e) => setBankFormat(e.target.value)}>
                  <option value="auto">Auto-detect format</option>
                  <option value="stanbic">Stanbic IBTC Bank</option>
                  <option value="opay">OPay Wallet / Bank</option>
                  <option value="kuda">Kuda Microfinance Bank</option>
                  <option value="access">Access Bank</option>
                  <option value="generic">Generic CSV export</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>
                  Target Account
                </label>
                <select value={selectedAccountId} onChange={(e) => setSelectedAccountId(e.target.value)}>
                  <option value="">Auto-match from statement</option>
                  {accounts.map((a) => <option key={a.id} value={a.id}>{a.name} ({a.institution})</option>)}
                </select>
              </div>
            </div>

            <div>
              <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>
                Upload statement file (CSV or text)
              </label>
              <input type="file" accept=".csv,.txt" onChange={handleFileUpload} />
            </div>

            <div>
              <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block", marginBottom: 4 }}>
                Or paste statement CSV text here
              </label>
              <textarea
                rows={5}
                value={statementText}
                onChange={(e) => setStatementText(e.target.value)}
                placeholder="Paste CSV rows from Stanbic, OPay, Kuda, or Access Bank..."
              />
            </div>

            <button className="btn" type="submit" disabled={busy === "parse" || !statementText.trim()}>
              {busy === "parse" ? "Analyzing Statement…" : "Parse & Analyze Statement"}
            </button>
          </form>

          {/* Parsed Results Review */}
          {parseResult && (
            <div style={{ marginTop: 24, borderTop: "1px solid var(--border)", paddingTop: 16 }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 12, marginBottom: 14 }}>
                <div>
                  <h3 style={{ margin: 0 }}>Review Parsed Transactions</h3>
                  <p className="sentence" style={{ margin: "2px 0 0 0" }}>
                    Detected: <strong style={{ textTransform: "capitalize" }}>{parseResult.detectedBank}</strong> · {parseResult.totalParsed} items · {parseResult.transfersCount} transfers · {parseResult.duplicatesCount} potential duplicates
                  </p>
                </div>

                <div className="row" style={{ gap: 8 }}>
                  <button className="btn ghost" onClick={selectAll}>Select all</button>
                  <button className="btn ghost" onClick={deselectDuplicates}>Deselect duplicates</button>
                  <button
                    className="btn"
                    disabled={selectedDraftIndices.size === 0 || busy === "commit_statement"}
                    onClick={() => void handleCommitStatement()}
                  >
                    {busy === "commit_statement" ? "Committing…" : `Commit ${selectedDraftIndices.size} to Ledger`}
                  </button>
                </div>
              </div>

              <div style={{ overflowX: "auto" }}>
                <table className="table">
                  <thead>
                    <tr>
                      <th style={{ width: 40 }}></th>
                      <th>Date</th>
                      <th>Type</th>
                      <th>Amount</th>
                      <th>Description</th>
                      <th>Account</th>
                      <th>Flags</th>
                    </tr>
                  </thead>
                  <tbody>
                    {parseResult.items.map((item) => {
                      const isChecked = selectedDraftIndices.has(item.tempIndex);
                      return (
                        <tr
                          key={item.tempIndex}
                          style={{
                            opacity: isChecked ? 1 : 0.45,
                            backgroundColor: item.isDuplicate ? "rgba(240, 180, 41, 0.05)" : item.isTransfer ? "rgba(30, 224, 135, 0.04)" : "transparent"
                          }}
                        >
                          <td>
                            <input
                              type="checkbox"
                              checked={isChecked}
                              onChange={() => toggleSelect(item.tempIndex)}
                            />
                          </td>
                          <td style={{ whiteSpace: "nowrap" }}>{item.date}</td>
                          <td>
                            <span style={{
                              padding: "2px 6px",
                              borderRadius: 4,
                              fontSize: "0.75rem",
                              fontWeight: 600,
                              backgroundColor: item.isTransfer ? "rgba(30, 224, 135, 0.15)" : item.type === "Income" ? "rgba(42, 233, 151, 0.15)" : "rgba(255, 255, 255, 0.08)",
                              color: item.isTransfer ? "#1ee087" : item.type === "Income" ? "#2ae997" : "inherit"
                            }}>
                              {item.type}
                            </span>
                          </td>
                          <td style={{ whiteSpace: "nowrap", fontWeight: 600 }}>
                            ₦{item.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                          </td>
                          <td title={item.rawNarration}>
                            <div>{item.description}</div>
                            {item.rawNarration !== item.description && (
                              <div style={{ fontSize: "0.75rem", color: "var(--text-muted)" }}>{item.rawNarration}</div>
                            )}
                          </td>
                          <td>{item.accountName || "Default"}</td>
                          <td>
                            {item.isTransfer && (
                              <span style={{
                                display: "inline-block",
                                padding: "2px 6px",
                                borderRadius: 4,
                                fontSize: "0.7rem",
                                backgroundColor: "rgba(30, 224, 135, 0.2)",
                                color: "#1ee087",
                                marginRight: 4
                              }}>
                                Transfer ({item.counterpartyAccountName || "Own Account"})
                              </span>
                            )}
                            {item.isDuplicate && (
                              <span style={{
                                display: "inline-block",
                                padding: "2px 6px",
                                borderRadius: 4,
                                fontSize: "0.7rem",
                                backgroundColor: "rgba(240, 180, 41, 0.2)",
                                color: "#f0b429"
                              }} title={item.duplicateReason || "Duplicate"}>
                                Duplicate warning
                              </span>
                            )}
                            {item.needsReview && !item.isTransfer && !item.isDuplicate && (
                              <span style={{
                                display: "inline-block",
                                padding: "2px 6px",
                                borderRadius: 4,
                                fontSize: "0.7rem",
                                backgroundColor: "rgba(255, 255, 255, 0.1)",
                                color: "var(--text-muted)"
                              }}>
                                Uncategorized
                              </span>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </Card>
      )}

      <AskLedger />

      {preview && (
        <Reveal watch={preview.summary}>
          <Card title="Confirm before anything is recorded">
            <p className="sentence">{preview.summary}</p>
            {preview.questions.map((q) => <p key={q} className="error">{q}</p>)}
            {preview.balanceImpacts.map((q) => <p key={q}>{q}</p>)}
            {preview.canCommit && <button className="btn" disabled={busy === "confirm"} onClick={() => void confirm()}>Confirm and record</button>}
          </Card>
        </Reveal>
      )}

      <table className="table" style={{ marginTop: 24 }}>
        <thead><tr><th>Date</th><th>Account</th><th>Type</th><th>Description</th><th>Amount</th><th>Actions</th></tr></thead>
        <tbody>
          {rows.map((row) => (
            editingId === row.id ? (
              <tr key={row.id} style={{ backgroundColor: "rgba(255, 255, 255, 0.05)" }}>
                <td>
                  <input
                    type="date"
                    value={editForm.date}
                    onChange={(e) => setEditForm({ ...editForm, date: e.target.value })}
                    style={{ width: "130px", padding: "4px" }}
                  />
                </td>
                <td>
                  <select
                    value={editForm.accountId}
                    onChange={(e) => setEditForm({ ...editForm, accountId: e.target.value })}
                    style={{ padding: "4px" }}
                  >
                    {accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                  </select>
                </td>
                <td>
                  <select
                    value={editForm.type}
                    onChange={(e) => setEditForm({ ...editForm, type: e.target.value })}
                    style={{ padding: "4px" }}
                  >
                    {["Income", "Expense", "Fee", "Savings", "Investment", "Refund", "Withdrawal", "Deposit", "Adjustment", "BusinessExpense", "BusinessRevenue"].map((t) => (
                      <option key={t}>{t}</option>
                    ))}
                  </select>
                </td>
                <td>
                  <input
                    value={editForm.description}
                    onChange={(e) => setEditForm({ ...editForm, description: e.target.value })}
                    style={{ width: "100%", padding: "4px" }}
                  />
                </td>
                <td>
                  <input
                    type="number"
                    step="0.01"
                    value={editForm.amount}
                    onChange={(e) => setEditForm({ ...editForm, amount: e.target.value })}
                    style={{ width: "100px", padding: "4px" }}
                  />
                </td>
                <td>
                  <div style={{ display: "flex", gap: "6px" }}>
                    <button className="btn" disabled={busy === `edit-${row.id}`} onClick={() => void saveEdit(row.id)}>Save</button>
                    <button className="btn ghost" onClick={() => setEditingId(null)}>Cancel</button>
                  </div>
                </td>
              </tr>
            ) : (
              <tr key={row.id}>
                <td>{row.date}</td>
                <td>{row.accountName}</td>
                <td>{row.type}{row.isTransfer ? " · transfer" : ""}{row.isBetting ? " · betting" : ""}</td>
                <td>{row.description}</td>
                <td>{row.amount.formatted}</td>
                <td>
                  <div style={{ display: "flex", gap: "6px" }}>
                    <button className="btn ghost" onClick={() => startEdit(row)}>Edit</button>
                    <button className="btn ghost" disabled={busy === row.id} onClick={() => void voidRow(row.id)}>Void</button>
                  </div>
                </td>
              </tr>
            )
          ))}
        </tbody>
      </table>
    </Shell>
  );
}
