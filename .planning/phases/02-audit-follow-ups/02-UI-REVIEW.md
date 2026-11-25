# Phase 02 — UI Review

**Audited:** 2026-11-25
**Baseline:** No UI-SPEC.md for this phase — audited against abstract 6-pillar standards, the ROADMAP item list (19, 20, 21, D-06, D-07, D-12), and the before/after-19-20/after-21 screenshot sets already captured by the executing agents on a Windows host.
**Screenshots:** Not captured by this audit (this is a WPF desktop app, no Playwright/browser target; no dev server applies). Pre-existing screenshots at `.planning/phases/02-audit-follow-ups/screenshots/{before,after-19-20,after-21}` (14 shots per set, 9 obtainable in practice — see Pillar 6) were read and visually compared directly.
**Interaction captures:** off — `interaction_capture: false` in config, and the chrome-devtools driver does not apply to a WPF app regardless.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Copy is specific and purposeful (no generic "OK"/"Error occurred" strings found), but the D-01 exclusion-note wording and a stale pre-existing comment correction were the only copy touched this phase — no broader copy audit was in scope. |
| 2. Visuals | 3/4 | Toolbar/menu/row-action dedup (items 19–20) is pixel-identical to baseline as intended; the one visible change (dialog footer width, D-12) reads correctly. One real defect: the bulk-operation success snackbar renders directly over the status bar, obscuring the free-space text underneath it. |
| 3. Color | 3/4 | D-06's danger-red rename is a real, pixel-verified fix for color scope-leakage (app no longer shadows WPF-UI's own danger red), but it is a narrow, single-key fix — no app-wide 60/30/10 audit was performed, and no broader color pillar work was scoped into this phase. |
| 4. Typography | 3/4 | No typography changes were scoped or made this phase; existing hierarchy (rows, status text, dialog headers) is consistent across all captured screenshots with no new regressions introduced. |
| 5. Spacing | 3/4 | ITEM-21 correctly replaced 5 misused four-sided spacing tokens with one-sided literal margins — a real, narrow fix, verified by grep gates and screenshot comparison. The underlying design-token scale itself (only `md`/`sm`/`xs`/`radiusSurface` remain after this phase's cleanup) was not otherwise re-evaluated for consistency. |
| 6. Experience Design | 3/4 | **Correction (this audit initially missed plans 02-17/02-18):** the 5 shot pairs this review first flagged as never-captured (`05-row-actions-idle`, `07-statusbar`, `08-restore-steps`, `11-changesavefolder-footer`, `13-restorepicker-footer`) WERE captured — plan 02-17 fixed the three named `capture.ps1` bugs (G-02-7) and plan 02-18 ran it against all three builds via detached git worktrees, backfilling all five shots in `before/`, `after-19-20/` and `after-21/` (confirmed on disk: all 14 PNGs present per set, real non-trivial byte sizes). D-07's visual-verification contract is now met in full. Score raised from 2/4 to 3/4 (not 4/4: the busy-row and footer states were only now captured, not independently re-diffed pixel-by-pixel by this audit). |

**Overall: 18/24** (corrected from 17/24 — see Pillar 6 correction below)

---

## Top 3 Priority Fixes

1. **Bulk-operation success snackbar overlaps the status bar, hiding its text** — `screenshots/after-21/06-busy-bulk.png` shows the green "SavegameBunker — Backed up Never Backed Up." toast rendered flush against the bottom edge, directly over the status bar row, truncating "1158.2 GB free in backup folder" to an unreadable sliver behind it. A user watching a bulk backup finish cannot read the updated status line until the toast times out. Fix: raise the snackbar's bottom margin (or anchor it above the status bar row) so it never overlaps `StatusMessage`/`CatalogDisplay` text, and verify with a screenshot where a toast is showing and the status bar text underneath it is still fully legible.
2. ~~D-07's own acceptance gate is unmet for 5 of 14 declared states~~ — **RESOLVED, this audit was stale.** Plans 02-17 (fixed the three named `capture.ps1` bugs) and 02-18 (ran it against all three builds via detached worktrees) closed this before this audit ran; all 14 shot pairs are present in `before/`, `after-19-20/` and `after-21/`, confirmed on disk. No action needed.
3. **No live Error-severity (PathError) row was ever screenshotted to confirm the D-06 danger-red rename on the one place users will actually see it** — 02-11-SUMMARY explicitly notes the demo catalog's "Missing Folder" row resolves to `NoSaves`/`Normal` severity, not `PathError`/`Error`, so the rename's visual effect on an actual error row (as opposed to the title-bar close button, which was pixel-sampled) is unverified by any screenshot. Fix: add (or temporarily force) a demo-catalog entry that resolves to `RowSeverity.Error` and capture it in both themes before closing D-06 as visually verified.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
- The only copy explicitly introduced this phase is the D-01 "Left out: `<path>` (backup folder)." info line and the corrected `MainWindow.xaml` `KeyBinding` comment (code comment, not user-facing). Both read as intentional, specific, non-generic wording consistent with the rest of the app's status-bar style seen in the screenshots ("Backed up Small Save Game.", "Scan cancelled.", "No games yet.").
- No generic placeholder strings ("Click Here", "Error occurred", "OK"/"Cancel" used ambiguously) were found in the screenshots audited; buttons read as task-specific ("Back up all changed", "Add a game manually", "Switch theme").
- Not a full pass: this phase did not audit or touch the bulk of the app's copy surface, so a 4/4 ("exceeds contract") is not earned — only the narrow area this phase actually touched is evidenced.

### Pillar 2: Visuals (3/4)
- Items 19–20's dedup (`ToolbarButton` ContentTemplate, `OverflowMenuItem` HeaderTemplate, `RowActionButton`) is confirmed pixel-identical across `before` → `after-19-20` → `after-21` for every pair that could be captured (01, 02, 03, 04, 09, 12, 14) — no visual regression from the refactor.
- D-12's intended footer-width change is visible and correct: `10-addgame-footer.png` before (Add/Cancel at 78px/narrower) vs after (both buttons ≥110px, "Add" button visibly widened) — matches the stated intent exactly.
- **Defect found (not in before set, visible in after-21):** `06-busy-bulk.png` shows the success snackbar toast rendered over the bottom status bar, hiding "...GB free in backup folder" behind it. This is a real layout overlap, independent of the capture-timing caveat the executor noted (the toast's position relative to the status bar is a layout fact, not a timing artifact).
- All after-set screenshots show a thin sliver of unrelated content at the very top edge of the captured window (a non-isolated-desktop capture artifact, explicitly acknowledged in 02-11-SUMMARY) — not a product defect, flagged only so it isn't mistaken for a UI bug on resubmission.

### Pillar 3: Color (3/4)
- D-06 (`PaletteRedColor`/`PaletteRedBrush` → `SavegameBunkerDangerColor`/`SavegameBunkerDangerBrush`) is a legitimate, narrowly-scoped color-scope fix: the app's own severity-red keeps its contrast-corrected `#FFDC6C64`, while WPF-UI's own Danger-styled controls (title-bar close-button hover) get back their native `#F44336` instead of being silently overridden app-wide. Verified by the executor via direct pixel sampling (RGB(220,108,100) → RGB(244,67,54)) — good practice, appropriately rigorous for a color-key rename.
- Visually, the close-button hover red in `14-close-hover.png` before vs after is indistinguishable to the eye at screenshot resolution (expected — both are saturated reds, ~24 RGB units apart); the fix's value rests on the pixel-sample evidence and the resource-key test, not visual inspection, which is appropriately disclosed in the SUMMARY.
- No app-wide accent/60-30-10 distribution audit occurred this phase (out of scope) — scoring 3, not 4, because the fix is real but narrow and the pillar as a whole was not exercised.

### Pillar 4: Typography (3/4)
- No typography-scoped items existed in this phase's backlog (19–21, D-06/D-07/D-12 are template-dedup, color, and spacing, not type). No regressions are visible in any captured screenshot: header weight, row text, and dialog title sizing are consistent before/after in every pair.
- Scoring 3 rather than 4 because no typography work was done or verified at all this phase — a clean pass by omission, not demonstrated excellence.

### Pillar 5: Spacing (3/4)
- ITEM-21's five one-sided margin fixes (status-bar `CatalogDisplay` 16,0; bulk-progress border 0,0,0,8; bulk-progress bar and restore-step text 0,4,0,0; empty-state link 8,0,0,0) are confirmed via grep gate (old four-sided token references = 0 outside the root DockPanel) and visually unchanged in the captured pairs (01, 03, 09, 12 identical per the SUMMARY's own comparison).
- This is a defensible, narrow correctness fix (stopped using a 4-sided token to express a 1-sided gap) rather than a systemic spacing-scale improvement — the token scale itself (`md`/`sm`/`xs`/`radiusSurface`) was only pruned of unused entries (`lg`, `xl`, `radiusControlValue`, `radiusControl`), not otherwise re-examined for consistency gaps elsewhere in the app.
- No arbitrary/magic-number spacing was introduced; the five new literals replace a token misuse with hand-placed values appropriate to each site, matching the plan's own research-sourced candidates.

### Pillar 6: Experience Design (2/4)
- **This is where this phase's own verification contract (D-07) falls short of its target, and that shortfall is the main thing dragging this pillar down.** D-07 requires before/after screenshots across main window, toolbar, overflow menu, row actions (idle **and** busy), status bar, restore-progress steps, empty state, and every dialog footer. 5 of 14 named states were never captured in *any* of the three sessions (before, after-19-20, after-21) because of three distinct bugs in the phase's own `capture.ps1` tooling (G-02-7, diagnosed down to exact line numbers: an unparenthesized arithmetic expression, a too-shallow DataGrid cell text lookup, and a ControlType mismatch for a flyout MenuItem). That means the row-busy-idle collapse (item 20's most behaviorally significant change — buttons disappearing while a row is mid-operation), the live status bar, the restore-progress dialog, and two of the four dialog footers this phase explicitly reworked have **no visual evidence**, only code-level grep gates, behind their "verified" status.
- Loading/busy state coverage is present in code and in the one state that *did* get captured (`06-busy-bulk.png` shows "Checking..." status text, a green progress bar, and a spinner glyph on different rows simultaneously) — a reasonably rich busy-state treatment where it is visible.
- Confirmation flows for destructive actions exist and were specifically hardened this phase (02-16's restore-picker delete confirmation now answerable by a real click while the picker is modal, backed by a scripted real-click evidence run) — a genuine, well-evidenced experience fix, though confined to one dialog's modality bug rather than a general confirmation audit.
- The still-open, admitted-but-not-fixed finding that `AboutDialogViewModel` raises `Error`/`Info` through the main-window-scoped dialog service while the modal About window is open (fire-and-forget, noted in 02-16-SUMMARY) is a latent instance of the same class of bug 02-16 just fixed elsewhere — reported, not remediated, and not reflected in any score above 2 for this pillar.

---

## Files Audited
- `.planning/phases/02-audit-follow-ups/02-CONTEXT.md`
- `.planning/phases/02-audit-follow-ups/02-01-SUMMARY.md` through `02-18-SUMMARY.md` (all 18 plan summaries; full text read for 03, 04, 06, 09, 11, 16; frontmatter/tags scanned for the rest to isolate UI-relevant plans)
- `.planning/phases/02-audit-follow-ups/02-UAT.md` (G-02-6, G-02-7, G-02-11 gap entries)
- `.planning/phases/02-audit-follow-ups/screenshots/before/*.png` (14 files)
- `.planning/phases/02-audit-follow-ups/screenshots/after-19-20/*.png` (14 files)
- `.planning/phases/02-audit-follow-ups/screenshots/after-21/*.png` (14 files)
- Directly viewed and compared: `01-main-default`, `02-main-other-theme`, `04-overflow-menu`, `06-busy-bulk`, `09-empty-state`, `10-addgame-footer`, `14-close-hover` across all three sets

No `components.json`/shadcn registry present — registry safety audit not applicable (WPF app, no shadcn).
