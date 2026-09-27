"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import type { Account, Preview, Transaction } from "@/lib/types";

export default function TransactionsPage() {
  const [rows, setRows] = useState<Transaction[]>([]);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [text, setText] = useState("Lunch 4300 from OPay");
  const [preview, setPreview] = useState<Preview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [assistant, setAssistant] = useState("");
  const [answer, setAnswer] = useState<string | null>(null);
  const [showDetail, setShowDetail] = useState(false);
  const [detail, setDetail] = useState({ date: new Date().toISOString().slice(0, 10), type: "Expense", accountId: "", counterpartyAccountId: "", amount: "", description: "" });

  async function load() {
    setRows(await api.transactions());
    setAccounts(await api.accounts());
  }
  useEffect(() => { void load().catch((err: Error) => setError(err.message)); }, []);

  async function previewQuick(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setPreview(await api.preview({ text }));
  }

  async function confirm() {
    if (!preview?.proposed || !preview.canCommit) return;
    await api.createTx(preview.proposed);
    setPreview(null);
    setText("");
    await load();
  }

  async function submitDetail(event: FormEvent) {
    event.preventDefault();
    const previewed = await api.preview({
      detailed: {
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
      }
    });
    setPreview(previewed);
  }

  async function ask(event: FormEvent) {
    event.preventDefault();
    const result = await api.ask(assistant);
    setAnswer(result.answer + (result.missingFacts.length ? ` Missing: ${result.missingFacts.join("; ")}` : ""));
  }

  return (
    <Shell>
      <h1>Where did my money go?</h1>
      <p className="lede">Quick entry is the everyday path. Detailed entry is for precision. The assistant proposes structured actions and waits for confirmation.</p>
      {error && <p className="error">{error}</p>}

      <Card title="Quick entry">
        <form className="stack" onSubmit={previewQuick}>
          <input value={text} onChange={(e) => setText(e.target.value)} placeholder="Lunch 4300 from OPay" />
          <button className="btn" type="submit">Parse and preview</button>
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
              <button className="btn" type="submit">Preview detailed entry</button>
            </form>
          </Card>
        </Reveal>
      )}

      <Card title="Ask the ledger">
        <form className="stack" onSubmit={ask}>
          <input value={assistant} onChange={(e) => setAssistant(e.target.value)} placeholder="How much has MatchPredictor cost me?" />
          <button className="btn ghost" type="submit">Ask</button>
        </form>
        {answer && <Reveal watch={answer}><p className="sentence">{answer}</p></Reveal>}
      </Card>

      {preview && (
        <Reveal watch={preview.summary}>
          <Card title="Confirm before anything is recorded">
            <p className="sentence">{preview.summary}</p>
            {preview.questions.map((q) => <p key={q} className="error">{q}</p>)}
            {preview.balanceImpacts.map((q) => <p key={q}>{q}</p>)}
            {preview.canCommit && <button className="btn" onClick={confirm}>Confirm and record</button>}
          </Card>
        </Reveal>
      )}

      <table className="table">
        <thead><tr><th>Date</th><th>Account</th><th>Type</th><th>Description</th><th>Amount</th><th></th></tr></thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.id}>
              <td>{row.date}</td>
              <td>{row.accountName}</td>
              <td>{row.type}{row.isTransfer ? " · transfer" : ""}{row.isBetting ? " · betting" : ""}</td>
              <td>{row.description}</td>
              <td>{row.amount.formatted}</td>
              <td><button className="btn ghost" onClick={() => api.voidTx(row.id).then(load)}>Void</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </Shell>
  );
}
