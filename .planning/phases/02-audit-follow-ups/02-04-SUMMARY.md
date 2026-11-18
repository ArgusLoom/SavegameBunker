---
phase: 02-audit-follow-ups
plan: 04
subsystem: ui
tags: [xaml, wpf-ui, styles, dedup, d-07, d-12]

requires:
  - phase: 02-audit-follow-ups (Plan 02-03)
    provides: the capture tooling (demo-data.ps1, capture.ps1) and the fourteen-shot `before/` baseline contract
provides:
  - one ToolbarButton ContentTemplate replacing ten copied six-line button contents (icon via Tag, label via Content)
  - one OverflowMenuItem HeaderTemplate replacing three copied 13-line menu headers (glyph via Tag, text via Header)
  - one RowActionButton style (BasedOn IconActionButton) collapsing the row Backup/Restore buttons while the row is busy
  - one DialogFooterButton style (MinWidth 110) shared by all eight dialog footer buttons
affects: [02-11, end-of-phase verification, D-07 capture runs]

actuals:
  tokens: 8756        # chars/4 over the realized diff (35026 chars across the three task commits)
  tasks: 3
  commits: 3          # measured: gsd-plan-head-before-02-04 (eb2f6d4)..HEAD

tech-stack:
  added: []           # zero packages; the shipping csproj keeps its two PackageReferences (T-02-SC)
  patterns: [geometry-through-Tag, label-through-Content, shared DataTemplate in a style setter, keyed style replacing inline property-element styles]

key-files:
  created: []
  modified:
    - src/SavegameBunker/Themes/Styles.xaml
    - src/SavegameBunker/MainWindow.xaml
    - src/SavegameBunker/Views/AddGameDialog.xaml
    - src/SavegameBunker/Views/ChangeSaveFolderDialog.xaml
    - src/SavegameBunker/Views/ScanWindow.xaml
    - src/SavegameBunker/Views/RestorePickerWindow.xaml

key-decisions:
  - "Styles order preserved load-bearing: ToolbarButton now sits below ToolbarButtonLabel and OverflowMenuItem below OverflowMenuGlyphPath, so every StaticResource the templates name is defined above them (a downward reference fails at runtime, not at build)."
  - "Both new templates bind only what differs (Tag for geometry, Content/Header for text); text stays DynamicResource and geometry stays StaticResource at every use site, byte-identical to before."

requirements-completed: [ITEM-19, ITEM-20, D-12]   # D-07 stays open until the owner's capture runs land the before/after-19-20 pairs

coverage:
  - id: D1
    description: "One ToolbarButton ContentTemplate renders all ten toolbar buttons (icon geometry via Tag, label via Content)"
    requirement: ITEM-19
    verification:
      - kind: other
        ref: "dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental → 0 Warning(s), 0 Error(s)"
        status: pass
      - kind: other
        ref: "grep gates: AccessText=0, ToolbarGlyph-in-MainWindow=0, ToolbarButton=10, ContentTemplate≥1 (all pass)"
        status: pass
      - kind: manual_procedural
        ref: "screenshots/before vs after-19-20 01-03 + access-key pass — pending on the owner's Windows machine"
        status: unknown
    human_judgment: true
    rationale: "Rendering and Alt-mnemonic behaviour are only observable in the running WPF app; this host is Linux."
  - id: D2
    description: "One OverflowMenuItem HeaderTemplate renders the three overflow items; RowActionButton collapses row Backup/Restore while the row IsBusy"
    requirement: ITEM-20
    verification:
      - kind: other
        ref: "dotnet build Release → 0/0; grep gates: MenuItem.Header=0, Button.Style=0, RowActionButton=2, Click handlers=3 (all pass)"
        status: pass
      - kind: manual_procedural
        ref: "screenshots/before vs after-19-20 04-06 + menu-click pass — pending on the owner's Windows machine"
        status: unknown
    human_judgment: true
    rationale: "Visual identity of templated XAML has no automated check in this repository."
  - id: D3
    description: "One DialogFooterButton style (MinWidth 110) shared by the eight footer buttons of the four dialogs — the one intended visible change (D-12)"
    requirement: D-12
    verification:
      - kind: other
        ref: "dotnet build Release → 0/0; grep gates: DialogFooterButton uses=8, remaining 80/90/110 MinWidths=2 (Browse only), key defined=1 (all pass)"
        status: pass
      - kind: manual_procedural
        ref: "screenshots/before vs after-19-20 10-13 — pending on the owner's Windows machine"
        status: unknown
    human_judgment: true
    rationale: "D-07: the owner reviews the image pairs; footer width is the only allowed difference."

duration: 3min
completed: 2026-11-18
status: complete
plan_head_before: eb2f6d4c9e8aaf1baaf5b5192818efe3cf1702de
plan_head_after: 5fdd98a58b33af8222711e7fbb0244cf85d55296
---

# Phase 02 Plan 04: Items 19 and 20 — presentation dedup Summary

**Four shared XAML styles replace ten toolbar content blocks, three menu headers, two inline row-action styles and four per-dialog footer widths — pure presentation, no visible change except the D-12 footer width.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-11-18T20:11:34Z
- **Completed:** 2026-11-18T20:13:44Z (SUMMARY written ~20:20Z)
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- `ToolbarButton` gained a `ContentTemplate` setter (vertical StackPanel: Viewbox/ToolbarGlyph + Path bound to the button's `Tag`, AccessText bound to `Content`); all ten toolbar buttons in MainWindow.xaml now state only `Content` + `Tag`, with every other attribute byte-identical. The style moved below `ToolbarButtonLabel` so its template's StaticResources resolve upward.
- `OverflowMenuItem` gained a `HeaderTemplate` setter (16×16 glyph bound to `Tag`, TextBlock bound to `Header`) and moved below `OverflowMenuGlyphPath`; the three overflow menu items state only `Header` + `Tag`, `Click` handlers and the hazard comments untouched.
- New `RowActionButton` (BasedOn `IconActionButton`) carries the IsBusy→Collapsed DataTrigger; both inline `<ui:Button.Style>` blocks on the row Backup/Restore buttons replaced by the attribute. The DropDownButton keeps `IconActionButton` and its own `IsEnabled` binding.
- New `DialogFooterButton` (BasedOn `{x:Type ui:Button}`) with `MinWidth 110` — the width that fits "Update folder"; all eight footer confirm/cancel buttons across the four dialogs use it and their 80/90/110 MinWidths are gone. Browse buttons and ScanWindow's All/None/Details buttons untouched.

## Access-key table (D-07) — PENDING on the owner's Windows machine

This host is Linux (no WPF app, no interactive desktop), so the running-app access-key pass could not be executed. Expected behaviour per key, to be observed by the owner on the demo catalog (`%TEMP%\SavegameBunkerDemo\games.json`):

| Key | Button | Expected | Observed |
|-----|--------|----------|----------|
| Alt+A | Add game | Add game dialog opens | [pending] |
| Alt+S | Scan | Scan starts | [pending] |
| Alt+R | Refresh | Status line shows the reload/checking message | [pending] |
| Alt+O | Open the games list | Catalog opens in the default editor | [pending] |
| Alt+C | Change backup folder | Backup-folder picker opens (Esc to close) | [pending] |
| Alt+B | Back up selected | Bulk backup starts after selecting a row | [pending] |
| Alt+T | Restore selected | Restore-selected confirmations appear (decline) | [pending] |
| Alt+H | Help | Help guide opens | [pending] |

Switch theme and Back up all changed carry no underscore key — they must render as plain text with no underlined letter: [pending].

## Per-pair verdicts against `before/` — PENDING

The `after-19-20/` capture set does not exist yet (same pending state as 02-03's `before/` set — the capture runs execute on the owner's Windows machine via `capture.ps1 -OutDir .planning/phases/02-audit-follow-ups/screenshots/after-19-20`, first a Release build). Planned verdicts, one line per pair, will record *identical* / *footer width only* / *other* per pair; every one is [pending] until that run:

01-main-default, 02-main-other-theme, 03-toolbar-alt, 04-overflow-menu, 05-row-actions-idle, 06-busy-bulk, 07-statusbar, 08-restore-steps, 09-empty-state, 10-addgame-footer, 11-changesavefolder-footer, 12-scan-footer, 13-restorepicker-footer, 14-close-hover — all [pending]. Expected: 01–09 and 14 identical; 10–13 differ only in footer button width (all footer buttons at least 110 wide).

## Task Commits

1. **Task 1: one content template for the toolbar buttons** - `b3f4310` (refactor) [ITEM-19]
2. **Task 2: share the overflow header and the row action style** - `826a76e` (refactor) [ITEM-19,ITEM-20]
3. **Task 3: one footer button style for every dialog** - `5fdd98a` (refactor) [D-12]

## Files Created/Modified

- `src/SavegameBunker/Themes/Styles.xaml` — ToolbarButton ContentTemplate (style relocated below ToolbarButtonLabel), OverflowMenuItem HeaderTemplate (relocated below OverflowMenuGlyphPath), new RowActionButton, new DialogFooterButton
- `src/SavegameBunker/MainWindow.xaml` — ten toolbar buttons reduced to Content+Tag; three menu items reduced to Header+Tag; two inline row-button style blocks replaced by the RowActionButton attribute
- `src/SavegameBunker/Views/AddGameDialog.xaml` — both footer buttons use DialogFooterButton
- `src/SavegameBunker/Views/ChangeSaveFolderDialog.xaml` — both footer buttons use DialogFooterButton
- `src/SavegameBunker/Views/ScanWindow.xaml` — both footer buttons use DialogFooterButton
- `src/SavegameBunker/Views/RestorePickerWindow.xaml` — both footer buttons use DialogFooterButton

## Decisions Made

- Followed the plan's style-relocation requirement literally (Templates reference upward only), documenting the runtime-XamlParseException rationale in the new comments.
- Nothing staged under `screenshots/` — `git status --porcelain .planning/phases/02-audit-follow-ups/screenshots` is empty (no images exist yet on this host).

## Deviations from Plan

None — plan executed exactly as written. The plan's own headless fallback (action step 5 of Task 1) was applied, not a deviation: 02-03's SUMMARY records that no interactive desktop exists on this host, so Tasks 1–3's capture/click/access-key steps are recorded as pending for the owner above.

## Issues Encountered

- `dotnet test SavegameBunker.sln -c Release` aborts on this host with "No frameworks were found" (Microsoft.WindowsDesktop.App 10.0 is not installable here) — the suite runs, including ResourceKeyTests, on the owner's machine / CI. Recorded as pending, not a failure.
- The `ls screenshots/after-19-20/*.png` acceptance gate cannot pass on this host by design; the capture runs are the owner's pending step (see verdict table above).

## Known Stubs

None — no stub code was introduced; the pending items are execution steps (captures, test run, access-key pass), not unfinished code.

## Threat Flags

None — no new network endpoints, auth paths, file access or schema changes; the shipping csproj keeps exactly two PackageReferences (T-02-SC) and the app was never launched on this host (T-02-28 moot here).

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Plan 02-11 edits the same XAML files and can build on the four new shared styles; it must rebase on this plan's commits to avoid conflicting edits.
- Owner's pending Windows steps (all D-07): capture `before/` and `after-19-20/` sets with capture.ps1 (Release build first), compare pairs, run the access-key table and the menu-click pass, then record the verdicts into this SUMMARY's two pending tables.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
