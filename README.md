# CloudSealed Predictive-ML-Core

Scores architecture risk from a declared system inventory.

Given a list of systems (name, type, criticality, public exposure, data
sensitivity, auth method) and optionally some latency/throughput metrics, it
scores each system on three risk dimensions, explains every finding, and rolls
the results into an overall architecture score. It is an HTTP service and a
CLI.

[![CI](https://github.com/cloudsealed/Predictive-ML-Core/actions/workflows/ci.yml/badge.svg)](https://github.com/cloudsealed/Predictive-ML-Core/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/CloudSealed.ML.Core.svg)](https://www.nuget.org/packages/CloudSealed.ML.Core)
[![NuGet downloads](https://img.shields.io/nuget/dt/CloudSealed.ML.Core.svg)](https://www.nuget.org/packages/CloudSealed.ML.Core)
[![Docker pulls](https://img.shields.io/docker/pulls/cloudsealed/predictive-ml-core.svg)](https://hub.docker.com/r/cloudsealed/predictive-ml-core)
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

## Install

```bash
dotnet add package CloudSealed.ML.Core
```

```csharp
using CloudSealed.ML.Engine.Scoring;
using CloudSealed.ML.Engine.Models;

var response = new ArchitectureAnalyzer().Analyze(request); // PredictArchitectureRequest
Console.WriteLine(response.OverallArchitectureScore);
```

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
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json --json
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json --html report.html
```

`--html` writes a self-contained report (inline CSS, no CDN) alongside
whatever other output is requested — open it straight from disk, or attach it
to an email.

Runs the same analysis without starting a server, printing either a
human-readable summary or the raw JSON response. [`examples/inventory.json`](examples/inventory.json)
is a ready-to-run sample with a mix of criticality levels and system types.

## GitHub Action

Run the audit in CI and get the findings as a pull request comment, without
installing anything locally:

```yaml
- uses: cloudsealed/Predictive-ML-Core@main
  with:
    inventory-json: inventory.json
    fail-on-severity: CRITICAL   # optional: fail the check on CRITICAL findings
```

Re-runs on the same PR edit the existing comment instead of piling up new
ones. See [action.yml](action.yml) for all inputs/outputs and
[.github/workflows/example-usage.yml](.github/workflows/example-usage.yml)
for a working example (this repository dogfoods its own action against
[examples/inventory.json](examples/inventory.json) on every push).

## Alerts

Send the result to Slack (or any generic webhook listener) when a finding
reaches a severity threshold, without standing up a dashboard:

```bash
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json --webhook-url "$SLACK_WEBHOOK_URL"
```

A Slack incoming-webhook URL (`hooks.slack.com`) is auto-detected and
rendered as a formatted message; any other URL receives the full JSON
response, so it works as-is with Teams, PagerDuty, or a custom listener.
Nothing is sent unless a finding is HIGH or CRITICAL. The same behaviour is
available in the HTTP API via the optional `webhookUrl` field on
`/v1/predict-architecture`. A failed webhook is logged and never fails the
request.

## How this compares to other architecture risk / catalog tools

Predictive-ML-Core is a scoring engine, not a service catalog or a
portfolio-wide code scanner — it deliberately has no database and no
infrastructure discovery (see [ARCHITECTURE.md](ARCHITECTURE.md): "No I/O,
no web"). It's the right size when you already have (or can quickly declare)
an inventory and want a fast, explainable risk score; it's the wrong tool if
you need a full service catalog with ownership and dependency graphs.

| | Predictive-ML-Core | Backstage | CAST Highlight | AWS Well-Architected Tool |
|---|---|---|---|---|
| Input | Declared JSON inventory | Service catalog + discovery plugins | Binary/source-code scan | Manual web form |
| Scoring | Deterministic rules, rule-by-rule breakdown | N/A (catalog, not scorer) | Proprietary | Structured questionnaire |
| History/trends | None (stateless by design) | Yes (persisted) | Yes | Yes (assessment versions) |
| Deployment | Library, CLI, self-hosted API, GitHub Action, MCP tool | Self-hosted platform | SaaS | AWS-managed |
| Cost | Free, open source (MIT) | Free, open source | Paid | Free (AWS-native) |

## FAQ

**How do I score single-point-of-failure risk for a list of services?**
Declare each system (name, type, criticality, public exposure, auth method)
in a JSON inventory and POST it to `/v1/predict-architecture`, or run the
CLI against the file — see [Install](#install) and [Use](#use).

**Why rules instead of a trained model?**
Because there's no labeled dataset of "this architecture had an incident" to
train on, and a model without that data would just wrap heuristics in ML
vocabulary — see ["Why this is not a trained model"](#why-this-is-not-a-trained-model).

**Can an AI agent call this directly instead of me hitting the API by hand?**
Yes — see [cloudsealed-mcp](https://github.com/cloudsealed/cloudsealed-mcp),
an MCP server that exposes this as a tool for Claude Code, Claude Desktop,
Cursor, and other MCP clients.

**Is this a replacement for Backstage or a CMDB?**
No — it's complementary. Point it at systems you've already cataloged
elsewhere; it doesn't try to be the catalog itself.

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

---

If the score breakdown helped you argue a redundancy or auth fix, a star helps other teams find it. Bug reports and PRs are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).
