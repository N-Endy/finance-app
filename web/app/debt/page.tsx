"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card, MoneyView } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { CounterpartyLoan, Liability } from "@/lib/types";

export default function DebtPage() {
  const [liabilities, setLiabilities] = useState<Liability[]>([]);
  const [loans, setLoans] = useState<CounterpartyLoan[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const [showAddLiability, setShowAddLiability] = useState(false);
  const [newLiability, setNewLiability] = useState({
    name: "",
    lender: "",
    principal: "",
    balance: "",
    currency: "NGN",
    interestRatePercent: "0",
    monthlyPayment: "",
    dueDate: "",
    notes: ""
  });

  const [showAddLoan, setShowAddLoan] = useState(false);
  const [newLoan, setNewLoan] = useState({
    borrowerName: "",
    amount: "",
    balanceRemaining: "",
    currency: "NGN",
    lentDate: new Date().toISOString().slice(0, 10),
    expectedRepaymentDate: "",
    notes: ""
  });

  const [updatingLiabilityId, setUpdatingLiabilityId] = useState<string | null>(null);
  const [updatedBalance, setUpdatedBalance] = useState("");

  const [updatingLoanId, setUpdatingLoanId] = useState<string | null>(null);
  const [updatedLoanBalance, setUpdatedLoanBalance] = useState("");
  const [updatedLoanStatus, setUpdatedLoanStatus] = useState("Active");

  async function load() {
    try {
      const [liabList, loanList] = await Promise.all([
        api.liabilities().catch(() => []),
        api.counterpartyLoans().catch(() => [])
      ]);
      setLiabilities(liabList);
      setLoans(loanList);
    } catch (err) {
      setError(failMessage(err));
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function handleCreateLiability(e: FormEvent) {
    e.preventDefault();
    if (!newLiability.name.trim() || !newLiability.balance) return;
    setError(null);
    setMessage(null);
    setBusy("create-liability");
    try {
      await api.createLiability({
        name: newLiability.name,
        lender: newLiability.lender || "Direct",
        principal: Number(newLiability.principal || newLiability.balance),
        balance: Number(newLiability.balance),
        currency: newLiability.currency,
        interestRatePercent: Number(newLiability.interestRatePercent || 0),
        monthlyPayment: Number(newLiability.monthlyPayment || 0),
        dueDate: newLiability.dueDate || null,
        notes: newLiability.notes || null
      });
      setShowAddLiability(false);
      setNewLiability({
        name: "",
        lender: "",
        principal: "",
        balance: "",
        currency: "NGN",
        interestRatePercent: "0",
        monthlyPayment: "",
        dueDate: "",
        notes: ""
      });
      setMessage("Liability recorded.");
      await load();
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleUpdateLiability(id: string) {
    if (!updatedBalance) return;
    setError(null);
    setMessage(null);
    setBusy(id);
    try {
      await api.updateLiability(id, { balance: Number(updatedBalance) });
      setUpdatingLiabilityId(null);
      setUpdatedBalance("");
      setMessage("Liability balance updated.");
      await load();
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleCreateLoan(e: FormEvent) {
    e.preventDefault();
    if (!newLoan.borrowerName.trim() || !newLoan.amount) return;
    setError(null);
    setMessage(null);
    setBusy("create-loan");
    try {
      await api.createCounterpartyLoan({
        borrowerName: newLoan.borrowerName,
        amount: Number(newLoan.amount),
        balanceRemaining: Number(newLoan.balanceRemaining || newLoan.amount),
        currency: newLoan.currency,
        lentDate: newLoan.lentDate,
        expectedRepaymentDate: newLoan.expectedRepaymentDate || null,
        notes: newLoan.notes || null
      });
      setShowAddLoan(false);
      setNewLoan({
        borrowerName: "",
        amount: "",
        balanceRemaining: "",
        currency: "NGN",
        lentDate: new Date().toISOString().slice(0, 10),
        expectedRepaymentDate: "",
        notes: ""
      });
      setMessage("Loan to counterparty recorded.");
      await load();
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleUpdateLoan(id: string) {
    if (updatedLoanBalance === "") return;
    setError(null);
    setMessage(null);
    setBusy(id);
    try {
      await api.updateCounterpartyLoan(id, {
        balanceRemaining: Number(updatedLoanBalance),
        status: updatedLoanStatus
      });
      setUpdatingLoanId(null);
      setUpdatedLoanBalance("");
      setMessage("Counterparty loan updated.");
      await load();
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  const totalDebtMinor = liabilities.reduce((sum, l) => sum + (l.balance.minor ?? 0), 0);
  const totalReceivablesMinor = loans.reduce((sum, l) => sum + (l.balanceRemaining.minor ?? 0), 0);

  return (
    <Shell>
      <div className="row" style={{ justifyContent: "space-between", alignItems: "flex-start" }}>
        <div>
          <h1>Liabilities, Debts & Receivables</h1>
          <p className="lede">Complete visibility into obligations you owe and money lent out to counterparties. Never hidden, always accounted.</p>
        </div>
        <div className="row">
          <button className="btn ghost" onClick={() => setShowAddLiability((v) => !v)}>
            {showAddLiability ? "Cancel" : "+ Add Liability"}
          </button>
          <button className="btn ghost" onClick={() => setShowAddLoan((v) => !v)}>
            {showAddLoan ? "Cancel" : "+ Lent Out Money"}
          </button>
        </div>
      </div>

      {message && <p className="sentence" style={{ color: "var(--accent)" }}>{message}</p>}
      {error && <p className="error">{error}</p>}

      <div className="grid two" style={{ marginTop: 16 }}>
        <Card title="Total Confirmed Debt">
          <div className="figure large">
            ₦{(totalDebtMinor / 100).toLocaleString("en-NG", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
          <p className="sentence">Directly subtracted from your Confirmed Net Worth.</p>
        </Card>
        <Card title="Total Receivables (Lent Out)">
          <div className="figure large">
            ₦{(totalReceivablesMinor / 100).toLocaleString("en-NG", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
          <p className="sentence">Treated as expected receivables until repaid; not spendable cash.</p>
        </Card>
      </div>

      {showAddLiability && (
        <Card title="Record New Debt or Liability">
          <form className="stack" onSubmit={handleCreateLiability}>
            <div className="grid two">
              <label>
                Liability Name
                <input
                  value={newLiability.name}
                  onChange={(e) => setNewLiability({ ...newLiability, name: e.target.value })}
                  placeholder="e.g. Car Note, Credit Card, Equipment Lease"
                  required
                />
              </label>
              <label>
                Lender / Creditor
                <input
                  value={newLiability.lender}
                  onChange={(e) => setNewLiability({ ...newLiability, lender: e.target.value })}
                  placeholder="e.g. Access Bank, Vendor, Private lender"
                />
              </label>
            </div>
            <div className="grid three">
              <label>
                Original Principal
                <input
                  type="number"
                  step="any"
                  value={newLiability.principal}
                  onChange={(e) => setNewLiability({ ...newLiability, principal: e.target.value })}
                  placeholder="Amount borrowed"
                />
              </label>
              <label>
                Current Remaining Balance
                <input
                  type="number"
                  step="any"
                  value={newLiability.balance}
                  onChange={(e) => setNewLiability({ ...newLiability, balance: e.target.value })}
                  placeholder="Current balance"
                  required
                />
              </label>
              <label>
                Currency
                <select
                  value={newLiability.currency}
                  onChange={(e) => setNewLiability({ ...newLiability, currency: e.target.value })}
                >
                  <option value="NGN">NGN (₦)</option>
                  <option value="USD">USD ($)</option>
                  <option value="GBP">GBP (£)</option>
                  <option value="EUR">EUR (€)</option>
                  <option value="USDT">USDT (₮)</option>
                </select>
              </label>
            </div>
            <div className="grid three">
              <label>
                Annual Interest Rate (%)
                <input
                  type="number"
                  step="0.01"
                  value={newLiability.interestRatePercent}
                  onChange={(e) => setNewLiability({ ...newLiability, interestRatePercent: e.target.value })}
                  placeholder="e.g. 18.5"
                />
              </label>
              <label>
                Monthly Minimum Payment
                <input
                  type="number"
                  step="any"
                  value={newLiability.monthlyPayment}
                  onChange={(e) => setNewLiability({ ...newLiability, monthlyPayment: e.target.value })}
                  placeholder="e.g. 35000"
                />
              </label>
              <label>
                Target Payoff / Due Date
                <input
                  type="date"
                  value={newLiability.dueDate}
                  onChange={(e) => setNewLiability({ ...newLiability, dueDate: e.target.value })}
                />
              </label>
            </div>
            <label>
              Notes / Payoff Terms
              <input
                value={newLiability.notes}
                onChange={(e) => setNewLiability({ ...newLiability, notes: e.target.value })}
                placeholder="Specific terms, penalty clauses, or payoff notes"
              />
            </label>
            <button className="btn" type="submit" disabled={busy === "create-liability"}>Record Liability</button>
          </form>
        </Card>
      )}

      {showAddLoan && (
        <Card title="Record Money Lent Out to Others">
          <form className="stack" onSubmit={handleCreateLoan}>
            <div className="grid two">
              <label>
                Borrower / Recipient Name
                <input
                  value={newLoan.borrowerName}
                  onChange={(e) => setNewLoan({ ...newLoan, borrowerName: e.target.value })}
                  placeholder="e.g. Emeka, Alex"
                  required
                />
              </label>
              <label>
                Currency
                <select
                  value={newLoan.currency}
                  onChange={(e) => setNewLoan({ ...newLoan, currency: e.target.value })}
                >
                  <option value="NGN">NGN (₦)</option>
                  <option value="USD">USD ($)</option>
                  <option value="GBP">GBP (£)</option>
                  <option value="EUR">EUR (€)</option>
                  <option value="USDT">USDT (₮)</option>
                </select>
              </label>
            </div>
            <div className="grid two">
              <label>
                Amount Lent
                <input
                  type="number"
                  step="any"
                  value={newLoan.amount}
                  onChange={(e) => setNewLoan({ ...newLoan, amount: e.target.value })}
                  placeholder="e.g. 100000"
                  required
                />
              </label>
              <label>
                Remaining Balance Owed
                <input
                  type="number"
                  step="any"
                  value={newLoan.balanceRemaining}
                  onChange={(e) => setNewLoan({ ...newLoan, balanceRemaining: e.target.value })}
                  placeholder="If partial repayment already occurred"
                />
              </label>
            </div>
            <div className="grid two">
              <label>
                Date Lent
                <input
                  type="date"
                  value={newLoan.lentDate}
                  onChange={(e) => setNewLoan({ ...newLoan, lentDate: e.target.value })}
                  required
                />
              </label>
              <label>
                Expected Repayment Date
                <input
                  type="date"
                  value={newLoan.expectedRepaymentDate}
                  onChange={(e) => setNewLoan({ ...newLoan, expectedRepaymentDate: e.target.value })}
                />
              </label>
            </div>
            <label>
              Context / Notes
              <input
                value={newLoan.notes}
                onChange={(e) => setNewLoan({ ...newLoan, notes: e.target.value })}
                placeholder="Reason or agreed timeline"
              />
            </label>
            <button className="btn" type="submit" disabled={busy === "create-loan"}>Record Lent Money</button>
          </form>
        </Card>
      )}

      <h2 style={{ marginTop: 24 }}>Liabilities & Debt Obligations</h2>
      {liabilities.length === 0 ? (
        <Card title="No Outstanding Liabilities">
          <p className="lede">You have no active liabilities or debts recorded. Any debts you enter will be deducted from your Confirmed Net Worth.</p>
        </Card>
      ) : (
        <div className="grid two">
          {liabilities.map((item) => (
            <Card key={item.id} title={`${item.name} (${item.lender})`}>
              <MoneyView money={item.balance} large />
              <p className="sentence">
                Principal: {item.principal.formatted} · Interest: {item.interestRatePercent}% · Monthly: {item.monthlyPayment.formatted}
              </p>
              {item.dueDate && <p className="sentence">Due Date: {item.dueDate}</p>}
              {item.notes && <p className="lede" style={{ marginTop: 6 }}>{item.notes}</p>}

              {updatingLiabilityId === item.id ? (
                <div className="row" style={{ marginTop: 12 }}>
                  <input
                    type="number"
                    step="any"
                    value={updatedBalance}
                    onChange={(e) => setUpdatedBalance(e.target.value)}
                    placeholder="New balance"
                    style={{ flex: 1 }}
                  />
                  <button className="btn" onClick={() => handleUpdateLiability(item.id)} disabled={busy === item.id}>Save</button>
                  <button className="btn ghost" onClick={() => setUpdatingLiabilityId(null)}>Cancel</button>
                </div>
              ) : (
                <div className="row" style={{ marginTop: 12 }}>
                  <button
                    className="btn ghost"
                    onClick={() => {
                      setUpdatingLiabilityId(item.id);
                      setUpdatedBalance(String(item.balance.major ?? ""));
                    }}
                  >
                    Update Balance
                  </button>
                  <button
                    className="btn ghost"
                    onClick={() => {
                      setUpdatedBalance("0");
                      void handleUpdateLiability(item.id);
                    }}
                  >
                    Mark as Paid Off
                  </button>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}

      <h2 style={{ marginTop: 32 }}>Counterparty Loans (Money Lent Out)</h2>
      {loans.length === 0 ? (
        <Card title="No Outstanding Receivables">
          <p className="lede">You have no recorded loans out to friends, family, or colleagues.</p>
        </Card>
      ) : (
        <div className="grid two">
          {loans.map((loan) => (
            <Card key={loan.id} title={`Owed by ${loan.borrowerName}`}>
              <MoneyView money={loan.balanceRemaining} large />
              <p className="sentence">
                Original amount: {loan.amount.formatted} · Lent on: {loan.lentDate}
              </p>
              {loan.expectedRepaymentDate && (
                <p className="sentence">Expected repayment: {loan.expectedRepaymentDate}</p>
              )}
              <span className={`badge ${loan.status.toLowerCase()}`}>{loan.status}</span>
              {loan.notes && <p className="lede" style={{ marginTop: 6 }}>{loan.notes}</p>}

              {updatingLoanId === loan.id ? (
                <div className="stack" style={{ marginTop: 12 }}>
                  <div className="row">
                    <input
                      type="number"
                      step="any"
                      value={updatedLoanBalance}
                      onChange={(e) => setUpdatedLoanBalance(e.target.value)}
                      placeholder="Remaining balance"
                      style={{ flex: 1 }}
                    />
                    <select
                      value={updatedLoanStatus}
                      onChange={(e) => setUpdatedLoanStatus(e.target.value)}
                      style={{ width: "auto" }}
                    >
                      <option value="Active">Active</option>
                      <option value="Repaid">Repaid</option>
                      <option value="Defaulted">Defaulted</option>
                    </select>
                  </div>
                  <div className="row">
                    <button className="btn" onClick={() => handleUpdateLoan(loan.id)} disabled={busy === loan.id}>Save</button>
                    <button className="btn ghost" onClick={() => setUpdatingLoanId(null)}>Cancel</button>
                  </div>
                </div>
              ) : (
                <div className="row" style={{ marginTop: 12 }}>
                  <button
                    className="btn ghost"
                    onClick={() => {
                      setUpdatingLoanId(loan.id);
                      setUpdatedLoanBalance(String(loan.balanceRemaining.major ?? ""));
                      setUpdatedLoanStatus(loan.status);
                    }}
                  >
                    Record Payment
                  </button>
                  <button
                    className="btn ghost"
                    onClick={() => {
                      setUpdatedLoanBalance("0");
                      setUpdatedLoanStatus("Repaid");
                      void handleUpdateLoan(loan.id);
                    }}
                  >
                    Mark as Settled
                  </button>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}
    </Shell>
  );
}
