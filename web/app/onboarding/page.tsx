"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import type { AuthStatus, OnboardingTemplate } from "@/lib/types";

export default function OnboardingPage() {
  const router = useRouter();
  const [status, setStatus] = useState<AuthStatus | null>(null);
  const [templates, setTemplates] = useState<OnboardingTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [selectedCurrency, setSelectedCurrency] = useState("USD");
  const [selectedTemplate, setSelectedTemplate] = useState("zero_based");
  const [accountName, setAccountName] = useState("Primary Checking");
  const [initialBalance, setInitialBalance] = useState("0");

  useEffect(() => {
    void api.me()
      .then((me) => {
        setStatus(me);
        if (!me.authenticated) {
          router.replace("/login");
          return;
        }
        if (me.onboardingCompleted) {
          router.replace("/");
          return;
        }
        if (me.baseCurrency) {
          setSelectedCurrency(me.baseCurrency);
        }
        return api.onboardingTemplates();
      })
      .then((tmpl) => {
        if (tmpl) setTemplates(tmpl);
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : "Failed to load onboarding options.");
      })
      .finally(() => setLoading(false));
  }, [router]);

  async function handleComplete() {
    setError(null);
    setSubmitting(true);
    try {
      const balanceNum = parseFloat(initialBalance) || 0;
      await api.applyOnboarding({
        templateKey: selectedTemplate,
        baseCurrency: selectedCurrency,
        primaryAccountName: accountName,
        initialBalance: balanceNum
      });
      router.replace("/budget");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to apply configuration.");
      setSubmitting(false);
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
        <p className="lede">Preparing your financial workspace...</p>
      </div>
    );
  }

  const currencies = [
    { code: "USD", symbol: "$", name: "US Dollar" },
    { code: "EUR", symbol: "€", name: "Euro" },
    { code: "GBP", symbol: "£", name: "British Pound" },
    { code: "NGN", symbol: "₦", name: "Nigerian Naira" },
    { code: "USDT", symbol: "₮", name: "Tether USD" }
  ];

  return (
    <div style={{ minHeight: "100vh", padding: "2.5rem 1rem", background: "var(--bg, #07110d)", color: "var(--fg, #e6f0eb)" }}>
      <div style={{ maxWidth: "760px", margin: "0 auto" }} className="stack">
        <header style={{ textAlign: "center", marginBottom: "1.5rem" }}>
          <h1 style={{ fontSize: "2rem", fontWeight: 700, letterSpacing: "-0.03em" }}>
            Initialize Your Financial OS
          </h1>
          <p className="lede" style={{ color: "var(--muted, #8e9e96)", marginTop: "0.5rem" }}>
            Welcome, {status?.displayName ?? "there"}! Let's tailor the ledger to your financial reality.
          </p>
        </header>

        {error && (
          <div style={{ padding: "0.75rem 1rem", background: "rgba(239,68,68,0.15)", border: "1px solid #ef4444", borderRadius: "8px", color: "#fca5a5" }}>
            {error}
          </div>
        )}

        {/* Step 1: Base Currency */}
        <section className="card stack" style={{ padding: "1.5rem" }}>
          <h2 style={{ fontSize: "1.15rem", fontWeight: 600 }}>1. Choose Your Base Currency</h2>
          <p style={{ fontSize: "0.88rem", color: "var(--muted, #8e9e96)", margin: 0 }}>
            All foreign holdings, net worth totals, and spendable amounts will convert to this currency.
          </p>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "0.75rem", marginTop: "0.5rem" }}>
            {currencies.map((c) => {
              const active = selectedCurrency === c.code;
              return (
                <button
                  key={c.code}
                  type="button"
                  onClick={() => setSelectedCurrency(c.code)}
                  style={{
                    padding: "0.85rem",
                    borderRadius: "8px",
                    border: active ? "2px solid var(--accent, #10b981)" : "1px solid rgba(255,255,255,0.08)",
                    background: active ? "rgba(16,185,129,0.12)" : "rgba(255,255,255,0.03)",
                    color: "#fff",
                    textAlign: "center",
                    cursor: "pointer"
                  }}
                >
                  <div style={{ fontSize: "1.25rem", fontWeight: 700 }}>{c.symbol} {c.code}</div>
                  <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)", marginTop: "0.2rem" }}>{c.name}</div>
                </button>
              );
            })}
          </div>
        </section>

        {/* Step 2: Financial Archetype */}
        <section className="card stack" style={{ padding: "1.5rem" }}>
          <h2 style={{ fontSize: "1.15rem", fontWeight: 600 }}>2. Select Your Financial Framework</h2>
          <p style={{ fontSize: "0.88rem", color: "var(--muted, #8e9e96)", margin: 0 }}>
            Choose an envelope budgeting archetype to preload your initial accounts and cash categories.
          </p>
          <div style={{ display: "grid", gap: "0.85rem", marginTop: "0.5rem" }}>
            {templates.map((t) => {
              const active = selectedTemplate === t.key;
              return (
                <div
                  key={t.key}
                  onClick={() => setSelectedTemplate(t.key)}
                  style={{
                    padding: "1rem 1.25rem",
                    borderRadius: "8px",
                    border: active ? "2px solid var(--accent, #10b981)" : "1px solid rgba(255,255,255,0.08)",
                    background: active ? "rgba(16,185,129,0.1)" : "rgba(255,255,255,0.02)",
                    cursor: "pointer",
                    transition: "border-color 0.2s"
                  }}
                >
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <div style={{ fontWeight: 600, fontSize: "1.05rem" }}>{t.name}</div>
                    {active && <span style={{ fontSize: "0.75rem", background: "#10b981", color: "#000", fontWeight: 700, padding: "2px 8px", borderRadius: "10px" }}>SELECTED</span>}
                  </div>
                  <div style={{ fontSize: "0.88rem", color: "#10b981", marginTop: "0.25rem", fontWeight: 500 }}>{t.tagline}</div>
                  <div style={{ fontSize: "0.82rem", color: "var(--muted, #8e9e96)", marginTop: "0.4rem" }}>{t.description}</div>
                  
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "0.4rem", marginTop: "0.6rem" }}>
                    {t.initialEnvelopes.map((env, i) => (
                      <span key={i} style={{ fontSize: "0.75rem", background: "rgba(255,255,255,0.06)", padding: "2px 8px", borderRadius: "4px" }}>
                        {env}
                      </span>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        {/* Step 3: Starting Account & Balance */}
        <section className="card stack" style={{ padding: "1.5rem" }}>
          <h2 style={{ fontSize: "1.15rem", fontWeight: 600 }}>3. Primary Account Setup</h2>
          <p style={{ fontSize: "0.88rem", color: "var(--muted, #8e9e96)", margin: 0 }}>
            Enter your main checking or operating account to get started. You can add more accounts at any time.
          </p>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", marginTop: "0.5rem" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Account Name</label>
              <input
                type="text"
                value={accountName}
                onChange={(e) => setAccountName(e.target.value)}
                placeholder="e.g. Chase Checking, Revolut, Stanbic"
                style={{ width: "100%", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.1)", background: "rgba(0,0,0,0.2)", color: "#fff" }}
              />
            </div>
            <div>
              <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Starting Balance ({selectedCurrency})</label>
              <input
                type="number"
                step="0.01"
                value={initialBalance}
                onChange={(e) => setInitialBalance(e.target.value)}
                placeholder="0.00"
                style={{ width: "100%", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.1)", background: "rgba(0,0,0,0.2)", color: "#fff" }}
              />
            </div>
          </div>
        </section>

        <div style={{ textAlign: "center", marginTop: "1rem" }}>
          <button
            type="button"
            className="btn"
            disabled={submitting}
            onClick={handleComplete}
            style={{ padding: "1rem 2.5rem", fontSize: "1.05rem", fontWeight: 700, borderRadius: "8px", cursor: "pointer" }}
          >
            {submitting ? "Building Your Financial OS..." : "Launch My Financial OS →"}
          </button>
        </div>
      </div>
    </div>
  );
}
