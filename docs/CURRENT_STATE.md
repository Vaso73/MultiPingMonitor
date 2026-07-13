# MultiPingMonitor Current State

Last updated: 2026-07-13 07:16 UTC

## Accepted runtime baseline

Sponsor Pro v1.1.2 remains the current accepted released runtime baseline.

Verified acceptance facts:

- Public source repository: `Vaso73/MultiPingMonitor`
- Private Sponsor Pro repository: `Vaso73-Software/Sponsor-Pro-Releases`
- Accepted public `main` commit:
  `59e8786a20697206371ad8e56cdc0f00a4f3f20b`
- Accepted version: `1.1.2`
- Accepted private tag: `multipingmonitor/v1.1.2`
- Release asset: `MultiPingMonitor.zip`
- Release ZIP size: `66628498` bytes
- Release ZIP SHA-256:
  `a339e531fccff5c7cd380c9c7624188b9619b04f0ed499bcacc4cf3675f2dd4c`
- ZIP contract: exactly one `MultiPingMonitor.exe`
- Windows acceptance method: in-app updater
- User-confirmed updater result:
  `Aktualizácia na verziu 1.1.2 bola úspešne dokončená.`
- Sponsor Pro v1.1.2 runtime acceptance: `closed/accepted`

Live Git, GitHub, release, backend and filesystem state must still be
verified before every write or release transaction.

## Current accepted development slice

The combined visual-polish slice is complete and accepted locally.

Active source branch before publication:

- branch: `fix/live-ping-visual-polish`
- base: `59e8786a20697206371ad8e56cdc0f00a4f3f20b`
- version before the release-only bump: `1.1.2`
- implementation paths: exactly 9
- staged paths before publication audit: 0
- remote feature branch before publication: absent

Implemented behavior:

- disabled Live Ping actions have a visible themed disabled state in Modern
  and Classic visual styles;
- resizable custom-chrome windows retain a crisp one-pixel inset frame;
- maximized and `NoResize` window behavior remains unchanged;
- application scrollbars use one shared themed template;
- normal, probe and Compact scrollbar dimensions remain distinct;
- legacy line-arrow scrollbar buttons are removed;
- all eight Settings tabs scroll vertically above the fixed footer;
- horizontal scrolling in Settings is disabled;
- Isolated Ping local scrollbar styling derives from the shared application
  scrollbar;
- DataGrid vertical scrollbar compensation matches the new 10-pixel width.

Changed application and test paths:

- `MultiPingMonitor/ResourceDictionaries/DataGridStyle.xaml`
- `MultiPingMonitor/ResourceDictionaries/ScrollBarStyle.xaml`
- `MultiPingMonitor/Styles/VisualStyle.Classic.xaml`
- `MultiPingMonitor/Styles/VisualStyle.Modern.xaml`
- `MultiPingMonitor/UI/IsolatedPingWindow.xaml`
- `MultiPingMonitor/UI/OptionsWindow.xaml`
- `MultiPingMonitor.Tests/LivePingDisabledVisualTests.cs`
- `MultiPingMonitor.Tests/ScrollBarAndOptionsLayoutTests.cs`
- `MultiPingMonitor.Tests/WindowMainPanelBorderVisualTests.cs`

No updater, backend, infrastructure, authentication, billing, network or
production change is part of this feature scope.

## Validation evidence

Technical validation of the accepted source:

- Release build: 0 warnings, 0 errors
- disabled-state tests: 2/2 PASS
- window-frame tests: 3/3 PASS
- scrollbar and Settings layout tests: 4/4 PASS
- complete automated suite: 477/477 PASS
- `git diff --check`: PASS
- SingleFile publish: PASS
- publish output: exactly one `MultiPingMonitor.exe`

Accepted Windows preview:

- path:
  `/home/vaio/backups/MultiPingMonitor/visual-polish-scrollbars-repaired-preview-20260713-063048/MultiPingMonitor.exe`
- size: `163504244` bytes
- SHA-256:
  `65333e1e08977f47c3042bc5620063a6be67be00e51f4f6f4ef1ada852ec9d65`
- displayed version: `1.1.2`
- Windows runtime validation: PASS

The user confirmed:

- disabled Live Ping visual states;
- action activation after a valid target and running ping;
- Modern and Classic window borders;
- normal and reduced window sizes;
- mouse-wheel scrolling;
- scrollbar thumb dragging;
- scrollbar track-click paging;
- fixed Settings footer;
- reduced-height Settings scrolling;
- Compact scrollbar appearance;
- no regression in previously accepted functionality.

This development slice is `closed/accepted`.

## Publication and release state

Publication of the accepted visual-polish source and the next patch release
has been explicitly approved.

The intended next patch version is `1.1.3`, derived from the verified current
source version `1.1.2`.

Feature publication and release-version publication remain separate Git
transactions:

1. feature/documentation branch, PR, scope verification and merge;
2. clean synchronized `main`;
3. release-only branch and version/manifest changes;
4. release PR and merge;
5. private Sponsor Pro publication;
6. download-back and backend verification.

The resulting v1.1.3 release must not be classified as accepted until the user
successfully validates it through the in-app updater.

## Local-only state

These branches and files remain local-only and must not be pushed:

- branch `docs/accept-sponsor-pro-v1-1-1`
- commit `28826892022ef66d8cec8f31c4e12c319bfc80d5`
- `/home/vaio/.config/multipingmonitor/agent-workflow.md`
- local release and orchestration runners
- logs, backups, evidence, recovery files and temporary working scripts

The isolated worktrees remain preserved until separately approved cleanup:

- `/home/vaio/projects/MultiPingMonitor`
- `/home/vaio/worktrees/MultiPingMonitor-window-border`

## Current scope

Current approved scope:

- publish the accepted 9-path visual-polish implementation;
- update `docs/CURRENT_STATE.md`;
- record the accepted local milestone in `docs/PROJECT_HISTORY.md`;
- merge through a feature PR;
- publish the next patch through the canonical release-only workflow;
- verify the private asset and update backend.

Not approved:

- unrelated UX or localization changes;
- updater implementation changes;
- backend or infrastructure changes;
- modification or publication of local helper scripts;
- destructive cleanup of isolated worktrees.

## Immediate next action

After successful remote publication, validate Sponsor Pro v1.1.3 through the
in-app updater and confirm the Windows runtime behavior.
