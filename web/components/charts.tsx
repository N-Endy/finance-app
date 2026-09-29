"use client";

import { useId, useState } from "react";
import { Card } from "@/components/ui";

export function NetWorthTrajectoryChart({
  currentNetWorth,
  monthlySavings = 250000,
  annualReturnRate = 0.12
}: {
  currentNetWorth: number;
  monthlySavings?: number;
  annualReturnRate?: number;
}) {
  const gradientId = useId();
  const [hoveredMonth, setHoveredMonth] = useState<number | null>(null);

  // Generate 12-month projection based on actuarial compounding
  const points: { month: string; netWorth: number }[] = [];
  const monthNames = ["Now", "+1m", "+2m", "+3m", "+4m", "+5m", "+6m", "+7m", "+8m", "+9m", "+10m", "+11m", "+12m"];
  const monthlyRate = annualReturnRate / 12;

  let balance = Math.max(0, currentNetWorth);
  for (let i = 0; i <= 12; i++) {
    points.push({ month: monthNames[i], netWorth: Math.round(balance) });
    balance = balance * (1 + monthlyRate) + monthlySavings;
  }

  const minVal = points[0].netWorth * 0.95;
  const maxVal = points[points.length - 1].netWorth * 1.05;
  const range = maxVal - minVal || 1;

  const width = 600;
  const height = 200;
  const padding = 30;

  const getX = (idx: number) => padding + (idx / (points.length - 1)) * (width - 2 * padding);
  const getY = (val: number) => height - padding - ((val - minVal) / range) * (height - 2 * padding);

  const pathD = points.reduce((acc, pt, idx) => {
    const x = getX(idx);
    const y = getY(pt.netWorth);
    return idx === 0 ? `M ${x},${y}` : `${acc} L ${x},${y}`;
  }, "");

  const areaD = `${pathD} L ${getX(points.length - 1)},${height - padding} L ${getX(0)},${height - padding} Z`;

  return (
    <Card title="12-Month Net Worth Trajectory">
      <p className="sentence">
        Projected trajectory compounding at {(annualReturnRate * 100).toFixed(0)}% p.a. with ₦{(monthlySavings / 1000).toFixed(0)}k/mo target contributions.
      </p>

      <div style={{ position: "relative", width: "100%", overflowX: "auto" }}>
        <svg viewBox={`0 0 ${width} ${height}`} style={{ width: "100%", height: "auto", display: "block" }}>
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#1ee087" stopOpacity="0.3" />
              <stop offset="100%" stopColor="#1ee087" stopOpacity="0.0" />
            </linearGradient>
          </defs>

          {/* Grid lines */}
          <line x1={padding} y1={padding} x2={width - padding} y2={padding} stroke="rgba(255,255,255,0.06)" strokeDasharray="3 3" />
          <line x1={padding} y1={height / 2} x2={width - padding} y2={height / 2} stroke="rgba(255,255,255,0.06)" strokeDasharray="3 3" />
          <line x1={padding} y1={height - padding} x2={width - padding} y2={height - padding} stroke="rgba(255,255,255,0.12)" />

          {/* Shaded Area & Line */}
          <path d={areaD} fill={`url(#${gradientId})`} />
          <path d={pathD} fill="none" stroke="#1ee087" strokeWidth="2.5" strokeLinecap="round" />

          {/* Data Points */}
          {points.map((pt, idx) => {
            const x = getX(idx);
            const y = getY(pt.netWorth);
            const isHovered = hoveredMonth === idx;
            return (
              <g key={pt.month} onMouseEnter={() => setHoveredMonth(idx)} onMouseLeave={() => setHoveredMonth(null)} style={{ cursor: "pointer" }}>
                <circle cx={x} cy={y} r={isHovered ? 5 : 3} fill={isHovered ? "#ffffff" : "#1ee087"} />
                {idx % 3 === 0 && (
                  <text x={x} y={height - 10} fill="var(--text-muted)" fontSize="10" textAnchor="middle">
                    {pt.month}
                  </text>
                )}
              </g>
            );
          })}
        </svg>

        {hoveredMonth !== null && (
          <div style={{
            position: "absolute",
            top: 8,
            right: 12,
            padding: "4px 10px",
            backgroundColor: "rgba(7, 17, 13, 0.95)",
            border: "1px solid #1ee087",
            borderRadius: 6,
            fontSize: "0.8rem",
            color: "#e8f5ee"
          }}>
            <strong>{points[hoveredMonth].month}:</strong> ₦{points[hoveredMonth].netWorth.toLocaleString()}
          </div>
        )}
      </div>
    </Card>
  );
}

export function CashFlowSankeyChart({
  income,
  expenses,
  savings,
  family
}: {
  income: number;
  expenses: number;
  savings: number;
  family: number;
}) {
  const total = Math.max(income, expenses + savings + family, 1);
  const expPct = Math.round((expenses / total) * 100);
  const savPct = Math.round((savings / total) * 100);
  const famPct = Math.round((family / total) * 100);
  const remPct = Math.max(0, 100 - expPct - savPct - famPct);

  return (
    <Card title="Monthly Cash-Flow Distribution">
      <p className="sentence">
        Visual flow of recorded monthly inflows into living expenses, family commitments, and wealth accumulation. Transfers between own accounts are excluded.
      </p>

      {/* Visual Stacked Flow Bar */}
      <div style={{ margin: "16px 0 12px 0" }}>
        <div style={{
          display: "flex",
          width: "100%",
          height: 28,
          borderRadius: 6,
          overflow: "hidden",
          border: "1px solid var(--border)"
        }}>
          {savPct > 0 && (
            <div style={{ width: `${savPct}%`, backgroundColor: "#1ee087" }} title={`Savings & Investments: ₦${savings.toLocaleString()} (${savPct}%)`} />
          )}
          {expPct > 0 && (
            <div style={{ width: `${expPct}%`, backgroundColor: "#f0b429" }} title={`Personal Expenses: ₦${expenses.toLocaleString()} (${expPct}%)`} />
          )}
          {famPct > 0 && (
            <div style={{ width: `${famPct}%`, backgroundColor: "#38bdf8" }} title={`Family Support: ₦${family.toLocaleString()} (${famPct}%)`} />
          )}
          {remPct > 0 && (
            <div style={{ width: `${remPct}%`, backgroundColor: "rgba(255,255,255,0.08)" }} title={`Unassigned / Remaining: ${remPct}%`} />
          )}
        </div>
      </div>

      {/* Legend & Amounts */}
      <div className="grid four" style={{ fontSize: "0.85rem", gap: 10 }}>
        <div style={{ padding: "8px 10px", background: "rgba(30, 224, 135, 0.08)", borderRadius: 6, border: "1px solid #1ee087" }}>
          <div style={{ color: "#1ee087", fontWeight: 600 }}>Savings & Invest ({savPct}%)</div>
          <div style={{ fontWeight: 700, marginTop: 2 }}>₦{savings.toLocaleString()}</div>
        </div>
        <div style={{ padding: "8px 10px", background: "rgba(240, 180, 41, 0.08)", borderRadius: 6, border: "1px solid #f0b429" }}>
          <div style={{ color: "#f0b429", fontWeight: 600 }}>Living Expenses ({expPct}%)</div>
          <div style={{ fontWeight: 700, marginTop: 2 }}>₦{expenses.toLocaleString()}</div>
        </div>
        <div style={{ padding: "8px 10px", background: "rgba(56, 189, 248, 0.08)", borderRadius: 6, border: "1px solid #38bdf8" }}>
          <div style={{ color: "#38bdf8", fontWeight: 600 }}>Family Support ({famPct}%)</div>
          <div style={{ fontWeight: 700, marginTop: 2 }}>₦{family.toLocaleString()}</div>
        </div>
        <div style={{ padding: "8px 10px", background: "rgba(255,255,255,0.02)", borderRadius: 6, border: "1px solid var(--border)" }}>
          <div style={{ color: "var(--text-muted)", fontWeight: 600 }}>Total Inflow</div>
          <div style={{ fontWeight: 700, marginTop: 2 }}>₦{income.toLocaleString()}</div>
        </div>
      </div>
    </Card>
  );
}

export function FireSimulatorWidget({
  currentInvestments = 0,
  monthlyExpenses = 350000
}: {
  currentInvestments?: number;
  monthlyExpenses?: number;
}) {
  const [annualSpend, setAnnualSpend] = useState(monthlyExpenses * 12);
  const [monthlyContribution, setMonthlyContribution] = useState(200000);
  const [expectedReturn, setExpectedReturn] = useState(14); // 14% nominal
  const [inflationRate, setInflationRate] = useState(10); // 10% inflation
  const [swrPercent, setSwrPercent] = useState(4.0); // 4% Rule

  // Real return rate = (1 + nominal) / (1 + inflation) - 1
  const realRate = ((1 + expectedReturn / 100) / (1 + inflationRate / 100)) - 1;
  const fireTarget = annualSpend / (swrPercent / 100);

  // Compute years to FIRE using actuarial compounding formula:
  // FV = PV*(1+r)^n + PMT * [((1+r)^n - 1) / r]
  let years = 0;
  let accumulated = currentInvestments;
  const annualSavings = monthlyContribution * 12;

  while (accumulated < fireTarget && years < 50) {
    accumulated = accumulated * (1 + realRate) + annualSavings;
    years++;
  }

  return (
    <Card title="30-Year Actuarial FIRE Simulator">
      <p className="sentence">
        Calculates your Financial Independence target in today&apos;s real purchasing power, adjusted for Nigerian inflation and compounding returns.
      </p>

      <div className="grid two" style={{ gap: 16, marginTop: 14 }}>
        <div className="stack" style={{ gap: 12 }}>
          <div>
            <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block" }}>
              Desired Annual Living Expense (Today&apos;s ₦): ₦{annualSpend.toLocaleString()}
            </label>
            <input
              type="range"
              min="1200000"
              max="24000000"
              step="300000"
              value={annualSpend}
              onChange={(e) => setAnnualSpend(Number(e.target.value))}
              style={{ width: "100%" }}
            />
          </div>

          <div>
            <label style={{ fontSize: "0.8rem", color: "var(--text-muted)", display: "block" }}>
              Monthly Investment Savings: ₦{monthlyContribution.toLocaleString()}/mo
            </label>
            <input
              type="range"
              min="50000"
              max="2000000"
              step="25000"
              value={monthlyContribution}
              onChange={(e) => setMonthlyContribution(Number(e.target.value))}
              style={{ width: "100%" }}
            />
          </div>

          <div className="grid two" style={{ gap: 10 }}>
            <div>
              <label style={{ fontSize: "0.75rem", color: "var(--text-muted)" }}>Nominal Return: {expectedReturn}%</label>
              <input type="range" min="8" max="25" value={expectedReturn} onChange={(e) => setExpectedReturn(Number(e.target.value))} style={{ width: "100%" }} />
            </div>
            <div>
              <label style={{ fontSize: "0.75rem", color: "var(--text-muted)" }}>Inflation: {inflationRate}%</label>
              <input type="range" min="5" max="20" value={inflationRate} onChange={(e) => setInflationRate(Number(e.target.value))} style={{ width: "100%" }} />
            </div>
          </div>
        </div>

        <div style={{
          padding: "16px",
          borderRadius: 8,
          backgroundColor: "rgba(30, 224, 135, 0.05)",
          border: "1px solid rgba(30, 224, 135, 0.2)",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center"
        }}>
          <div style={{ fontSize: "0.8rem", textTransform: "uppercase", letterSpacing: "0.08em", color: "var(--text-muted)" }}>
            Your FIRE Target (At {swrPercent}% SWR)
          </div>
          <div style={{ fontSize: "1.8rem", fontWeight: 800, color: "#1ee087", marginTop: 4 }}>
            ₦{Math.round(fireTarget).toLocaleString()}
          </div>
          <p className="sentence" style={{ marginTop: 8 }}>
            At this pace, with a <strong>{(realRate * 100).toFixed(1)}% real return</strong>, you achieve financial freedom in approximately:
          </p>
          <div style={{ fontSize: "1.5rem", fontWeight: 700, color: "#ffffff", marginTop: 2 }}>
            {years >= 50 ? "50+ years" : `${years} years (${new Date().getFullYear() + years})`}
          </div>
        </div>
      </div>
    </Card>
  );
}
