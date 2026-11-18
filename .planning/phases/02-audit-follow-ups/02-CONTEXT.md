# Phase 2: Audit follow-ups - Context

**Gathered:** 2026-11-18
**Status:** Ready for planning

<domain>
## Phase Boundary

Close the open findings from the 2026-11-18 audit scan and the clean-code items from the same
day's scan, exactly as listed in the Phase 2 section of `.planning/ROADMAP.md`: defects 0–3 and
clean-code items 4, 5, 6, 7, 9, 11, 14, 15, 16, 19, 20, 21, 22, 23, 24. The items under
"Considered and dropped" (8, 10, 12, 13, 17, 18 and the empty-state part of 20) stay out of scope,
except for the one-line stale-comment correction at `MainWindow.xaml:286-287` that item 18 names.
The fix directions, order constraints and keep/leave notes in the ROADMAP backlog are binding
unless a decision below refines them.

</domain>

<decisions>
## Implementation Decisions

### Item 0 — deliberately excluded folders
- **D-01:** When the backup walk deliberately leaves out a folder inside the save folder (one that
  is or contains the backup root, or is or lies inside a per-game archive folder
  `<backupRoot>\<id>`), the backup result message carries a plain **informational** line naming
  each left-out path and why (e.g. "Left out: <path> (backup folder)"). It must NOT reuse the
  unreadable-path warning wording of `DescribeSkippedPathsWarning` ("could not be read and are NOT
  in this backup"), because the exclusion is the user's chosen layout, not a defect. Excluded
  folders are not counted as skipped/unreadable paths.
- **D-02:** The plan must add an explicit test proving restore never deletes a folder the backup
  deliberately excluded. Today `RestoreService.cs:68` refuses when the backup root is inside the
  save folder; the test must cover every exclusion case of D-01 (backup root inside the save
  folder, per-game archive folder inside the save folder), not rely on reasoning that the guard
  happens to cover them.
- **D-03:** Item 0 is an observable behavior change (save subfolders that used to be pruned are
  now archived; exclusions are reported). Record it in `SPEC.md` §3 Backup (with the reasoning
  inline, per CLAUDE.md) and add a `### Fixed` entry under `## [Unreleased]` in `CHANGELOG.md`,
  written in the existing user-facing style. Update `docs/DECISIONS.md` only if the change revises
  a recorded design decision.

### Item 3 — unreachable test and flaky timeouts
- **D-04:** `StartupUnsecuredPromptTests.Yes_with_an_unset_backup_root_opens_the_picker_and_cancelling_is_a_no_op`
  drives a state the app cannot reach. Rework it so its intent ("cancelling the folder picker is a
  no-op that writes nothing") is exercised through a path the app actually reaches. If an existing
  test (e.g. `MainViewModelTests.A_cancelled_first_backup_folder_prompt_writes_nothing`) already
  covers exactly that reachable path, delete the unreachable test instead and say so in the plan.
- **D-05:** Fix `DriveHints.SuggestBackupStartFolder` to check `DriveType` before `IsReady` first.
  If the two named tests still depend on the machine's real drives after that fix, inject the
  drive probe as a delegate seam (the Phase 1 coordinator seam style, no new dependency) so the
  tests stop touching real drives. Do NOT resolve the flake by raising timeouts alone.

### Item 21 — palette and visible layout
- **D-06:** Rename `PaletteRedColor`/`PaletteRedBrush` to app-prefixed keys
  (`SavegameBunkerDangerColor`/`SavegameBunkerDangerBrush`) and update all references
  (`App.xaml`, `Themes/Styles.xaml:148`, `:162`, `Views/AddGameDialog.xaml:97`). App text keeps the
  WCAG-AA red `#FFDC6C64`; WPF-UI controls get the library's own red back. Research must list
  which WPF-UI 4.3.0 controls/templates consume `PaletteRed*`, so the visible effect of dropping
  the override is known before execution.
- **D-07:** Visual verification for items 19, 20 and 21 is by **before/after screenshots** taken by
  the executor from the running app (main window with rows, toolbar, overflow menu, row action
  buttons in busy and idle state, each dialog footer). Items 19/20 must show no visible
  difference; item 21's spacing changes and the red change must be visible only where intended.
  Access keys (`_` mnemonics) on the templated toolbar buttons are checked explicitly. The owner
  reviews the image pairs; no interactive owner checkpoint is required for this.

### Plan structure and workflow
- **D-08:** Work stays on the current branch `dev/v1.1`; no phase branch. Record the full-suite test
  count as the baseline at the start, keep the suite green after every plan; CI on the PR to `main`
  remains the gate.
- **D-09:** Defect fixes land before the test-harness items 22–24. Tests written for the defects use
  the current helpers; item 22 later migrates them along with everything else.

### Claude's Discretion
- Grouping of items into plans and waves (user chose "planner decides"), within the ROADMAP order
  constraints: 0 → 4 → 5; 2 → 14; 3 → 24; 22 → 24 → 23; and D-09.
- Exact wording of the D-01 info line, within the existing message style.
- Item 11: remove or assert the unreachable UI-thread branch of `DialogService.Confirm`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Scope and backlog
- `.planning/ROADMAP.md` §"Phase 2: Audit follow-ups" — the item list, fix directions, order and dropped items (line numbers refer to `dev/v1.1` at 7f6ddd5)
- `.planning/STATE.md` — current position and roadmap evolution notes
- `CLAUDE.md` — hard constraints (no refusing/redirecting backup destinations, two NuGet dependencies, restore order, error contract, English-only files)

### Behavior specification
- `SPEC.md` §3 Backup and §"Backup destination" — to be revised for item 0 (D-03)
- `SPEC.md` §4 Restore — restore order and the save-folder replacement that D-02 protects
- `CHANGELOG.md` `## [Unreleased]` — style for the D-03 entry
- `docs/DECISIONS.md` — update only if a recorded decision changes

### Prior phase
- `.planning/phases/01-bugfixing-refactoring/01-RESEARCH.md`, `01-PATTERNS.md` — coordinator seams, test patterns (`WaitForAsync`, no SynchronizationContext in the test host)
- `.planning/codebase/TESTING.md`, `.planning/codebase/CONVENTIONS.md` — test and code conventions

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `DirectoryWalk.ForEachFile` / its `onSkipped` channel: already carries unreadable paths to the backup result; D-01 needs a separate channel (or a separate list) for deliberate exclusions so they are not mixed with skipped paths.
- `BackupService.DescribeSkippedPathsWarning` (`BackupService.cs:215`): pattern for appending a suffix to the created message; the D-01 info line is appended the same way, with different wording.
- `TestFileSystem.cs`: existing test helpers for building save trees.

### Established Patterns
- Exclusions today: `excludedDirs = { backupRoot, targetDir }` (`BackupService.cs:116`), pruned in `DirectoryWalk.cs:106-108` in both directions (sub inside excluded, excluded inside sub) — the "sub inside excluded" direction is item 0's bug when the save folder itself sits inside the backup root.
- `BackupService.IsSameOrInside` (`BackupService.cs:801`) is used by `DirectoryWalk` and `RestoreService` (`:68`, `:76`); item 4 extracts it into a `PathCompare` helper after item 0.
- Services return `OperationResult`, never throw (ServiceContractTests enforces this).

### Integration Points
- `RestoreService.cs:68` guard — D-02's test target.
- `App.xaml` resource dictionary and `Themes/Styles.xaml` — D-06.
- `StartupUnsecuredPromptTests.cs:274` — D-04.
- `DriveHints.cs:69` — D-05.

</code_context>

<specifics>
## Specific Ideas

- The D-01 info line is a note, not a warning: a user who put the backup folder inside a save folder chose that layout, and the message should not make every backup look broken.
- The red stays the contrast-corrected `#FFDC6C64` for the app's own text; only the key name and its reach change.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 02-audit-follow-ups*
*Context gathered: 2026-11-18*
