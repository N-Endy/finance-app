"use client";

import { FormEvent, useState } from "react";
import { Card, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import { failMessage } from "@/lib/feedback";

export function AskLedger() {
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function ask(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const result = await api.ask(question);
      setAnswer(result.answer + (result.missingFacts.length ? ` Missing: ${result.missingFacts.join("; ")}` : ""));
    } catch (err) {
      setError(failMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card title="Ask the ledger">
      <p className="lede">Answers come from recorded facts. Missing balances stay UNKNOWN.</p>
      <form className="stack" onSubmit={ask}>
        <input value={question} onChange={(e) => setQuestion(e.target.value)} placeholder="How much has MatchPredictor cost me?" />
        <button className="btn ghost" type="submit" disabled={busy}>Ask</button>
      </form>
      {error && <p className="error">{error}</p>}
      {answer && <Reveal watch={answer}><p className="sentence">{answer}</p></Reveal>}
    </Card>
  );
}
