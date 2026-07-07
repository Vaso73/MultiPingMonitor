# MultiPingMonitor Decisions

This file records long-lived project decisions.

Each decision remains active until explicitly superseded by a later entry.

## D-001 — Public Free and private Sponsor Pro distribution

Date: 2026-04-28
Status: active

Decision:

- Public GitHub Releases in `Vaso73/MultiPingMonitor` remain Free-only through
  v0.4.6.
- Sponsor Pro binaries are distributed only through
  `Vaso73-Software/Sponsor-Pro-Releases`.
- Private Sponsor Pro tags use `multipingmonitor/vX.Y.Z`.
- A public remote Sponsor Pro tag must not be created.

Reason:

Keep public Free distribution separate from private Sponsor Pro access.

Consequences:

The public repository may expose update metadata but must not host Sponsor Pro
release binaries or Sponsor Pro release tags.

## D-002 — Single-file Sponsor Pro package

Date: 2026-07-07
Status: active

Decision:

- The GitHub asset is named `MultiPingMonitor.zip`.
- The ZIP contains exactly one entry: `MultiPingMonitor.exe`.
- The EXE is produced through
  `MultiPingMonitor/Properties/PublishProfiles/SingleFile.pubxml`.
- `FolderPublish.pubxml` must never produce the Sponsor Pro release package.

Reason:

The established v1.0.17 distribution is a portable self-contained single-file
application.

Consequences:

A ZIP containing DLLs, runtime files, language directories, PDB files, or any
other entry is invalid even when it also contains an EXE.

## D-003 — GitHub publishing is one guarded transaction

Date: 2026-07-07
Status: active

Decision:

After explicit approval to publish, the complete GitHub publishing operation
runs without another approval pause through:

- all pre-publish guards;
- release creation or correction;
- asset upload or replacement;
- publication;
- Latest verification;
- remote download;
- size and SHA-256 verification;
- ZIP integrity and content verification;
- byte-for-byte comparison.

Reason:

Prevent partially completed releases and prevent an upload from being mistaken
for a fully verified publication.

Consequences:

The dedicated publishing entry point must return PASS only after the downloaded
remote ZIP is fully verified.

## D-004 — Windows acceptance uses the downloaded GitHub asset

Date: 2026-07-07
Status: active

Decision:

Manual Windows testing must use the asset downloaded back from the private
GitHub release, not an unverified local build.

Reason:

The tested binary must be identical to the binary actually distributed.

Consequences:

A release is not accepted until the user confirms the Windows runtime test of
the downloaded asset.

## D-005 — Feature and release work remain staged

Date: 2026-07-07
Status: active

Decision:

Feature development, feature PR merge, release-version PR merge, guarded GitHub
publishing, and Windows acceptance remain separate reviewable stages.

The GitHub publishing stage itself is atomic, but the broader workflow must not
be collapsed into one monolithic operation.

Reason:

Preserve review gates and reduce the blast radius of mistakes.

## D-006 — Project-memory file responsibilities

Date: 2026-07-07
Status: active

Decision:

- `AGENTS.md` stores stable workflow and safety rules.
- `docs/CURRENT_STATE.md` stores current checkpoint memory.
- `docs/HANDOFF_TEMPLATE.md` stores the unchanged handoff request.
- `docs/PROJECT_HISTORY.md` stores concise append-only history.
- `docs/DECISIONS.md` stores long-lived decisions.
- `docs/RELEASE.md` stores the complete release procedure.

Reason:

Prevent duplication, stale state, and loss of continuation context.

Consequences:

`docs/CURRENT_STATE.md` never replaces a live audit, and complete stable rules
must not be copied into every handoff.

## D-007 — The user's handoff request remains unchanged

Date: 2026-07-07
Status: active

Decision:

The user does not need to alter their established handoff request when new
project-memory files are introduced.

Reason:

Project internals must adapt to the stable user workflow, not require repeated
changes to the user's prompt.

Consequences:

`AGENTS.md` requires automatic consultation of all relevant project-memory
files. The current user request always overrides a stale stored template.

## D-008 — Update checker, access control, and updater remain separate

Date: 2026-07-07
Status: active

Decision:

The manual update checker, Sponsor Pro access or licensing, and any future
automatic updater are separate concerns.

Reason:

Checking public version metadata must not implicitly grant private release
access or perform an installation.

Consequences:

A future updater must be designed and reviewed separately, preserve user
configuration, create a backup, support restart and rollback, and must not
store GitHub credentials in plaintext or in `MultiPingMonitor.xml`.
