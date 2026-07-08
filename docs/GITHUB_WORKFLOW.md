# MultiPingMonitor GitHub Workflow

This file is the stable GitHub workflow reference for MultiPingMonitor.
Every new chat or agent session must read this file together with `AGENTS.md`
before proposing GitHub, release, or updater work.

## Communication and execution rules

- Communicate with the user in Slovak.
- Prefer one complete CLI workflow block for GitHub delivery when the scope is already approved.
- Do not paste very large interactive heredocs directly into zsh.
- For larger patches, generate and run a temporary script file, or use short deterministic commands.
- Use visible progress markers such as `| STEP |`, `| RUN |`, `| PASS |`, `| FAIL |`, and `RESULT=...`.
- Use `export GIT_PAGER=cat` and `export PAGER=cat`.
- Do not use `set -e` or `set -euo pipefail`.
- Do not use explicit `exit` in interactive SSH blocks.
- Keep `main` clean and synced before starting.
- Do not push directly to `main`.
- Use branch + PR + scope check + merge + sync main.
- Push/PR/merge/release only after validation gates pass.

## Standard one-step GitHub workflow: no release

Use this when the user says equivalent to “daj to na GitHub bez release”.

1. `git fetch origin --prune`
2. checkout `main`
3. `git pull --ff-only origin main`
4. verify clean status and `HEAD...origin/main == 0 0`
5. create/reset a feature branch from `origin/main`
6. patch exactly the approved scope
7. run scope guard using `git status --porcelain=v1 --untracked-files=all`
8. run `git diff --check`
9. run targeted tests
10. run full tests or the agreed project helper
11. run publish/build guard when app/release output can be affected
12. verify expected changed files exactly
13. commit
14. push branch
15. create PR with `gh pr create`
16. read PR metadata with `gh pr view`
17. verify PR file scope with `gh pr diff --name-only`
18. merge PR with `gh pr merge --merge --delete-branch`
19. checkout `main`
20. `git pull --ff-only origin main`
21. verify clean status and ahead/behind `0 0`
22. print final `RESULT=...`

## Standard one-step GitHub workflow: with release

Use this only when the user explicitly asks for GitHub plus release, or when a release slice is explicitly approved.

1. Complete the “no release” workflow first for the feature PR.
2. Create a release-only version bump branch from synced `main`.
3. Change only approved version/manifest/release metadata files.
4. Run version/release validation.
5. Commit, push, PR, scope-check, merge, and sync `main`.
6. Tag/publish using the project release workflow.
7. For Sponsor Pro releases, the asset name must be exactly `MultiPingMonitor.zip`.
8. The Sponsor Pro ZIP must contain exactly one item: `MultiPingMonitor.exe`.
9. Verify the uploaded GitHub asset by downloading it back.
10. Verify size and SHA-256 match the manifest.
11. Verify `https://updates.watel.cloud/v1/update/latest` matches the new release.
12. Only then hand off Windows PowerShell `scp` or runtime test commands.

## MultiPingMonitor release constraints

- Public Free release stays frozen unless explicitly approved.
- Sponsor Pro releases go to `Vaso73-Software/Sponsor-Pro-Releases`.
- Final release asset name is `MultiPingMonitor.zip`.
- The ZIP contains exactly one file: `MultiPingMonitor.exe`.
- The app remains one portable `MultiPingMonitor.exe`.
- No persistent helper EXE, DLL, service, or installer is allowed.
- For self-update replacement, a temporary helper may be a temporary copy of the same EXE.
- Backups go beside the running app under `<appdir>/backup`.
- The `backup` directory must not be included in its own backup.
- No silent install: the user must explicitly confirm before installing an update.
- After a Sponsor Pro release, backend latest metadata must match the newly published release.
