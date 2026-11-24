---
phase: 02-audit-follow-ups
verified: 2026-11-24T17:57:23Z
status: human_needed
score: 27/27 must-haves verified (code-level); 1 present-but-behavior-unverified; 4 human-verification items outstanding
covered_files:
  - ".planning/STATE.md"
  - ".planning/phases/02-audit-follow-ups/02-01-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-01-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-02-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-02-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-03-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-03-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-04-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-04-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-05-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-05-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-06-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-06-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-07-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-07-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-08-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-08-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-09-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-09-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-10-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-10-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-11-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-11-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-12-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-12-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-13-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-13-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-14-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-14-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-15-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-15-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-16-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-16-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-17-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-17-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-18-PLAN.md"
  - ".planning/phases/02-audit-follow-ups/02-18-SUMMARY.md"
  - ".planning/phases/02-audit-follow-ups/02-REVIEW-DISPOSITION.md"
  - ".planning/phases/02-audit-follow-ups/02-REVIEW.md"
  - ".planning/phases/02-audit-follow-ups/screenshots/capture.ps1"
  - "CHANGELOG.md"
  - "src/SavegameBunker/Services/DialogService.cs"
  - "src/SavegameBunker/Views/RestorePickerWindow.xaml"
  - "src/SavegameBunker/Views/RestorePickerWindow.xaml.cs"
covered_digest: "v4:sha256:e1b4e869d73edeff1f37dd36da7b006c4db5fa54e9ac8e2eaa709473b0464e71"
behavior_unverified: 1
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 23/23
  gaps_closed:
    - "G-02-6 (UAT test 6): the restore picker's delete confirmation is now hosted in the picker window's own ContentDialogHost, not MainWindow's — code-level fix confirmed, with a real-click (not UI-Automation-InvokePattern) evidence script run twice by Plan 02-16, both runs PASS a-e"
    - "G-02-7 (UAT test 7): capture.ps1's three diagnosed element-lookup bugs (and three more found at execution time) are fixed; all three screenshot sets (before/, after-19-20/, after-21/) now hold all fourteen named shots — confirmed directly on disk by this verification (14/14/14), not merely claimed by SUMMARY"
    - "UAT tests 2 and 3 (Alt-mnemonic pass, live bulk-progress-panel run) were separately confirmed passing live per 02-UAT.md — no longer outstanding"
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Re-run UAT test 6: on a catalog where a game has two or more backups, open Restore, press a row's delete button, Cancel, then press it again and Delete — both with a real mouse."
    expected: "The confirmation renders inside the restore list window and is answerable by both clicks while the list is open; Cancel keeps the archive, Delete removes exactly that one row and its file."
    why_human: "Plan 02-16's own `<verify>` names this UAT test as 'the owner's check of record' and explicitly defers to it, despite running a strong automated real-click proxy (SetCursorPos/mouse_event + WindowFromPoint, twice, all of a-e PASS) — because that proxy still runs on the executor's own terms. `.planning/STATE.md`'s Blocker for Phase 02 Item 11 is still open and the plan's SUMMARY says it 'can be cleared once the owner re-runs UAT test 6' — that re-run has not happened yet. This verifier did not re-run the (gitignored, non-committed, interactive-desktop-only) evidence script either, so the behavior itself is present and wired but not independently behavior-verified in this session."
  - test: "Re-run UAT test 1 (before/ vs after-19-20/ comparison for items 19, 20) on the now-complete fourteen-shot sets."
    expected: "Toolbar icons/labels/spacing/hover/Alt-underlines identical 01-05; menu/row-action identical 04-06; 01-09-14 pixel-identical; 10-13 differ only in footer-button width (D-12)."
    why_human: "02-UAT.md (status: complete) still reports test 1 against the old partial (9/14) sets and has not been re-run against the now-complete 14/14/14 sets this verification confirmed on disk. Plan 02-18's own Task 2 already recorded pair verdicts for the five new shots (05/07/08 identical, 11/13 differ in footer width as D-12 expects) — strong supporting evidence — but that is the executor's own self-check, not the owner's UAT re-run."
  - test: "Re-run UAT test 5 (after-19-20/ vs after-21/ comparison for item 21) and UAT test 7 (before/ shot-table completeness) on the complete sets."
    expected: "Spacing changes appear only at the status bar/bulk-progress panel/restore-step text/empty-state link; close-button hover colour changes; every one of the fourteen named shots is present and shows only the app window in its named state."
    why_human: "Same staleness as test 1 — 02-UAT.md has not been re-run since Plan 02-18 filled the five previously-missing shots in all three sets. Plan 02-18's own pair-verdict table is consistent with the expected verdict (including a genuine before/after-21 status-bar spacing difference it found), but is not a substitute for the owner's own review of record."
  - test: "Decide whether to restore the two disclosed-but-now-untracked findings (WR-01 backup-of-own-archive-folder repack; IN-01 orphaned `xs` Styles.xaml token) to `.planning/WISHLIST.md` or a fresh `02-REVIEW-DISPOSITION.md` row."
    expected: "Either both findings get a durable home (WISHLIST.md or a disposition row), or an explicit owner decision that losing them is acceptable."
    why_human: "The 2026-11-24 code review (`82a608f`) overwrote `02-REVIEW-DISPOSITION.md` wholesale with only Plan 02-16's own new WR-01 (a different finding — `Confirm`'s blocked-thread leak on Alt+F4, already accepted per T-02-30), silently dropping the prior review's WR-01/IN-01 rows. Neither finding is tracked in WISHLIST.md. This is a documentation/process gap, not a code defect — the underlying facts (repack behaviour, orphaned token) are unchanged and low-severity — but it needs an owner decision, not a verifier judgment call."
---

# Phase 02: Audit follow-ups Verification Report

**Phase Goal:** Close the open findings from the 2026-11-18 audit scan that need a design decision
or test-harness work, plus the clean-code items from the same day's scan that remove real
duplication or real defects (ROADMAP items 0–7, 9, 11, 14–16, 19–24, the `MainWindow.xaml:286-287`
comment, and decisions D-01..D-13 per `02-CONTEXT.md`). The quick fixes already landed before this
phase.

**Verified:** 2026-11-24
**Status:** human_needed
**Re-verification:** Yes — continuing from the prior `02-VERIFICATION.md` (2026-11-19, `human_needed`,
23/23). That report had no `gaps:` frontmatter block (its only outstanding items were
human-verification, not failed truths), so this run does not follow the strict re-verification-mode
optimization path; it is a full independent check that also covers three new plans (02-16, 02-17,
02-18) executed since then specifically to close two of that report's human-verification gaps
(G-02-6, G-02-7, both harvested into `02-UAT.md` as failed UAT tests 6 and 7).

## What changed since the prior verification

`git diff --stat 880442f..HEAD -- src/ tests/ CHANGELOG.md SPEC.md docs/ screenshots/capture.ps1`
shows exactly five files touched: `DialogService.cs`, `RestorePickerWindow.xaml(.cs)`,
`CHANGELOG.md` and `capture.ps1`. No other source or test file changed — confirming the prior
report's 23 code-level truths (items 0–24, D-01..D-13, the `MainWindow.xaml` comment) have not
regressed; this was re-confirmed directly in this session with an independent
`dotnet build -c Release --no-incremental` (0 Warnings/0 Errors) and `dotnet test -c Release
--no-build` (**745 passed, 0 failed, 0 skipped**).

Three gap-closure plans ran between the prior verification and this one:

- **Plan 02-16** (requirements: `ITEM-11`, `gap_ids: [G-02-6]`): gives `RestorePickerWindow` its
  own `ContentDialogHost` and has `DialogService.ShowRestorePickerDialog` build a picker-owned
  `ContentDialogService`/`DialogService` pair instead of handing `RestorePickerViewModel` the
  `MainWindow`-scoped one. Fixes the confirmed real defect from Plan 02-10's manual sub-check
  (confirmation rendered behind the modal picker, unreachable by a real click).
- **Plan 02-17** (requirements: `D-07`, `gap_ids: [G-02-7]`): fixes three diagnosed `capture.ps1`
  bugs (unparenthesised arithmetic, `ControlType.Custom`-by-Name row matching, a `Button` search
  for a `MenuItem`) plus three more found while executing, so all fourteen D-07 shots become
  capturable against the current build.
- **Plan 02-18** (requirements: `D-07`, `D-12`): runs the fixed `capture.ps1` against three
  disposable `git worktree` builds (57811c1, 987b131, b54a058) to fill the five previously-missing
  shots (05, 07, 08, 11, 13) into `before/`, `after-19-20/` and `after-21/`.

## Goal Achievement

### Observable Truths

Truths 1–23 (items 0–24, D-01..D-13, the `MainWindow.xaml` comment) were established and verified
code-level in the prior `02-VERIFICATION.md` and re-confirmed here via the diff-stat regression
check above (no file backing any of them changed) plus the independent 745/745 green build/test
run. They are not re-tabulated in full; see the prior report for the per-truth evidence. New and
re-scoped truths from this round:

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 24 | G-02-6: `RestorePickerWindow` has its own `ContentDialogHost`; `DialogService.ShowRestorePickerDialog` builds a picker-owned `ContentDialogService` + `DialogService` bound to it and passes that to `RestorePickerViewModel` instead of `this` | ✓ VERIFIED | `RestorePickerWindow.xaml:134` declares `x:Name="RootDialogHost"`; `DialogService.cs:195-199` builds `pickerContentDialogs`/`pickerDialogs` and constructs `RestorePickerViewModel(gameDisplayName, backups, pickerDialogs, deleteArchive)`; `IDialogService.cs`, `App.xaml.cs`, every view model and every test confirmed byte-identical to `880442f` (`git diff --quiet` exits 0, re-run in this session); Release build 0/0, suite 745/745 |
| 25 | G-02-6 (behaviour): with the picker open, a real mouse click reaches the delete confirmation inside the picker (not behind it); Cancel keeps the archive, Delete removes exactly one | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Code is present and wired (truth 24); Plan 02-16's own gitignored real-click script (`SetCursorPos`/`mouse_event` + `WindowFromPoint`, never `InvokePattern`) ran twice and printed PASS for checks a–e both times — strong supporting evidence, but not re-run independently in this session, and the project's own designated "check of record" (UAT test 6) has not been re-run by the owner; `.planning/STATE.md`'s Blocker for Phase 02 Item 11 is still open. See Human Verification item 1 |
| 26 | G-02-7: `capture.ps1` can produce all fourteen D-07 shots against the current build via `Find-RowButton` (DataItem + nested Text), `Find-AppDialogWindow`/`Find-AppMenuItem` (process-scoped), no positional fallback, and no unparenthesised `op_Subtraction` sites | ✓ VERIFIED | `grep -c 'ControlType\]::Custom' capture.ps1` → 0; `Find-RowButton`, `Find-AppDialogWindow`, `Find-AppMenuItem` all present (confirmed by grep in this session); directly confirmed behavioural proof below (truth 27) |
| 27 | `screenshots/before/`, `after-19-20/` and `after-21/` each hold all fourteen named D-07 shots, enabling UAT tests 1, 5 and 7 to be re-run on complete sets | ✓ VERIFIED | Confirmed directly on disk in this session: `ls .../before/*.png \| wc -l` → 14, same for `after-19-20/` and `after-21/`; all fourteen expected filenames present in each directory (01 through 14, including the five previously-missing 05/07/08/11/13) |

**Score:** 27/27 truths verified (24 of the original 23 plus 3 new from this round are fully
code-level verified); 1 present-but-behavior-unverified (truth 25).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/SavegameBunker/Views/RestorePickerWindow.xaml` | own `ContentDialogHost`, muted row-selection style, delete-icon margin | ✓ VERIFIED | `RootDialogHost` present; `0d9d45a`'s selection/margin fix also confirmed present |
| `src/SavegameBunker/Views/RestorePickerWindow.xaml.cs` | parameterless constructor | ✓ VERIFIED | `public RestorePickerWindow()` confirmed, no `ViewModels` using |
| `src/SavegameBunker/Services/DialogService.cs` | picker-owned dialog service in `ShowRestorePickerDialog` | ✓ VERIFIED | `pickerContentDialogs`, `pickerDialogs` confirmed at the expected lines |
| `CHANGELOG.md` | Fixed entry for the restore-list delete confirmation | ✓ VERIFIED | Line 55: "Deleting a backup in the restore list now asks inside the restore list." |
| `.planning/phases/02-audit-follow-ups/screenshots/capture.ps1` | `Find-RowButton`, `Find-AppDialogWindow`, `Find-AppMenuItem`, no `op_Subtraction` sites | ✓ VERIFIED | All three functions present; no unparenthesised arithmetic matches remain |
| `.../screenshots/before/`, `after-19-20/`, `after-21/` | fourteen named PNGs each | ✓ VERIFIED | 14/14/14 confirmed on disk |
| `.planning/STATE.md` Blocker (Phase 02, Item 11) | cleared once owner re-confirms | ⚠️ not yet cleared | Still present — correctly so, pending the human-verification item below; not a defect in this verification |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `DialogService.cs` | `RestorePickerWindow.xaml` | `pickerContentDialogs.SetDialogHost(dialog.RootDialogHost)` | ✓ WIRED | Confirmed by grep |
| `DialogService.cs` | `RestorePickerViewModel.cs` | `new RestorePickerViewModel(gameDisplayName, backups, pickerDialogs, deleteArchive)` | ✓ WIRED | Confirmed by grep; old `this`-passing form confirmed absent (0 matches outside comments) |
| `capture.ps1` | `MainWindow.xaml` | `Find-RowButton` matches the Game cell's `DisplayName` Text inside a `DataItem` | ✓ WIRED | Confirmed by grep; `ControlType]::DataItem` present, `ControlType]::Custom` row-matching absent |
| `capture.ps1` | `RestorePickerWindow.xaml` | `Find-AppDialogWindow` with the picker title built via `[char]0x2014` | ✓ WIRED | Confirmed by grep (`0x2014` present) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Release build is clean | `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` | `0 Warning(s)`, `0 Error(s)` | ✓ PASS |
| Full regression suite is green | `dotnet test SavegameBunker.sln -c Release -tl:off --no-build` | `Passed! Failed: 0, Passed: 745, Skipped: 0, Total: 745` | ✓ PASS |
| `screenshots/before/` holds all fourteen shots | `ls .../before/*.png \| wc -l` | `14` | ✓ PASS |
| `screenshots/after-19-20/` holds all fourteen shots | `ls .../after-19-20/*.png \| wc -l` | `14` | ✓ PASS |
| `screenshots/after-21/` holds all fourteen shots | `ls .../after-21/*.png \| wc -l` | `14` | ✓ PASS |
| Picker-owned dialog service wiring present | `grep -n 'pickerContentDialogs\|pickerDialogs' DialogService.cs` | 3 matches at the expected lines | ✓ PASS |
| No stray `op_Subtraction` sites remain | `grep -c 'ControlType\]::Custom' capture.ps1` | `0` | ✓ PASS |
| STATE.md Blocker for Item 11 status | `grep -n 'Phase 02, Item 11' .planning/STATE.md` | found, still open | ✓ PASS (expected — not yet owner-cleared) |

The real-click behavioral proof for G-02-6 (checks a–e) is **not** re-run in this verification —
it requires an interactive Windows desktop, launches the app, and mutates a demo backup folder,
which this session did not perform. This is why truth 25 is reported PRESENT_BEHAVIOR_UNVERIFIED
rather than VERIFIED.

### Requirements Coverage

No requirement IDs are registered for Phase 2 in `.planning/REQUIREMENTS.md` (confirmed: its only
IDs are `REF-01..REF-05`, all Phase 1). Phase 2's traceability keys are the ROADMAP backlog item
numbers and the `D-01..D-13` decisions in `02-CONTEXT.md`. Plans 02-16/17/18 declare
`requirements: [ITEM-11]`, `[D-07]` and `[D-07, D-12]` respectively — all three are already within
the phase's committed scope (item 11, decisions D-07 and D-12), not new scope. No orphaned
requirements.

### Anti-Patterns Found

`grep -n -E "TODO|FIXME|XXX|TBD|PLACEHOLDER|HACK"` across the five files this round's plans touched
(`DialogService.cs`, `RestorePickerWindow.xaml(.cs)`, `CHANGELOG.md`, `capture.ps1`) returned zero
matches. No debt markers.

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `.planning/phases/02-audit-follow-ups/02-REVIEW-DISPOSITION.md` | — | wholesale overwrite dropped prior findings | ℹ️ Info | The 2026-11-24 code review replaced the file's only row with its own new WR-01, silently losing the prior review's WR-01 (backup-of-own-archive-folder repack) and IN-01 (orphaned `xs` Styles.xaml token). Neither is tracked in `WISHLIST.md` either. Documentation/process gap, not a code defect — see Human Verification item 4 |

### Deferred / Disclosed, Not Gaps

- **WR-01 (2026-11-24 review)**: `DialogService.Confirm`'s blocked worker thread has no escape if
  the owning window closes while a dialog is pending — pre-existing gap, now reachable from the
  picker's own dialog service too. Matches the plan's own threat register (T-02-30, disposition
  "accept", severity low); `02-16-SUMMARY.md`'s Alt+F4 observation shows the main window stays
  responsive either way. Open, accepted, not a phase-goal blocker.
- **WR-01 (2026-11-19 review, now untracked)**: backup of a save folder equal to its own archive
  folder repacks every earlier archive on each run. Not data loss (Restore already refuses this
  layout). Still true, still deferred to the next milestone — but no longer recorded anywhere
  after the disposition overwrite. See Human Verification item 4.
- **IN-01 (2026-11-19 review, now untracked)**: cosmetic orphaned `xs` spacing token left by item
  21's own edit in `Styles.xaml` (confirmed still present: `Styles.xaml:57`). Deferred to the next
  cleanup pass. See Human Verification item 4.

## Human Verification Required

See frontmatter `human_verification` for the full list (4 items). In summary:

1. **UAT test 6 must be re-run by the owner with a real mouse** on the now-fixed restore picker
   before `.planning/STATE.md`'s Blocker for Phase 02 Item 11 is cleared. Strong automated evidence
   already exists (Plan 02-16's real-click script, PASS a–e twice) but is not a substitute for the
   project's own designated check of record.
2. **UAT tests 1 and 5** (before/after-19-20 and after-19-20/after-21 screenshot comparisons for
   items 19–21) need to be re-run against the now-complete fourteen-shot sets; `02-UAT.md` still
   reflects the old, partial sets.
3. **UAT test 7** (shot-table completeness) needs the same re-run; Plan 02-18's own pair-verdict
   table is consistent with what D-07/D-12 expect, but has not been reconciled into `02-UAT.md`.
4. **An owner decision on WR-01/IN-01 tracking**: restore the two 2026-11-19-review findings
   (backup-of-own-archive repack; orphaned `xs` token) to `WISHLIST.md` or a disposition row, or
   explicitly accept losing that tracking.

None of these are code-level gaps: every artifact and wiring claim checked out against the actual
source tree in this session, the complete screenshot sets exist on disk (not merely claimed), and
the full regression suite passed at 745/745 on an independent build.

## Gaps Summary

No must-have truth failed, no required artifact is missing or a stub, and no key link is unwired.
Both gaps carried forward from the prior verification (G-02-6, G-02-7) have real, substantive
code-level fixes with strong supporting evidence. The phase stays at `human_needed` rather than
`passed` because the project's own UAT process — the designated check of record for exactly this
kind of interactive/visual claim — has not yet been re-run against the new fixes, and because one
documentation-tracking regression (the lost WR-01/IN-01 rows) needs an owner decision rather than a
verifier judgment call.

---

_Verified: 2026-11-24_
_Verifier: Claude (gsd-verifier)_
