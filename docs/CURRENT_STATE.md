# MultiPingMonitor Current State

Last updated: 2026-07-09 10:50 UTC

## Accepted baseline

Sponsor Pro v1.0.27 is the current accepted runtime baseline.

Accepted on Windows after in-app updater validation from accepted Sponsor Pro v1.0.26 to v1.0.27.

Repository state at acceptance:

- `main` HEAD: `c3c42c2bee0888637d6ae5f1d8566d32879b3f5b`
- Version: `1.0.27`
- Sponsor Pro tag: `multipingmonitor/v1.0.27`
- Sponsor Pro ZIP asset: `MultiPingMonitor.zip`
- ZIP SHA-256: `e62854c01494e0fa5da56d2253d3e1f5220d7c21f5eb32ea73019093252cd5a0`
- EXE SHA-256: `298a8fa0b0fafd823dba061f50d165eb0d4ad14de230b5e082c916e393c6d234`

Accepted changes since v1.0.26:

- Main hamburger menu, tray menu, Compact menu button, and Compact right-click menu were reorganized.
- Compact menu button remains global.
- Host-specific `Odstrániť hostiteľa` remains only in Compact right-click when right-clicking a host.
- Compact mode switch action now uses the shared toggle-display icon.
- Menu-order regression tests were added.

Validation summary:

- Menu-order visual preview was accepted by the user.
- Full test suite passed before release.
- Sponsor Pro ZIP was downloaded back and verified.
- ZIP contains exactly one entry: `MultiPingMonitor.exe`.
- Backend latest endpoint returned `status=ok`, `latestVersion=1.0.27`, `tagName=multipingmonitor/v1.0.27`, and matching asset metadata under `asset.*`.
- Windows runtime acceptance passed through the in-app updater from v1.0.26 to v1.0.27.
- About window showed Sponsor Pro v1.0.27.
- User confirmed all menus are correct and as expected.

## Current repository state

Expected live state after sync:

- Branch: `main`
- HEAD: `c3c42c2bee0888637d6ae5f1d8566d32879b3f5b`
- Working tree: clean
- Ahead/behind vs `origin/main`: `0 0`
- Version: `1.0.27`

Always verify live before writing.

## Release workflow note

The v1.0.27 release proved the preferred guarded one-step orchestration model:

- One deterministic script may orchestrate the full release.
- It must still use branch + PR + merge for the version bump.
- It must never push directly to `main`.
- It must publish Sponsor Pro only after main is synced at the bumped version.
- It must verify local ZIP, download-back ZIP, and backend latest endpoint.
- Backend latest endpoint asset metadata must be read from `asset.name`, `asset.sha256`, and `asset.size`.
- Final Windows acceptance must still be done through the in-app updater.

## Next planned scope

No next code scope is selected yet.

Potential next planning topics:

- minor UI polish only if user identifies remaining UX friction;
- otherwise keep v1.0.27 as the stable accepted Sponsor Pro baseline.

## Hard rules for next session

- Communicate only in Slovak.
- Read `AGENTS.md` and this file before writes.
- Do not push directly to `main`.
- Use branch + PR + scope check + merge + sync main.
- Future release acceptance must use the in-app updater, not manual EXE replacement.
