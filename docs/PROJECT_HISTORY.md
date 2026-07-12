# MultiPingMonitor Project History

This is a concise append-only history of material completed milestones,
accepted releases, and important incidents.

It does not replace Git history, release metadata, logs, checkpoints, or live
state.

## 2026-04-28 — Sponsor Pro distribution baseline

- Sponsor Pro releases v1.0.16 and v1.0.17 were preserved in the private
  `Vaso73-Software/Sponsor-Pro-Releases` repository.
- Public GitHub Releases remained Free-only through v0.4.6.
- Accepted archived v1.0.17 asset:
  - asset: `MultiPingMonitor.zip`
  - size: `66946591` bytes
  - SHA-256:
    `f47456b3f5b8175996f56b775fe3c07765a29ba777f9e4fb2031aca5c32532af`
  - ZIP entry count: `1`
  - only entry: `MultiPingMonitor.exe`
- v1.0.17 was manually tested in Windows and accepted by the user.

Status: closed/accepted.

## 2026-07-07 — About window and manual update checker

Implemented scope:

- About window
- displayed application version
- Sponsor Pro edition display
- manual Sponsor Pro update checker
- public Sponsor Pro update manifest

Identity:

- feature commit:
  `18833b898123757506d9ebeadcf671420fda7bec`
- feature PR: #125
- merge commit:
  `f6ec862f56f00ab5f0429de7a9fc7ef4ad109027`

Validation:

- targeted update-checker tests: 15/15 passed
- full test suite: 350/350 passed
- Release build: passed
- build warnings: 0
- build errors: 0

Source slice status: closed/accepted.

## 2026-07-07 — v1.0.18 source release merge

Identity:

- release commit:
  `fcc92c126439ffaa44190e62f2105a1c4e9b0d73`
- release PR: #126
- public `main` merge commit:
  `6453931fe166a32bcdc0fdf440d0e6694501b192`

Changes:

- AssemblyVersion changed to 1.0.18.
- AssemblyFileVersion changed to 1.0.18.
- Public Sponsor Pro manifest was updated for the initially generated
  v1.0.18 ZIP.

Source merge status: closed.
Runtime release acceptance: pending.

## 2026-07-07 — v1.0.18 packaging incident

Verified remote state at the time of the incident:

- private tag: `multipingmonitor/v1.0.18`
- private release ID: `350423531`
- release was published
- release was not a prerelease
- release was private Latest
- public Latest remained `v0.4.6`
- no public remote `v1.0.18` tag existed

Published ZIP identity:

- asset: `MultiPingMonitor.zip`
- size: `70893515` bytes
- SHA-256:
  `51bc985a41824c7df7833be91c8de1e7511a23630d01e175f7b4051f15ea749e`

Incident:

- downloaded GitHub asset matched the local ZIP byte-for-byte;
- ZIP contained 466 FolderPublish entries;
- included launcher EXE size was only `324096` bytes;
- required package is one self-contained `MultiPingMonitor.exe`;
- Windows runtime acceptance was stopped.

Status: incident open / v1.0.18 release not accepted.

## 2026-07-07 — Canonical project-memory model

Approved file responsibilities:

- `AGENTS.md` — stable workflow and safety rules
- `docs/CURRENT_STATE.md` — checkpoint memory and one next action
- `docs/HANDOFF_TEMPLATE.md` — unchanged user handoff request
- `docs/PROJECT_HISTORY.md` — concise append-only history
- `docs/DECISIONS.md` — long-lived decisions
- `docs/RELEASE.md` — canonical release procedure

The user's standard handoff request remains unchanged.

Status: documentation slice pending complete review.

## 2026-07-09 08:29 UTC - Sponsor Pro v1.0.26 accepted

Sponsor Pro v1.0.26 was released and accepted.

Release facts:

- `main` HEAD: `0e19d6d9f1ad845a83235c3f6274cba8913f887b`
- Sponsor Pro tag: `multipingmonitor/v1.0.26`
- ZIP asset: `MultiPingMonitor.zip`
- ZIP SHA-256: `f957b955e310b9a4a836f057ece6c0a86a27f10ddab81ce2802609a84209c0fd`
- EXE SHA-256: `cf80e0edabfd8eac9dbd3bcb694a24afed3db05fef7d20c33f2721f4d139e434`

Included changes:

- PR #148 expanded Compact right-click menu with existing app and compact-set actions.
- PR #149 renamed Slovak user-facing `Možnosti` labels to `Nastavenia`.
- PR #150 bumped version to v1.0.26.

Validation:

- Full test suite passed.
- Sponsor Pro ZIP download-back verification passed.
- Backend latest endpoint returned v1.0.26 and correct asset metadata under `asset.*`.
- Windows runtime acceptance passed through the in-app updater from official v1.0.25 to v1.0.26.

## 2026-07-09 10:50 UTC - Sponsor Pro v1.0.27 accepted

Sponsor Pro v1.0.27 was released and accepted.

Release facts:

- `main` HEAD: `c3c42c2bee0888637d6ae5f1d8566d32879b3f5b`
- Sponsor Pro tag: `multipingmonitor/v1.0.27`
- ZIP asset: `MultiPingMonitor.zip`
- ZIP SHA-256: `e62854c01494e0fa5da56d2253d3e1f5220d7c21f5eb32ea73019093252cd5a0`
- EXE SHA-256: `298a8fa0b0fafd823dba061f50d165eb0d4ad14de230b5e082c916e393c6d234`

Included changes:

- PR #152 reorganized application menu order.
- PR #153 bumped version to v1.0.27.

Validation:

- Full test suite passed.
- Sponsor Pro ZIP download-back verification passed.
- Backend latest endpoint returned v1.0.27 and correct asset metadata under `asset.*`.
- Windows runtime acceptance passed through the in-app updater from v1.0.26 to v1.0.27.
- User confirmed all menu ordering and icons were correct.

Workflow note:

- v1.0.27 used a successful guarded one-step release orchestration script.
- The script still preserved branch + PR + merge before publishing.
- Future releases may use this model when all gates are explicit and fail closed.

## 2026-07-09 — External language pack foundation local checkpoint

- Local-only branch `feature/external-lang-pack-foundation` reached commit `420282f` / `420282fd0b437cac5f43bdda0fa3c10ed92349c0`.
- The slice added the external language pack foundation for runtime-generated `lang/sk-SK.lang`.
- Build, full tests, diff check, publish single-EXE contract, local ZIP contract, and Windows runtime seed generation were validated before this checkpoint.
- The branch was intentionally not pushed because the foundation does not yet switch the live UI to external `.lang` lookup.
- The next scope is active external `.lang` runtime localization, still local-only.

## 2026-07-09 — External language runtime lookup local validation

- Local-only branch `feature/external-lang-pack-foundation` added runtime external `.lang` lookup on top of the language pack foundation.
- The implementation uses a custom `ResourceManager` bridge and does not hand-edit generated `Strings.Designer.cs`.
- Options language choices now include discovered external packs such as `Slovenčina (sk-SK)`.
- Build, full tests, diff check, single-file publish contract, and Windows runtime validation passed.
- The branch remains local-only and is not pushed because the next approved slice is live Options apply/save behavior.

## 2026-07-10 — Sponsor Pro v1.1.0 accepted

- Completed and merged the external language-pack and full localization feature through PR `#157`.
- Merged the release-only v1.1.0 metadata through PR `#158`.
- Corrected the canonical publisher remote-manifest guard through PR `#159`.
- Published private release `multipingmonitor/v1.1.0` as release ID `352086825`.
- Verified `MultiPingMonitor.zip` contains exactly one `MultiPingMonitor.exe`.
- Verified ZIP size `66623359` and SHA-256
  `5808d2f708233c2dc96fa761491a146af5ed53a6258b93f0a8bcb033dc350fe0`.
- Verified the released executable SHA-256
  `3f5440ad254a28eb9641ba81ff86cf549cb57a6d50220a4a18d2a6a95dc5fcab`.
- Verified the private GitHub asset by downloading it back byte-for-byte.
- Verified the updater backend reports v1.1.0 with matching tag, asset, size, SHA-256, and `status=ok`.
- The user updated from accepted v1.0.28 through the in-app updater.
- Windows showed FileVersion and ProductVersion `1.1.0`; the installed EXE hash matched the release.
- The user confirmed all requested runtime and localization smoke tests as functional.
- Final state: Sponsor Pro v1.1.0 `closed/accepted`.

## 2026-07-12 — Portable DPI/topology-safe window placement accepted

- Replaced the rejected physical-pixel/PMv2 experiment with schema v4 WPF
  logical placement.
- Added exact per-machine persistence plus a portable shared fallback.
- Preserved edge anchoring or relative placement across display topologies.
- Preserved independent Normal and Compact position and size.
- Fixed shutdown persistence so closing in a mode different from startup does
  not overwrite the other mode.
- Added geometry, storage, and mode-switch regression tests.
- Final full automated suite passed: 461/461.
- Single-file publish contract passed.
- Runtime acceptance passed on Surface at 125% and MINISFORUM at 100%.
- Accepted diagnostic SHA-256:
  `fb00f59589186c62e0ce892e7e755bfa934c58dcdd772925b00a9adee76baa10`.
- Final state: `closed/accepted`, committed locally, not pushed.
