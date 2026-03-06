# Agentic Issue Triage System — Implementation Plan

> **Format:** [GitHub Agentic Workflows](https://github.github.com/gh-aw/) (`.md` files compiled via `gh aw compile`)

## 1. Executive Summary

This plan describes three manually-triggerable **GitHub Agentic Workflows** that run on your **fork** of `dotnet/machinelearning`. Each workflow is a Markdown file (`.md`) with YAML frontmatter that compiles to a `.lock.yml` GitHub Actions workflow via `gh aw compile`. An AI agent executes the natural-language instructions in the Markdown body at runtime.

| Workflow | File | Trigger | Purpose |
|---|---|---|---|
| **Single Issue Triage** | `triage-single-issue.md` | `workflow_dispatch` (issue number) | Triage + optionally reproduce a single upstream issue |
| **Triage Stats Dashboard** | `triage-stats.md` | `workflow_dispatch` | Aggregate triage data into a pinned dashboard issue |
| **Bulk Triage** | `bulk-triage.md` | `workflow_dispatch` (N issues / N days) | Fan out `triage-single-issue` workers for a batch of upstream issues |

All three workflows are **manual-only** (`workflow_dispatch`). The upstream repo (`dotnet/machinelearning`) is **public**, so no PAT is needed for reading its issues, comments, or labels — the default `github` toolset handles this.

### How gh-aw Works

1. You write a `.md` file in `.github/workflows/` with YAML frontmatter (triggers, tools, safe-outputs) and a Markdown body (natural-language agent instructions).
2. Run `gh aw compile` to generate a `.lock.yml` file (the actual GitHub Actions workflow).
3. Commit both the `.md` and `.lock.yml` files.
4. When triggered, the compiled workflow spins up an AI agent that executes the Markdown instructions using the declared tools, then performs declared safe-outputs (create issues, add comments, dispatch workflows, etc.) based on the agent's structured output.

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        YOUR FORK                                     │
│                                                                      │
│  .github/workflows/                                                  │
│  ┌──────────────────┐   dispatch    ┌───────────────────────────┐    │
│  │ bulk-triage.md    │─────────────▶│ triage-single-issue.md     │    │
│  │ (orchestrator)    │  (×N issues) │ (worker)                   │    │
│  └──────────────────┘              └────────────┬────────────────┘    │
│                                                  │ safe-outputs       │
│                                                  ▼                    │
│                                         ┌──────────────────┐         │
│                                         │  Fork Issue #N    │         │
│                                         │  [triage] ...     │         │
│                                         └──────────────────┘         │
│  ┌──────────────────┐                                                │
│  │ triage-stats.md   │──── reads fork issues ──▶ updates dashboard   │
│  │ (dashboard)       │                                               │
│  └──────────────────┘                                                │
│                                                                      │
│  .github/aw/shared/                                                  │
│  ┌──────────────────────────┐                                        │
│  │ triage-rules.lock.md     │  ← compiled shared knowledge           │
│  │ (classification rules,   │     imported by single-issue +          │
│  │  schemas, ML.NET areas)  │     bulk-triage workflows               │
│  └──────────────────────────┘                                        │
│                                                                      │
│   Agent reads from upstream via `github` toolset (public repo):      │
│     • dotnet/machinelearning issues, comments, labels                │
└─────────────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

| Decision | Rationale |
|---|---|
| **gh-aw `.md` workflows** | Markdown + frontmatter → compiled to `.lock.yml`. Agent instructions in natural language. Safe-outputs enforce separation of concerns (agent runs read-only; separate jobs perform writes). |
| **No PAT for upstream reads** | `dotnet/machinelearning` is public. The `github` toolset can read its issues/PRs/labels with the default `GITHUB_TOKEN`. |
| **Fork issues as tracking records** | Each investigated upstream issue gets a mirror issue in your fork. Title: `[triage] upstream#NNNN: <original title>`. Created via `create-issue` safe-output. |
| **Dedup via issue search** | Before triaging, the agent searches fork issues for `[triage] upstream#NNNN` markers using the `github` toolset. |
| **DispatchOps pattern** | `bulk-triage.md` (orchestrator) dispatches `triage-single-issue.md` (worker) via `dispatch-workflow` safe-output — the same pattern used in the devops-health-check reference. |
| **Shared knowledge file** | Classification rules, JSON schemas, and ML.NET area mappings live in `.github/aw/shared/triage-rules.lock.md`, imported by workflows that need them. |
| **`cache-memory` for state** | Dashboard workflow uses `cache-memory` to persist triage statistics across runs and detect deltas. |
| **Safe reproduction in sandbox** | The gh-aw agent runs in a sandboxed environment. Repro code runs inside the sandbox using `bash` tool commands. |

---

## 3. Prerequisites

### 3.1 Software

| Requirement | Purpose |
|---|---|
| `gh` CLI with `gh-aw` extension | Compile `.md` → `.lock.yml` (`gh aw compile`) |
| GitHub Actions enabled on fork | Run the compiled workflows |
| Copilot subscription (Pro+ or Enterprise) | gh-aw uses Copilot as the AI engine |

### 3.2 Secrets

Since the upstream repo is **public**, no special PAT is needed for reading issues. The workflows use only:

| Secret | Purpose | When needed |
|---|---|---|
| *(none required for basic operation)* | `GITHUB_TOKEN` auto-provided by Actions | Always |

> **Note:** If you later want cross-repo write operations (e.g., commenting on upstream issues), you'd add a PAT as `github-token:` in the relevant safe-output. For this plan, all writes happen on the fork only.

### 3.3 Copilot Plan Requirement

Gh-aw uses the Copilot engine by default. A **Copilot Pro+** or **Copilot Enterprise** subscription is required. Each workflow run consumes premium requests.

---

## 4. File Structure

```
.github/
├── aw/
│   ├── actions-lock.json                    # Pinned action SHAs (auto-managed by gh aw compile)
│   └── shared/
│       └── triage-rules.lock.md             # Compiled shared knowledge: classification rules,
│                                            #   JSON schemas, ML.NET area mappings
├── workflows/
│   ├── copilot-setup-steps.yml              # (existing) Environment setup
│   ├── triage-single-issue.md               # Workflow 1: Single issue triage + repro (worker)
│   ├── triage-single-issue.lock.yml         # Compiled (auto-generated, DO NOT EDIT)
│   ├── triage-stats.md                      # Workflow 2: Dashboard aggregation
│   ├── triage-stats.lock.yml                # Compiled (auto-generated, DO NOT EDIT)
│   ├── bulk-triage.md                       # Workflow 3: Batch orchestrator
│   ├── bulk-triage.lock.yml                 # Compiled (auto-generated, DO NOT EDIT)
│   ├── locker.yml                           # (existing)
│   └── backport.yml                         # (existing)
├── scripts/
│   └── triage/                              # (empty, reserved for future helper scripts)
└── docs/
    └── agentic-triage-plan.md               # This document
```

---

## 5. Workflow 1: `triage-single-issue.md` — Single Issue Triage & Reproduction

This is the **worker** workflow. It triages one upstream issue and creates/updates a fork tracking issue with findings. It can be triggered manually or dispatched by `bulk-triage.md`.

### 5.1 Complete Frontmatter

```yaml
---
name: "Triage Single Issue"
description: >
  Worker workflow that triages a single upstream dotnet/machinelearning issue.
  Classifies it into one of 6 categories, optionally reproduces bug reports,
  and creates a tracking issue in this fork with findings.

on:
  workflow_dispatch:
    inputs:
      upstream_issue:
        description: "Upstream issue number (e.g. 7100)"
        required: true
        type: string
      upstream_repo:
        description: "Upstream repo (owner/name)"
        required: false
        default: "dotnet/machinelearning"
        type: string
      skip_repro:
        description: "Skip reproduction step"
        required: false
        type: boolean
        default: false

permissions:
  contents: read
  issues: read

tools:
  github:
    toolsets: [repos, issues, pull_requests]
  cache-memory:
  bash: ["cat", "grep", "head", "tail", "find", "ls", "wc", "jq", "date",
         "sort", "uniq", "dotnet", "mkdir", "cd", "echo", "cp"]
  edit:

imports:
  - ../aw/shared/triage-rules.lock.md

safe-outputs:
  create-issue:
    title-prefix: "[triage] "
    labels: [triage, ai-investigation]
    max: 1
    expires: false
  add-comment:
    target: "*"
    max: 3
  add-labels:
    allowed: [bug, enhancement, duplicate, discussion, wrong-usage, needs-info,
              reproduced, repro-failed, fix-suggested]
    max: 5
    target: "*"

network:
  allowed:
    - defaults
    - dotnet
---
```

### 5.2 Markdown Body (Agent Instructions) — Summary

The Markdown body below the frontmatter contains natural-language instructions for the AI agent. Here is the planned structure:

````markdown
# Triage Single Issue — Worker Agent

You are an expert ML.NET issue triager. Your job is to analyze upstream issue
#${{ github.event.inputs.upstream_issue }} from ${{ github.event.inputs.upstream_repo }},
classify it, optionally reproduce it, and create a tracking issue in this repository.

## Step 1: Deduplication Check

Search THIS repository's issues for an existing tracking issue:
- Search for issues with title containing "[triage] upstream#${{ github.event.inputs.upstream_issue }}"
- If found and the body contains "Status: COMPLETE", call noop with message
  "Already triaged — fork issue #<number> exists with status COMPLETE"
- If found with "Status: IN_PROGRESS", call noop with message
  "Triage already in progress — fork issue #<number>"
- If not found, proceed to Step 2

## Step 2: Read Upstream Issue

Use the github tools to fetch from the PUBLIC upstream repository
${{ github.event.inputs.upstream_repo }}:
1. Get issue #${{ github.event.inputs.upstream_issue }} (title, body, labels, state)
2. Get all comments on the issue
3. Note the issue author, creation date, and any referenced versions

## Step 3: Classification

Classify the issue into EXACTLY ONE category:
[... detailed classification rules imported from triage-rules.lock.md ...]

## Step 4: Duplicate Search

If the issue might be a duplicate:
- Search upstream issues for similar keywords/error messages
- If a probable duplicate is found, set classification to "duplicate"
  and record the original issue number

## Step 5: Reproduction (Bug Reports Only)

If classification is "bug-report" AND skip_repro is not true:
1. Create a temp directory and a standalone .NET console project
2. Add relevant NuGet packages (Microsoft.ML, etc.)
3. Write reproduction code based on the issue
4. Build with `dotnet build` and capture output
5. If it builds, run with `dotnet run` and capture output
6. Analyze results and determine root cause
7. Examine the ML.NET source code in this repo for the likely fix location

SAFETY: Work only in /tmp/repro-${{ github.event.inputs.upstream_issue }}/

## Step 6: Create Tracking Issue

Create a fork issue with your findings using create_issue:
- Title: "[triage] upstream#<N>: <original issue title>"
- Body: Markdown report with all findings (see output format below)

## Step 7: Add Labels

Based on classification, add the appropriate labels to the created issue.

## Output Format

[... structured output templates for create_issue, add_comment, add_labels ...]

## If No Action Needed

If you determined in Step 1 that this issue was already triaged, you MUST call
the noop tool: {"noop": {"message": "No action needed: [reason]"}}
````

### 5.3 Classification Categories

| Category | Label | Description |
|---|---|---|
| `unclear` | `needs-info` | Issue is unclear or missing critical information |
| `duplicate` | `duplicate` | Duplicate of another existing issue — agent identifies the original |
| `discussion` | `discussion` | Not a bug report; a question or discussion |
| `wrong-usage` | `wrong-usage` | User configuration or usage error |
| `feature-request` | `enhancement` | Feature request, not a bug |
| `bug-report` | `bug` | Confirmed or likely bug based on the description |

### 5.4 Fork Issue Body Format

The agent's `create_issue` output will produce a body like:

````markdown
# [triage] upstream#7100: AutoML throws NullReferenceException

**Upstream:** https://github.com/dotnet/machinelearning/issues/7100
**Status:** COMPLETE
**Classification:** bug-report
**Confidence:** 0.85
**Reproduced:** ✅ Yes
**Area:** AutoML
**Investigated at:** (workflow run link auto-appended by gh-aw footer)

---

## Triage Summary

**Category:** Bug Report
**Reasoning:** The issue describes a NullReferenceException when using
AutoML's BinaryClassification experiment with a specific dataset shape...

**Summary:** User reports that AutoML pipeline crashes with NRE when
the training data contains nullable float columns...

**Suggested Labels:** bug, area-AutoML

## Reproduction Results

**Reproduced:** Yes
**Platform:** Linux (ubuntu-latest)
**Steps:**
1. Created console project with Microsoft.ML 3.0.1 and Microsoft.ML.AutoML 0.21.1
2. Generated test data with nullable float columns
3. Ran BinaryClassification experiment
4. Observed NullReferenceException at line 342 of ExperimentRunner.cs

**Error Output:**
```
Unhandled exception: System.NullReferenceException: Object reference not set...
```

**Root Cause Analysis:**
The `ExperimentRunner.GetColumnPurpose()` method does not check for nullable
column types before accessing `.RawType.RawType`...

## Suggested Fix

**Files:** `src/Microsoft.ML.AutoML/ExperimentRunner.cs`
**Description:** Add null check before accessing `RawType` property on line 342.
Guard with `column.Type is NullableDataViewType` check.
**Complexity:** Low
````

---

## 6. Workflow 2: `triage-stats.md` — Dashboard & Statistics

### 6.1 Complete Frontmatter

```yaml
---
name: "Triage Stats Dashboard"
description: >
  Aggregates all triage tracking issues in this fork and generates/updates
  a pinned dashboard issue with statistics and action items.

on:
  workflow_dispatch:

permissions:
  contents: read
  issues: read

tools:
  github:
    toolsets: [repos, issues]
  cache-memory:
  bash: ["cat", "grep", "head", "tail", "wc", "jq", "date", "sort", "uniq", "echo"]

safe-outputs:
  create-issue:
    title-prefix: "[triage-dashboard] "
    labels: [dashboard, automation]
    max: 1
    close-older-issues: true
    expires: false
  update-issue:
    target: "*"
    title-prefix: "[triage-dashboard] "
    body:
    max: 1
  add-comment:
    target: "*"
    max: 1
---
```

### 6.2 Markdown Body (Agent Instructions) — Summary

````markdown
# Triage Stats Dashboard Agent

You are a data aggregation agent. Your job is to collect all triage tracking
issues in this repository and produce a comprehensive dashboard.

## Step 1: Find Existing Dashboard

Search this repo for an open issue with title starting with "[triage-dashboard]".
- If found, you will UPDATE it (Step 4a)
- If not found, you will CREATE a new one (Step 4b)

## Step 2: Collect Triage Data

List all issues in this repository labeled "triage":
- For each issue, extract from the body:
  - Upstream issue number and link
  - Classification (bug-report, feature-request, duplicate, etc.)
  - Whether reproduction was attempted and succeeded
  - Whether a fix was suggested
  - Status (COMPLETE, IN_PROGRESS)
  - Area component

## Step 3: Compute Statistics

Calculate:
- Total triaged issues
- Breakdown by classification
- Reproduction success rate (for bug reports)
- Number with suggested fixes
- Pending (IN_PROGRESS) count
- Area distribution

Compare against previous run stats from cache-memory to compute deltas.
Store current stats in cache-memory for next run.

## Step 4a: Update Existing Dashboard

If a dashboard issue exists, use update_issue to REPLACE its body with the
new dashboard content.

## Step 4b: Create New Dashboard

If no dashboard issue exists, use create_issue to create one with the
dashboard content.

## Step 5: Add Comment with Delta

Post a comment on the dashboard issue summarizing what changed since last run.

## Dashboard Template

[... Markdown template with statistics table, recent investigations table,
     action items (ready-for-fix, needs-upstream-response, potential-PRs) ...]

## If No Triage Issues Found

Call noop: {"noop": {"message": "No triage issues found in this repository"}}
````

### 6.3 Dashboard Content Structure

The dashboard issue will contain:

```markdown
# 🔍 ML.NET Issue Triage Dashboard

> Last updated: <timestamp>

## Summary Statistics
| Metric | Count | Δ |
|---|---|---|
| Total Investigated | 42 | +3 |
| Bug Reports | 18 | +2 |
| Feature Requests | 10 | — |
| Duplicates | 7 | +1 |
| Questions/Discussion | 4 | — |
| Wrong Usage | 2 | — |
| Unclear | 1 | — |
| **Reproduced** | **12** | **+2** |
| Fix Suggested | 8 | +1 |

## By Area
| Area | Bugs | Features | Other |
|---|---|---|---|
| AutoML | 5 | 3 | 2 |
| Core | 4 | 2 | 1 |
| FastTree | 3 | 1 | 0 |
| ...

## Recent Investigations
| Upstream | Classification | Reproduced? | Status | Fork Issue |
|---|---|---|---|---|
| [#7100](https://github.com/dotnet/machinelearning/issues/7100) | 🐛 bug | ✅ | Complete | #15 |
| [#7098](https://github.com/dotnet/machinelearning/issues/7098) | 🔁 duplicate | — | Complete | #14 |

## Action Items
- 🐛 **Ready for fix** (reproduced + fix suggested): #15, #12, #8
- ❓ **Needs upstream info**: #13
- 📋 **Low-complexity fixes**: #15, #8
```

---

## 7. Workflow 3: `bulk-triage.md` — Batch Triage Orchestrator

### 7.1 Complete Frontmatter

```yaml
---
name: "Bulk Triage"
description: >
  Orchestrator that fetches a batch of upstream issues from
  dotnet/machinelearning, deduplicates against existing fork investigations,
  and dispatches triage-single-issue workers for each new issue.

on:
  workflow_dispatch:
    inputs:
      mode:
        description: "Selection mode: last-n-issues or last-n-days"
        required: true
        type: choice
        options: [last-n-issues, last-n-days]
      count:
        description: "Number of issues (for last-n-issues) or days (for last-n-days)"
        required: true
        default: "10"
        type: string
      upstream_repo:
        description: "Upstream repo"
        required: false
        default: "dotnet/machinelearning"
        type: string
      skip_repro:
        description: "Skip reproduction for all dispatched workers"
        required: false
        type: boolean
        default: false
      labels_filter:
        description: "Only triage issues with these labels (comma-separated, optional)"
        required: false
        type: string
      exclude_labels:
        description: "Exclude issues with these labels (comma-separated, optional)"
        required: false
        default: "enhancement"
        type: string

permissions:
  contents: read
  issues: read
  actions: read

tools:
  github:
    toolsets: [repos, issues, actions]
  cache-memory:
  bash: ["cat", "grep", "head", "tail", "wc", "jq", "date", "sort", "uniq", "echo"]

imports:
  - ../aw/shared/triage-rules.lock.md

safe-outputs:
  dispatch-workflow:
    workflows:
      - triage-single-issue
    max: 20
  create-issue:
    title-prefix: "[bulk-triage-run] "
    labels: [automation, bulk-triage]
    max: 1
    close-older-issues: true
    expires: 7d

network:
  allowed:
    - defaults
---
```

### 7.2 Markdown Body (Agent Instructions) — Summary

````markdown
# Bulk Triage Orchestrator

You are a triage orchestration agent. Your job is to collect upstream issues,
deduplicate against existing investigations, and dispatch worker workflows.

## Step 1: Fetch Upstream Issues

Using the github tools, fetch open issues from ${{ github.event.inputs.upstream_repo }}:

- **Mode "last-n-issues"**: Get the N most recently created open issues
- **Mode "last-n-days"**: Get open issues created in the last N days

Apply filters:
- If labels_filter is set, only include issues with those labels
- If exclude_labels is set, exclude issues with those labels

## Step 2: Deduplicate

For each collected upstream issue:
- Search THIS repository's issues for "[triage] upstream#<number>"
- If a tracking issue exists with "Status: COMPLETE", skip it
- If a tracking issue exists with "Status: IN_PROGRESS", skip it

Build a list of issues that need triaging.

## Step 3: Dispatch Workers

For each issue in the deduped list, dispatch triage-single-issue:
```
dispatch_workflow({
  "workflow": "triage-single-issue",
  "inputs": {
    "upstream_issue": "<number>",
    "upstream_repo": "${{ github.event.inputs.upstream_repo }}",
    "skip_repro": "${{ github.event.inputs.skip_repro }}"
  }
})
```

Maximum: 20 dispatches per run (enforced by safe-outputs max).
Note: gh-aw enforces a 5-second delay between dispatches for rate limiting.

## Step 4: Create Run Summary Issue

Create a summary issue documenting this bulk run:
- How many upstream issues were fetched
- How many were already triaged (skipped)
- How many workers were dispatched
- List of dispatched issue numbers

## If No Issues to Triage

If all fetched issues are already triaged, call noop:
{"noop": {"message": "No new issues to triage — all N fetched issues already have tracking issues"}}
````

### 7.3 DispatchOps Pattern

This follows the same orchestrator → worker pattern as the devops-health-check reference:

1. **Orchestrator** (`bulk-triage.md`) runs first, collects data, makes decisions
2. **Workers** (`triage-single-issue.md`) are dispatched via `dispatch-workflow` safe-output
3. Each worker runs independently as its own workflow run
4. Workers have `workflow_dispatch` with `inputs:` matching what the orchestrator provides
5. The `dispatch-workflow` safe-output validates at compile time that `triage-single-issue` exists and accepts `workflow_dispatch`
6. `dispatch-workflow` is same-repo only — both workflows must live in the same fork

---

## 8. Shared Knowledge File: `triage-rules.lock.md`

Located at `.github/aw/shared/triage-rules.lock.md`, this file contains classification rules, output schemas, and ML.NET area mappings. It is imported by `triage-single-issue.md` and `bulk-triage.md` via the `imports:` frontmatter.

### 8.1 Planned Content

```markdown
# ML.NET Triage Rules

## Classification Categories

| Category | Label | When to use |
|---|---|---|
| unclear | needs-info | Issue body is vague, missing repro steps, no clear description of expected vs actual behavior |
| duplicate | duplicate | Substantially the same problem as another open or recently-closed issue. You MUST identify the original issue number. |
| discussion | discussion | Not reporting a defect — asking a question, seeking guidance, or starting a discussion |
| wrong-usage | wrong-usage | The described behavior is expected based on the API contract. User is misusing the API or has a configuration error. |
| feature-request | enhancement | Requesting new functionality, a new API, or a capability that doesn't exist yet |
| bug-report | bug | Describes unexpected behavior that appears to be a defect in the library |

## Classification Priority

If an issue could fit multiple categories, use this priority:
1. duplicate (if a clear original exists)
2. unclear (if you genuinely can't understand the issue)
3. wrong-usage (if the behavior is actually correct/expected)
4. discussion (if it's a question with no defect claim)
5. feature-request (if it's asking for something new)
6. bug-report (default for anything describing broken behavior)

## ML.NET Area Components

Map issues to these areas based on namespaces, APIs, and NuGet packages mentioned:

| Area | Packages / Namespaces | Src Directories |
|---|---|---|
| Core | Microsoft.ML | src/Microsoft.ML/ |
| AutoML | Microsoft.ML.AutoML | src/Microsoft.ML.AutoML/ |
| FastTree | Microsoft.ML.FastTree | src/Microsoft.ML.FastTree/ |
| LightGBM | Microsoft.ML.LightGbm | src/Microsoft.ML.LightGbm/ |
| ImageAnalytics | Microsoft.ML.ImageAnalytics | src/Microsoft.ML.ImageAnalytics/ |
| Data.Analysis | Microsoft.Data.Analysis (DataFrame) | src/Microsoft.Data.Analysis/ |
| CpuMath | Microsoft.ML.CpuMath | src/Microsoft.ML.CpuMath/ |
| GenAI | Microsoft.ML.GenAI.* (LLaMA, Phi, Mistral) | src/Microsoft.ML.GenAI.*/ |
| TimeSeries | Microsoft.ML.TimeSeries | src/Microsoft.ML.TimeSeries/ |
| Transforms | Microsoft.ML.Transforms | src/Microsoft.ML.Transforms/ |
| DataView | Microsoft.ML.DataView | src/Microsoft.ML.DataView/ |
| TorchSharp | Microsoft.ML.TorchSharp | src/Microsoft.ML.TorchSharp/ |
| Fairlearn | Microsoft.ML.Fairlearn | src/Microsoft.ML.Fairlearn/ |
| Ensemble | Microsoft.ML.Ensemble | src/Microsoft.ML.Ensemble/ |
| KMeans | Microsoft.ML.KMeansClustering | src/Microsoft.ML.KMeansClustering/ |
| Mkl | Microsoft.ML.Mkl.Components | src/Microsoft.ML.Mkl.Components/ |
| OneDal | Microsoft.ML.OneDal | src/Microsoft.ML.OneDal/ |
| ONNX | Microsoft.ML.OnnxRuntime, OnnxTransformer | src/Microsoft.ML.OnnxRuntime*/ |
| CodeGenerator | Microsoft.ML.CodeGenerator (Model Builder) | src/Microsoft.ML.CodeGenerator/ |
| Extensions.ML | Microsoft.Extensions.ML | src/Microsoft.Extensions.ML/ |

## Triage JSON Schema (for agent internal use)

When building the fork issue body, structure your findings to include these fields:

- upstreamIssue: <number>
- upstreamRepo: <owner/name>
- title: <issue title>
- classification: <one of the 6 categories>
- confidence: <0.0 to 1.0>
- summary: <2-3 sentence summary>
- reasoning: <why this classification>
- duplicateOf: <number or null>
- suggestedLabels: <list of labels>
- areaComponent: <area from table above>
- reproduced: <true, false, or null if not attempted>
- rootCauseAnalysis: <text or null>
- suggestedFix: { files, description, complexity } or null
```

---

## 9. Fork Issue Lifecycle

```
Upstream Issue #7100                    Fork Issue #15
    │                                      │
    │  bulk-triage or manual dispatch      │  Created by create-issue safe-output
    │◄─────────────────────────────────────┤  Title: [triage] upstream#7100: ...
    │                                      │  Labels: triage, ai-investigation
    │                                      │
    │  Agent reads via github toolset      │  Body contains:
    │  (public repo — no PAT needed)       │    Classification + reasoning
    │──────────────────────────────────────►│    Reproduction results
    │                                      │    Root cause analysis
    │  Agent reproduces in sandbox         │    Suggested fix
    │──────────────────────────────────────►│    Status: COMPLETE
    │                                      │
    │                                      │  Additional labels added:
    │                                      │    bug, reproduced, fix-suggested
    │                                      │    (via add-labels safe-output)
```

### Dedup Mechanism

- The agent searches fork issues for `[triage] upstream#<N>` in the title
- All issues created by gh-aw contain a hidden marker: `<!-- gh-aw-workflow-id: triage-single-issue -->`
- This marker can also be used for searching: `repo:owner/fork "gh-aw-workflow-id: triage-single-issue" in:body`
- If found with status COMPLETE → `noop` (skip)
- If found with status IN_PROGRESS → `noop` (skip, another run is active)
- If not found → proceed with triage and `create-issue`

---

## 10. Label Scheme (Fork)

| Label | Color | Applied when |
|---|---|---|
| `triage` | `#0075ca` | All triage tracking issues (via `create-issue` config) |
| `ai-investigation` | `#7057ff` | All triage tracking issues (via `create-issue` config) |
| `bug` | `#d73a4a` | Classification = bug-report |
| `enhancement` | `#a2eeef` | Classification = feature-request |
| `duplicate` | `#cfd3d7` | Classification = duplicate |
| `discussion` | `#d4c5f9` | Classification = discussion |
| `wrong-usage` | `#fbca04` | Classification = wrong-usage |
| `needs-info` | `#e4e669` | Classification = unclear |
| `reproduced` | `#0e8a16` | Reproduction succeeded |
| `repro-failed` | `#b60205` | Reproduction attempted but failed |
| `fix-suggested` | `#1d76db` | Repro includes a fix suggestion |
| `dashboard` | `#5319e7` | The dashboard issue (via `create-issue` config) |
| `automation` | `#ededed` | Dashboard and bulk-run summary issues |
| `bulk-triage` | `#c5def5` | Bulk-run summary issues |

Labels must exist in the fork before the workflows run. Create them manually or via a setup script.

---

## 11. Security Considerations

| Concern | Mitigation |
|---|---|
| **Agent runs read-only** | gh-aw enforces separation: the agent has only read permissions. All writes (issue creation, labels, comments) go through safe-outputs, which are validated and executed by separate permission-controlled jobs. |
| **Upstream issue prompt injection** | gh-aw automatically sanitizes agent output (XML escaping, domain allowlists, bot mention limits). The structured safe-output schema constrains what the agent can create. |
| **No secrets exposed to agent** | No custom PATs needed. The `GITHUB_TOKEN` is not directly accessible to the agent — it's used only by the safe-output jobs. |
| **Sandbox isolation** | The agent runs inside a sandboxed environment with restricted network access (`defaults` + `dotnet` only). `bash` tool is allowlisted to specific commands. |
| **Resource consumption** | Each triage ≈ 5–15 premium requests, each repro ≈ 10–30. The `dispatch-workflow` max of 20 caps bulk runs. |
| **No self-dispatch loops** | gh-aw validates at compile time that a workflow cannot dispatch itself. |
| **Fork-only writes** | All `create-issue`, `add-labels`, `add-comment` safe-outputs target the fork. No cross-repo write operations configured. |

---

## 12. Implementation Phases

### Phase 1: Foundation — Single Issue Triage (no repro)

**Goal:** Get end-to-end single-issue triage working.

| Task | Files | Effort |
|---|---|---|
| Install `gh-aw` CLI extension | Local setup | 15m |
| Write shared knowledge file | `.github/aw/shared/triage-rules.lock.md` | 1h |
| Write `triage-single-issue.md` (frontmatter + agent instructions, classification only, no repro) | `.github/workflows/triage-single-issue.md` | 2h |
| Compile: `gh aw compile` | Generates `.lock.yml` + `actions-lock.json` | 5m |
| Create labels on fork | Manual or script | 15m |
| Commit & push `.md`, `.lock.yml`, `actions-lock.json`, shared knowledge | Git | 10m |
| Test: trigger manually for 2–3 known upstream issues | Manual | 1h |
| Review created fork issues, tune prompts | Manual | 1h |
| **Total** | | **~5.75h** |

### Phase 2: Add Reproduction

**Goal:** Bug reports get reproduced in the sandbox.

| Task | Files | Effort |
|---|---|---|
| Extend `triage-single-issue.md` agent instructions with reproduction steps | `.github/workflows/triage-single-issue.md` | 1.5h |
| Add `dotnet` to `bash` allowlist if not already present | Frontmatter update | 5m |
| Add `dotnet` to `network.allowed` for NuGet package restore | Frontmatter update | 5m |
| Recompile: `gh aw compile` | Generates updated `.lock.yml` | 5m |
| Test on a known-reproducible upstream bug | Manual | 1h |
| Tune reproduction prompt based on results | Iterative | 1h |
| **Total** | | **~4h** |

### Phase 3: Dashboard

**Goal:** Centralized visibility of all triage results.

| Task | Files | Effort |
|---|---|---|
| Write `triage-stats.md` (frontmatter + agent instructions) | `.github/workflows/triage-stats.md` | 2h |
| Compile: `gh aw compile` | Generates `.lock.yml` | 5m |
| Test after 5+ triage issues exist | Manual | 30m |
| **Total** | | **~2.5h** |

### Phase 4: Bulk Orchestrator

**Goal:** Batch processing with automatic dispatch.

| Task | Files | Effort |
|---|---|---|
| Write `bulk-triage.md` (frontmatter + agent instructions) | `.github/workflows/bulk-triage.md` | 2h |
| Compile: `gh aw compile` (validates dispatch-workflow target) | Generates `.lock.yml` | 5m |
| Test with `last-n-issues` mode (N=3) | Manual | 30m |
| Test with `last-n-days` mode | Manual | 30m |
| **Total** | | **~3h** |

### Phase 5: Polish & Iteration

- Tune prompts based on triage quality across 20+ issues
- Add `schedule: daily` or `schedule: weekly` trigger to `bulk-triage.md` (optional)
- Consider adding `repo-memory` for longer-term trend storage
- Add Mermaid charts to dashboard issue body
- Consider adding `/triage <N>` command trigger via `issue_comment` event
- Monitor premium request consumption and tune batch sizes

---

## 13. Compilation & Deployment Workflow

```
1. Edit the .md file (frontmatter or body)
   └──▶ If ONLY the markdown body changed:
        • You can edit directly on GitHub.com — no recompile needed!
        • The body is loaded at runtime.
   └──▶ If frontmatter changed:
        • Run: gh aw compile
        • This regenerates:
          - .github/workflows/<name>.lock.yml
          - .github/aw/actions-lock.json
        • Commit both the .md and .lock.yml

2. Push to fork's default branch
   └──▶ GitHub Actions picks up the .lock.yml
   └──▶ Trigger via Actions tab → workflow_dispatch
```

> **Key insight:** Markdown body changes do NOT require recompilation. The body is embedded at runtime. Only frontmatter changes (triggers, tools, safe-outputs, permissions) require `gh aw compile`.

---

## 14. Cost Estimation

| Operation | Premium Requests (est.) | Actions Minutes (est.) |
|---|---|---|
| Single triage (no repro) | 5–15 | 3–8 min |
| Single triage + repro | 15–45 | 10–30 min |
| Bulk triage (10 issues, no repro) | 50–150 | 30–80 min |
| Bulk triage (10 issues, with repro) | 150–450 | 100–300 min |
| Dashboard update | 5–10 | 2–5 min |

**Monthly estimate:** Triaging 50 issues/month with repro ≈ 750–2250 premium requests + 500–1500 Actions minutes.

---

## 15. Open Questions / Decisions

| # | Question | Default Assumption | Impact |
|---|---|---|---|
| 1 | Which AI engine/model? | Default Copilot engine (auto model selection) | Cost vs accuracy |
| 2 | Maximum issues per bulk run? | 20 (enforced by `dispatch-workflow: max: 20`) | Cost control |
| 3 | Should the repro step attempt full ML.NET build or standalone project only? | Standalone project (faster, simpler) | Repro fidelity |
| 4 | Dashboard: pinned issue or GitHub Projects board? | Issue with `close-older-issues: true` (zero extra setup) | UX |
| 5 | Add `schedule` trigger for automated bulk triage? | Not initially — start manual | Cost control |
| 6 | Use `repo-memory` vs `cache-memory` for persistent state? | `cache-memory` (simpler, works across runs) | State durability |
| 7 | Add `web-fetch` tool for reading external bug tracker links? | Not initially (keep sandbox tight) | Triage quality vs security |

---

## 16. References

- [GitHub Agentic Workflows — Overview](https://github.github.com/gh-aw/introduction/overview/)
- [gh-aw Frontmatter Reference](https://github.github.com/gh-aw/reference/frontmatter/)
- [gh-aw Safe Outputs Reference](https://github.github.com/gh-aw/reference/safe-outputs/)
- [gh-aw Tools Reference](https://github.github.com/gh-aw/reference/tools/)
- [gh-aw DispatchOps Pattern](https://github.github.com/gh-aw/patterns/dispatch-ops/)
- [gh-aw SideRepoOps Pattern](https://github.github.com/gh-aw/patterns/side-repo-ops/)
- [gh-aw Cross-Repository Operations](https://github.github.com/gh-aw/reference/cross-repository/)
- [gh-aw Workflow Structure](https://github.github.com/gh-aw/reference/workflow-structure/)
- [gh-aw CLI Commands](https://github.github.com/gh-aw/setup/cli/)
- [Existing `copilot-setup-steps.yml`](./../workflows/copilot-setup-steps.yml) in this repo
- [Existing issue templates](./../ISSUE_TEMPLATE/) — Bug report and feature request templates
- [Existing `untriaged.yml` policy](./../policies/untriaged.yml) — Upstream auto-labeling
