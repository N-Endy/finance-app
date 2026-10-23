"use client";

import { FormEvent, useEffect, useState } from "react";
import { Shell, Card } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";
import type { HouseholdWorkspace } from "@/lib/types";

export default function HouseholdPage() {
  const [workspace, setWorkspace] = useState<HouseholdWorkspace | null>(null);
  const [tokenInput, setTokenInput] = useState("");
  const [copied, setCopied] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  async function load() {
    try {
      const ws = await api.householdWorkspace();
      setWorkspace(ws);
    } catch (err) {
      setError(failMessage(err));
    }
  }

  useEffect(() => {
    void load();
    if (typeof window !== "undefined") {
      const params = new URLSearchParams(window.location.search);
      const urlToken = params.get("token");
      if (urlToken) setTokenInput(urlToken);
    }
  }, []);

  async function handleCreateInvite() {
    setError(null);
    setMessage(null);
    setBusy("invite");
    try {
      const res = await api.inviteHouseholdMember("Partner");
      await load();
      setMessage(`New invite link generated! Share with your partner.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleJoin(event: FormEvent) {
    event.preventDefault();
    if (!tokenInput.trim()) {
      setError("Please enter or paste an invite token.");
      return;
    }
    setError(null);
    setMessage(null);
    setBusy("join");
    try {
      const res = await api.joinHousehold(tokenInput.trim());
      await load();
      setMessage(res.message);
      setTokenInput("");
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  async function handleRemoveMember(userId: string, name: string) {
    if (!confirm(`Are you sure you want to remove ${name} from this household?`)) return;
    setError(null);
    setMessage(null);
    setBusy(`remove-${userId}`);
    try {
      await api.removeHouseholdMember(userId);
      await load();
      setMessage(`Removed ${name} from the household.`);
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(null);
    }
  }

  function copyInviteLink() {
    if (!workspace?.activeInviteToken) return;
    const origin = typeof window !== "undefined" ? window.location.origin : "";
    const link = `${origin}/household?token=${workspace.activeInviteToken}`;
    navigator.clipboard.writeText(link);
    setCopied(true);
    setTimeout(() => setCopied(false), 2500);
  }

  const isOwner = workspace?.myRole === "Owner";

  return (
    <Shell>
      <h1>Household Collaboration & Joint Ledger</h1>
      <p className="lede">
        Full joint transparency across shared accounts, envelope budgets, and real-time member transaction attribution.
      </p>

      {message && <p className="sentence">{message}</p>}
      {error && <p className="error">{error}</p>}

      {workspace && (
        <>
          {/* Workspace Status Banner */}
          <div style={{
            padding: "1.25rem 1.5rem",
            borderRadius: "10px",
            background: "rgba(16,185,129,0.1)",
            border: "1px solid rgba(16,185,129,0.25)",
            marginBottom: "1.25rem"
          }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
              <div>
                <div style={{ fontSize: "0.8rem", textTransform: "uppercase", letterSpacing: "0.05em", color: "#10b981", fontWeight: 600 }}>
                  Active Household Workspace
                </div>
                <div style={{ fontSize: "1.5rem", fontWeight: 700, marginTop: "0.2rem" }}>
                  {workspace.name}
                </div>
                <div style={{ fontSize: "0.85rem", color: "var(--muted, #8e9e96)", marginTop: "0.25rem" }}>
                  Base Currency: <strong>{workspace.baseCurrency}</strong> • Your Role: <strong>{workspace.myRole}</strong>
                </div>
              </div>
              <div style={{ textAlign: "right" }}>
                <span style={{
                  fontSize: "0.85rem",
                  padding: "0.4rem 0.8rem",
                  borderRadius: "20px",
                  background: isOwner ? "rgba(16,185,129,0.2)" : "rgba(59,130,246,0.2)",
                  color: isOwner ? "#10b981" : "#60a5fa",
                  fontWeight: 600
                }}>
                  {workspace.members.length} {workspace.members.length === 1 ? "Active Member" : "Active Members"}
                </span>
              </div>
            </div>
          </div>

          <div className="grid two" style={{ gap: "1.25rem", alignItems: "start" }}>
            {/* Members Roster Card */}
            <Card title="Household Members">
              <p className="sentence" style={{ margin: "0 0 1rem", fontSize: "0.85rem" }}>
                Everyone in this workspace shares full visibility of accounts and budget envelopes.
              </p>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                {workspace.members.map((member) => (
                  <div
                    key={member.userId}
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      padding: "0.75rem 1rem",
                      borderRadius: "6px",
                      background: "rgba(0,0,0,0.2)",
                      border: "1px solid rgba(255,255,255,0.06)"
                    }}
                  >
                    <div>
                      <div style={{ fontWeight: 600, fontSize: "0.95rem" }}>
                        {member.displayName}
                        {member.role === "Owner" && (
                          <span style={{ marginLeft: "0.5rem", fontSize: "0.7rem", color: "#10b981", background: "rgba(16,185,129,0.15)", padding: "0.15rem 0.4rem", borderRadius: "4px" }}>
                            Primary Owner
                          </span>
                        )}
                        {member.role === "Partner" && (
                          <span style={{ marginLeft: "0.5rem", fontSize: "0.7rem", color: "#60a5fa", background: "rgba(59,130,246,0.15)", padding: "0.15rem 0.4rem", borderRadius: "4px" }}>
                            Partner
                          </span>
                        )}
                      </div>
                      <div style={{ fontSize: "0.78rem", color: "var(--muted, #8e9e96)" }}>
                        {member.email} • Joined {new Date(member.joinedAt).toLocaleDateString()}
                      </div>
                    </div>
                    {isOwner && member.role !== "Owner" && (
                      <button
                        type="button"
                        className="btn ghost"
                        disabled={busy === `remove-${member.userId}`}
                        onClick={() => void handleRemoveMember(member.userId, member.displayName)}
                        style={{ fontSize: "0.8rem", color: "#ef4444", padding: "0.3rem 0.6rem" }}
                      >
                        Remove
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </Card>

            {/* Invite Partner Card */}
            <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
              {isOwner && (
                <Card title="Invite Your Partner">
                  <p className="sentence" style={{ margin: "0 0 0.8rem", fontSize: "0.85rem" }}>
                    Share this invite code or link with your partner. Once joined, you both manage the same joint envelope budget.
                  </p>
                  {workspace.activeInviteToken ? (
                    <div className="stack" style={{ gap: "0.6rem" }}>
                      <div style={{
                        padding: "0.65rem 0.85rem",
                        background: "rgba(0,0,0,0.3)",
                        borderRadius: "6px",
                        border: "1px dashed rgba(255,255,255,0.2)",
                        fontSize: "0.85rem",
                        wordBreak: "break-all",
                        fontFamily: "monospace"
                      }}>
                        {workspace.activeInviteToken}
                      </div>
                      <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                        <button
                          type="button"
                          className="btn"
                          onClick={copyInviteLink}
                          style={{ fontWeight: 600 }}
                        >
                          {copied ? "✓ Copied Link!" : "Copy Invite Link"}
                        </button>
                        <button
                          type="button"
                          className="btn ghost"
                          disabled={busy === "invite"}
                          onClick={() => void handleCreateInvite()}
                        >
                          Generate Fresh Code
                        </button>
                      </div>
                    </div>
                  ) : (
                    <button
                      type="button"
                      className="btn"
                      disabled={busy === "invite"}
                      onClick={() => void handleCreateInvite()}
                    >
                      {busy === "invite" ? "Generating..." : "Generate Partner Invite Link"}
                    </button>
                  )}
                </Card>
              )}

              {/* Join Household Card */}
              <Card title="Join Another Household">
                <p className="sentence" style={{ margin: "0 0 0.8rem", fontSize: "0.85rem" }}>
                  Received an invite token from your partner? Enter it here to merge into their household workspace.
                </p>
                <form className="stack" onSubmit={handleJoin} style={{ gap: "0.6rem" }}>
                  <input
                    type="text"
                    value={tokenInput}
                    onChange={(e) => setTokenInput(e.target.value)}
                    placeholder="Paste 32-character invite token"
                    style={{ fontSize: "0.9rem" }}
                  />
                  <button
                    type="submit"
                    className="btn"
                    disabled={busy === "join" || !tokenInput.trim()}
                    style={{ fontWeight: 600 }}
                  >
                    {busy === "join" ? "Joining Household..." : "Join Household"}
                  </button>
                </form>
              </Card>
            </div>
          </div>

          {/* Joint Architecture Explainer */}
          <div style={{ marginTop: "1.5rem" }}>
            <Card title="Principles of Household Financial Collaboration">
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: "1rem", fontSize: "0.85rem", color: "var(--muted, #8e9e96)" }}>
                <div>
                  <div style={{ fontWeight: 600, color: "var(--foreground, #fff)", marginBottom: "0.25rem" }}>
                    100% Joint Transparency
                  </div>
                  Both partners see all checking, savings, envelopes, and bills. Eliminates friction and financial surprises.
                </div>
                <div>
                  <div style={{ fontWeight: 600, color: "var(--foreground, #fff)", marginBottom: "0.25rem" }}>
                    Zero-Remainder Money Pool
                  </div>
                  Every dollar earned by either partner flows into the common unassigned pool before getting assigned to specific envelopes.
                </div>
                <div>
                  <div style={{ fontWeight: 600, color: "var(--foreground, #fff)", marginBottom: "0.25rem" }}>
                    Clear Multi-Member Attribution
                  </div>
                  Transactions and balance revisions record the exact partner who recorded them, maintaining an immutable audit history.
                </div>
              </div>
            </Card>
          </div>
        </>
      )}
    </Shell>
  );
}
