# MultiPingMonitor Current State

Recorded: 2026-07-08

This document is checkpoint memory. It does not replace a fresh live Git,
GitHub, filesystem, build, release, backend, or Windows runtime audit.

## Project identity

- Public repository: `Vaso73/MultiPingMonitor`
- Private Sponsor Pro release repository:
  `Vaso73-Software/Sponsor-Pro-Releases`
- DEV repository: `/home/vaio/projects/MultiPingMonitor`
- Normal DEV user: `vaio`
- Git project: yes
- Windows TestRuntime:
  `C:\Users\info\OneDrive\Dokumenty\!!!!_GitHub_!!!!\Projekty\MultiPingMonitor\TestRuntime`

## Last verified source state

Verified live on: 2026-07-08

- Current branch before this documentation slice: `main`
- Public `main` HEAD:
  `9273b74e5ed2b63b51c035123a54e76151de9072`
- Subject:
  `Merge pull request #137 from Vaso73/fix/harden-single-file-update-installer`
- `origin/main`:
  `9273b74e5ed2b63b51c035123a54e76151de9072`
- Ahead/behind against `origin/main`: `0 0`
- Working tree before this documentation slice: clean
- Source version on `main`: `1.0.20`
- AssemblyVersion: `1.0.20`
- AssemblyFileVersion: `1.0.20`
- `MultiPingMonitor/app.config`: missing as expected after PR #137

## Current shipped Sponsor Pro baseline

Latest shipped Sponsor Pro release verified by live backend:

- Version: `1.0.20`
- Tag: `multipingmonitor/v1.0.20`
- Release name: `MultiPingMonitor Pro v1.0.20`
- Release ID: `350924496`
- Published at: `2026-07-08T12:51:21Z`
- Backend stage: `github-app-latest`
- Backend status: `ok`
- Backend latest endpoint:
  `https://updates.watel.cloud/v1/update/latest`

Backend asset metadata verified on 2026-07-08:

- Asset: `MultiPingMonitor.zip`
- Content type: `application/zip`
- Asset ID: `470268290`
- Size: `66428569` bytes
- SHA-256:
  `37afc464ae9f7ead8f5cfbbe1de8cda66dd4a46ac12abbb85b9c14b82252e92d`

Windows runtime acceptance for v1.0.20:
unknown / verify live if needed before using it as a runtime baseline.

## Recent merged slices

### PR #135 — Install Sponsor Pro updates from About

Status: merged.

Scope:

- Added client-side Sponsor Pro update installer.
- Added update helper mode before main window startup.
- Updated About window flow for user-confirmed Sponsor Pro update installation.
- Updated update check manifest/result handling.
- Added initial updater tests.
- Added project workflow documentation.

Known status:

- Git work is merged.
- Original validation flow had issues later corrected by PR #136 and PR #137.
- No Windows self-update runtime acceptance has been completed.

### PR #136 — Stabilize Sponsor Pro update installer validation

Status: merged / technical validation accepted.

Scope:

- Fixed updater test expectation around helper mode.
- Fixed update-check tests to use valid 64-character SHA placeholders.
- Updated workflow hard-stop rules in `AGENTS.md` and
  `docs/GITHUB_WORKFLOW.md`.

Validation recorded in handoff:

- `UpdateInstallServiceTests`: 12/12 passed
- `UpdateCheckServiceTests`: 15/15 passed
- full tests: 377/377 passed
- publish succeeded

Known status:

- Publish output still had a config artifact before PR #137.

### PR #137 — Harden single-file update publish output

Status: merged / technical validation accepted.

Scope:

- Removed legacy `MultiPingMonitor/app.config`.
- Updated `UpdateInstallService.ResolveCurrentExecutablePath` to avoid
  `Assembly.Location`.
- Added hardening tests.

Validation recorded in handoff:

- targeted `UpdateInstallServiceTests`: 14/14 passed
- full tests: 379/379 passed
- single-file publish output contained exactly `MultiPingMonitor.exe`
- publish inventory:
  - EXE count: 1
  - DLL count: 0
  - PDB count: 0
  - config count: 0
- publish output SHA-256 from validation build:
  `b107916a2476abfd0065fec0e9b84c06b8f410a01733daf68af0dc44a8b18c5d`
- no IL3000 failure was reported by the guard after the fix

## Live file identity verified on 2026-07-08

- `MultiPingMonitor/MultiPingMonitor.csproj`
  - lines: 50
  - sha256:
    `f2e30bbb1669d4121874e8def62b42aafe5f7e0ec4f3fe9819c7fa7783a71ca6`
- `MultiPingMonitor/Properties/PublishProfiles/SingleFile.pubxml`
  - lines: 16
  - sha256:
    `d2f12ce2fdbc9d7272ce4a0c7b2cae110d1773f394f4427d3e060ac16d6451a9`
- `MultiPingMonitor/Properties/PublishProfiles/FolderPublish.pubxml`
  - lines: 14
  - sha256:
    `650a3e78e6f4915c2e209f14a8510585f814e91c110ad5cd672587aece790627`
- `MultiPingMonitor/Classes/UpdateInstallService.cs`
  - lines: 578
  - sha256:
    `ec90883e8a72f9833d785828cc2a69f5dfc67f42e32fd3dba87ee16e1aa4651e`
- `MultiPingMonitor/Classes/UpdateCheckService.cs`
  - lines: 332
  - sha256:
    `2684fb4a0d76967029cf40fdc7c5370ec87e2e1f6c3f00a4d8bc7ea7f811ef69`
- `MultiPingMonitor.Tests/UpdateInstallServiceTests.cs`
  - lines: 179
  - sha256:
    `3f0bfb1298a73d4a369007c319ef82e834845290dda84583309b531072b3cfff`
- `MultiPingMonitor.Tests/UpdateCheckServiceTests.cs`
  - lines: 271
  - sha256:
    `91edcb31f849a588c0f27c8634a3b982eddf7782b94c2d1090459abcd10e7a34`

## Current updater state

- Existing shipped v1.0.20 can detect a newer release.
- Main after PR #135, PR #136, and PR #137 contains the client-side Sponsor Pro
  update installer and single-file publish hardening.
- The completed installer workflow is not yet accepted by Windows runtime
  self-update testing.
- The first future release containing this updater code is expected to be the
  next Sponsor Pro release, likely v1.0.21.
- A full self-update test requires an updater-capable installed/source version
  updating to a later version. Practical end-to-end self-update testing is
  expected from v1.0.21 to v1.0.22 or equivalent.

## Closed / accepted

- `main` is synced with `origin/main` at
  `9273b74e5ed2b63b51c035123a54e76151de9072`.
- Working tree was clean during the 2026-07-08 audit.
- PR #135 is merged.
- PR #136 is merged.
- PR #137 is merged.
- Source version is `1.0.20`.
- Backend latest endpoint reports v1.0.20.
- `MultiPingMonitor/app.config` is removed.
- PR #137 publish hardening validation showed a one-file Sponsor Pro publish
  output.

## Audit-only

- 2026-07-08 release preflight audit.
- 2026-07-08 short completion audit.
- Live backend latest endpoint inspection.
- Live private release list inspection.
- `docs/CURRENT_STATE.md` drift inspection.

## Pending

- Merge this documentation-only checkpoint update after PR review.
- Fresh live audit after the documentation PR is merged.
- Dedicated release-only version bump for next Sponsor Pro release, likely
  v1.0.21.
- Clean release build and Sponsor Pro ZIP packaging.
- Verify Sponsor Pro ZIP contains exactly one entry:
  `MultiPingMonitor.exe`.
- Publish private Sponsor Pro release to
  `Vaso73-Software/Sponsor-Pro-Releases`.
- Verify uploaded GitHub asset by downloading it back.
- Verify backend latest endpoint after publishing.
- Manual Windows runtime validation from the downloaded v1.0.21 ZIP.
- Later full self-update test from an updater-capable version to a newer
  version.

## Unknown / verify live

- Exact current private release asset metadata from `gh release view`; backend
  metadata is known and listed above.
- Current Windows runtime behavior of updater installation.
- Whether v1.0.20 was manually accepted as a Windows runtime release.
- Current source, release, backend, and Git state at the start of any later
  release slice.

## Current approved scope

Documentation-only correction of `docs/CURRENT_STATE.md`.

This documentation slice must not:

- change application code;
- change tests;
- change version numbers;
- change update manifest data;
- build, publish, tag, or release;
- change private GitHub release assets;
- change backend state;
- merge without final review if the user has not explicitly approved merging.

## Immediate next action

Open a documentation-only pull request that updates only:

- `docs/CURRENT_STATE.md`

Required guards:

- branch starts from synchronized `origin/main`;
- only `docs/CURRENT_STATE.md` changes;
- UTF-8 without BOM;
- LF line endings;
- no application, test, manifest, workflow, release asset, backend, tag, or
  runtime changes;
- pull request file scope contains only `docs/CURRENT_STATE.md`.

After this documentation PR is reviewed and merged, the next separate action is
a fresh release preflight audit before any release-only version bump to v1.0.21.

Do not proceed to version bump, release build, GitHub release, backend update,
or Windows runtime testing from this documentation slice.
