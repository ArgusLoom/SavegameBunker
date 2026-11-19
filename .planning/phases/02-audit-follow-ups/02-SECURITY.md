---
phase: "02"
slug: "audit-follow-ups"
status: verified
threats_open: 0
asvs_level: 1
created: "2026-11-19"
---

# Phase 02 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| hand-edited games.json → backup walk | `backupRoot` and `saveDirTemplate` are user input; their relative position decides what is archived | local file paths |
| hand-edited paths → containment checks | backup root, save folder and archive paths come from games.json or the picker | local file paths |
| restore → save folder on disk | restore deletes the old save folder wholesale; the guard decides whether that folder may hold archives | local file deletion |
| restore picker row → file deletion | a confirmed click deletes one archive on disk | local file deletion |
| archive bytes → verification | a just-written or about-to-be-restored zip is checked before success or the swap | local file integrity |
| app → mapped network drives | DriveInfo.IsReady on a stale SMB mapping blocks the calling thread | local/network I/O |
| typed/picked folder → resolve walk | the save-folder field is user input; the helper only reads (resolve, exists, count) | local file paths |
| concurrent writers → one game's archive folder | a row backup and a bulk backup of the same game share targetDir and its staging files | local file writes |
| file system faults → skip policy | which exceptions count as "skip this path" decides what a backup silently omits | local file I/O errors |
| worker thread (bulk loop) → bound UI state | progress counters and row properties are written from Task.Run | cross-thread UI state |
| background caller → UI-thread dialog | Confirm blocks a worker until the user answers | cross-thread UI state |
| UI thread ↔ worker threads | sweep callbacks, arming and Confirm cross threads; in the test host OnUi runs inline | cross-thread UI state |
| app resource dictionary → WPF-UI resources | app keys shadow library keys of the same name app-wide | theme resources |
| test cleanup → file system | Dispose deletes a directory tree recursively | temp file deletion |
| test split → test discovery | xUnit discovers tests by class; a dropped section or a broken base silently shrinks the suite | test discovery |
| test timing → CI signal | timeouts decide whether a slow run reads as a failure | test timing |
| demo reset → file system | demo-data.ps1 deletes and recreates folders | temp file deletion |
| capture automation → user's machine | the executor drives the real app and photographs the screen, including a backup/restore and the access-key pass | local UI automation |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-02-01 | Tampering | RestoreService.Restore with save folder == backup folder | high | mitigate | `IsSameOrInside` refusal before CleanStaleRestoreTemp and the safety backup; pinned fact | closed |
| T-02-02 | Repudiation | BackupService archive walk (silent subfolder omission) | high | mitigate | D-10 prune rule archives item-0 layouts in full; round-trip facts | closed |
| T-02-03 | Information Disclosure | left-out note and refusal message | low | accept | Both name local folders to the local user only; no exfiltration | closed |
| T-02-04 | Denial of Service | SaveFolderPreview.Describe on a huge/slow folder | low | accept | Already runs under Task.Run behind a resolve token | closed |
| T-02-05 | Tampering | demo session driving backup/restore | medium | mitigate | App launched only on demo catalogs under %TEMP%; `games.json` diff-guarded | closed |
| T-02-06 | Information Disclosure | screenshots | low | mitigate | capture.ps1 copies only the app window rectangle; images untracked | closed |
| T-02-07 | Denial of Service | DriveHints.SuggestBackupStartFolder | medium | mitigate | `ShouldProbeReadiness` gates IsReady to Fixed/Removable; pinned theory | closed |
| T-02-08 | Tampering | internal MainViewModel constructor seam | low | accept | Internal, InternalsVisibleTo only; App keeps public ctor | closed |
| T-02-09 | Tampering | RunBulkBackupAsync preparation window | high | mitigate | IsBulkPreparing gates every operation command; pinned fact | closed |
| T-02-10 | Denial of Service | startup prompt arm/settle race | medium | mitigate | `_startupPromptLock`; arm/settle/consume facts | closed |
| T-02-11 | Tampering | PathCompare.IsSameOrInside | high | mitigate | Separator-aware prefix test; drive-root/prefix/case/UNC pins | closed |
| T-02-12 | Repudiation | BackupService.Backup with zero archived entries | medium | mitigate | Fail-and-delete-staging when nothing archived and nothing skipped | closed |
| T-02-13 | Denial of Service | stale CanExecute after operation flag/row busy change | medium | mitigate | RequeryOperationGates from every hook and IsBusy branch | closed |
| T-02-14 | Tampering | cross-thread write of bulk progress state | medium | mitigate | onItem invoked only through _onUi; routed-call-count fact | closed |
| T-02-15 | Tampering | DescribeVerificationFailure mode refactor | high | mitigate | Backup keeps recorded hashes, restore keeps disk-hash mode; facts green | closed |
| T-02-16 | Denial of Service | FileSystemFault.Is broadening a catch | medium | mitigate | Predicate is exactly the union every replaced list matched | closed |
| T-02-17 | Tampering | picker delete action | high | mitigate | Action is BackupService.DeleteArchive (one file, never recursive); pinned fact | closed |
| T-02-18 | Denial of Service | DialogService.Confirm on the UI thread | medium | mitigate | Debug.Assert on off-UI-thread contract; every caller under Task.Run | closed |
| T-02-19 | Spoofing | error-severity rendering | medium | mitigate | Rename + every reference in one commit; owned-prefix guard test | closed |
| T-02-20 | Tampering | demo session for after-21 shots | medium | mitigate | Only scratch catalogs passed; default games.json never opened | closed |
| T-02-21 | Tampering | TempDir.Dispose recursive delete | medium | mitigate | Path always built in ctor under %TEMP%/guid; junctions removed first | closed |
| T-02-22 | Repudiation | silent test loss during helper migration | medium | mitigate | Total + sorted test-name diff compared before/after | closed |
| T-02-23 | Repudiation | silent test loss during TestGames migration | medium | mitigate | Total + sorted test-name diff compared before/after | closed |
| T-02-24 | Repudiation | unasserted poll masking a timed-out wait | low | mitigate | `Assert.DoesNotContain(... Checking)` after the poll | closed |
| T-02-25 | Repudiation | silent test loss during the class split | medium | mitigate | Total, name list, Fact/Theory counts compared; scripted slicing | closed |
| T-02-26 | Denial of Service | extra parallel load from more test classes (flake) | medium | mitigate | `[Collection("MainViewModel")]` on all seven classes; 5-run flake check | closed |
| T-02-27 | Tampering | demo-data.ps1 reset deleting outside its own folder | high | mitigate | Script refuses any root not named SavegameBunkerDemo or inside the repo | closed |
| T-02-28 | Tampering | demo session for after-19-20 shots + access-key pass | medium | mitigate | App launched only on demo catalogs; restore confirmations declined | closed |
| T-02-SC | Tampering | NuGet installs | high | mitigate | Zero packages added; shipping csproj stays at 2, test csproj at 3 — confirmed by grep | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on (high) count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|--------------|------|
| R-02-01 | T-02-03 | Left-out-note and refusal messages name only local folder paths to the local user; nothing leaves the machine | plan author | 2026-11-18 |
| R-02-02 | T-02-04 | SaveFolderPreview.Describe already runs off the UI thread behind a resolve token; a slow walk cannot block or race | plan author | 2026-11-18 |
| R-02-03 | T-02-08 | The MainViewModel test-seam constructor is `internal`, reachable only via InternalsVisibleTo from the test assembly; App.xaml.cs keeps the public constructor | plan author | 2026-11-18 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-19 | 29 | 29 | 0 | orchestrator (L1 grep-depth, register_authored_at_plan_time: true, asvs_level: 1 — short-circuit per workflow, no auditor spawn required) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-19
