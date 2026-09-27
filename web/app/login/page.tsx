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
        <p className="lede">See where your money is, what it is for, and what you can spend.</p>
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
        <button className="btn" type="submit">{status?.needsSetup ? "Create account" : "Sign in"}</button>
        {status?.needsSetup && <p className="lede">Your plan loads on first setup. Unknown balances stay blank until you enter them.</p>}
      </form>
    </div>
  );
}
