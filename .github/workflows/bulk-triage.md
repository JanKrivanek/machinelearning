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

# Bulk Triage Orchestrator

You are a triage orchestration agent. Your job is to collect upstream issues
from ${{ github.event.inputs.upstream_repo }}, deduplicate against existing
investigations in this fork, and dispatch triage-single-issue worker workflows
for each new issue that needs triaging.

## Step 1: Fetch Upstream Issues and Deduplicate

Using the github tools, fetch open issues from ${{ github.event.inputs.upstream_repo }}
and build a list of issues that **have not yet been triaged**.

### Mode: last-n-issues — Find N Unprocessed Issues

If `${{ github.event.inputs.mode }}` is "last-n-issues":

The goal is to dispatch **exactly N** (= ${{ github.event.inputs.count }}) unprocessed
issues, where N is the requested count. Because some recent upstream issues may already
have been triaged in a previous run, you may need to look further back in time.

**Algorithm:**
1. Set `target = ${{ github.event.inputs.count }}` (desired unprocessed count)
2. Set `page_size = max(target * 2, 30)` (fetch in batches — larger than target to account for skips)
3. Set `page = 1`, `unprocessed = []`, `skipped = []`, `total_fetched = 0`
4. **Loop** (max 5 iterations to avoid runaway):
   a. Fetch `page_size` open issues from the upstream repo, sorted by creation
      date descending, page `page`
   b. If no issues returned → break (no more issues exist)
   c. For each issue in the page:
      - Apply label filters (include/exclude) — skip non-matching issues
      - Search THIS repository's issues for `[triage] upstream#<number>` in the title
      - If a tracking issue exists with "Status: COMPLETE" → add to `skipped`
      - If a tracking issue exists with "Status: IN_PROGRESS" → add to `skipped`
      - If no tracking issue found → add to `unprocessed`
   d. `total_fetched += len(page_results)`
   e. If `len(unprocessed) >= target` → break (we have enough)
   f. `page += 1`
5. Trim `unprocessed` to the first `target` items (if we found more than needed)

This ensures that if, say, 5 of the last 10 issues were already triaged, the workflow
keeps looking at older issues until it finds 10 unprocessed ones.

### Mode: last-n-days

If `${{ github.event.inputs.mode }}` is "last-n-days":
- Calculate the date that is ${{ github.event.inputs.count }} days ago
- Get ALL open issues created since that date using the `since` parameter
- Apply label filters (same as above)
- Deduplicate: search THIS repo for `[triage] upstream#<number>` for each issue
- Collect all unprocessed issues (no cap — limited only by the dispatch max of 20)

### Apply Filters (both modes)

- **labels_filter** (if set): Only include issues that have ALL of the specified
  labels. Parse `${{ github.event.inputs.labels_filter }}` as a comma-separated list.
- **exclude_labels** (if set): Exclude issues that have ANY of the specified labels.
  Parse `${{ github.event.inputs.exclude_labels }}` as a comma-separated list.

### Dedup Check

For each upstream issue, search THIS repository's issues:
1. Search for `[triage] upstream#<number>` in issue titles
2. If a tracking issue exists with "Status: COMPLETE" in the body → **skipped (already triaged)**
3. If a tracking issue exists with "Status: IN_PROGRESS" in the body → **skipped (in progress)**
4. If no tracking issue found → **needs triage**

Record: total fetched, total after filtering, skipped count, and unprocessed count.

## Step 2: Dispatch Workers

For each issue marked as "needs triage", dispatch the `triage-single-issue` workflow:

```json
{
  "workflow": "triage-single-issue",
  "inputs": {
    "upstream_issue": "<issue_number>",
    "upstream_repo": "${{ github.event.inputs.upstream_repo }}",
    "skip_repro": "${{ github.event.inputs.skip_repro }}"
  }
}
```

**Important constraints:**
- Maximum 20 dispatches per run (enforced by the safe-outputs max setting)
- gh-aw automatically enforces a 5-second delay between dispatches for rate limiting
- If more than 20 issues need triaging, dispatch only the first 20 (most recent)
  and note the remainder in the summary

Record the list of issue numbers that were dispatched.

## Step 3: Create Run Summary Issue

Create a summary issue documenting this bulk triage run:

**Title:** `Bulk triage: ${{ github.event.inputs.mode }} = ${{ github.event.inputs.count }}`

Note: The title will be automatically prefixed with "[bulk-triage-run] " by the safe-output config.

**Body:**

```markdown
# Bulk Triage Run Summary

**Mode:** ${{ github.event.inputs.mode }}
**Count:** ${{ github.event.inputs.count }}
**Upstream Repo:** ${{ github.event.inputs.upstream_repo }}
**Skip Repro:** ${{ github.event.inputs.skip_repro }}
**Labels Filter:** ${{ github.event.inputs.labels_filter || 'none' }}
**Exclude Labels:** ${{ github.event.inputs.exclude_labels || 'none' }}
**Run at:** <current timestamp>

## Results

| Metric | Count |
|---|---|
| Upstream issues scanned | <N> |
| After label filtering | <N> |
| Already triaged (skipped) | <N> |
| In progress (skipped) | <N> |
| Unprocessed found | <N> |
| **Workers dispatched** | **<N>** |
| Remaining (over dispatch limit) | <N> |

## Dispatched Issues

| # | Upstream Issue | Title | Labels |
|---|---|---|---|
| 1 | [#<N>](https://github.com/<repo>/issues/<N>) | <title> | <labels> |
| ... | ... | ... | ... |

## Skipped Issues (Already Triaged)

| Upstream Issue | Fork Tracking Issue | Status |
|---|---|---|
| [#<N>](https://github.com/<repo>/issues/<N>) | #<fork_issue> | COMPLETE |
| ... | ... | ... |
```

## If No Issues to Triage

If ALL fetched issues are already triaged (no workers need dispatching), call noop:
```json
{"noop": {"message": "No new issues to triage — all <N> fetched issues already have tracking issues"}}
```

Do NOT create a summary issue in this case.
