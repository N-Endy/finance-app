"use client";

import { useEffect, useState } from "react";
import { Shell, Card, MoneyView, Reveal } from "@/components/ui";
import { api } from "@/lib/api";
import type { MoneyMapNode } from "@/lib/types";

export default function MoneyPage() {
  const [nodes, setNodes] = useState<MoneyMapNode[]>([]);
  const [selected, setSelected] = useState<MoneyMapNode | null>(null);

  useEffect(() => { void api.moneyMap().then(setNodes); }, []);

  return (
    <Shell>
      <h1>Where is my money?</h1>
      <p className="lede">Salary flows through Stanbic. Secondary income stays on Kuda until the waterfall is applied. Click a bucket for the job, the rule, and what breaks if you spend it.</p>
      <div className="grid three">
        {nodes.map((node) => (
          <button key={node.id} className="card map-node" onClick={() => setSelected(node)}>
            <h3>{node.kind}</h3>
            <div className="figure" style={{ fontSize: 20 }}>{node.label}</div>
            {node.amount && <MoneyView money={node.amount} />}
          </button>
        ))}
      </div>
      {selected && (
        <Reveal watch={selected.id}>
          <Card title={selected.label}>
            <p className="sentence"><strong>What it is for.</strong> {selected.purpose}</p>
            <p className="sentence"><strong>When it can be used.</strong> {selected.whenToUse}</p>
            <p className="sentence"><strong>If you spend it.</strong> {selected.ifSpent}</p>
          </Card>
        </Reveal>
      )}
    </Shell>
  );
}
