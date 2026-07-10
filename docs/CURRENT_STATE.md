# MultiPingMonitor Current State

Last updated: 2026-07-10 04:47 UTC

## Accepted baseline

Sponsor Pro v1.0.28 remains the current accepted released runtime baseline.

The current branch contains additional local-only development work for external `.lang` localization. This branch is not pushed, not released, and has no pull request.

## Current live checkpoint

Verified live state for this checkpoint update:

- Repository: `/home/vaio/projects/MultiPingMonitor`
- Branch: `feature/external-lang-pack-foundation`
- HEAD before checkpoint update: `ba4e23d`
- HEAD label: `ba4e23d` — `Localize main window chrome and probe stats`
- Upstream for the local branch: none configured
- Comparison with `origin/main`: `0 10`
- Working tree before checkpoint update: clean
- Push state: local-only branch, not pushed
- PR state: no PR created
- Release state: no tag, no release, no Sponsor Pro publish for this branch
- Backend latest metadata: not changed

This section reflects the latest verified live state. Older checkpoint sections below may describe previous intermediate states.


## Current repository state

Verified live state before this checkpoint update:

- Repository: `/home/vaio/projects/MultiPingMonitor`
- Branch: `feature/external-lang-pack-foundation`
- HEAD before checkpoint update: `b38cfda17e671b941cf353e17b5aeae85351d6bb`
- HEAD label: `b38cfda` — `Document fail-fast command workflow`
- Upstream for the local branch: none configured
- Comparison with `origin/main`: `0 5`
- Working tree before checkpoint update: clean
- Push state: local-only branch, not pushed
- PR state: no PR created
- Release state: no tag, no release, no Sponsor Pro publish for this branch
- Backend latest metadata: not changed

Always verify live state before writing.

## Current local development status

External language-pack foundation, runtime use of external language packs, Options live localization apply/save behavior, fail-fast workflow documentation, user-facing Options to Settings text cleanup, Live Ping window chrome localization, and MainWindow chrome/probe stats localization are implemented and committed locally.

Recent local commit chain:

- `ba4e23d` — `Localize main window chrome and probe stats`
- `365697e` — `Localize Live Ping window chrome`
- `076cb8b` — `Rename options text to settings`
- `6a23119` — `Update current localization checkpoint`
- `b38cfda` — `Document fail-fast command workflow`
- `7e426a9` — `Add live apply behavior for options localization`
- `de8755b` — `Use external language packs at runtime`
- `8e6aaf7` — `Update current localization branch checkpoint`
- `420282f` — `Add external language pack foundation`
- `716099b` — `Merge pull request #156 from Vaso73/release/bump-version-to-1-0-28`

Current accepted local development state:

- External `.lang` foundation is implemented.
- Runtime lookup through external language packs is implemented.
- Built-in English fallback remains required.
- External `lang/*.lang` files may exist beside the EXE at runtime.
- Generated/user-edited external language-pack `TEXT` values must not be overwritten.
- `.lang` files must not be packaged into Sponsor Pro publish output.
- The app remains a single portable `MultiPingMonitor.exe`.

## Completed slice: external language-pack foundation

Commit:

- `420282f` — `Add external language pack foundation`

Implemented:

- Added `LanguagePackKeys`.
- Added `LanguagePackSeeds`.
- Added `LanguagePackService`.
- Added tests for stable language-pack generation and preservation behavior.
- Runtime seed generation for `lang/sk-SK.lang`.
- User-edited language-pack `TEXT` entries are preserved.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: runtime use of external language packs

Commit:

- `de8755b` — `Use external language packs at runtime`

Implemented:

- Added `ExternalLanguageResourceManager`.
- Added `LanguageRuntimeService`.
- Existing `Properties.Strings.*` C# lookups and many XAML `x:Static resource:Strings.*` lookups now resolve through an injected `ResourceManager`.
- `Strings.Designer.cs` was not hand-edited.
- `App.xaml.cs` applies language by language code before creating windows.
- `ApplicationOptions` keeps legacy language enum compatibility and adds authoritative `LanguageCode`.
- `Configuration` persists `Language` as `System`, `en`, or an external language code such as `sk-SK`.
- Old config values `System`, `English`, and `Slovak` remain backward-compatible.
- Options language ComboBox is populated from built-in choices plus discovered `lang/*.lang` packs.
- `Slovenčina (sk-SK)` is discovered from `lang/sk-SK.lang`.

Validation:

- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- `dotnet test MultiPingMonitor.sln -c Release --no-build`: passed, 431 total, 0 failed.
- Single-file publish passed.
- Publish output contained exactly one file: `MultiPingMonitor.exe`.
- `LANG_OUTPUT_COUNT=0`; `.lang` files were not packaged.
- Windows runtime showed Options language choices: `System`, `English`, `Slovenčina (sk-SK)`.
- Windows runtime showed Slovak UI after selecting `Slovenčina (sk-SK)` and restarting.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: Options live apply/save behavior

Commit:

- `7e426a9` — `Add live apply behavior for options localization`

Implemented:

- Added `LocalizationRefreshService`.
- Added `LanguageRuntimeService.CaptureResourceSnapshot()`.
- Added `Common_Apply`.
- Added `Options_LanguageApplyHint`.
- Extended stable language-pack key set:
  - `EntryCount=516`
  - `ResourceKeys.Count=516`
  - last key `20515`
- Changed Options footer buttons:
  - `Použiť` / `Apply`
  - `Uložiť` / `Save`
  - `Zrušiť` / `Cancel`
- Added/reworked Options flow:
  - `Apply_Click`
  - `Save_Click`
  - `ApplyOptions(closeAfterApply)`
  - `SaveAllOptions`
  - `RefreshLocalizedText`
- Added accepted preview snapshot update after Apply/Save.
- Added `MainWindow.ApplyRuntimeOptionChanges(...)`.
- Fixed `System OS default` language behavior by using the original system default culture instead of the currently applied thread culture.
- Refreshes `LanguageComboBox` after Apply so selected language display updates immediately.

Preserved behavior:

- Existing live behavior for Theme / VisualStyle / DisplayMode / CompactSource was intentionally preserved.
- No version bump.
- No release.
- No push or PR.
- Publish contract remains single EXE with no packaged `.lang`.

Validation:

- Initial targeted stable-key tests caught expected count/key failures and the workflow stopped for correction.
- Stable key count and last key were corrected.
- Targeted language-pack stable test passed.
- Final validation after user Windows acceptance:
  - `git diff --check`: passed
  - `dotnet build MultiPingMonitor.sln -c Release`: passed
  - `dotnet test MultiPingMonitor.sln -c Release --no-build`: passed
    - total: 431
    - failed: 0
    - succeeded: 431
    - skipped: 0
  - single-file publish passed
  - publish output contained exactly one top-level file: `MultiPingMonitor.exe`
  - `LANG_OUTPUT_COUNT=0`
  - final published local EXE SHA-256:
    `a8724d1b023dabd983bc360a3795bcb2b60d6b764aea611d9322f68a6064ca22`

Windows runtime acceptance:

- User visually confirmed the local runtime behavior as functionally correct.
- Options/Nastavenia shows buttons `Použiť`, `Uložiť`, `Zrušiť`.
- English to Apply works without restart.
- Slovenčina / `sk-SK` to Apply works without restart.
- Language ComboBox refreshes correctly after Apply.
- `System OS default` behaves correctly according to OS language.

Status:

- Closed and accepted locally.
- Committed locally.
- Not pushed.

## Completed slice: fail-fast workflow documentation

Commit:

- `b38cfda` — `Document fail-fast command workflow`

Implemented in `docs/GITHUB_WORKFLOW.md`:

- Added `Fail-fast command workflow` section.
- Documents that `status` must not be used as a zsh shell variable.
- Uses safe names such as `git_status_short`, `branch_name`, and `head_sha`.
- Adds explicit `STOP_*` gates.
- Requires stopping after patch/build/test/publish failures.
- Prohibits continuing to long tests after failed patch/build.
- Prohibits publish after failed tests.
- Prohibits handing a runtime EXE to the user after failed tests or failed publish-contract validation.
- Recommends two-stage validation for risky patches:
  1. patch + diff check + build + targeted tests;
  2. full tests + single-file publish contract after stage 1 passes.

Validation:

- Precheck passed.
- `AGENTS.md` and `docs/GITHUB_WORKFLOW.md` were read before write.
- Patch passed.
- `git diff --check` passed.
- Docs-only staged diff check passed.
- Local docs commit created.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: user-facing Options to Settings text cleanup

Commit:

- `076cb8b` — `Rename options text to settings`

Implemented:

- User-facing English text was changed from `Options` to `Settings` where appropriate.
- Slovak user-facing text remains `Nastavenia`.
- Existing resource key names were intentionally preserved, including `Menu_Options`, `Options_Title`, and `Tray_Options`, to minimize compatibility risk and avoid unnecessary API/key churn.
- Updated related help text to reflect external language packs and live Apply/Save behavior.
- No source-code logic was changed.

Validation:

- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Completed slice: Live Ping window chrome localization

Commit:

- `365697e` — `Localize Live Ping window chrome`

Implemented:

- Localized Live Ping window chrome/user-facing labels:
  - window title `Live Ping Monitor`
  - arrange tooltip
  - start button
  - always-on-top label
  - copy target/address labels
  - add-to-set label
  - paused banner
  - stop/resume button
  - clear/close footer buttons
  - stats labels `Sent`, `Recv`, `Lost`
- Reused existing Live Ping resource keys where available.
- Added new resource keys:
  - `LivePing_Title`
  - `LivePing_Clear`
  - `LivePing_Close`
  - `LivePing_StatsSent`
  - `LivePing_StatsReceivedShort`
  - `LivePing_StatsLost`
- Extended stable language-pack key set:
  - `EntryCount=522`
  - `ResourceKeys.Count=522`
  - last key `20521`
- Updated `LanguagePackSeeds`.
- Updated `LanguagePackServiceTests` expectations.
- Avoided `x:Static resource:Strings.LivePing_*` in `LivePingMonitorWindow.xaml` after WPF build failure; Live Ping chrome text is now set via code-behind to match the runtime localization model used in this slice.

Validation:

- Initial patch correctly stopped at build failure:
  - `LivePingMonitorWindow.xaml(47,25): error MC2000`
  - root cause: problematic `x:Static resource:Strings.LivePing_Arrange` usage in this window.
- Recovery patch removed LivePing `x:Static` references from `LivePingMonitorWindow.xaml`.
- `STATIC_REFERENCES=NONE` for `x:Static resource:Strings.LivePing_*` in `LivePingMonitorWindow.xaml`.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0

Status:

- Closed locally.
- Committed locally.
- Not pushed.

## Recommended next localization audit

Continue with a fresh read-only audit for the next remaining hardcoded UI area.

Already completed or covered in this branch:

- About / `O aplikácii` resource coverage was already present before this checkpoint.
- User-facing Options to Settings / `Nastavenia` cleanup is committed.
- New Live Ping menu key already exists.
- Live Ping window chrome localization is committed.
- Compact start/stop set keys already exist.

Candidate next areas:

- remaining title-bar close/minimize/maximize/restore tooltips in other windows
- Help window title-bar tooltips
- Update window close tooltip
- Add-to-set/New-alias dialog close tooltips and any remaining hardcoded dialog labels

Runtime validation note:

- Live Ping window must be visually checked on Windows in English and Slovenčina because text is set via code-behind and window reopen/apply behavior matters.


## Completed slice: MainWindow chrome and probe stats localization

Commit:

- `ba4e23d` — `Localize main window chrome and probe stats`

Implemented:

- Localized MainWindow title-bar button tooltip/accessibility text:
  - `Tooltip_Minimize`
  - `Tooltip_Maximize`
  - `Tooltip_RestoreDown`
  - existing `Tooltip_Close`
- Added `x:Name` to the main title-bar minimize and close buttons so code-behind can update their tooltip and automation name at runtime.
- Added `RefreshWindowChromeLocalization()` and calls from startup/runtime option apply flow.
- MainWindow probe-card stat labels now use existing resource keys:
  - `Probe_Stat_Sent`
  - `Probe_Stat_Received`
  - `Probe_Stat_Lost`
- `Probe-Util.cs` now builds `StatisticsText` from localized resource text.
- Extended stable language-pack key set:
  - `EntryCount=525`
  - `ResourceKeys.Count=525`
  - last key `20524`
- Added new language-pack keys:
  - `Tooltip_Minimize`
  - `Tooltip_Maximize`
  - `Tooltip_RestoreDown`
- Updated `LanguagePackSeeds`.
- Updated `LanguagePackServiceTests` expectations.

Validation:

- Initial patch correctly stopped at build failure:
  - duplicate `ResourceText` helper in `MainWindow.xaml.cs`
  - no commit was created from the failed build
- Recovery patch removed the duplicate helper and kept the intended localization slice.
- `git diff --check`: passed.
- `dotnet build MultiPingMonitor.sln -c Release`: passed.
- Targeted `LanguagePackServiceTests`: passed, 7 total, 0 failed.
- Full test suite passed:
  - total: 431
  - failed: 0
  - succeeded: 431
  - skipped: 0

Status:

- Closed locally.
- Committed locally.
- Not pushed.


## Current scope

Continue locally only.

Currently approved scope:

- Update `docs/CURRENT_STATE.md` to reflect live state after commits `7e426a9` and `b38cfda`.
- This checkpoint update is docs-only.
- No source-code change is included in this checkpoint update.

Out of scope until explicitly approved:

- GitHub push.
- Pull request.
- Merge.
- Tag.
- Release.
- Version bump.
- Sponsor Pro publish.
- Backend latest metadata update.
- Updater release test.
- Unrelated features.
- Changes outside MultiPingMonitor.

## Immediate next action

After this docs-only checkpoint update, review the resulting diff and commit only `docs/CURRENT_STATE.md` locally.

Do not push this branch, create a PR, tag, release, publish Sponsor Pro artifacts, or update backend latest metadata without explicit user approval.

Recommended next development discussion after checkpoint commit:

- Decide whether to continue localization coverage by adding already-existing menu/UI strings into the external language-pack set.
- User mentioned candidate areas:
  - About / `O aplikácii`
  - Options should be renamed app-wide to Settings / `Nastavenia`
  - New Live Ping
  - Stop set
  - Start set
  - other already-existing menu items still using built-in/static strings

Before any next write slice, run a fresh targeted read-only audit of the specific menu/UI areas to be changed.

## Known risks and regression prevention

Known shell/workflow risk:

- A previous shell block used zsh read-only variable `status`.
- A patch failed but long tests/publish still continued.
- Prevention is now documented in `docs/GITHUB_WORKFLOW.md`.
- Use explicit `STOP_*` gates.
- Do not continue to full tests after failed patch/build/targeted test.
- Do not publish after failed tests.

Known runtime bug fixed:

- `System OS default` incorrectly used current thread culture after switching language.
- This made System behave like the language most recently applied to the thread.
- Fixed by storing original system default culture/UICulture in `LanguageRuntimeService`.

Regression tests for future localization/UI work:

- Slovak to English to Apply.
- English to Slovak to Apply.
- System OS default to Apply.
- ComboBox display immediately after Apply.
- Save applies language directly and closes Options.
- Cancel does not save changes that were not already applied.
- User-edited external language-pack `TEXT` entries remain preserved.
- Publish output remains exactly one EXE and no `.lang` files.

Do not blindly overwrite:

- `MultiPingMonitor/Classes/LanguagePackKeys.cs`
- `MultiPingMonitor/Classes/LanguagePackSeeds.cs`
- generated external `.lang` user `TEXT` values
- `MultiPingMonitor/Properties/Strings.Designer.cs`
- runtime `lang/*.lang` files beside the EXE

Do not package:

- `lang/`
- `*.lang`

Runtime validation requirement:

- Future localization UI changes require visual Windows runtime validation because WPF static localization/live refresh cannot be fully proven by unit tests alone.

## Hard rules

- Communicate only in Slovak.
- At the beginning of each new project chat, provide a block for audit and loading project working files.
- Read `AGENTS.md` and `docs/CURRENT_STATE.md` before writes.
- Fresh live audit controls time-variable facts.
- Stop writes if live state differs materially from checkpoint memory or handoff.
- Do not push directly to `main`.
- Do not push this branch without explicit approval.
- Do not create a PR without explicit approval.
- Do not tag or release without explicit approval.
- Do not publish Sponsor Pro artifacts without explicit approval.
- Keep Sponsor Pro ZIP exactly one entry: `MultiPingMonitor.exe`.
- Keep the application as one portable EXE.
- Do not add persistent helper EXE/DLL/service/installer.
- Do not package `lang/*.lang` files into release ZIP.
- Do not overwrite user-edited language-pack `TEXT` entries.
- Before any future push, real local Windows runtime test must pass.
- Final released-version acceptance remains through the in-app updater unless the user explicitly approves a local diagnostic exception.
