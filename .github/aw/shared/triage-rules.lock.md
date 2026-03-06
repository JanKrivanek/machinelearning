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

## Confidence Scoring

- **0.9 – 1.0:** Very confident — clear issue with strong evidence
- **0.7 – 0.89:** Confident — reasonable classification with supporting detail
- **0.5 – 0.69:** Moderate — some ambiguity but best-fit classification
- **< 0.5:** Low — issue is unclear or could reasonably fit multiple categories; prefer "unclear" classification

## Reproduction Guidelines

When reproducing bug reports:

1. **Always** create a standalone .NET console project — never modify the ML.NET source tree
2. **Work in** `/tmp/repro-<issue-number>/` only
3. **Add NuGet packages** matching the versions mentioned in the issue (or latest stable if unspecified)
4. **Time limit:** If `dotnet build` takes > 5 minutes or `dotnet run` takes > 3 minutes, abort and note the timeout
5. **Capture all output:** stdout, stderr, and exit code
6. **Analyze results:** Compare actual output against the issue's expected behavior
7. **Check source:** If reproduced, examine the ML.NET source code in this repo for the likely root cause

## Fork Issue Body Template

Use this template when creating the tracking issue via `create-issue`:

```markdown
# [triage] upstream#<N>: <original issue title>

**Upstream:** https://github.com/<upstream_repo>/issues/<N>
**Status:** COMPLETE
**Classification:** <category>
**Confidence:** <0.0 to 1.0>
**Reproduced:** <✅ Yes / ❌ No / ⏭️ Skipped / ⚠️ Failed>
**Area:** <area component>
**Investigated at:** <this will be auto-appended by gh-aw>

---

## Triage Summary

**Category:** <Full category name>
**Reasoning:** <2-3 sentences explaining why this classification was chosen>

**Summary:** <2-3 sentence overview of the issue>

**Suggested Labels:** <comma-separated list>

## Reproduction Results

> Only include this section for bug-report classification when reproduction was attempted.

**Reproduced:** <Yes/No>
**Platform:** Linux (ubuntu-latest)
**Steps:**
1. <step 1>
2. <step 2>
...

**Build Output:**
<relevant build output if applicable>

**Error Output:**
<error output or runtime output>

**Root Cause Analysis:**
<analysis of why the bug occurs, referencing source files>

## Suggested Fix

> Only include this section if a fix can be reasonably suggested.

**Files:** <list of source files>
**Description:** <description of the fix>
**Complexity:** <Low / Medium / High>
```
