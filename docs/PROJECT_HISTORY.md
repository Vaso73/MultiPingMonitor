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
