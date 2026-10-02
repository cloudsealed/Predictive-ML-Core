# CloudSealed Predictive-ML-Core ⚡ ARCHITECTURE RISK & BLAST-RADIUS ENGINE

> **The ultra-fast, deterministic architecture risk scoring engine and IaC dependency analyzer for high-reliability cloud systems.**

[![CI](https://github.com/cloudsealed/Predictive-ML-Core/actions/workflows/ci.yml/badge.svg)](https://github.com/cloudsealed/Predictive-ML-Core/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/CloudSealed.ML.Core.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/CloudSealed.ML.Core)
[![NuGet downloads](https://img.shields.io/nuget/dt/CloudSealed.ML.Core.svg?style=flat-square&color=green)](https://www.nuget.org/packages/CloudSealed.ML.Core)
[![Docker pulls](https://img.shields.io/docker/pulls/cloudsealed/predictive-ml-core.svg?style=flat-square)](https://hub.docker.com/r/cloudsealed/predictive-ml-core)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![MCP Ready](https://img.shields.io/badge/MCP-Native-orange.svg?style=flat-square)](https://github.com/cloudsealed/cloudsealed-mcp)

---

## 💡 Why CloudSealed Predictive-ML-Core?

In modern cloud-native engineering, microservices, complex cloud architectures, and distributed systems suffer from **hidden points of failure**, **untraceable dependency cascades**, and **silent architectural drift**. 

Traditional tools either force manual compliance questionnaires (AWS Well-Architected Tool) or rely on **black-box AI models that hallucinate risk scores** without auditability.

**`CloudSealed.ML.Core` solves this problem.** It automatically ingests declared infrastructure (Terraform, Kubernetes, Docker Compose, Mermaid diagrams, or JSON inventories), computes dependency blast-radii in $O(V+E)$, and scores architecture risk across 3 core dimensions with **100% deterministic, auditable rules**.

```
                           INPUT INGESTION
 ┌──────────────┐   ┌──────────────┐   ┌────────────────┐   ┌──────────────┐
 │  Terraform   │   │  Kubernetes  │   │ Docker Compose │   │ Mermaid DAG  │
 └──────┬───────┘   └──────┬───────┘   └──────┬─────────┘   └──────┬───────┘
        │                  │                  │                    │
        └──────────────────┴─────────┬────────┴────────────────────┘
                                     ▼
                      ┌─────────────────────────────┐
                      │ CloudSealed Predictive Core │
                      │   - AVX2 SIMD Telemetry     │
                      │   - DAG Blast Radius DFS    │
                      │   - Risk Scoring Rules      │
                      └──────────────┬──────────────┘
                                     ▼
                       AUDITABLE OUTPUT & REMEDIATION
 ┌────────────────┐   ┌────────────────┐   ┌────────────────┐   ┌──────────────┐
 │ GitHub PR Bot  │   │ Interactive    │   │  Slack / Teams │   │ GenAI Code   │
 │   Comment      │   │ HTML Dashboard │   │ Webhook Alerts │   │ Remediation  │
 └────────────────┘   └────────────────┘   └────────────────┘   └──────────────┘
```

---

## 🔥 Key Technical Highlights

### 🚀 1. AVX2 SIMD Zero-Allocation Telemetry Probe (`ZeroAllocRiskProbe`)
Designed for real-time observability pipelines. Evaluates p99 latencies, mean ratios, and performance anomalies using **AVX2 SIMD vectorization over `ReadOnlySpan<float>`**, operating with **exactly 0 bytes allocated per operation**.

### 🕸️ 2. $O(V+E)$ DAG Blast-Radius Engine (`DependencyGraphAnalyzer`)
Zero-allocation Directed Acyclic Graph (DAG) depth-first traversal engine. Instantly calculates cascading risk factors, upstream/downstream dependency depth, and total blast radius when a specific service fails.

### 📄 3. Native Infrastructure-as-Code (IaC) & Diagram Ingestion
No need to build JSON inputs manually. Parse your infrastructure directly from:
* **Terraform (`.tf`)**: Scans cloud resources, databases, and network bounds.
* **Kubernetes Manifests (`.yaml`)**: Extracts deployments, ingress, and pod dependencies.
* **Docker Compose (`docker-compose.yml`)**: Analyzes multi-container topology.
* **Architecture Diagrams (`Mermaid.js`)**: Parses text-based architecture charts directly into scoring inventories.

### 📊 4. 100% Deterministic & Auditable (Zero AI Hallucinations)
Compliant with **SOC 2, ISO 27001, and HIPAA audit requirements**. Every single score comes with a strict mathematical breakdown:
$$\text{riskScore} = \min\left(\sum \text{rulePoints}, 100\right)$$
The score and its explanation can never drift apart.

### 🤖 5. GenAI Automated Code Remediation (`GenAiRemediationEngine`)
When an architectural vulnerability is flagged (e.g., Single Point of Failure or Auth Leakage), the engine automatically crafts exact prompt directives to query **Claude, OpenAI, Gemini, or Ollama** to output ready-to-apply C# and Terraform fix code.

### 🔌 6. AI-Agent First Architecture (MCP Integration)
Native integration with the **Model Context Protocol (MCP)**. LLM Coding Agents (Claude Code, Cursor, Copilot, ChatGPT) can call `cloudsealed_score_architecture_risk` via [cloudsealed-mcp](https://github.com/cloudsealed/cloudsealed-mcp) to evaluate infrastructure safety during code generation.

---

## ⚡ Quickstart

### Option 1: C# / .NET 10 Package

Install via NuGet:

```bash
dotnet add package CloudSealed.ML.Core
```

Analyze your infrastructure in code:

```csharp
using CloudSealed.ML.Engine.Scoring;
using CloudSealed.ML.Engine.Models;

var request = new PredictArchitectureRequest
{
    CompanyName = "Acme Global",
    Systems = new List<SystemInput>
    {
        new SystemInput
        {
            Name = "checkout-api",
            Type = "API",
            Criticality = "CRITICAL",
            PublicFacing = true,
            AuthMethod = null // Single point of failure + Unauthenticated Public API risk
        },
        new SystemInput
        {
            Name = "main-db",
            Type = "DATABASE",
            Criticality = "HIGH",
            PublicFacing = false
        }
    }
};

var analyzer = new ArchitectureAnalyzer();
PredictArchitectureResponse response = analyzer.Analyze(request);

Console.WriteLine($"Overall Architecture Score: {response.OverallArchitectureScore}/100");

foreach (var prediction in response.Predictions)
{
    Console.WriteLine($"System: {prediction.SystemName}");
    Console.WriteLine($"  SPOF Risk: {prediction.RiskScores.SinglePointOfFailure}");
    Console.WriteLine($"  Coupling Risk: {prediction.RiskScores.ExcessiveCoupling}");
    Console.WriteLine($"  Scalability Gap: {prediction.RiskScores.ScalabilityGap}");
}
```

---

### Option 2: Command Line (CLI) & Interactive HTML Reports

Run audit scans directly against JSON inventory files, Terraform specs, or K8s manifests:

```bash
# Human-readable CLI summary
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json

# Raw JSON output for automation
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json --json

# Generate a self-contained interactive HTML Dashboard (Offline, Zero CDN dependencies)
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json --html audit-report.html
```

---

### Option 3: GitHub Action (CI/CD Automated PR Auditing)

Automatically analyze infrastructure changes on every Pull Request and block critical architectural risk:

```yaml
name: Architecture Audit CI

on:
  pull_request:
    branches: [ main ]

jobs:
  audit-architecture:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Run CloudSealed Architecture Risk Audit
        uses: cloudsealed/Predictive-ML-Core@main
        with:
          inventory-json: examples/inventory.json
          fail-on-severity: CRITICAL # Fails workflow if CRITICAL risk is detected
```

*The action automatically writes interactive, formatted PR comments and updates them on subsequent pushes.*

---

### Option 4: REST API Service (Docker)

Run as an enterprise microservice:

```bash
docker run -p 8092:8092 cloudsealed/predictive-ml-core
```

POST `/v1/predict-architecture`:

```bash
curl -X POST http://localhost:8092/v1/predict-architecture \
  -H 'Content-Type: application/json' \
  -d '{
        "companyName": "Acme Enterprise",
        "systems": [
          { 
            "name": "payment-gateway", 
            "type": "API", 
            "criticality": "CRITICAL",
            "publicFacing": true, 
            "authMethod": "OAuth2" 
          }
        ]
      }'
```

---

### Option 5: Slack / Teams / Webhook Alerts

Send real-time alerts to Slack or operational channels whenever a HIGH or CRITICAL architectural flaw is introduced:

```bash
dotnet run --project src/CloudSealed.ML.CLI -- examples/inventory.json --webhook-url "$SLACK_WEBHOOK_URL"
```

---

## 🎯 Scoring Dimensions & Auditability

`CloudSealed.ML.Core` evaluates infrastructure across 3 primary risk dimensions:

| Risk Dimension | Description | Scoring Factors |
|---|---|---|
| **`singlePointOfFailure`** | Evaluates redundancy and single-instance vulnerability. | Base weight by `criticality` (CRITICAL=55, HIGH=35, MED=15), plus service type modifier (DATABASE +15, THIRD_PARTY +20). |
| **`excessiveCoupling`** | Detects unauthorized exposure and dependency proliferation. | Unauthenticated public endpoints (+40), third-party fan-out, and unmanaged API surfaces. |
| **`scalabilityGap`** | Measures load limits and latency degradation. | SIMD tail-latency ratio evaluation (p99 > 1000ms, p99/avg > 3x), unmonitored critical databases. |

### Every Score is Fully Provenanced
```json
{
  "singlePointOfFailure": 60,
  "scoreBreakdown": {
    "singlePointOfFailure": [
      { 
        "rule": "criticality=CRITICAL", 
        "points": 55, 
        "rationale": "CRITICAL system without declared redundancy poses high business continuity risk." 
      },
      { 
        "rule": "type=API", 
        "points": 5, 
        "rationale": "API surface adds base protocol overhead and exposure point." 
      }
    ]
  }
}
```

---

## 📊 Feature Comparison Matrix

| Feature | CloudSealed Predictive-ML-Core | Backstage | CAST Highlight | AWS Well-Architected Tool |
|---|---|---|---|---|
| **Input Mode** | Terraform, K8s, Compose, Mermaid, JSON | Custom Plugins & Yaml | Codebase Scanner | Manual Web Questionnaire |
| **Auditability** | 100% Deterministic Rule Breakdown | N/A (Catalog) | Proprietary SaaS | Manual Form Answers |
| **Latency Engine** | Zero-Allocation AVX2 SIMD | None | None | None |
| **Blast Radius** | $O(V+E)$ DAG Engine | Manual Graph View | Portfolio Scan | Static Documentation |
| **AI Remediation** | GenAI Patch Generation | None | None | None |
| **Execution** | C# Lib, Native CLI, Docker, GitHub Action, MCP | Self-Hosted Portal | SaaS | AWS Console |
| **Cost / License** | 100% Free & Open Source (MIT) | Open Source | Commercial SaaS | Free (AWS Native) |

---

## 🛠️ Custom Rules & Extension

Want to add custom FinOps policies or internal compliance rules? **The engine is built to be hackable.**

1. Fork this repository.
2. Open [`src/CloudSealed.ML.Engine/Scoring/RiskRules.cs`](src/CloudSealed.ML.Engine/Scoring/RiskRules.cs).
3. Add your custom risk weight constants and rationales.
4. Run `dotnet test` to ensure rule-breakdown invariants are preserved!

---

## 👥 Community & Contributing

We welcome pull requests, new IaC parsers, and custom risk rule additions!
* Check out our open issues: [GitHub Issues](https://github.com/cloudsealed/Predictive-ML-Core/issues)
* Read our [CONTRIBUTING.md](CONTRIBUTING.md) and [ARCHITECTURE.md](ARCHITECTURE.md) guides.

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).
