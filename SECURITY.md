# Security policy

## Supported versions

Only the latest release receives fixes. SavegameBunker has no auto-update, so a fix ships as a new
release on the [releases page](https://github.com/ArgusLoom/SavegameBunker/releases).

## Reporting a vulnerability

Please **do not** open a public issue for a security problem. Report it privately instead:
[Report a vulnerability](https://github.com/ArgusLoom/SavegameBunker/security/advisories/new)
(GitHub's private vulnerability reporting).

Useful to include:

- the SavegameBunker version and Windows version,
- what an attacker controls (a `games.json`, a backup ZIP, a folder layout, …),
- steps to reproduce, and what happens versus what should happen.

You can expect a first reply within a week. Once a fix is released, the advisory is published with
credit to you unless you prefer otherwise.

## Scope

SavegameBunker reads a catalog file and ZIP archives that may come from someone else. Issues in that
handling are in scope, for example:

- a ZIP that writes outside the save folder on restore, or escapes the zip-bomb limits,
- a catalog entry that makes the app open or run something other than a folder or the catalog file,
- a backup that follows links out of the save folder and packs unrelated files.

Out of scope: a game rejecting restored saves, and anything that needs an attacker who already
controls your Windows account.
