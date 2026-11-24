---
phase: 02-audit-follow-ups
reviewed: 2026-11-24T00:00:00Z
depth: standard
files_reviewed: 17
files_reviewed_list:
  - CHANGELOG.md
  - README.md
  - docs/screenshot.png
  - docs/userguide/de/docs/getting-started.md
  - docs/userguide/de/docs/how-to/back-up-your-saves.md
  - docs/userguide/de/docs/how-to/change-the-backup-folder.md
  - docs/userguide/de/docs/how-to/move-to-a-new-pc.md
  - docs/userguide/de/docs/how-to/restore-a-save.md
  - docs/userguide/de/docs/reference/keyboard-shortcuts.md
  - docs/userguide/en/docs/getting-started.md
  - docs/userguide/en/docs/how-to/back-up-your-saves.md
  - docs/userguide/en/docs/how-to/change-the-backup-folder.md
  - docs/userguide/en/docs/how-to/move-to-a-new-pc.md
  - docs/userguide/en/docs/how-to/restore-a-save.md
  - docs/userguide/en/docs/reference/keyboard-shortcuts.md
  - src/SavegameBunker/Themes/Strings.en.xaml
  - tests/SavegameBunker.Tests/MainViewModelRestoreTests.cs
findings:
  critical: 0
  warning: 3
  info: 0
  total: 3
  necessity: 0
status: issues_found
---

# Phase 02-audit-follow-ups: Code Review Report

**Reviewed:** 2026-11-24T00:00:00Z
**Depth:** standard
**Files Reviewed:** 17
**Status:** issues_found

## Summary

Reviewed the toolbar-label-shortening change: three `Strings.en.xaml` labels and their Alt
mnemonics, two restore pre-flight error strings, one test assertion, and README/CHANGELOG/EN+DE
user-guide prose plus the re-captured screenshot.

The three renamed keys themselves are correct: each `_access` variant is still its clean sibling
plus exactly one inserted underscore (`Backup _folder`, `_Back up`, `Res_tore`), matching the
invariant `StringsTests.cs` enforces, and none of the three new mnemonics (`F`, `B`, `T`) collides
with `A`/`S`/`R`/`O` or with each other. The two restore-pre-flight strings and the one test
assertion that quoted the old "Change backup folder" button name were updated consistently, and
every EN/DE user-guide page and the README were updated to the same three new names with no
leftover "Back up selected" / "Restore selected" / "Change backup folder" text in rendered
copy — `grep` across the whole repo confirms the only surviving instances of the old names are
internal code comments and feature/class names (`BackupSelectedCommand`,
`BulkBackupCoordinator`, `"Back up selected"` as a section label in test files), which name the
*feature*, not the button text, and were correctly left alone.

Three issues surfaced, none introduced by this diff but all squarely inside the explicit checks
this review was asked to make (mnemonic collisions, stale quoted names) and all inside the files
reviewed (two of the three) or directly caused by this diff's scope without being carried through
to a document CLAUDE.md names as authoritative (the third). `necessity-review.md` was read; none
of the changes in this file set are guard/defensive code, so no necessity findings apply.

## Warnings

### WR-01: Keyboard-shortcuts reference still documents "Alt, H → Help" for the renamed About button

**File:** `docs/userguide/en/docs/reference/keyboard-shortcuts.md:14`
**File:** `docs/userguide/de/docs/reference/keyboard-shortcuts.md:14`
**Issue:** Both the English and German keyboard-shortcuts pages list `Alt, H` → `Help`. The toolbar
button that row describes no longer exists under that name or that letter: `MainWindow.xaml:46-54`
binds it to `AboutCommand` with `Content="{DynamicResource str_toolbar_about_access}"`, and
`Strings.en.xaml:49` defines `str_toolbar_about_access` as `A_bout` — mnemonic `B`, not `H`. This
predates the current diff (the About-for-Help swap shipped earlier, per the `[Unreleased]`
CHANGELOG entry), but both files are in this phase's explicit review scope and the task asked
specifically to check for stale quoted button names, so it is reported here. A reader following
this reference will press Alt+H expecting the user-guide link and get nothing.
**Fix:** Change the row in both files to `**Alt, B** | **About**` (or drop the row's claim about a
`Help` action entirely), matching `str_toolbar_about_access`.

### WR-02: Alt+B mnemonic collision between "Back up" and "About" (same AccessKeyManager scope)

**File:** `src/SavegameBunker/Themes/Strings.en.xaml:47` (`str_toolbar_backUpSelected_access` = `_Back up`)
**File:** `src/SavegameBunker/Themes/Strings.en.xaml:49` (`str_toolbar_about_access` = `A_bout`)
**Issue:** WPF access keys are case-insensitive, so `_Back up` (mnemonic `B`) and `A_bout`
(mnemonic `b`) are the same access key. `MainWindow.xaml` has no `AccessKeyScope` boundary between
the two buttons — `AboutButton` (line 46), `BackupSelectedButton` (line 134) and every other
toolbar button sit in the same outer `DockPanel`/`WrapPanel`, which share one
window-level `AccessKeyManager` scope. With a selection active (so `BackupSelectedButton` is
visible per `HasAnySelected`), Alt+B cannot reliably invoke either button — WPF's duplicate-access-key
handling cycles focus between matches rather than invoking one outright, the exact failure
`StringsTests.The_seven_toolbar_mnemonics_are_all_different_letters` exists to catch for the other
seven buttons. That test's own `accessKeys` array (`StringsTests.cs:153-162`) deliberately omits
`str_toolbar_about_access`, so this pair is untested. This is not a regression from this diff — the
previous labels (`_Back up selected` / `A_bout`) collided on the same two letters — but it is a
live collision inside the exact check this review was asked to make, newly adjacent to the three
renamed keys reviewed here.
**Fix:** Either add `str_toolbar_about_access` to the uniqueness test's array (so the real letter
set is enforced) and pick a different mnemonic for one of the two buttons — e.g. `Ab_out` (`O` is
already taken by Open the games list, so `u` or another free letter) — or, if the collision is
judged acceptable because the two buttons are never both focal targets of the same gesture in
practice, say so in a comment next to both string keys so the omission in `StringsTests.cs` reads
as a deliberate decision rather than an oversight.

### WR-03: SPEC.md still names the three pre-rename toolbar labels

**File:** `SPEC.md:295-296`
**Issue:** `SPEC.md` — which `CLAUDE.md`'s hard constraints call "the public specification" that
"scope changes revise... carrying the reasoning inline" — still reads: `Window actions: Add game,
Scan, Refresh, Open the games list, Change backup folder, Back up all changed, Back up selected,
Restore selected, Switch theme...`. This phase renamed all three of those to "Backup folder",
"Back up" and "Restore" everywhere else touched (Strings.en.xaml, CHANGELOG.md, README.md, both
user guides), but `SPEC.md` was not part of this phase's file list and was left with the old
names. `SPEC.md` is not among the files this review was scoped to, but the omission is a direct,
provable consequence of this phase's change and falls squarely under the project's own
documentation-consistency rule.
**Fix:** Update `SPEC.md:295-296` to the current labels ("Backup folder, Back up all changed, Back
up, Restore") in the same commit (or a fast-follow) that ships this rename, per CLAUDE.md's rule
that scope/behavior changes revise `SPEC.md` inline.

---

_Reviewed: 2026-11-24T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
