---
phase: "01"
slug: "bugfixing-refactoring"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-18"
---

# Phase 01 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> The register was authored at plan time (all five PLAN files carry a `<threat_model>` block);
> every mitigation below was verified against the implementation at L1 (grep/diff/test-presence)
> on 2026-11-18, against branch `refactor/v1.1-phase-1` at `7f1135b`.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| test host → local filesystem | Pin/contract/coordinator tests create and delete files; every written path is rooted in the test's own `%TEMP%/SavegameBunkerTests/<guid>` directory, removed in Dispose | test payloads only, nothing outside the test sandbox |
| UI thread ↔ worker thread | Bulk backup loop and status sweep run on the thread pool and write bound row properties; marshalling goes through `onUi` (or the row-progress pattern the view model owns) | bound view-model state — a raw worker write would crash the app (RESEARCH Pitfall 1) |
| app → backup folder / save folder | Backup writes into the user-chosen backup root; Restore is the app's only destructive operation and keeps its load-bearing order (safety backup → temp extract → remove old → rename in) | user save data / archives |
| developer machine → GitHub | Commits land on `refactor/v1.1-phase-1` only; `main` and the `phase1-baseline` tag are never pushed by agents | source, via the owner's existing gh/deploy-key auth |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-01-01 | Tampering | MainViewModelGuardPinTests fixture | low | mitigate | All pin paths under the test's own temp GUID directory; restore-order pin declines every confirmation | closed |
| T-01-02 | Tampering | remote `main` branch | low | mitigate | All commits on `refactor/v1.1-phase-1`; `main`/`phase1-baseline` never pushed by agents | closed |
| T-01-03 | Tampering | ServiceContractTests invoking destructive services | medium | mitigate | Path arguments rooted in temp dir or deliberately unresolvable; Restore's confirm delegate always false | closed |
| T-01-04 | Denial of Service | ServiceContractTests run time | low | mitigate | No UNC/network hostile input — no dead-host stall | closed |
| T-01-05 | Denial of Service | BulkBackupCoordinator release path | medium | mitigate | Release `finally` moved verbatim; release/onFinished facts in BulkBackupCoordinatorTests (9 facts) | closed |
| T-01-06 | Tampering | BackupService final rename at the cap | medium | mitigate | Two-arg `File.Move` never overwrites; cap facts assert pre-existing archives keep their bytes, staging file deleted | closed |
| T-01-07 | Information Disclosure | REF-04a failure message | low | accept | Message names local backup folder/timestamp to the local user only — same exposure as every existing BackupService message; nothing leaves the machine | closed (accepted) |
| T-01-08 | Denial of Service | StatusSweepCoordinator result application | medium | mitigate | Every row write via `onUi` with token check inside the callback (moved verbatim); coordinator tests count the marshalled writes | closed |
| T-01-09 | Information Disclosure | Game cell skipped-path sentence | low | accept | Relative paths of the user's own save folder shown to the same local user; Backup's message already shows the same paths | closed (accepted) |
| T-01-10 | Tampering | SaveStatusScanner | low | mitigate | Scan stays read-only; the change only adds a collecting delegate — no file opened/created/moved/removed | closed |
| T-01-11 | Denial of Service | RowActionsCoordinator per-row release | medium | mitigate | IsBusy raised before the first await, released in `finally` (moved verbatim); cancelled-root and failed-write facts prove balanced counters | closed |
| T-01-12 | Tampering | Restore path (destructive) | high | mitigate | RowActionsCoordinator only calls GameActions.RestoreAsync; `git diff --quiet phase1-baseline -- src/SavegameBunker/Services/RestoreService.cs` exits 0 — restore order byte-identical | closed |
| T-01-13 | Elevation of Privilege | IRowActionHost on a public type | low | mitigate | MainViewModel implements the internal interface explicitly (9 explicit members, no public member added); GameRowViewModel/IDialogService diff gates hold | closed |
| T-01-SC | Tampering | NuGet installs | high | mitigate | Zero packages added in the whole phase: shipping csproj = 2 `<PackageReference>`, test csproj = 3 (xUnit); no DI container type anywhere in `src` | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| T-01-07 | T-01-07 | Rename-cap message names a local path to the local user — pre-existing exposure class of all BackupService messages; nothing leaves the machine | owner (plan-time disposition "accept") | 2026-11-17 |
| T-01-09 | T-01-09 | Skipped-path sentence shows the user's own save folder paths on their own row — same data Backup's outcome message already surfaces | owner (plan-time disposition "accept") | 2026-11-17 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-18 | 14 | 14 | 0 | devlead (gsd-secure-phase 1, L1, ASVS 1) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-18 — secure-phase audit; 14/14 closed (12 mitigated + verified, 2 accepted risks), no blocking threat open.
