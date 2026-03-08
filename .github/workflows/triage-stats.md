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

imports:
  - ../aw/shared/dashboard-format.md

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
---

# Triage Stats Dashboard Agent

You are a data aggregation agent. Your job is to collect all triage tracking
issues in this repository and produce a comprehensive dashboard.

The dashboard format, data extraction rules, and category emoji mappings are
defined in the imported `dashboard-format.md` shared file. Follow those rules
precisely.

## Step 1: Find Existing Dashboard

Search this repo for an open issue with title starting with "[triage-dashboard]".
- If found, you will UPDATE it in Step 4a
- If not found, you will CREATE a new one in Step 4b
- Record the dashboard issue number if found

## Step 2: Collect Triage Data

List ALL issues in this repository labeled "triage" — paginate through every
page so that no issue is missed:

```bash
gh issue list --repo "$GITHUB_REPOSITORY" \
  --label "triage" --state all \
  --json number,title,body,labels,state,createdAt \
  --limit 200
```

For each issue, extract structured data from the body using the parsing rules
defined in dashboard-format.md:
- **Upstream issue number** and link
- **Classification** (bug-report, feature-request, duplicate, discussion, wrong-usage, unclear)
- **Whether reproduction was attempted** and succeeded
- **Whether a fix was suggested**
- **Status** (COMPLETE or IN_PROGRESS)
- **Area component**
- **Confidence score**

## Step 3: Compute Statistics

Calculate the following:
- **Total triaged issues** (count of all triage-labeled issues)
- **Breakdown by classification** (count per category)
- **Reproduction success rate** (for bug reports only: reproduced / attempted)
- **Number with suggested fixes**
- **Pending count** (IN_PROGRESS status)
- **Area distribution** (count per area component)
- **Average confidence score**

### Delta Computation
- Load previous run stats from cache-memory (key: `triage-dashboard-stats`)
- Compare current stats against previous to compute deltas ("+N" or "−N")
- Store current stats in cache-memory for the next run

## Step 4a: Update Existing Dashboard

If a dashboard issue was found in Step 1, use `update-issue` to REPLACE its body
with the new dashboard content using the **Dashboard Body Template** from
dashboard-format.md.

Set `<WORKFLOW_NAME>` to "Triage Stats Dashboard".

## Step 4b: Create New Dashboard

If no dashboard issue was found, use `create-issue` to create one:
- **Title:** `ML.NET Issue Triage Dashboard`
  (will be prefixed with "[triage-dashboard] " automatically)
- **Body:** Use the Dashboard Body Template from dashboard-format.md

## If No Triage Issues Found

If there are no issues labeled "triage" in this repository, call noop:
```json
{"noop": {"message": "No triage issues found in this repository"}}
```
