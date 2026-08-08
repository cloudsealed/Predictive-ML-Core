# Methodology

How `Predictive-ML-Core` turns a declared system inventory into architecture
risk scores, and why the method is deterministic, explainable rules rather than
a trained model.

## Why not a trained model

The repository originally scaffolded a `Microsoft.ML.FastTree` regressor that
predicted latency from low-level OS telemetry (context switches, GC
collections, IOPS throttling). None of that data exists in this service's
contract: the request carries a *declared inventory* — name, type, criticality,
public exposure, data sensitivity, auth method — and optionally a few latency
numbers. There is also no labelled corpus of past assessments to fit a model
against.

A supervised model needs labelled examples of "this architecture failed" to
learn from. Without them, wrapping heuristics in ML vocabulary produces numbers
that *look* learned but are not — and that is precisely the kind of claim that
collapses under scrutiny. So the engine scores with explicit, weighted rules
where every point is traceable to an input field, and keeps the same HTTP
contract a real model would use, so a model can replace the rules later without
consumers changing.

## The three risk dimensions

Each system is scored 0–100 on three dimensions. Weights live in
`RiskRules.cs`.

**`singlePointOfFailure`** — base weight by `criticality` (LOW 0, MEDIUM 15,
HIGH 35, CRITICAL 55) plus a `type` modifier (DATABASE +15: state is costly to
replicate; THIRD_PARTY_SERVICE +20: outside your control, no declared fallback;
APPLICATION/API +5). The schema has no redundancy field, so the worst case —
single instance — is assumed and stated in the finding.

**`excessiveCoupling`** — a proxy for exposure and dependency, since the request
carries no dependency graph. `publicFacing` without `authMethod` scores +40;
with an `authMethod`, +15; internal systems get a +5 base. `THIRD_PARTY_SERVICE`
adds +25. A fan-out term adds points when the whole inventory declares more than
two third-party dependencies — an organisation-level coupling signal, capped.

**`scalabilityGap`** — prefers real data: when `historicalMetrics` is present,
`p99LatencyMs` above 1000 ms and a p99/avg ratio above 3 (heavy tail) each add
weight. Without metrics it falls back to a weaker, explicitly conditional
signal — a sensitive database, or a critical system with no observed load.

The `overallArchitectureScore` is a **criticality-weighted** average across
systems, not a flat mean, so one CRITICAL system with a severe single point of
failure cannot dilute into a healthy score among many LOW-criticality systems.

## Explainability as a first-class output

The differentiator over a black-box score is that every number is auditable.
Each dimension is computed as a list of `RuleContribution { rule, points,
rationale }`, and the response returns that `scoreBreakdown` alongside the
scores. The identity

```
riskScore == min( sum(breakdown.points), 100 )
```

holds for every dimension of every system, and `ScoreBreakdownTests` enforces
it — the explanation cannot silently diverge from the score. Example:

```
singlePointOfFailure = 60
  ├─ criticality=CRITICAL   +55  "impact of a failure grows with declared criticality"
  └─ type=API                +5  "application/API adds moderate single-instance risk"
```

Every response also carries `engineVersion` and `method`
(`deterministic-rule-scoring`) as provenance, so a consumer persisting findings
can record exactly what produced them.

## Honest limitations

The scores are a structured reading of *declared* inventory, not a measurement
of a running system. They are only as good as the inventory, and where a signal
is inferred rather than observed (everything except the `historicalMetrics`
path) the finding says so. This honesty is deliberate: the value is a
transparent, reproducible risk model, not a prediction dressed up as one.
