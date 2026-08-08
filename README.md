# CloudSealed Predictive-ML-Core

Scores architecture risk from a declared system inventory.

Given a list of systems (name, type, criticality, public exposure, data
sensitivity, auth method) and optionally some latency/throughput metrics, it
scores each system on three risk dimensions, explains every finding, and rolls
the results into an overall architecture score. It is an HTTP service and a
CLI.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

---

## Why this is not a trained model

The repository originally scaffolded a `Microsoft.ML.FastTree` regressor that
trained on low-level OS telemetry (context switches, GC collections, IOPS
throttling) to predict latency. That data does not exist anywhere in this
service's actual contract: the request only carries a declared system
inventory, not runtime telemetry, and there is no labeled training set of past
assessments to fit a model against.

A supervised model needs labeled examples of "this architecture had an
incident" to learn from. Wrapping heuristics in ML vocabulary without that data
would produce numbers that look statistically grounded but are not. Instead,
`Predictive-ML-Core` scores architecture risk with explicit, weighted rules —
every score traces back to a specific field in the request and the finding
text states the assumption behind it. A real model becomes viable once enough
real assessments accumulate to serve as training data, keeping the same
contract.

## The method

Three dimensions are scored per system, 0–100:

**`singlePointOfFailure`** — base weight by `criticality` (LOW=0, MEDIUM=15,
HIGH=35, CRITICAL=55) plus a modifier by `type` (DATABASE +15: state is more
expensive to replicate; THIRD_PARTY_SERVICE +20: outside your control, no
fallback declared; APPLICATION/API +5). The request schema has no redundancy
field, so the finding text states the assumption explicitly: single instance,
worst case.

**`excessiveCoupling`** — proxy for exposure and dependency, since the request
carries no dependency graph. `publicFacing` without `authMethod` scores
highest (+40); with an `authMethod` declared, less (+15); internal systems get
a small base (+5). `THIRD_PARTY_SERVICE` type adds +25. Fan-out across the
whole request — more than two `THIRD_PARTY_SERVICE` entries — adds an
organization-level coupling bonus, capped, since that pattern is a system-wide
signal, not just a per-system one.

**`scalabilityGap`** — prefers real data: if `historicalMetrics` is present,
`p99LatencyMs` above 1000ms and a p99/avg ratio above 3 (heavy tail under
load) both add weight. Without `historicalMetrics`, it falls back to a weaker,
explicitly conditional signal — DATABASE with `dataSensitivity` declared, or
CRITICAL systems with no load data at all ("unknown risk", not a measurement).

Each rule that crosses its threshold generates the corresponding `finding` and
`recommendation` — severity is derived from the score, not picked by hand.

**`overallArchitectureScore`** is a criticality-weighted average across
systems, not a flat mean. A flat mean lets a single CRITICAL system with a
severe single-point-of-failure dilute into a "fine" score once there are
enough LOW-criticality systems in the same inventory; the weighting keeps that
system's risk from disappearing.

All weights live as named constants in
[`RiskRules.cs`](src/CloudSealed.ML.Engine/Scoring/RiskRules.cs), each with a
one-line rationale.

## Every score is auditable

The response does not just give a number — it gives the rules that produced it.
Each `riskScore` ships with a `scoreBreakdown` of
`{ rule, points, rationale }` entries, and

```
riskScore == min( sum(breakdown.points), 100 )
```

holds exactly (a test enforces it, so the explanation can never drift from the
score). For example:

```jsonc
"singlePointOfFailure": 60,
"scoreBreakdown": {
  "singlePointOfFailure": [
    { "rule": "criticality=CRITICAL", "points": 55, "rationale": "..." },
    { "rule": "type=API",             "points": 5,  "rationale": "..." }
  ]
}
```

Every response also carries `engineVersion` and `method` as provenance. This
traceability is the point of choosing deterministic rules over a black box —
see [METHODOLOGY.md](METHODOLOGY.md) and [ARCHITECTURE.md](ARCHITECTURE.md).

## Use

### HTTP service

```bash
docker run -p 8092:8092 cloudsealed/predictive-ml-core
```

```
GET  /health
POST /v1/predict-architecture
```

```bash
curl -X POST localhost:8092/v1/predict-architecture \
  -H 'Content-Type: application/json' \
  -d '{
        "companyName": "Acme",
        "systems": [
          { "name": "checkout-api", "type": "API", "criticality": "CRITICAL",
            "publicFacing": true, "authMethod": null }
        ]
      }'
```

Set `PREDICTIVE_ML_CORE_API_KEY` to require an `X-Api-Key` header. Payloads
above ~2MB are rejected.

Response shape:

```jsonc
{
  "predictions": [
    {
      "systemName": "checkout-api",
      "riskScores": { "singlePointOfFailure": 60, "excessiveCoupling": 40, "scalabilityGap": 15 },
      "findings": [
        { "title": "Ponto único de falha: checkout-api", "severity": "HIGH",
          "description": "...", "remediation": "..." }
      ],
      "recommendations": [
        { "title": "Implementar redundância", "description": "...", "effort": "MEDIUM" }
      ]
    }
  ],
  "architectureSummary": "...",
  "overallArchitectureScore": 58
}
```

### CLI

```bash
dotnet run --project src/CloudSealed.ML.CLI -- inventory.json
dotnet run --project src/CloudSealed.ML.CLI -- inventory.json --json
```

Runs the same analysis without starting a server, printing either a
human-readable summary or the raw JSON response.

## Development

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/CloudSealed.ML.API   # serves on :8092
```

Tests cover each risk rule in isolation, the criticality-weighted overall
score, and the HTTP endpoint (auth, validation, response shape).

## License

MIT. See [LICENSE](LICENSE).
