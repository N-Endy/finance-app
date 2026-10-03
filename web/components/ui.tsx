"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useLayoutEffect, useRef, useState } from "react";
import type { Money } from "@/lib/types";
import { api } from "@/lib/api";

const nav = [
  { group: "Position", items: [["/", "Home"], ["/today", "Today"], ["/money", "Map"], ["/transactions", "Transactions"]] as const },
  { group: "Plan", items: [["/goals", "Goals"], ["/budget", "Budget"], ["/accounts", "Accounts"]] as const },
  { group: "Wealth", items: [["/investments", "Investments"], ["/business", "Business"], ["/debt", "Debt & Loans"]] as const },
  { group: "Review", items: [["/reports", "Reports"], ["/settings", "Settings"]] as const }
];

const tabs = [
  ["/", "Home"],
  ["/today", "Today"],
  ["/money", "Map"],
  ["/transactions", "Transactions"]
] as const;

const moreGroups = nav.filter((group) => group.group !== "Position");

function isActive(path: string, href: string) {
  return path === href;
}

export function Shell({ children }: { children: React.ReactNode }) {
  const path = usePathname();
  const [moreOpen, setMoreOpen] = useState(false);
  const moreActive = moreGroups.some((group) => group.items.some(([href]) => path === href));

  return (
    <div className="app-shell">
      <header className="top-bar">
        <div className="brand">Finance OS</div>
      </header>
      <nav className="side">
        <div className="brand">Finance OS</div>
        {nav.map((group) => (
          <div key={group.group}>
            <div className="group">{group.group}</div>
            {group.items.map(([href, label]) => (
              <Link key={href} href={href} className={isActive(path, href) ? "active" : ""}>{label}</Link>
            ))}
          </div>
        ))}
      </nav>
      <main>{children}</main>
      <nav className="bottom-nav" aria-label="Primary">
        {tabs.map(([href, label]) => (
          <Link key={href} href={href} className={isActive(path, href) ? "active" : ""} onClick={() => setMoreOpen(false)}>
            {label}
          </Link>
        ))}
        <button
          type="button"
          className={moreActive || moreOpen ? "active" : ""}
          aria-expanded={moreOpen}
          onClick={() => setMoreOpen((open) => !open)}
        >
          More
        </button>
      </nav>
      {moreOpen && (
        <div className="more-backdrop" onClick={() => setMoreOpen(false)}>
          <div className="more-sheet" role="dialog" aria-label="More screens" onClick={(event) => event.stopPropagation()}>
            <p className="lede">More</p>
            {moreGroups.map((group) => (
              <div key={group.group}>
                <div className="group">{group.group}</div>
                {group.items.map(([href, label]) => (
                  <Link key={href} href={href} className={isActive(path, href) ? "active" : ""} onClick={() => setMoreOpen(false)}>
                    {label}
                  </Link>
                ))}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export function MoneyView({ money, large }: { money: Money; large?: boolean }) {
  const provenance = money.provenance ?? money.provenanceLabel ?? "unknown";
  return (
    <div style={{ minWidth: 0, width: "100%" }}>
      <div className={`figure${large ? " large" : ""}`}>{money.formatted}</div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "4px", alignItems: "center" }}>
        <span className={`badge ${provenance}`}>{provenance.replace("_", " ")}</span>
        {money.asOf && <span className="badge">as of {money.asOf}</span>}
        {money.note && <span className="badge" style={{ backgroundColor: "rgba(30, 224, 135, 0.15)", color: "#1ee087" }}>live ledger</span>}
      </div>
      {money.note && <p className="sentence" style={{ marginTop: 4, fontSize: "0.8rem", color: "var(--text-muted)" }}>{money.note}</p>}
      {money.needed && <p className="sentence" style={{ marginTop: 8 }}>{money.needed}</p>}
    </div>
  );
}

export function Reveal({ children, watch }: { children: React.ReactNode; watch: unknown }) {
  const ref = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => {
    const node = ref.current;
    if (!node) return;
    const go = () => node.scrollIntoView({ behavior: "smooth", block: "start" });
    go();
    const timer = window.setTimeout(go, 280);
    return () => window.clearTimeout(timer);
  }, [watch]);
  return <div ref={ref} className="reveal">{children}</div>;
}

export function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="card">
      <h3>{title}</h3>
      {children}
    </section>
  );
}

export function useLoad<T>(loader: () => Promise<T>) {
  const React = require("react") as typeof import("react");
  const [data, setData] = React.useState<T | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const reload = React.useCallback(async () => {
    try {
      setError(null);
      setData(await loader());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    }
  }, [loader]);
  React.useEffect(() => { void reload(); }, [reload]);
  return { data, error, reload };
}

export function ExplainModal({ metric, onClose }: { metric: string; onClose: () => void }) {
  const React = require("react") as typeof import("react");
  const [data, setData] = React.useState<import("@/lib/types").Explain | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  React.useEffect(() => {
    setData(null);
    setError(null);
    void api.explain(metric).then(setData).catch((err: Error) => setError(err.message));
  }, [metric]);
  return (
    <Reveal watch={`${metric}:${data?.sentence ?? error ?? "loading"}`}>
    <div className="card" style={{ marginTop: 16 }}>
      <div className="row">
        <h3 style={{ margin: 0 }}>How this was calculated</h3>
        <button className="btn ghost" onClick={onClose}>Close</button>
      </div>
      {error && <p className="error">{error}</p>}
      {!data && !error && <p className="lede">Working out the arithmetic…</p>}
      {data && (
        <>
          <p className="sentence">{data.sentence}</p>
          <p className="lede">{data.formula}</p>
          <table className="table">
            <tbody>
              {data.lines.map((line) => (
                <tr key={line.label}>
                  <td>{line.label}</td>
                  <td>{line.amount.formatted}</td>
                  <td>{line.note}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
    </Reveal>
  );
}
