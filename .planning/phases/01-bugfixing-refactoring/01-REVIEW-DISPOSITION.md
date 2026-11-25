---
phase: 01
review: 01-REVIEW.md
titles: json
findings:
  - id: WR-01
    severity: warning
    disposition: skipped
    title: "A backup's timestamp is taken after verification, so a save rewritten during the backup reads \"Up to date\""
  - id: WR-02
    severity: warning
    disposition: fixed
    title: "`DirectoryWalk` does not guard `MoveNext`, so one mid-listing IO error aborts the whole walk"
  - id: WR-03
    severity: warning
    disposition: fixed
    title: "Bulk restore leaves rows stuck on \"Checking\" and a restore bar stuck on if an item throws (previous WR-01, still open)"
  - id: WR-04
    severity: warning
    disposition: fixed
    title: "Bulk backup reports \"all N backed up\" and drops each game's incompleteness and restore-cap warnings"
  - id: WR-05
    severity: warning
    disposition: fixed
    title: "Success-message warnings run into each other with no separating punctuation"
  - id: IN-01
    severity: info
    disposition: open
    title: "A permanently unreadable file makes its game a perpetual \"back up all changed\" target"
  - id: IN-02
    severity: info
    disposition: open
    title: "Doc comments still describe the two-verdict rule after `LastBackupPartial` joined it"
  - id: IN-03
    severity: info
    disposition: open
    title: "`-partial`, `-N` and `-pre-restore` archives take their `TakenAt` from the file's mtime, not the name"
  - id: IN-04
    severity: info
    disposition: open
    title: "Three new string keys sit under the \"Offline user guide outcomes\" comment"
  - id: IN-05
    severity: info
    disposition: open
    title: "Impossible-state guard: `try/catch` around `Directory.Exists` in `IsBackupRootReachable`"
  - id: IN-06
    severity: info
    disposition: open
    title: "Impossible-state guard: `default:` arms duplicating the `Checking` case (carried over from the previous review)"
  - id: IN-07
    severity: info
    disposition: open
    title: "No test pins the new sweep behaviour that makes the backup-root-aware scan and memoized advisory work"
open: 7
total: 12
recorded: 2026-11-25T08:48:08.470Z
---

# Phase 01: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| WR-01 | warning | skipped | 01-REVIEW-FIX.md |
| WR-02 | warning | fixed | 01-REVIEW-FIX.md |
| WR-03 | warning | fixed | 01-REVIEW-FIX.md |
| WR-04 | warning | fixed | 01-REVIEW-FIX.md |
| WR-05 | warning | fixed | 01-REVIEW-FIX.md |
| IN-01 | info | open | - |
| IN-02 | info | open | - |
| IN-03 | info | open | - |
| IN-04 | info | open | - |
| IN-05 | info | open | - |
| IN-06 | info | open | - |
| IN-07 | info | open | - |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
Set `deferred` by hand and put the reason in the Source cell; both are preserved. A `|` in the reason is kept as prose and escaped on the next run.
Re-running the gate keeps every row it can. A row the current review no longer reports is kept and its Source cell flagged, so a finding does not leave this record silently. ONE exception: when a finding id is REUSED by a different finding, the earlier decision cannot keep a row — the id is taken — and it is dropped. A RECORDED decision (anything but `open`) is named on the console when that happens; a row still at `open` is replaced silently, because `open` records no decision to lose.
