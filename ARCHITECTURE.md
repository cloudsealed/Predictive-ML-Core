# Architecture — CloudSealed Predictive-ML-Core

Scores architecture risk from a declared system inventory. HTTP service and CLI
over one scoring core.

```
  PredictArchitectureRequest
  (companyName, systems[], historicalMetrics?)
            │
            ▼
  ┌──────────────────────────────────────────────┐
  │  CloudSealed.ML.Engine                         │
  │                                                │
  │  Scoring/RiskRules.cs   ── named weights &     │
  │        │                   thresholds, each    │
  │        │                   with a rationale    │
  │        ▼                                        │
  │  Scoring/ArchitectureAnalyzer.cs               │
  │        ├─ ScoreSpof / ScoreCoupling /          │
  │        │  ScoreScalabilityGap                   │
  │        │     → (score, RuleContribution[])      │
  │        ├─ findings + recommendations            │
  │        └─ criticality-weighted overall score    │
  └──────────────────────────────────────────────┘
            │
   ┌────────┴─────────┐
   ▼                  ▼
  API (Program.cs)   CLI (Program.cs)
  /v1/predict-        inventory.json
  architecture        [--json]
```

## Projects

| Project | Role |
|---|---|
| `CloudSealed.ML.Engine` | Contract models + the scoring core. No I/O, no web. |
| `CloudSealed.ML.API` | Minimal ASP.NET API: `GET /health`, `POST /v1/predict-architecture`, `X-Api-Key` auth, Swagger UI. |
| `CloudSealed.ML.CLI` | Runs the same analyzer on a local JSON inventory, human or `--json` output. |
| `CloudSealed.ML.Tests` | xUnit: rules in isolation, breakdown fidelity, weighted overall score, HTTP endpoint. |

## Why the boundaries are where they are

**Weights are data, not code.** Every risk weight and threshold is a named
constant in `RiskRules.cs` with a one-line rationale, kept out of the branching
logic in `ArchitectureAnalyzer.cs`. Retuning the model is editing a table, and
the rationale travels with the number.

**Scoring is traceable by construction.** `ScoreSpof`, `ScoreCoupling` and
`ScoreScalabilityGap` do not return a bare integer — they return the score
*and* the list of `RuleContribution`s that produced it. The response therefore
carries a `scoreBreakdown` where `sum(points)` (capped at 100) reconstructs
every `riskScore` exactly; a test enforces that identity so the explanation can
never drift from the number. This is the property a black-box model cannot
offer and is the reason the method is deterministic rules rather than a trained
classifier (see `METHODOLOGY.md`).

**Contract is additive-only.** The response shape mirrors
`framework4d-predictive-ml-client.ts` field-for-field. `scoreBreakdown`,
`engineVersion` and `method` were added as *new* fields; older clients ignore
them, so the contract is extended without a breaking version bump.

**One core, two surfaces.** API and CLI both call the same `Analyze()` — no
second path that could diverge.
