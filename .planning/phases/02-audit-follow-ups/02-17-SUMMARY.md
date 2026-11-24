---
phase: 02-audit-follow-ups
plan: 17
subsystem: tooling
tags: [capture-ps1, ui-automation, d-07]

requires:
  - phase: 02-audit-follow-ups
    provides: "02-03's capture.ps1/demo-data.ps1 and 02-16's picker-owned dialog host (shot 13 runs after it so it photographs the fixed picker)"
provides:
  - "capture.ps1 can take all fourteen D-07 shots against the current Release build: Find-RowButton (DataItem + nested Text row lookup), Find-AppDialogWindow (process-scoped owned-window/dialog lookup), Find-AppMenuItem (process-scoped MenuItem lookup)"
affects: ["Plan 02-18 (uses this capture.ps1 to produce the before/after-19-20/after-21 screenshot sets)"]

actuals:
  tokens: 3150        # chars/4 over the realized diff (git diff a5f546f..HEAD, 12601 chars)
  tasks: 2
  commits: 2
  plan_head_before: a5f546fa7d6dc7b3befe2c672d0a10ab37f7607e
  plan_head_after: 7e695552733b75b73b26dc485ae418e86fe6cc21

tech-stack:
  added: []           # test tooling only; shipping csproj untouched
  patterns: ["process-scoped UI Automation lookup (ProcessIdProperty) for owned windows and popup roots, instead of assuming a window sits inside the main window's subtree or is the first differently-named top-level window"]

key-files:
  created: []
  modified:
    - .planning/phases/02-audit-follow-ups/screenshots/capture.ps1

key-decisions:
  - "Task 1 (committed earlier as a5f546f) fixed the three op_Subtraction sites and added Find-RowButton, removing the positional backup fallback."
  - "Task 2 added Find-AppDialogWindow and Find-AppMenuItem and rewrote shots 08, 09, 11 and 13 per the plan's diagnosis."
  - "Found during this plan's own verification (not in 02-UAT.md's diagnosis): Shot-12's scan-window detection picked 'the first top-level window whose Name differs from the main window's' — on a shared desktop that can be any unrelated window, not the real Scan dialog. The screenshot still looked correct (Save-WindowShot always captures the main app window's own rect, which the real Scan window renders over regardless of which element the code thought it was), but every close attempt against the wrong element did nothing, leaking the Scan dialog into shots 13 and 14. Fixed by routing Shot-12's scan lookup through Find-AppDialogWindow (process-scoped, title-matched) like every other dialog shot, and by closing the window with WM_CLOSE sent directly to its own HWND (via PostMessage, added to the Win32 Native type) instead of UI Automation's Invoke pattern + SendKeys {ESC}, since SendKeys needs that window to hold input focus and an automated run on a shared desktop cannot guarantee that."
  - "One run (of five full runs taken while verifying this plan) saw shot 08 MISSING with 'no restore picker window appeared' on its first attempt; an immediate re-run passed cleanly with no code change, and two further repeats also passed — treated as a transient timing flake on a loaded shared desktop, not a capture.ps1 defect, since the lookup and wait logic are unchanged from the code that otherwise passed reproducibly five times in a row."
  - "A real external window (a Brave browser tab playing a YouTube video, already open on this desktop before this plan started) was captured in shot 13 on the very first full run, exactly as T-02-31 anticipates for a shared desktop; minimizing it was sufficient and no capture.ps1 change was needed for that part."

requirements-completed: [D-07]
gap_ids_closed: [G-02-7]

coverage:
  - id: D1
    description: "All fourteen D-07 shots capturable on the current Release build: 05/07 (arithmetic fix + DataItem row lookup), 08 (restore picker + confirmation), 11 (overflow menu item lookup), 13 (restore picker, not any top-level window)"
    requirement: "D-07"
    verification:
      - kind: other
        ref: "Five full fourteen-shot runs against the current Release build; the final three in a row printed 14 ok / 0 MISSING / 0 WARNING, and all fourteen images were read with the Read tool each time and matched their named states"
        status: pass
    human_judgment: true
    rationale: "Image correctness (does the screenshot actually show what its name says) can only be judged by looking at it; recorded in the shot table below."
  - id: D2
    description: "No positional fallback remains in any row lookup; ControlType.Custom row matching replaced by DataItem + nested Text everywhere (Find-RowButton)"
    requirement: "D-07"
    verification:
      - kind: other
        ref: "Plan's acceptance-criteria greps: ControlType]::Custom count 0, ControlType]::DataItem count >=1, Find-RowButton present, $backs[1] positional fallback count 0, ControlViewWalker present"
        status: pass
    human_judgment: false
  - id: D3
    description: "demo-data.ps1 and all app source untouched; this plan's commits change capture.ps1 only"
    requirement: "D-07"
    verification:
      - kind: other
        ref: "git diff --quiet 880442f -- demo-data.ps1 (exit 0); git status --porcelain shows only capture.ps1 modified by this plan's commits"
        status: pass
    human_judgment: false

duration: 70min
completed: 2026-11-24
status: complete
---

# Phase 02 Plan 17: capture.ps1 drives the restore picker, overflow menu and dialogs it opens Summary

**capture.ps1 now takes all fourteen D-07 shots against the current Release build: the three diagnosed element-lookup bugs (op_Subtraction arithmetic, row matching by generic Custom-element Name instead of the nested display-name Text, and a main-window button search for a flyout menu item) are fixed, plus two more found at planning time (the restore picker's own confirmation vs. the per-game one, and shot 09's demo reset emptying the backup shot 13 needs) and one found only while verifying this plan's own run (Shot-12's scan-window detection could grab an unrelated top-level window on a shared desktop and then fail silently to close it, leaking the Scan dialog into shots 13 and 14).**

## Performance

- **Duration:** ~70 min (this session; Task 1 was committed in an earlier session)
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- `Find-RowButton` ($Root, $DisplayName, $ButtonName): matches a row by its `ControlType.DataItem` having a nested `ControlType.Text` descendant with the exact display name, then finds the button inside that same DataItem. No positional fallback. Used by `Backup-SmallGameRow`, Shot-08 and Shot-13.
- Three `op_Subtraction` sites fixed by parenthesizing each arithmetic `New-Object`/`Bitmap`/`Rectangle` argument (Save-ElementShot's Bitmap width/height, Shot-07's Bitmap height and strip Rectangle y).
- `Find-AppDialogWindow` ($TitlePrefix): searches the SavegameBunker process's own top-level elements (and their `ControlType.Window` descendants) for a Name starting with the prefix — used for the restore picker, Change Save Folder dialog, and (added this session) the Scan window.
- `Find-AppMenuItem` ($Text): same process-scoped search for a `ControlType.MenuItem` whose own Name or nested Text matches — used for the overflow flyout's "Change save folder" item, which renders in its own popup root outside the main window's subtree.
- Shot-08: waits for the restore picker titled `Restore — Large Save Game`, selects the first (newest) archive via `SelectionItemPattern`, clicks the picker's own Restore, then polls for the main window's per-game confirmation (detected by a `Cancel` button appearing) and accepts the **last** `Restore` button in the main window's descendants (the confirmation's, since `RootDialogHost` is the root Grid's last child and so sorts after every row button).
- Shot-11: looks up "Change save folder" via `Find-AppMenuItem`, waits for the dialog, screenshots it, then cancels and waits for it to close.
- Shot-13: waits for the picker titled `Restore — Small Save Game` via `Find-AppDialogWindow` (not "any other named top-level window"), screenshots it, cancels, waits for it to close.
- Shot-09: repeats the prologue's Small Save Game backup after the demo reset's relaunch, so shot 13 has an archive to restore.
- Shot-12 (found during this plan's own verification, not in 02-UAT.md): now finds the Scan window via `Find-AppDialogWindow 'Scan'` instead of "the first top-level window with a different name", and closes it by sending `WM_CLOSE` to its own window handle (`PostMessage`, added to the `Win32.Native` P/Invoke type) instead of UI Automation's Invoke pattern plus `SendKeys {ESC}` — both of which need that window to hold input focus, which an automated run on a shared desktop cannot guarantee.

## Task Commits

1. **Task 1: Shots 05 and 07 end to end, DataItem row lookup** - `a5f546f` (fix) — committed in an earlier session
2. **Task 2: Shots 08, 11, 13 drive the windows they open; full run passes** - `7e69555` (fix)

## Files Created/Modified

- `.planning/phases/02-audit-follow-ups/screenshots/capture.ps1` — see Accomplishments above

## Decisions Made

See `key-decisions` in the frontmatter.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - defect exposed by the run, not in the plan's diagnosis] Shot-12's scan-window detection and close were acting on the wrong window**
- **Found during:** Task 2's own full-run verification (first full run: shot 13's screenshot showed an unrelated Brave/YouTube window; a second full run, after minimizing that window, showed shots 13 and 14 both with the Scan dialog still visibly open behind the intended content)
- **Issue:** `foreach ($top in $all) { if ($top.Current.Name -and $top.Current.Name -ne $win.Current.Name) { $scan = $top; break } }` picks the *first* top-level window (enumeration order, not identity) whose name differs from the main window's. On a shared desktop with other windows open, this can be any of them — not the real Scan window. The subsequent close logic (Invoke the Cancel button found inside `$scan`, then `SendKeys {ESC}`, then check whether a window named `$scanName` still exists) then operated on that wrong element: the Cancel-button lookup inside it silently returned nothing, `SendKeys {ESC}` went to whatever actually held focus, and the "is it still there" check trivially passed since the wrong element's name was never going to reappear — so the loop reported success while the real Scan dialog stayed open the whole time, bleeding into shots 13 and 14's screenshots.
- **Fix:** Scan detection now calls `Find-AppDialogWindow 'Scan'` — the same process-scoped, title-matched lookup every other dialog shot uses — instead of the ad hoc "first differently-named top-level window" scan. Closing now sends `WM_CLOSE` directly to the found window's own `NativeWindowHandle` via a new `PostMessage` P/Invoke on `Win32.Native`, confirmed by polling `Find-AppDialogWindow 'Scan'` until it returns null (throwing if it does not close within 6s) — this does not depend on input focus the way Invoke/SendKeys did.
- **Files modified:** `.planning/phases/02-audit-follow-ups/screenshots/capture.ps1` (Shot-12ScanFooter only)
- **Verification:** two full fourteen-shot runs after the fix both printed 14 ok / 0 MISSING / 0 WARNING; shots 13 and 14 were read with the Read tool both times and show only the intended content with no Scan dialog bleed-through.
- **Committed in:** `7e69555` (part of Task 2's single commit; this was found and fixed before that commit, not as a follow-up)

**2. [Not a capture.ps1 defect — shared-desktop occlusion, anticipated by T-02-31] A real external window appeared in shot 13 on the first full run**
- **Found during:** Task 2's first full-run verification
- **Issue:** A Brave browser window playing a YouTube video, already open on this desktop before this plan started, was captured in place of the restore picker in one shot. `Save-WindowShot`'s own comment already documents this exact failure mode (`CopyFromScreen copies whatever is topmost on screen... another window can occlude it between shots`) and its mitigation (`SetForegroundWindow` before capturing); this one case evidently raced past that mitigation.
- **Fix:** Minimized the Brave window (not closed — it is the user's own window, unrelated to this plan) before retaking. No capture.ps1 change was needed or made for this; the existing `SetForegroundWindow` mitigation is unchanged.
- **Verification:** every subsequent full run (four more) showed no external-window content in any of the fourteen images.
- **Committed in:** n/a — no code change.

### Noted, not fixed

**Shot 08 MISSING on one of five full runs ("no restore picker window appeared for Large Save Game")**
- One full run (the first run after the Shot-12 fix) reported this; three further full runs (one immediately after, two more afterward) all passed shot 08 cleanly with no code change. Treated as a transient timing flake on a loaded shared desktop (this session was also running `dotnet build`/`dotnet test` concurrently in places), not a logic defect — the lookup and 10s poll window are unchanged from code that otherwise passed reproducibly.

---

**Total deviations:** 1 auto-fixed (Rule 1, a genuine capture.ps1 defect this plan's own run exposed), 1 non-code environmental issue (noted and worked around), 1 transient flake (noted, not reproduced on repeat).
**Impact on plan:** No scope creep — both code changes stayed inside `Shot-12ScanFooter` and the `Win32.Native` P/Invoke block, consistent with the plan's own "fix it in capture.ps1 and repeat the full run" deviation rule.

## Issues Encountered

See Deviations above.

## Shot table (final verified full run)

| Shot | Result | What the image shows |
|------|--------|----------------------|
| 01-main-default | ok | Main window, default theme, four demo games |
| 02-main-other-theme | ok | Main window, alternate theme |
| 03-toolbar-alt | ok | Toolbar with Alt held (access-key underlines) |
| 04-overflow-menu | ok | Row's overflow (More actions) flyout open |
| 05-row-actions-idle | ok | One row's three idle action buttons (Actions cell crop) |
| 06-busy-bulk | ok | Bulk backup in progress ("Backing up 1 of 2 — Large Save Game") |
| 07-statusbar | ok | Status bar strip after "Backed up all 2 games." |
| 08-restore-steps | ok | Main window mid-restore ("Restoring Large Save Game...") |
| 09-empty-state | ok | Empty-list state after demo reset ("No games yet") |
| 10-addgame-footer | ok | Add game dialog with its footer |
| 11-changesavefolder-footer | ok | Change Save Folder dialog (reached via the overflow menu item) |
| 12-scan-footer | ok | Scan dialog ("No known games with save data found") |
| 13-restorepicker-footer | ok | Restore — Small Save Game picker, one backup listed, footer |
| 14-close-hover | ok | Main window with the close button hovered (red highlight) |

Shot 05 crops the Actions cell (the StackPanel holding Back up / Restore / More actions), not the whole row — matching the plan's preferred outcome.

## Known Stubs

None.

## Threat Flags

T-02-31 (Information Disclosure via shared-desktop screen capture) was directly observed once (the Brave/YouTube window in shot 13's first attempt) and handled per its own mitigation: the image was discarded and retaken, never committed, and the run output stayed in the gitignored `.temp/` directory and was deleted after verification. T-02-32 and T-02-SC: unaffected — no change to `demo-data.ps1` or any NuGet reference.

## User Setup Required

None.

## Next Phase Readiness

- G-02-7's script half is closed: `capture.ps1` can take all fourteen D-07 shots against the current Release build, reproducibly (confirmed over the final three of five full runs).
- Plan 02-18 can now run `capture.ps1` to produce the actual `before/`, `after-19-20/` and `after-21/` screenshot sets it needs.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-24*

## Self-Check: PASSED

- `capture.ps1` confirmed present on disk with the expected functions (`Find-RowButton`, `Find-AppDialogWindow`, `Find-AppMenuItem`) and no `ControlType]::Custom` row matching remaining.
- Both task commits (`a5f546f`, `7e69555`) confirmed as ancestors of HEAD.
- All plan acceptance-criteria greps (both tasks) re-verified as passing.
- Plan-level `<verification>`: AST check 0, parse errors 0; final full run 14 ok / 0 MISSING / 0 WARNING; all fourteen images read and matched their names; `demo-data.ps1` and app source untouched (`git diff --quiet 880442f` exit 0); only `capture.ps1` changed by this plan's commits.
