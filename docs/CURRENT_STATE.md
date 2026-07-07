# MultiPingMonitor Current State

Recorded: 2026-07-07

This document is checkpoint memory. It does not replace a fresh live Git,
GitHub, filesystem, build, or runtime audit.

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

- Public `main` HEAD:
  `6453931fe166a32bcdc0fdf440d0e6694501b192`
- Subject:
  `Merge pull request #126 from Vaso73/release/bump-version-to-1-0-18`
- Source version on `main`: `1.0.18`
- Feature PR #125: merged
- Release PR #126: merged
- Current documentation branch:
  `docs/add-project-workflow`
- Documentation branch base HEAD:
  `6453931fe166a32bcdc0fdf440d0e6694501b192`
- Current working-tree state after this write:
  `unknown / verify live`

## Current accepted baseline

The last accepted Sponsor Pro runtime release is v1.0.17.

Verified archived v1.0.17 release asset:

- Asset: `MultiPingMonitor.zip`
- Size: `66946591` bytes
- SHA-256:
  `f47456b3f5b8175996f56b775fe3c07765a29ba777f9e4fb2031aca5c32532af`
- ZIP entry count: `1`
- Only entry: `MultiPingMonitor.exe`
- Windows runtime test: accepted by the user

## v1.0.18 source state

v1.0.18 source work is merged into public `main`.

Feature scope:

- About window
- displayed version and Sponsor Pro edition
- manual Sponsor Pro update checker
- public update manifest

Completed technical validation:

- targeted update-checker tests: 15/15 passed
- full test suite: 350/350 passed
- Release build: passed
- build warnings: 0
- build errors: 0

## v1.0.18 release incident

Private release tag:

    multipingmonitor/v1.0.18

Last verified private release ID:

    350423531

Last verified publication state:

- published
- not a prerelease
- private Latest
- public Latest remained `v0.4.6`
- no public remote `v1.0.18` tag existed

Published asset currently described by the public manifest:

- Asset: `MultiPingMonitor.zip`
- Size: `70893515` bytes
- SHA-256:
  `51bc985a41824c7df7833be91c8de1e7511a23630d01e175f7b4051f15ea749e`

This asset is not an accepted release artifact.

Verified packaging defect:

- ZIP entry count: `466`
- ZIP contains the complete FolderPublish output
- included `MultiPingMonitor.exe` size: `324096` bytes
- required package: one self-contained `MultiPingMonitor.exe`
- Windows runtime acceptance: not completed
- release status: pending correction

Do not describe v1.0.18 as accepted until the corrected asset is published,
downloaded, verified, and accepted in Windows runtime testing.

## Relevant recovery and evidence

Release logdir:

    /home/vaio/backups/MultiPingMonitor/release-v1.0.18-20260707-153834

Incorrect local v1.0.18 ZIP:

    /home/vaio/backups/MultiPingMonitor/release-v1.0.18-20260707-153834/assets/v1.0.18/MultiPingMonitor.zip

Downloaded copy of the same published asset:

    /home/vaio/backups/MultiPingMonitor/release-v1.0.18-20260707-153834/published-asset-verification/MultiPingMonitor.zip

Recovery bundle:

    /home/vaio/backups/MultiPingMonitor/release-v1.0.18-20260707-153834/recovery/MultiPingMonitor-all-local-refs.bundle

Archived accepted v1.0.17 ZIP:

    /home/vaio/backups/MultiPingMonitor/pro-latest-20260428-164558/assets/v1.0.17/MultiPingMonitor.zip

## Current approved scope

Create and review the canonical project-memory documentation:

- `AGENTS.md`
- `docs/HANDOFF_TEMPLATE.md`
- `docs/CURRENT_STATE.md`
- `docs/PROJECT_HISTORY.md`
- `docs/DECISIONS.md`
- `docs/RELEASE.md`

This documentation slice must not:

- change application code;
- change the update manifest;
- change version numbers;
- create the publishing script;
- replace the private release asset;
- change GitHub release state;
- commit, push, create a PR, or merge without separate approval.

## Immediate next action

Complete the remaining uncommitted project-memory documents and then review
the entire documentation slice.

Required guards:

- branch remains `docs/add-project-workflow`;
- HEAD remains
  `6453931fe166a32bcdc0fdf440d0e6694501b192`;
- only approved documentation files are untracked;
- no application, manifest, workflow, release, or runtime files change;
- all documents use UTF-8 without BOM and LF line endings;
- all cross-references are valid;
- the handoff template remains unchanged.

Do not commit or push before the complete documentation review is accepted.

## Pending after this documentation slice

After the documentation slice is accepted and merged:

1. Create and review the guarded Sponsor Pro publishing script.
2. Produce a correct v1.0.18 SingleFile EXE.
3. Create a ZIP containing only that EXE.
4. Update the public manifest through a dedicated correction PR.
5. Run the complete guarded correction, publication, download, and
   verification transaction in one invocation.
6. Test the downloaded corrected GitHub asset in Windows.
7. Mark v1.0.18 accepted only after user confirmation.

All GitHub and release state must be verified live before that work begins.
