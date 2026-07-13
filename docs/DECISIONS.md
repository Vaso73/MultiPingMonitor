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
configuration, avoid persistent local EXE backups, support restart and a temp-only transactional swap, and must not
store GitHub credentials in plaintext or in `MultiPingMonitor.xml`.

For self-update replacement, MultiPingMonitor must avoid persistent local EXE backups. The helper may use only a temporary transactional swap under the system temp directory during replacement, and must remove temporary update files after a successful update. Manual rollback remains available by downloading an older GitHub release.

## D-006 — Local-first development and release preparation

Status: accepted

Decision:

MultiPingMonitor development and release preparation are local-first.

Intermediate work should stay on LXC DEV and should not be pushed to GitHub or
opened as pull requests merely because a small step was completed.

GitHub is used when:

- the user explicitly asks for GitHub delivery;
- the local result has been validated and accepted;
- a final release branch must be pushed and merged for reproducible release
  history;
- the private Sponsor Pro release is being published;
- collaboration or cross-environment transfer requires it.

Rationale:

Repeated push, pull-request, and merge steps slow down the working loop and make
release preparation noisy. Local-first work keeps iteration fast while still
requiring the final release to be traceable to a Git commit.

Consequences:

- Use local branches and local commits during preparation.
- Use one final release pull request after local validation and user approval.
- Do not create documentation-only checkpoint pull requests unless explicitly
  approved or required for safety.
- Private Sponsor Pro release publication still requires strict final GitHub,
  ZIP, download-back, and backend verification.

## D-007 — Fixed Windows TestRuntime directory

Status: accepted

Decision:

MultiPingMonitor Windows runtime testing must use this fixed directory:

`C:\Users\info\OneDrive\Dokumenty\!!!!_GitHub_!!!!\Projekty\MultiPingMonitor\TestRuntime`

Per-version runtime subdirectories are not used for normal testing.

Rationale:

The fixed `TestRuntime` root contains accepted runtime configuration, logs, and
support files. Testing from a new per-version subdirectory can accidentally
remove the executable from the real configuration context and produce misleading
results.

Consequences:

- Copy test executables and ZIPs directly into the fixed `TestRuntime` root.
- Preserve existing config files unless the user explicitly approves reset.
- Provide exact Windows PowerShell `scp` commands for that fixed destination.
- Do not treat isolated per-version folder tests as normal acceptance tests.

## 2026-07-12 — Logical window placement with machine exactness and portable fallback

Status: accepted

Decision:

- Persist window geometry only in WPF logical units.
- Store exact placement per computer in
  `data/machines/<COMPUTERNAME>/window-placement.xml`.
- Store portable fallback placement in `MultiPingMonitor.xml`.
- Prefer machine placement on the same computer.
- On another topology, preserve edge anchoring or relative position and
  clamp/resize the complete window into the available working area.
- Keep separate keys for Normal and Compact modes.
- Ignore schema v3 physical-pixel records.
- MainWindow saves the actual current mode during shutdown and does not use a
  static startup-mode closing key.

Rationale:

- WPF already operates in device-independent logical units.
- Manual conversion caused recursive 0.8 shrink/drift at 125% scaling.
- Portable operation requires exact same-machine restoration and safe
  cross-machine fallback.

Consequences:

- Do not restore the rejected PMv2/native physical-pixel model.
- Future placement changes require automated regression coverage and real
  Windows tests at both 100% and 125% scaling.
