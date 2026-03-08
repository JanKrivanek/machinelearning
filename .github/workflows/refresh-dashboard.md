---
name: "Refresh Triage Dashboard"
description: >
  Standalone idempotent workflow that scans ALL triage issues in this repository,
  computes aggregate statistics, and updates the pinned dashboard issue body.
  Produces a point-in-time snapshot — no comments, no history.

on:
  schedule: "30 */6 * * *"
  workflow_dispatch:

permissions:
  contents: read
  issues: read

tools:
  github:
    toolsets: [repos, issues]
  bash: ["gh", "jq", "date", "echo", "sort", "uniq", "head", "tail", "grep", "wc", "cat"]

imports:
  - ../aw/shared/dashboard-format.md

safe-outputs:
  create-issue:
    title-prefix: "[triage-dashboard] "
    labels: [dashboard, automation]
    max: 1
    expires: false
  update-issue:
    target: "*"
    max: 1
  hide-comment:
    max: 50
---

# Refresh Triage Dashboard

You are a data aggregation agent. Your sole job is to scan **every** triage
tracking issue in this repository, compute statistics, and produce an
up-to-date dashboard issue body. This workflow is **idempotent** — running it
any number of times in the same state produces the same result.

The dashboard format and data extraction rules are defined in the imported
`dashboard-format.md` shared file. Follow those rules precisely.

## Step 1 — Find or Identify Dashboard Issue

```bash
DASH=$(gh issue list --repo "$GITHUB_REPOSITORY" \
  --label "dashboard" --state open \
  --json number,title --jq '.[0].number')
echo "Dashboard issue: ${DASH:-not found}"
```

If no dashboard issue exists, you will create one in Step 5.
If one exists, record its number for the update in Step 5.

## Step 2 — Collect ALL Triage Issues

Fetch **every** issue in this repository that has the `triage` label.
Paginate through ALL pages — do not stop at a single page.

```bash
# Fetch all triage issues (paginate with --limit 200 or multiple calls)
gh issue list --repo "$GITHUB_REPOSITORY" \
  --label "triage" --state all \
  --json number,title,body,labels,state,createdAt \
  --limit 200
```

If there are more than 200 issues, make additional paginated API calls until
you have fetched every triage issue. Use the GitHub REST API with `page`
parameter if needed:

```bash
PAGE=1
while true; do
  BATCH=$(gh api "repos/$GITHUB_REPOSITORY/issues?labels=triage&state=all&per_page=100&page=$PAGE" \
    --jq 'length')
  if [ "$BATCH" -eq 0 ]; then break; fi
  PAGE=$((PAGE + 1))
done
```

**You MUST process every single triage issue — none may be skipped.**

## Step 3 — Parse Each Issue

For each triage issue, extract structured data from the issue body using the
parsing rules in `dashboard-format.md`:

1. **Upstream issue number** — from the `**Upstream:**` URL or from the title
   pattern `[triage] upstream#<NUMBER>`
2. **Status** — from `**Status:** <VALUE>` (COMPLETE or IN_PROGRESS)
3. **Classification** — from `**Classification:** <VALUE>`
   (bug-report, feature-request, duplicate, discussion, wrong-usage, unclear)
4. **Confidence** — from `**Confidence:** <VALUE>` (decimal 0.0–1.0)
5. **Reproduced** — from `**Reproduced:** <VALUE>` (Yes/No/Failed/Skipped)
6. **Area** — from `**Area:** <VALUE>`
7. **Fix suggested** — `true` if the issue has the `fix-suggested` label OR
   contains a `## Suggested Fix` section in the body

Also read the issue's labels to detect: `reproduced`, `repro-failed`,
`fix-suggested`, `bug`, `enhancement`, `discussion`, `wrong-usage`,
`needs-info`, `duplicate`.

Build a complete list of parsed records — one per triage issue.

## Step 4 — Compute Statistics

From the parsed records, compute:

### Summary counts

| Metric | How to compute |
|---|---|
| Total Investigated | Count of all triage issues |
| Bug Reports | Count where classification = "bug-report" |
| Feature Requests | Count where classification = "feature-request" |
| Duplicates | Count where classification = "duplicate" |
| Questions/Discussion | Count where classification = "discussion" |
| Wrong Usage | Count where classification = "wrong-usage" |
| Unclear | Count where classification = "unclear" |
| Reproduced | Count where reproduced = Yes |
| Repro Failed | Count where reproduced = Failed |
| Fix Suggested | Count where fix_suggested = true |
| Pending (In Progress) | Count where status = "IN_PROGRESS" |

### Average confidence

Compute the arithmetic mean of all confidence scores (rounded to 2 decimals).

### Area breakdown

Group issues by Area. For each area, count:
- Bugs (classification = bug-report)
- Features (classification = feature-request)
- Other (all other classifications)
- Total

Sort areas alphabetically.

### All Investigations table

Build a row for EVERY triage issue. Sort by fork issue number descending
(newest first). Each row must include:
- Upstream issue link
- Classification with emoji (see emoji mapping in dashboard-format.md)
- Reproduced status (see repro display mapping)
- Fix suggested (✅ Yes / ❌ No)
- Status (✅ COMPLETE / 🔄 IN_PROGRESS)
- Fork issue number link

### Action Items

Populate each section per the rules in dashboard-format.md:
- **Ready for Fix**: issues with reproduced=Yes AND fix_suggested=true
- **Needs Upstream Info**: classification=unclear or has needs-info label
- **Low-Complexity Fixes**: fix_suggested=true AND body mentions
  "Low complexity" or "one-line" or similar low-effort indicators
- **Potential Duplicates**: classification=duplicate

## Step 5 — Update or Create Dashboard

### 5a. Generate Dashboard Body

Using the **Dashboard Body Template** from `dashboard-format.md`, fill in all
computed values. Set:
- `<TIMESTAMP_ISO8601>` to the current UTC time
- `<WORKFLOW_NAME>` to "Refresh Triage Dashboard"

### 5b. Update Existing Dashboard

If a dashboard issue was found in Step 1:

```bash
gh issue edit "$DASH" --repo "$GITHUB_REPOSITORY" --body "$DASHBOARD_BODY"
```

Use the `update-issue` safe output to replace the issue body entirely.

### 5c. Create New Dashboard

If no dashboard issue was found:

Create a new issue with:
- **Title:** `ML.NET Issue Triage Dashboard`
- **Body:** The generated dashboard body

Use the `create-issue` safe output. The title will be auto-prefixed with
`[triage-dashboard] `.

## Step 6 — Clean Up Comments

If the dashboard issue has any comments, hide ALL of them. The dashboard is
a point-in-time snapshot and does not need comment history.

```bash
# Get all comment node IDs
COMMENTS=$(gh api graphql -f query='
  query($owner: String!, $name: String!, $number: Int!) {
    repository(owner: $owner, name: $name) {
      issue(number: $number) {
        comments(first: 100) {
          nodes { id }
        }
      }
    }
  }
' -f owner="OWNER" -f name="REPO" -F number="$DASH" \
  --jq '.data.repository.issue.comments.nodes[].id')

for NODE_ID in $COMMENTS; do
  gh api graphql -f query='
    mutation($id: ID!) {
      minimizeComment(input: {subjectId: $id, classifier: OUTDATED}) {
        minimizedComment { isMinimized }
      }
    }
  ' -f id="$NODE_ID"
done
```

Use the `hide-comment` safe output for each comment (budget: 50).

## Rules

1. **Idempotent** — Running twice in the same state produces the exact same dashboard body
2. **Complete** — Every triage issue MUST appear in the dashboard, no matter how many exist
3. **Body-only** — The dashboard is the issue body. No comments are added.
4. **Non-destructive** — Only modifies the dashboard issue; never touches other issues
5. **Budget-aware** — Max 1 update-issue, max 1 create-issue, max 50 hide-comment
6. **Format-faithful** — Follow the template from dashboard-format.md exactly
