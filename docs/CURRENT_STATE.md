# MultiPingMonitor Current State

Last updated: 2026-07-09 08:29 UTC

## Accepted baseline

Sponsor Pro v1.0.26 is the current accepted runtime baseline.

Accepted on Windows after in-app updater validation from official Sponsor Pro v1.0.25 to v1.0.26.

Repository state at acceptance:

- `main` HEAD: `0e19d6d9f1ad845a83235c3f6274cba8913f887b`
- Version: `1.0.26`
- Sponsor Pro tag: `multipingmonitor/v1.0.26`
- Sponsor Pro ZIP asset: `MultiPingMonitor.zip`
- ZIP SHA-256: `f957b955e310b9a4a836f057ece6c0a86a27f10ddab81ce2802609a84209c0fd`
- EXE SHA-256: `cf80e0edabfd8eac9dbd3bcb694a24afed3db05fef7d20c33f2721f4d139e434`

Accepted changes since v1.0.25:

- Compact right-click menu expanded with existing app and compact-set actions.
- `Nový Live Ping...` moved to the first position in Compact right-click menu.
- Slovak user-facing `Možnosti` labels renamed to `Nastavenia`.

Validation summary:

- Full test suite passed before release.
- Sponsor Pro ZIP was downloaded back and verified.
- ZIP contains exactly one entry: `MultiPingMonitor.exe`.
- Backend latest endpoint returned `status=ok`, `latestVersion=1.0.26`, `tagName=multipingmonitor/v1.0.26`, and asset metadata under `asset.*`.
- Windows runtime acceptance passed through the in-app updater.
- Final Windows EXE `FileVersion` was `1.0.26` and SHA-256 matched the released EXE.

## Current repository state

Expected live state after sync:

- Branch: `main`
- HEAD: `0e19d6d9f1ad845a83235c3f6274cba8913f887b`
- Working tree: clean
- Ahead/behind vs `origin/main`: `0 0`
- Version: `1.0.26`

Always verify live before writing.

## Next planned scope

Next planned development scope is menu-order reorganization across the app.

Initial UX observation from accepted v1.0.26 runtime:

- Compact right-click menu is now functionally accepted.
- Tray/main/compact menu ordering is still inconsistent.
- The next slice should first audit all menu builders and current ordering.
- Then propose a unified ordering model before changing code.

Planned direction for menu organization:

- Put fast creation/opening actions near the top.
- Keep display-mode switching and Compact targets close together.
- Keep diagnostic tools such as Traceroute and Flood Host grouped together.
- Keep Settings/History/Help/About in a predictable support section.
- Keep Exit at the bottom.
- Do not rename internal `Options*` identifiers unless separately approved.

## Hard rules for next session

- Communicate only in Slovak.
- Read `AGENTS.md` and this file before writes.
- Do not push directly to `main`.
- Use branch + PR + scope check + merge + sync main.
- Final release acceptance for future versions must use the in-app updater, not manual EXE replacement.
