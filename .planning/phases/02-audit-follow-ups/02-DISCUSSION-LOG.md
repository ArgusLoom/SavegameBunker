# Phase 2: Audit follow-ups - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-18
**Phase:** 02-audit-follow-ups
**Areas discussed:** Item 0 exclusion report, Item 3 unreachable test, Item 21 visible changes, Plan split and order

---

## Item 0 exclusion report

| Option | Description | Selected |
|--------|-------------|----------|
| Info line, not warning | Plain note naming each left-out path and why; not the unreadable-path warning wording | ✓ |
| Same warning as unreadable | Reuse DescribeSkippedPathsWarning | |
| Silent | Fix pruning only, report nothing | |

**User's choice:** Info line, not warning

| Option | Description | Selected |
|--------|-------------|----------|
| Researcher checks it | Confirm RestoreService.cs:68 covers every excluded-folder case | |
| Explicit test required | Plan adds a test that restore never deletes an excluded folder | ✓ |

**User's choice:** Explicit test required

| Option | Description | Selected |
|--------|-------------|----------|
| SPEC + CHANGELOG | Revise SPEC.md backup section with reasoning; add CHANGELOG fix entry | ✓ |
| CHANGELOG only | Treat as a bug fix | |

**User's choice:** SPEC + CHANGELOG

---

## Item 3 unreachable test

| Option | Description | Selected |
|--------|-------------|----------|
| Rework to reachable path | Keep the intent via a reachable route; delete if already covered | ✓ |
| Delete it | Covered by A_cancelled_first_backup_folder_prompt_writes_nothing | |
| Keep, justify | Defensive guard with a comment | |

**User's choice:** Rework to reachable path

| Option | Description | Selected |
|--------|-------------|----------|
| Inject drive probe | Delegate seam so tests do not touch real drives | ✓ |
| Raise timeout only | Named-constant timeout from item 24 | |

**User's choice:** Inject drive probe (if the DriveType fix alone does not cure the flake)

---

## Item 21 visible changes

| Option | Description | Selected |
|--------|-------------|----------|
| Rename, own key only | App-prefixed key; WPF-UI controls get library red back | ✓ |
| Keep override, document it | Keep PaletteRed* as a deliberate app-wide override | |

**User's choice:** Rename, own key only

| Option | Description | Selected |
|--------|-------------|----------|
| Owner checkpoint | Owner runs the app and confirms visually | |
| Screenshots before/after | Executor launches the app and compares screenshots | ✓ |

**User's choice:** Screenshots before/after

---

## Plan split and order

| Option | Description | Selected |
|--------|-------------|----------|
| Defects first, then layers | Plan per defect chain, then per layer | |
| Strictly by layer | One plan per layer | |
| Planner decides | Only roadmap order constraints fixed | ✓ |

**User's choice:** Planner decides

| Option | Description | Selected |
|--------|-------------|----------|
| Stay on dev/v1.1 | Current branch, baseline count, suite green per plan | ✓ |
| New phase branch | refactor/v1.1-phase-2 | |

**User's choice:** Stay on dev/v1.1

| Option | Description | Selected |
|--------|-------------|----------|
| Defects first | Data-loss fix does not wait on fixture churn | ✓ |
| Fixtures first | Items 22/24 first | |

**User's choice:** Defects first

---

## Claude's Discretion

- Plan/wave grouping within the roadmap order constraints
- Wording of the item 0 info line
- Item 11: remove or assert the unreachable UI-thread branch of DialogService.Confirm

## Deferred Ideas

None.
