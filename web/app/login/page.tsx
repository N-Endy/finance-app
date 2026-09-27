"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import type { AuthStatus } from "@/lib/types";

export default function LoginPage() {
  const router = useRouter();
  const [status, setStatus] = useState<AuthStatus | null>(null);
  const [email, setEmail] = useState("nnamdi@local");
  const [password, setPassword] = useState("");
  const [name, setName] = useState("Nnamdi");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void api.me().then((me: AuthStatus) => {
      setStatus(me);
      if (me.authenticated) router.replace("/");
    });
  }, [router]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      if (status?.needsSetup) {
        await api.setup({ displayName: name, email, password });
      } else {
        await api.login({ email, password });
      }
      router.replace("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not sign in.");
    }
  }

  return (
    <div className="auth">
      <form className="card auth-card stack" onSubmit={submit}>
        <h1>Finance OS</h1>
        <p className="lede">A calm ledger for one owner. Bank balances are not treated as spendable cash.</p>
        {status?.needsSetup && (
          <div>
            <label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
        )}
        <div>
          <label>Email</label>
          <input value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div>
          <label>Password</label>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} minLength={10} required />
        </div>
        {error && <p className="error">{error}</p>}
        <button className="btn" type="submit">{status?.needsSetup ? "Create owner and seed the plan" : "Sign in"}</button>
        {status?.needsSetup && <p className="lede">First run seeds the September 2026 plan snapshot. Last-known figures stay labelled. Missing balances stay UNKNOWN.</p>}
      </form>
    </div>
  );
}
