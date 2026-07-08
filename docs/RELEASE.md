# MultiPingMonitor Sponsor Pro Release Workflow

This document is the canonical Sponsor Pro build, packaging, publishing,
correction, verification, acceptance, and cleanup procedure.

Read `AGENTS.md` and `docs/CURRENT_STATE.md` before using this workflow.

## Local-first release preparation

Sponsor Pro release preparation is local-first.

Do not push, create a pull request, merge, tag, publish, or update backend
metadata for every intermediate release step. Prepare the release locally first:

1. create a local release branch from synchronized `main`;
2. update only the approved release files;
3. run version guards;
4. run tests;
5. produce the SingleFile executable;
6. create and validate the local `MultiPingMonitor.zip`;
7. copy the artifact to Windows TestRuntime when runtime validation is needed;
8. wait for user acceptance of the Windows test when relevant.

Only after the local artifact is technically valid and accepted should GitHub be
used:

1. push the final local release branch;
2. create one release pull request;
3. verify scope and mergeability;
4. merge after approval;
5. publish the private Sponsor Pro release;
6. download the private GitHub asset back;
7. verify ZIP identity, SHA-256, file count, and executable metadata;
8. verify the backend latest endpoint.

The release remains unaccepted until the user confirms Windows runtime testing
of the downloaded GitHub asset.

## 1. Distribution contract

Public source repository:

    Vaso73/MultiPingMonitor

Private Sponsor Pro release repository:

    Vaso73-Software/Sponsor-Pro-Releases

Public GitHub Releases remain Free-only through:

    v0.4.6

Private Sponsor Pro tag format:

    multipingmonitor/vX.Y.Z

Private release asset name:

    MultiPingMonitor.zip

The public repository must not receive:

- a Sponsor Pro GitHub release;
- a public remote Sponsor Pro tag;
- the private Sponsor Pro binary asset.

The public repository may contain update metadata, but private binary access
remains separate.

## 2. Required stage separation

The complete workflow consists of separate reviewable stages:

1. feature development and local validation;
2. feature pull request and merge;
3. release-only version and manifest work;
4. release pull request and merge;
5. one complete guarded GitHub publishing transaction;
6. manual Windows runtime acceptance;
7. cleanup and final project-memory update.

Do not combine all stages into one monolithic script.

Stage 5 is intentionally atomic. After the user explicitly approves its start,
it must continue without another approval prompt through final verification of
the ZIP downloaded back from GitHub.

## 3. Feature completion gate

Before release-version work begins:

- the feature pull request is merged;
- local `main` is synchronized with `origin/main`;
- the working tree is clean;
- targeted tests passed;
- the full suite passed when application behavior changed;
- the relevant build passed;
- the merged changed-file scope was verified;
- no unrelated changes remain.

Feature implementation and release-version work must use separate branches and
separate pull requests.

## 4. Release-only branch

Create a dedicated branch:

    release/bump-version-to-X-Y-Z

The release branch normally changes only:

    MultiPingMonitor/Properties/AssemblyInfo.cs
    updates/sponsor-pro.json

Additional files require explicit approval.

The final manifest values must be derived from the exact final release ZIP.

Do not commit approximate, preliminary, stale, or manually estimated artifact
metadata.

## 5. Canonical publish profile

Use only:

    MultiPingMonitor/Properties/PublishProfiles/SingleFile.pubxml

Canonical publish command:

    dotnet publish MultiPingMonitor/MultiPingMonitor.csproj \
      -p:PublishProfile=SingleFile \
      --no-restore

Expected executable:

    MultiPingMonitor/bin/publish/single-file/MultiPingMonitor.exe

Required publish properties:

- configuration: Release;
- runtime identifier: win-x64;
- self-contained: true;
- publish single file: true;
- trimming: false;
- native libraries included for self-extraction.

The resulting executable FileVersion must match the intended release version.

## 6. Canonical packaging contract

Create the archive only from the final SingleFile executable:

    zip -9 -j MultiPingMonitor.zip \
      MultiPingMonitor/bin/publish/single-file/MultiPingMonitor.exe

The ZIP must contain exactly one entry:

    MultiPingMonitor.exe

Required checks:

    unzip -Z1 MultiPingMonitor.zip
    unzip -Z1 MultiPingMonitor.zip | wc -l
    unzip -t MultiPingMonitor.zip

Required results:

- entry list is exactly `MultiPingMonitor.exe`;
- entry count is exactly `1`;
- ZIP integrity passes.

The ZIP must not contain:

- DLL files;
- PDB files;
- configuration files;
- `.deps.json`;
- `.runtimeconfig.json`;
- framework runtime files;
- language directories;
- any other entry.

An archive is invalid when it contains an EXE together with any additional
entry.

## 7. FolderPublish prohibition

The following profile is not a Sponsor Pro release source:

    MultiPingMonitor/Properties/PublishProfiles/FolderPublish.pubxml

It may be used only for local diagnostics or folder-based investigation.

Never package:

    MultiPingMonitor/bin/publish/folder/*

Never create the Sponsor Pro ZIP from an entire publish directory.

The v1.0.18 packaging incident demonstrated that successful upload, matching
hashes, and byte-for-byte remote verification do not prove that the selected
local artifact was correct. Archive content validation is mandatory before any
remote write.

## 8. Manifest contract

Manifest:

    updates/sponsor-pro.json

Required fields:

- `schemaVersion`;
- `channel`;
- `latestVersion`;
- `releaseTag`;
- `assetName`;
- `assetSize`;
- `sha256`.

Required fixed values:

- `schemaVersion`: `1`;
- `channel`: `sponsor-pro`;
- `releaseTag`: `multipingmonitor/vX.Y.Z`;
- `assetName`: `MultiPingMonitor.zip`.

`assetSize` and `sha256` must describe the exact approved one-entry ZIP.

The manifest must not be finalized before the canonical ZIP has been built and
validated.

## 9. Release pull-request gate

Before committing the release-only branch:

- AssemblyVersion matches the intended release;
- AssemblyFileVersion matches the intended release;
- manifest version matches;
- manifest tag matches;
- manifest asset name matches;
- ZIP integrity passes;
- ZIP entry count is exactly one;
- the only ZIP entry is `MultiPingMonitor.exe`;
- executable FileVersion matches;
- ZIP size equals manifest `assetSize`;
- ZIP SHA-256 equals manifest `sha256`;
- required tests and build validations passed;
- release diff contains only approved files;
- `git diff --check` passes.

Then:

1. commit only the approved release files;
2. push the release branch;
3. create a dedicated release pull request;
4. verify pull-request files and complete diff;
5. verify checks and mergeability;
6. merge only after explicit approval;
7. synchronize local `main`;
8. verify local `main` equals `origin/main`;
9. revalidate version and manifest from merged `main`.

GitHub publication must not begin before the release pull request is merged.

## 10. Publishing approval boundary

The user gives one explicit approval for the complete GitHub publishing phase.

After that approval, one invocation must continue through:

- every pre-publish guard;
- release creation or correction;
- upload or replacement;
- publication and Latest state;
- remote metadata verification;
- download of the published asset;
- size and SHA-256 verification;
- ZIP integrity and content verification;
- executable version verification;
- byte-for-byte comparison;
- public repository invariants.

The transaction does not include:

- feature development;
- feature pull-request merge;
- release pull-request merge;
- manual Windows runtime acceptance.

## 11. Required publishing entry point

The intended version-controlled entry point is:

    scripts/publish-sponsor-pro-release.sh

Until that script is implemented, reviewed, and validated, do not improvise a
Sponsor Pro publication through unrelated ad-hoc commands.

The script must support:

- new-release mode;
- explicit existing-release correction mode;
- fail-closed validation;
- timestamped logs and recovery data;
- rollback attempt when correction fails after remote modification.

## 12. Local artifact identity

The publishing script must receive or derive one explicit approved local ZIP
path.

It must record:

- absolute ZIP path;
- byte size;
- SHA-256;
- ZIP entry list;
- ZIP entry count;
- ZIP integrity result;
- extracted executable size;
- executable FileVersion.

The script must not silently search multiple directories and select an
artifact by newest timestamp.

Ambiguous or multiple candidates must fail the transaction before remote
modification.

## 13. Pre-publish guards

Before any GitHub write, the single transaction must verify:

1. repository root is correct;
2. current branch is `main`;
3. local `main` equals `origin/main`;
4. working tree is clean;
5. intended version is explicit;
6. intended private tag is explicit;
7. AssemblyVersion matches;
8. AssemblyFileVersion matches;
9. local manifest parses successfully;
10. remote `main` manifest equals the local manifest;
11. exact approved local ZIP exists;
12. ZIP integrity passes;
13. ZIP entry count is exactly one;
14. only entry is `MultiPingMonitor.exe`;
15. extracted executable FileVersion matches;
16. ZIP size equals manifest `assetSize`;
17. ZIP SHA-256 equals manifest `sha256`;
18. required build and test evidence is available;
19. public Latest remains `v0.4.6`;
20. no public remote Sponsor Pro tag exists;
21. private repository access works;
22. the exact private release state is unambiguous;
23. no unrelated private asset would be replaced.

Any failed pre-publish guard must prevent every remote modification.

## 14. New-release mode

When the exact private tag does not exist:

1. create a private draft release;
2. use the exact intended private tag;
3. use the approved title and release notes;
4. keep `prerelease=false`;
5. upload exactly one asset named `MultiPingMonitor.zip`;
6. verify uploaded asset name, state, count, and size;
7. publish the release;
8. mark it Latest;
9. re-read release metadata through the GitHub API.

If a draft was created but any later pre-publication check fails:

- keep the release as a draft;
- do not publish it;
- preserve logs and evidence;
- return FAIL.

## 15. Existing-release correction mode

Correction mode must be explicit.

Before deleting or replacing the current remote asset:

1. read and save complete current release metadata;
2. identify the exact existing asset;
3. download the existing asset to a timestamped recovery directory;
4. verify recovery download completion;
5. record recovery asset size;
6. record recovery asset SHA-256;
7. record release ID and asset ID;
8. verify the new local replacement ZIP completely;
9. verify merged `main` manifest describes the replacement ZIP;
10. verify correction targets exactly one private release and one asset.

Only after all guards pass may the transaction remove the previous asset and
upload the replacement.

The script must never automatically delete:

- the release;
- the private tag;
- unrelated assets;
- recovery data;
- previous logs.

If replacement or final verification fails after removing the previous asset:

1. attempt to restore the verified recovery asset;
2. re-read remote release metadata;
3. verify and report the resulting remote state;
4. preserve all logs and recovery evidence;
5. return FAIL.

Successful rollback does not convert the correction run into PASS.

## 16. Post-upload release verification

After upload or replacement, verify:

- exact repository;
- exact release ID;
- exact private tag;
- expected title;
- `draft=false`;
- `prerelease=false`;
- non-empty publication timestamp;
- release is private Latest;
- exactly one release asset exists;
- asset name is `MultiPingMonitor.zip`;
- asset state is uploaded;
- asset size matches the manifest;
- public Latest remains `v0.4.6`;
- no public Sponsor Pro tag exists.

An upload response alone is not release success.

## 17. Required remote download verification

The same transaction must then:

1. create a new empty verification directory;
2. download `MultiPingMonitor.zip` from the private GitHub release;
3. verify downloaded byte size;
4. verify downloaded SHA-256;
5. verify ZIP integrity;
6. verify ZIP entry count is exactly one;
7. verify the only entry is `MultiPingMonitor.exe`;
8. extract or inspect the executable;
9. verify downloaded executable FileVersion;
10. compare downloaded ZIP byte-for-byte with the approved local ZIP;
11. re-read private Latest;
12. re-read public Latest;
13. reconfirm no public Sponsor Pro tag exists.

Required successful result:

    RESULT=PASS_SPONSOR_PRO_RELEASE_PUBLISHED_AND_VERIFIED

No other condition may return this result.

## 18. Failure behavior

The publishing transaction must fail closed.

Before remote modification:

- perform no remote write after a failed guard;
- identify the exact failed guard;
- preserve available local evidence;
- return a nonzero status.

After remote modification:

- preserve all logs and recovery data;
- report the exact final remote state;
- attempt the defined rollback when applicable;
- never report PASS without final remote download verification.

Every run must create a timestamped log and recovery directory outside the
release artifact directory.

Logs must not contain secrets, tokens, passwords, private keys, or reusable
authentication material.

## 19. Public repository invariants

Before and after private Sponsor Pro publication:

- public Latest must remain `v0.4.6`;
- no public Sponsor Pro release may exist;
- no public remote Sponsor Pro tag may exist;
- no Sponsor Pro binary asset may be uploaded publicly.

Any failed invariant makes the transaction FAIL and requires an explicit
manual review before further remote changes.

## 20. Windows TestRuntime acceptance

After the guarded publishing transaction returns PASS:

1. use the ZIP downloaded back from the private GitHub release;
2. copy that exact ZIP to the fixed Windows TestRuntime;
3. close all running MultiPingMonitor instances;
4. clear previous TestRuntime contents;
5. verify ZIP size and SHA-256 again in Windows;
6. extract the ZIP;
7. confirm only `MultiPingMonitor.exe` was extracted;
8. confirm FileVersion;
9. start the application;
10. verify normal monitoring;
11. verify Sponsor Pro edition;
12. verify About window and displayed version;
13. verify manual update check reports the installed release as current;
14. verify Compact Mode;
15. verify configuration loading;
16. verify configuration persistence;
17. verify clean application shutdown.

The release becomes accepted only after explicit user confirmation of the
manual Windows runtime test.

## 21. Cleanup and project memory

Cleanup occurs only after release acceptance.

Possible approved cleanup:

- delete merged local branches;
- prune deleted remote branches;
- preserve final release metadata;
- preserve final ZIP identities;
- preserve logs and recovery bundle;
- update `docs/CURRENT_STATE.md`;
- append a concise `docs/PROJECT_HISTORY.md` entry;
- update `docs/DECISIONS.md` only when a new long-term decision was made.

Do not automatically delete release assets, tags, releases, drafts, recovery
bundles, or logs.

## 22. GitHub Actions status

Current `.github/workflows/release.yml` packages a publish directory and is not
the canonical Sponsor Pro release path.

It must not be used for Sponsor Pro publication until a separately approved
change:

1. makes it produce the required SingleFile executable;
2. makes the ZIP contain exactly one entry;
3. implements or invokes the complete guarded publishing transaction;
4. validates correction and rollback behavior;
5. is independently tested against this document.

## 23. Prohibited release practices

Do not:

- use FolderPublish for a Sponsor Pro release;
- package an entire publish directory;
- accept a ZIP merely because it contains an EXE somewhere;
- publish before the release pull request is merged;
- publish an asset that does not match the merged manifest;
- create a public Sponsor Pro tag or release;
- split the approved GitHub publishing transaction before remote ZIP
  verification finishes;
- report success before downloading and validating the remote asset;
- test an unverified local build instead of the downloaded GitHub asset;
- mark a release accepted before manual Windows validation;
- store GitHub credentials in plaintext or in `MultiPingMonitor.xml`;
- combine development, pull-request merging, publication, and Windows
  acceptance into one monolithic operation.

## Fixed Windows TestRuntime directory for release validation

Release runtime validation must use the fixed Windows directory:

`C:\Users\info\OneDrive\Dokumenty\!!!!_GitHub_!!!!\Projekty\MultiPingMonitor\TestRuntime`

Do not create per-version runtime subdirectories for normal release validation.
The fixed root directory contains the accepted runtime configuration and logs.
A local or downloaded release candidate should replace the intended
`MultiPingMonitor.exe` and, when needed, `MultiPingMonitor.zip` in that root
directory while preserving the existing configuration files.

A release is not considered Windows-runtime accepted when it was tested only
from an isolated per-version subdirectory, unless the user explicitly approves
that special test mode.
