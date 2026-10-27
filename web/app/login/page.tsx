"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import type { AuthStatus } from "@/lib/types";

export default function LoginPage() {
  const router = useRouter();
  const [status, setStatus] = useState<AuthStatus | null>(null);
  const [mode, setMode] = useState<"login" | "register">("login");
  
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [baseCurrency, setBaseCurrency] = useState("USD");
  const [archetype, setArchetype] = useState("zero_based");
  
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void api.me().then((me: AuthStatus) => {
      setStatus(me);
      if (me.authenticated) {
        if (!me.onboardingCompleted) {
          router.replace("/onboarding");
        } else {
          router.replace("/");
        }
      } else if (me.needsSetup) {
        setMode("register");
      }
    });
  }, [router]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      if (status?.needsSetup) {
        const res = await api.setup({
          displayName: name || "User",
          email,
          password,
          baseCurrency,
          archetype
        }) as AuthStatus | undefined;
        if (!res?.onboardingCompleted) {
          router.replace("/onboarding");
        } else {
          router.replace("/");
        }
      } else if (mode === "register") {
        const res = await api.register({
          displayName: name || "User",
          email,
          password,
          baseCurrency,
          archetype
        });
        if (!res.onboardingCompleted) {
          router.replace("/onboarding");
        } else {
          router.replace("/");
        }
      } else {
        const res = await api.login({ email, password });
        if (!res.onboardingCompleted) {
          router.replace("/onboarding");
        } else {
          router.replace("/");
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Authentication failed.");
    } finally {
      setLoading(false);
    }
  }

  if (status === null) {
    return (
      <div className="auth" style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", padding: "1.5rem" }}>
        <div className="card auth-card stack" style={{ maxWidth: "440px", width: "100%", padding: "2.5rem", textAlign: "center" }}>
          <h1 style={{ fontSize: "1.85rem", fontWeight: 700, letterSpacing: "-0.03em", margin: 0 }}>Finance OS</h1>
          <p className="lede" style={{ fontSize: "0.95rem", color: "var(--muted)", marginTop: "0.4rem" }}>
            Give every dollar, pound, or naira a job.
          </p>
          <div style={{ marginTop: "2rem", opacity: 0.6, fontSize: "0.9rem" }}>Loading financial workspace…</div>
        </div>
      </div>
    );
  }

  const isSetup = status.needsSetup;

  return (
    <div className="auth" style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", padding: "1.5rem" }}>
      <div className="card auth-card stack" style={{ maxWidth: "440px", width: "100%", padding: "2rem" }}>
        <div style={{ textAlign: "center", marginBottom: "0.5rem" }}>
          <h1 style={{ fontSize: "1.85rem", fontWeight: 700, letterSpacing: "-0.03em", margin: 0 }}>Finance OS</h1>
          <p className="lede" style={{ fontSize: "0.95rem", color: "var(--muted)", marginTop: "0.4rem" }}>
            Give every dollar, pound, or naira a job.
          </p>
        </div>

        {!isSetup && (
          <div style={{ display: "flex", background: "rgba(255,255,255,0.04)", borderRadius: "8px", padding: "4px", gap: "4px" }}>
            <button
              type="button"
              onClick={() => { setMode("login"); setError(null); }}
              style={{
                flex: 1,
                padding: "8px 12px",
                border: "none",
                borderRadius: "6px",
                cursor: "pointer",
                fontWeight: 600,
                fontSize: "0.85rem",
                background: mode === "login" ? "var(--bg-elevated, #16241e)" : "transparent",
                color: mode === "login" ? "var(--fg, #fff)" : "var(--muted, #8e9e96)"
              }}
            >
              Sign In
            </button>
            <button
              type="button"
              onClick={() => { setMode("register"); setError(null); }}
              style={{
                flex: 1,
                padding: "8px 12px",
                border: "none",
                borderRadius: "6px",
                cursor: "pointer",
                fontWeight: 600,
                fontSize: "0.85rem",
                background: mode === "register" ? "var(--bg-elevated, #16241e)" : "transparent",
                color: mode === "register" ? "var(--fg, #fff)" : "var(--muted, #8e9e96)"
              }}
            >
              Create Account
            </button>
          </div>
        )}

        <form onSubmit={submit} className="stack" style={{ gap: "1rem" }}>
          {(mode === "register" || isSetup) && (
            <div>
              <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Full Name</label>
              <input
                type="text"
                placeholder="Jane Doe"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                style={{ width: "100%", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.1)", background: "rgba(0,0,0,0.2)", color: "#fff" }}
              />
            </div>
          )}

          <div>
            <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Email Address</label>
            <input
              type="email"
              placeholder="you@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              style={{ width: "100%", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.1)", background: "rgba(0,0,0,0.2)", color: "#fff" }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Password</label>
            <input
              type="password"
              placeholder="At least 10 characters"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              minLength={10}
              required
              style={{ width: "100%", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.1)", background: "rgba(0,0,0,0.2)", color: "#fff" }}
            />
          </div>

          {(mode === "register" || isSetup) && (
            <>
              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Primary Base Currency</label>
                <select
                  value={baseCurrency}
                  onChange={(e) => setBaseCurrency(e.target.value)}
                  style={{ width: "100%", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.1)", background: "rgba(10,20,15,0.9)", color: "#fff" }}
                >
                  <option value="USD">USD ($) - US Dollar</option>
                  <option value="EUR">EUR (€) - Euro</option>
                  <option value="GBP">GBP (£) - British Pound</option>
                  <option value="NGN">NGN (₦) - Nigerian Naira</option>
                  <option value="USDT">USDT (₮) - Tether USD</option>
                </select>
              </div>

              <div>
                <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "0.3rem" }}>Financial Framework</label>
                <select
                  value={archetype}
                  onChange={(e) => setArchetype(e.target.value)}
                  style={{ width: "100%", padding: "0.75rem", borderRadius: "6px", border: "1px solid rgba(255,255,255,0.1)", background: "rgba(10,20,15,0.9)", color: "#fff" }}
                >
                  <option value="zero_based">Zero-Based Budgeting (Give Every Money a Job)</option>
                  <option value="fifty_thirty_twenty">50 / 30 / 20 Framework (Needs, Wants, Savings)</option>
                  <option value="solopreneur">Solopreneur & Freelance (Biz, Tax & Runway)</option>
                  <option value="blank">Blank Slate (Custom Configuration)</option>
                </select>
              </div>
            </>
          )}

          {error && (
            <p className="error" style={{ color: "#ef4444", fontSize: "0.85rem", background: "rgba(239,68,68,0.1)", padding: "0.6rem 0.8rem", borderRadius: "6px", margin: 0 }}>
              {error}
            </p>
          )}

          <button
            className="btn"
            type="submit"
            disabled={loading}
            style={{ width: "100%", padding: "0.85rem", cursor: "pointer", fontWeight: 600, marginTop: "0.5rem" }}
          >
            {loading ? "Please wait..." : isSetup ? "Complete Setup" : mode === "register" ? "Create Universal Account" : "Sign In"}
          </button>

          {!isSetup && (
            <div style={{ textAlign: "center", fontSize: "0.85rem", color: "var(--muted)" }}>
              {mode === "login" ? (
                <span>New to Finance OS? <button type="button" onClick={() => setMode("register")} style={{ background: "none", border: "none", color: "var(--accent, #10b981)", cursor: "pointer", fontWeight: 600, textDecoration: "underline" }}>Create account</button></span>
              ) : (
                <span>Already have an account? <button type="button" onClick={() => setMode("login")} style={{ background: "none", border: "none", color: "var(--accent, #10b981)", cursor: "pointer", fontWeight: 600, textDecoration: "underline" }}>Sign in</button></span>
              )}
            </div>
          )}
        </form>
      </div>
    </div>
  );
}
