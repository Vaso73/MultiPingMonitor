# MultiPingMonitor Project Instructions

This file is the canonical source of stable workflow, safety, project-memory,
packaging, distribution, and acceptance rules for MultiPingMonitor.

Every new chat, AI-agent session, or project handoff must read this file before
auditing, changing, building, publishing, or releasing the project.

## 1. Authoritative project memory

Each project-memory file has exactly one responsibility:

- `AGENTS.md`
  Stable workflow, safety rules, project boundaries, and source precedence.
- `docs/CURRENT_STATE.md`
  Last accepted checkpoint memory, current scope, pending state, and exactly
  one immediate next action. It never replaces a fresh live audit.
- `docs/HANDOFF_TEMPLATE.md`
  The user's unchanged handoff request and mandatory output structure.
- `docs/PROJECT_HISTORY.md`
  Concise append-only history of completed milestones and material incidents.
- `docs/DECISIONS.md`
  Long-lived project decisions, rationale, consequences, and status.
- `docs/RELEASE.md`
  Canonical build, packaging, release, GitHub publishing, correction,
  rollback, and post-publication validation procedure.

Do not duplicate complete stable rules across these files.

## 2. Source precedence

Use this precedence:

1. The current explicit user instruction controls the requested action and
   output format.
2. Fresh live Git, GitHub, filesystem, build, and runtime state controls
   time-variable facts.
3. `AGENTS.md` controls stable workflow and safety restrictions.
4. `docs/CURRENT_STATE.md` provides checkpoint memory.
5. Confirmed checkpoints, recovery bundles, relevant logdirs, and validation
   outputs provide technical evidence.
6. The current handoff provides continuation context.
7. `docs/DECISIONS.md` provides long-term decisions.
8. `docs/PROJECT_HISTORY.md` provides concise historical context.
9. `docs/RELEASE.md` controls the release procedure.

When live state differs from checkpoint memory, stop all writes and report the
difference.

When the current user instruction differs from
`docs/HANDOFF_TEMPLATE.md`, follow the current user instruction. Do not modify
the stored template unless that update is separately approved.

## 3. Mandatory start of every session

Before any write operation:

1. Read the complete `AGENTS.md`.
2. Read `docs/CURRENT_STATE.md` if it exists.
3. Read only the additional project documents relevant to the requested scope.
4. Confirm repository path, branch, HEAD, upstream, and working-tree state.
5. Run the shortest targeted fresh read-only audit required for the decision.
6. Compare live state with the handoff and checkpoint memory.
7. Stop writes if any material difference or ambiguity exists.
8. Continue with only one approved next action.

A generic ChatGPT session cannot be assumed to have direct access to local
server files. The handoff must therefore instruct the next session to read
these files and include enough verified context to continue safely.

## 4. Communication

- Communicate with the user exclusively in Slovak.
- Before every technical step, explain in one short nontechnical sentence what
  will be done.
- Prefer short, bounded command blocks and minimal relevant output.
- Do not conceal uncertainty.
- Mark unverified or time-variable facts as `unknown / verify live`.
- Do not require the user to change their standard handoff request.

## 5. Project identity and locations

Public source repository:

    Vaso73/MultiPingMonitor

Private Sponsor Pro release repository:

    Vaso73-Software/Sponsor-Pro-Releases

Primary Debian LXC DEV repository:

    /home/vaio/projects/MultiPingMonitor

Normal DEV user:

    vaio

Windows runtime test directory:

    C:\Users\info\OneDrive\Dokumenty\!!!!_GitHub_!!!!\Projekty\MultiPingMonitor\TestRuntime

Preferred DEV helpers:

    mpm
    mpms
    mpma
    mpmt
    mpmq
    mpmb

Do not use or recreate the deleted Windows source/build repository:

    C:\Users\info\MultiPingMonitor

## 6. Shell and Git safety

- Do not use `set -e` or `set -euo pipefail`.
- Prefer `set +e` and explicit return-code checks.
- Do not use explicit shell `exit` commands in blocks intended for an active
  SSH session.
- Do not use `git fetch --prune-tags`.
- Prefer:

      export GIT_PAGER=cat
      export PAGER=cat

- Do not use `path` or `status` as zsh variables. They have special meaning in
  zsh and can corrupt PATH or fail as read-only variables.
- Large pasted zsh blocks can interleave. Prefer short, bounded phases.
- Never expose or store secrets, tokens, passwords, or private keys.
- Never commit directly to `main`.
- Never expand scope without approval.
- Never perform a push, pull request, merge, tag, release, deployment, remote
  deletion, or destructive cleanup without the approval required by the
  active workflow.
- Never copy project-specific state or configuration from another project
  without explicit approval.

## 7. Development workflow

Default workflow is local-first.

Use one dedicated local branch for one bounded scope, but do not push or open a
pull request for every intermediate operation. Development, release
preparation, validation, and Windows runtime testing should normally happen on
the DEV machine first.

Required local order:

1. Perform a fresh targeted read-only audit.
2. Create a local branch from synchronized `main`.
3. Implement only the approved scope.
4. Run targeted tests.
5. Run the full suite when application behavior changed.
6. Run the appropriate build, publish, or packaging validation.
7. Inspect the exact changed-file scope and complete diff.
8. Confirm a clean diff check.
9. Commit only approved files locally.
10. Produce and validate the local artifact when the slice affects release or
    runtime behavior.
11. Run Windows runtime testing from the local artifact when relevant.
12. Continue locally until the user accepts the result or explicitly asks to
    publish to GitHub.

GitHub is used only when one of these is true:

- the user explicitly asks to push, create a pull request, or publish;
- the result is accepted and ready to become a final release candidate;
- a release commit must be pushed so the private Sponsor Pro release is
  reproducible from Git history;
- collaboration or cross-environment transfer requires a remote branch;
- a critical documentation or safety correction is explicitly approved for
  remote publication.

For release work, prefer one final GitHub transaction:

1. push the final local release branch;
2. create one release pull request;
3. verify pull-request file scope and mergeability;
4. merge after explicit approval;
5. synchronize local `main`;
6. publish the private Sponsor Pro release;
7. download and verify the published asset;
8. verify the backend latest endpoint.

Do not create pull requests for small intermediate edits merely to keep
checkpoint documents current. Checkpoint drift should be reported and, when
safe, corrected locally as part of the current local workflow.

Feature work and release-version work must remain separate unless the user
explicitly approves a combined local release-candidate slice.

## 8. Release workflow authority

The complete release procedure is defined in:

    docs/RELEASE.md

The following rules are non-negotiable:

- Sponsor Pro packaging uses `SingleFile.pubxml`.
- `MultiPingMonitor.zip` contains exactly one entry:
  `MultiPingMonitor.exe`.
- `FolderPublish.pubxml` must never create a Sponsor Pro release asset.
- Public GitHub Releases remain Free-only through `v0.4.6`.
- Sponsor Pro binaries are published only in
  `Vaso73-Software/Sponsor-Pro-Releases`.
- A public remote Sponsor Pro tag must not be created.
- GitHub publishing is one complete guarded transaction from pre-publish
  validation through download and verification of the published ZIP.
- Once that transaction is explicitly approved, it must not pause for another
  approval before final remote ZIP verification.
- Final Windows runtime acceptance remains a separate user validation.
- A release is accepted only after the user confirms the Windows runtime test.

The current `.github/workflows/release.yml` is not the authoritative Sponsor
Pro release path until explicitly corrected and validated against
`docs/RELEASE.md`.

## 9. Handoff compatibility contract

When the user sends their standard request for a project handoff:

- do not ask them to change, extend, or restate it;
- use the current request as the authoritative output contract;
- use `docs/HANDOFF_TEMPLATE.md` as the stored canonical template;
- automatically consult the relevant project-memory files defined here;
- return the handoff in one single copy block;
- do not create or modify files, checkpoints, commits, branches, pull
  requests, releases, deployments, or runtime state;
- use only verified facts;
- mark unavailable facts as `unknown / verify live`;
- include exactly one immediate next action;
- do not repeat complete stable rules already present in `AGENTS.md`.

Handoff generation is always read-only.

## 10. State lifecycle

Use these classifications consistently:

- `closed/accepted`
  Technical validation passed and the user accepted the result.
- `audit-only`
  State was inspected but nothing was changed.
- `pending`
  Work is incomplete, unaccepted, or awaiting a required decision.
- `unknown / verify live`
  State was not verified or may have changed.

Do not describe a published artifact as accepted merely because upload,
publication, or automated verification succeeded.

## 11. Project-memory update policy

Update `docs/CURRENT_STATE.md` only after:

- an accepted slice;
- a verified merged pull request;
- an accepted release;
- a material change in current scope or next action;
- an explicitly approved documentation checkpoint.

Add an entry to `docs/PROJECT_HISTORY.md` only for a completed milestone,
material incident, accepted release, or important workflow correction.

Add an entry to `docs/DECISIONS.md` only for a long-lived project decision.

Do not update project memory based on an expected future outcome.

## 12. Session-close protocol

Before moving to a new chat:

1. Perform only the live read-only checks required for an accurate handoff.
2. Do not automatically create a checkpoint, file, commit, or other change.
3. Generate the handoff according to `docs/HANDOFF_TEMPLATE.md` and the
   current user request.
4. Include the last accepted baseline.
5. Include current branch, HEAD, and working-tree state when verified.
6. Include the last relevant checkpoint, logdir, and recovery reference.
7. Include exactly one next action.
8. Mark missing or unverified facts.
9. Stop at the requested handoff output.

## 13. Prohibited practices

Do not:

- skip the fresh live audit;
- use a monolithic script for development, feature merge, release pull-request
  merge, GitHub publishing, and Windows acceptance;
- package an entire publish directory for a Sponsor Pro release;
- use FolderPublish as the release artifact;
- publish an asset that does not match the merged manifest;
- create a public Sponsor Pro tag or release;
- test a local build instead of the asset downloaded from GitHub;
- mark a release accepted before manual Windows validation;
- silently overwrite authoritative project-memory files;
- treat `docs/CURRENT_STATE.md` as live truth;
- include secrets in project documentation or handoffs.

<!-- MPM_GITHUB_WORKFLOW_BEGIN -->
## MultiPingMonitor GitHub workflow reference

Before GitHub, release, or updater work, read `docs/GITHUB_WORKFLOW.md`.
That file is the stable workflow source for this project.

Required summary:
- use one complete CLI workflow when the scope is already approved;
- do not paste large interactive heredocs into zsh;
- for larger patches, use a temporary script file or short deterministic commands;
- keep progress visible with `| STEP |`, `| RUN |`, `| PASS |`, `| FAIL |`, and `RESULT=...`;
- never push directly to `main`;
- use branch + PR + scope check + merge + sync main;
- create a release only in an explicitly approved release slice.
- a failed validation gate must not continue to commit, push, PR creation, merge, or release;
- temporary workflow scripts must not remain as untracked files in the repository root.
<!-- MPM_GITHUB_WORKFLOW_END -->

## Fixed Windows TestRuntime directory

Windows runtime testing for MultiPingMonitor must use the fixed directory:

`C:\Users\info\OneDrive\Dokumenty\!!!!_GitHub_!!!!\Projekty\MultiPingMonitor\TestRuntime`

Do not create per-version runtime subdirectories for normal Windows testing.

The root `TestRuntime` directory contains the accepted runtime configuration,
logs, compact-set configuration, and other files that represent the real test
context. For each test, replace only the intended executable, ZIP, or explicitly
approved runtime artifact. Preserve existing configuration files unless the user
explicitly approves a config reset.

When handing off files to Windows, always provide exact PowerShell `scp`
commands that copy into this fixed root directory unless the user explicitly
requests a different destination.

<!-- MPM_RUNTIME_UPDATE_TESTING_RULE_START -->
## MultiPingMonitor runtime acceptance rule

From Sponsor Pro v1.0.25 onward, new MultiPingMonitor versions must be accepted through the app's own self-update workflow. Do not use Windows PowerShell `scp`, manual ZIP extraction, or manual `MultiPingMonitor.exe` replacement as the normal runtime acceptance path. Use manual copying only when the user explicitly approves it as an exceptional diagnostic/local-debug step.

Before telling the user to update from the app, verify that the live update endpoint returns the intended version, tag, asset name, SHA-256, size, and `status=ok`.

<!-- MPM_RUNTIME_UPDATE_TESTING_RULE_END -->
